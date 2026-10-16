using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace ME.BECS.Views {

    // Native simulation is time-sliced through one graph per prefab. An owner stores
    // only Unity's public playback/particle/trail state, never a GameObject graph.
    // This preserves authored clocks, local space, collision and nested emitters.
    internal sealed class ParticleViewBatch : IDisposable {

        private sealed class Snapshot : IDisposable {
            internal ParticleSystem.PlaybackState playback;
            internal NativeList<ParticleSystem.Particle> particles = new NativeList<ParticleSystem.Particle>(Allocator.Persistent);
            internal ParticleSystem.Trails trails;
            internal bool activeSelf;
            internal bool active;
            public void Dispose() => this.particles.Dispose();
        }

        private sealed class Owner : IDisposable {
            internal Snapshot[] nodes;
            internal Vector3 scale;
            internal IBatchedParticleViewModule[] modules;
            internal Vector3[] orbitCenters;
            internal Vector3[] orbitPositions;
            internal float age;
            public void Dispose() {
                foreach (var node in this.nodes) node.Dispose();
            }
        }

        private sealed class Node : IDisposable {
            internal ParticleSystem simulation;
            internal ParticleGeometryBatch geometry;
            internal bool hasTrails;
            internal ParticleGeometryBatch trails;
            public void Dispose() {
                this.geometry.Dispose();
                this.trails?.Dispose();
            }
        }

        private readonly GameObject simulationRoot;
        private readonly Node[] nodes;
        private readonly ParticleSystem[] roots;
        private readonly System.Collections.Generic.List<Owner> owners = new System.Collections.Generic.List<Owner>();
        private readonly System.Collections.Generic.Stack<Owner> unusedOwners = new System.Collections.Generic.Stack<Owner>();
        private readonly bool meshInstance;
        private readonly ParticleMeshBatch meshes;
        private readonly IBatchedParticleViewModule[] modules;
        private readonly BatchedParticleView.Orbit[] orbits;
        private readonly Vector3[] orbitLocalPositions;

        internal ParticleViewBatch(EntityView prefab, Transform parent, BatchedParticleView.Mode mode) {
            this.meshInstance = mode == BatchedParticleView.Mode.MeshInstance;
            if (this.meshInstance == true) {
                this.meshes = new ParticleMeshBatch(prefab.gameObject, parent);
                this.nodes = Array.Empty<Node>();
                this.roots = Array.Empty<ParticleSystem>();
                return;
            }

            this.simulationRoot = UnityEngine.Object.Instantiate(prefab.gameObject, parent);
            foreach (var source in this.simulationRoot.GetComponentsInChildren<AudioSource>(true)) {
                source.playOnAwake = false;
                source.Stop();
                source.enabled = false;
            }
            this.simulationRoot.SetActive(true);
            this.orbits = this.simulationRoot.GetComponent<BatchedParticleView>().orbits;
            this.orbitLocalPositions = new Vector3[this.orbits.Length];
            for (int i = 0; i < this.orbits.Length; ++i) this.orbitLocalPositions[i] = this.orbits[i].transform.localPosition;
            this.meshes = new ParticleMeshBatch(this.simulationRoot, parent);
            var modules = new System.Collections.Generic.List<IBatchedParticleViewModule>();
            foreach (var item in this.simulationRoot.GetComponent<EntityView>().GetAllModules().items) {
                if (item.enabled == true && item.module is IBatchedParticleViewModule module) modules.Add(module);
            }
            this.modules = modules.ToArray();
            foreach (var behaviour in this.simulationRoot.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            var systems = this.simulationRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: false);
            var rootSystems = new System.Collections.Generic.List<ParticleSystem>();
            this.nodes = new Node[systems.Length];
            for (int i = 0; i < systems.Length; ++i) {
                var simulation = systems[i];
                simulation.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
                simulation.useAutoRandomSeed = false;
                var main = simulation.main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var renderer = simulation.GetComponent<ParticleSystemRenderer>();
                // BakeMesh needs CPU geometry; configure before initializing playback.
                renderer.enableGPUInstancing = false;
                var hasTrails = simulation.trails.enabled;
                var node = new Node() { simulation = simulation, hasTrails = hasTrails,
                    geometry = new ParticleGeometryBatch(parent, renderer) };
                if (hasTrails == true) node.trails = new ParticleGeometryBatch(parent, renderer, trails: true);
                this.nodes[i] = node;
                var ancestor = simulation.transform.parent;
                while (ancestor != null && ancestor != this.simulationRoot.transform.parent && ancestor.GetComponent<ParticleSystem>() == null) ancestor = ancestor.parent;
                if (ancestor == null || ancestor == this.simulationRoot.transform.parent) rootSystems.Add(simulation);
                simulation.name = "[Simulation] " + simulation.name;
            }
            foreach (var renderer in this.simulationRoot.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var light in this.simulationRoot.GetComponentsInChildren<Light>(true)) light.enabled = false;
            // Skinned renderers can still be the authored emission shape.
            foreach (var renderer in this.simulationRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
            this.roots = rootSystems.ToArray();
        }

        internal void RemoveAtSwapBack(int index) {
            if (this.meshInstance == true || index >= this.owners.Count) return;
            var removed = this.owners[index];
            this.owners[index] = this.owners[this.owners.Count - 1];
            this.owners.RemoveAt(this.owners.Count - 1);
            this.unusedOwners.Push(removed);
        }

        internal void Commit(NativeList<ParticlesProvider.ParticleInstanceData> instances, NativeList<Ent> entities, float dt, Camera camera) {
            this.meshes.Clear();
            foreach (var node in this.nodes) {
                node.geometry.Clear();
                node.trails?.Clear();
            }
            for (int i = 0; i < instances.Length; ++i) {
                var instance = instances[i];
                if (this.meshInstance == true) {
                    var matrix = Matrix4x4.TRS((Vector3)instance.position, (Quaternion)(Unity.Mathematics.quaternion)instance.rotation, instance.scale);
                    this.meshes.Append(matrix, instance.randomSeed);
                    continue;
                }
                if (i == this.owners.Count) {
                    Owner owner;
                    if (this.unusedOwners.Count > 0) {
                        owner = this.unusedOwners.Pop();
                    } else {
                        owner = new Owner() { nodes = new Snapshot[this.nodes.Length], modules = new IBatchedParticleViewModule[this.modules.Length], orbitCenters = new Vector3[this.orbits.Length], orbitPositions = new Vector3[this.orbits.Length] };
                        for (int j = 0; j < this.nodes.Length; ++j) owner.nodes[j] = new Snapshot();
                        for (int j = 0; j < this.modules.Length; ++j) owner.modules[j] = this.modules[j].CreateBatchInstance();
                    }
                    this.owners.Add(owner);
                }
                var state = this.owners[i];
                for (int orbit = 0; orbit < this.orbits.Length; ++orbit) this.orbits[orbit].transform.localPosition = instance.pendingSpawn == true ? this.orbitLocalPositions[orbit] : state.orbitPositions[orbit];
                if (instance.pendingSpawn == true) {
                    this.SetPose((Vector3)instance.position, (Quaternion)(Unity.Mathematics.quaternion)instance.rotation, instance.scale);
                    state.age = 0f;
                    for (int orbit = 0; orbit < this.orbits.Length; ++orbit) state.orbitCenters[orbit] = this.orbits[orbit].transform.position;
                    for (int j = 0; j < this.nodes.Length; ++j) {
                        this.nodes[j].simulation.gameObject.SetActive(true);
                        this.nodes[j].simulation.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        this.nodes[j].simulation.randomSeed = Unity.Mathematics.math.hash(new Unity.Mathematics.uint2(instance.randomSeed, (uint)j + 1u)) | 1u;
                    }
                    this.PrepareModules(state, entities[i], dt, newOwner: true);
                    this.Simulate(0f, restart: true);
                } else {
                    this.SetPose(instance.previousPosition, instance.previousRotation, state.scale);
                    this.ApplyOrbits(state, state.age);
                    for (int j = 0; j < this.nodes.Length; ++j) this.nodes[j].simulation.gameObject.SetActive(state.nodes[j].activeSelf);
                    // Clear the previous graph before activating/restoring nodes.
                    // A zero-particle sub-emitter can be stopped even after Simulate.
                    foreach (var node in this.nodes) node.simulation.Clear(withChildren: false);
                    for (int j = 0; j < this.nodes.Length; ++j) {
                        var simulation = this.nodes[j].simulation;
                        if (simulation.gameObject.activeInHierarchy == false) continue;
                        simulation.Play(withChildren: false);
                        simulation.Pause(withChildren: false);
                    }
                    for (int j = 0; j < this.nodes.Length; ++j) {
                        var snapshot = state.nodes[j];
                        var simulation = this.nodes[j].simulation;
                        if (simulation.gameObject.activeInHierarchy == false) continue;
                        simulation.SetPlaybackState(snapshot.playback);
                        simulation.SetParticles(snapshot.particles.AsArray(), snapshot.particles.Length);
                        if (this.nodes[j].hasTrails == true) simulation.SetTrails(snapshot.trails);
                    }
                    // Prime native transform history before switching owners. Otherwise
                    // rateOverDistance connects two unrelated entities with a long trail.
                    if (dt > 0f) this.Simulate(0f, restart: false);
                    this.SetPose((Vector3)instance.position, (Quaternion)(Unity.Mathematics.quaternion)instance.rotation, instance.scale);
                }
                state.age += Mathf.Max(0f, dt);
                this.ApplyOrbits(state, state.age);
                if (instance.pendingSpawn == false) this.PrepareModules(state, entities[i], dt, newOwner: false);
                // A module can toggle a branch for this owner (for example loot's
                // warning). Its activation history must never come from the last owner.
                foreach (var node in this.nodes) {
                    if (node.simulation.gameObject.activeInHierarchy == false) {
                        node.simulation.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
                if (instance.pendingSpawn == false) {
                    for (int j = 0; j < this.nodes.Length; ++j) {
                        var simulation = this.nodes[j].simulation;
                        if (simulation.gameObject.activeInHierarchy == true && state.nodes[j].active == false) {
                            // A view module may have called Play while reactivating this node.
                            simulation.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
                            simulation.randomSeed = Unity.Mathematics.math.hash(new Unity.Mathematics.uint2(instance.randomSeed, (uint)j + 1u)) | 1u;
                            simulation.Simulate(0f, withChildren: false, restart: true, fixedTimeStep: false);
                        }
                    }
                }
                if (dt > 0f) this.Simulate(dt, restart: false);
                for (int orbit = 0; orbit < this.orbits.Length; ++orbit) state.orbitPositions[orbit] = this.orbits[orbit].transform.localPosition;
                this.meshes.Append(this.simulationRoot.transform.localToWorldMatrix, instance.randomSeed);
                state.scale = instance.scale;
                for (int j = 0; j < this.nodes.Length; ++j) {
                    var node = this.nodes[j];
                    var snapshot = state.nodes[j];
                    snapshot.activeSelf = node.simulation.gameObject.activeSelf;
                    snapshot.active = node.simulation.gameObject.activeInHierarchy;
                    // Unity leaves inactive fields (notably angularVelocity when
                    // Rotation is disabled) untouched. They must be zero before a
                    // round trip, or SetParticles introduces arbitrary motion.
                    snapshot.particles.Clear();
                    snapshot.particles.Resize(node.simulation.particleCount, NativeArrayOptions.ClearMemory);
                    var count = node.simulation.GetParticles(snapshot.particles.AsArray());
                    snapshot.particles.ResizeUninitialized(count);
                    if (node.hasTrails == true) {
                        node.simulation.GetTrails(ref snapshot.trails);
                    }
                    snapshot.playback = node.simulation.GetPlaybackState();
                }
                for (int j = 0; j < this.nodes.Length; ++j) {
                    var node = this.nodes[j];
                    var snapshot = state.nodes[j];
                    node.simulation.SetParticles(snapshot.particles.AsArray(), snapshot.particles.Length);
                }
                foreach (var node in this.nodes) {
                    if (node.simulation.gameObject.activeInHierarchy == false) continue;
                    if (node.hasTrails == true) node.trails.Append(node.simulation, camera);
                    node.geometry.Append(node.simulation, camera);
                }
            }
            foreach (var node in this.nodes) {
                node.geometry.Commit();
                node.trails?.Commit();
            }
            this.meshes.Commit();
            if (this.meshInstance == false) this.meshes.Hide();
        }

        private void PrepareModules(Owner state, Ent owner, float dt, bool newOwner) {
            this.meshes.Prepare();
            var viewData = new ViewData(owner, default);
            foreach (var module in state.modules) module.PrepareBatch(in viewData, dt, newOwner);
        }

        private void SetPose(Vector3 position, Quaternion rotation, Vector3 scale) {
            this.simulationRoot.transform.SetPositionAndRotation(position, rotation);
            this.simulationRoot.transform.localScale = scale;
        }

        private void Simulate(float dt, bool restart) {
            foreach (var system in this.roots) {
                if (system.gameObject.activeInHierarchy == true) system.Simulate(dt, withChildren: true, restart: restart, fixedTimeStep: false);
            }
        }

        private void ApplyOrbits(Owner state, float age) {
            for (int i = 0; i < this.orbits.Length; ++i) {
                var orbit = this.orbits[i];
                var center = state.orbitCenters[i];
                orbit.transform.position = new Vector3(center.x + Mathf.Cos(age * orbit.speed) * orbit.radius,
                    orbit.transform.position.y, center.z + Mathf.Sin(age * orbit.speed) * orbit.radius);
            }
        }

        public void Dispose() {
            foreach (var owner in this.owners) owner.Dispose();
            foreach (var owner in this.unusedOwners) owner.Dispose();
            foreach (var node in this.nodes) node.Dispose();
            this.meshes.Dispose();
        }
    }
}
