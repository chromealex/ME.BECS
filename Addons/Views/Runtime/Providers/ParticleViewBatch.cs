using System;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

namespace ME.BECS.Views {

    // Owners use a prepared resident graph when available; overflow keeps the
    // time-sliced graph. All simulations append to the same ordered outputs.
    internal sealed class ParticleViewBatch : IDisposable {
        private static readonly ProfilerMarker RestoreMarker = new ProfilerMarker("[Particles Batch] Restore owner");
        private static readonly ProfilerMarker InitializeMarker = new ProfilerMarker("[Particles Batch] Initialize owner");
        private static readonly ProfilerMarker ModulesMarker = new ProfilerMarker("[Particles Batch] Prepare modules");
        private static readonly ProfilerMarker SimulationMarker = new ProfilerMarker("[Particles Batch] Simulate");
        private static readonly ProfilerMarker SnapshotMarker = new ProfilerMarker("[Particles Batch] Capture snapshots");
        private static readonly ProfilerMarker RenderSyncMarker = new ProfilerMarker("[Particles Batch] Sync resident particles");

        private sealed class Snapshot : IDisposable {
            internal ParticleSystem.PlaybackState playback;
            internal NativeList<ParticleSystem.Particle> particles = new NativeList<ParticleSystem.Particle>(Allocator.Persistent);
            internal ParticleSystem.Trails trails;
            internal bool activeSelf;
            internal bool active;
            public void Dispose() => this.particles.Dispose();
        }

        private sealed class Owner : IDisposable {
            internal Simulation simulation;
            internal Snapshot[] nodes;
            internal Vector3 scale;
            internal IBatchedParticleViewModule[] modules;
            internal Vector3[] orbitCenters;
            internal Vector3[] orbitPositions;
            internal float age;
            internal bool transformHistoryPending;
            public void Dispose() {
                foreach (var node in this.nodes) node?.Dispose();
            }
        }

        private sealed class Node : IDisposable {
            internal ParticleSystem simulation;
            internal ParticleSystemRenderer renderer;
            internal ParticleGeometryBatch geometry;
            internal bool hasTrails;
            internal ParticleGeometryBatch trails;
            public void Dispose() {
                this.geometry?.Dispose();
                this.trails?.Dispose();
            }
        }

        private sealed class Simulation : IDisposable {
            internal readonly GameObject root;
            internal readonly Transform transform;
            internal readonly Node[] nodes;
            internal readonly ParticleSystem[] roots;
            internal readonly IBatchedParticleViewModule[] modules;
            internal readonly BatchedParticleView.Orbit[] orbits;
            internal readonly Vector3[] orbitLocalPositions;
            internal readonly ParticleMeshBatch meshes;
            internal readonly ParticleMeshBatch.Sources meshSources;
            internal readonly bool resident;
            internal Owner residentOwner;

            internal Simulation(EntityView prefab, Transform parent, Simulation shared = null) {
                this.resident = shared != null;
                this.root = UnityEngine.Object.Instantiate(prefab.gameObject, parent);
                this.transform = this.root.transform;
                try {
                    foreach (var source in this.root.GetComponentsInChildren<AudioSource>(true)) {
                        source.playOnAwake = false;
                        source.Stop();
                        source.enabled = false;
                    }
                    this.root.SetActive(true);
                    this.orbits = this.root.GetComponent<BatchedParticleView>().orbits;
                    this.orbitLocalPositions = new Vector3[this.orbits.Length];
                    for (int i = 0; i < this.orbits.Length; ++i) this.orbitLocalPositions[i] = this.orbits[i].transform.localPosition;
                    this.meshSources = ParticleMeshBatch.CaptureSources(this.root);
                    this.meshes = this.resident == true ? shared.meshes : new ParticleMeshBatch(this.root, parent);
                    var modules = new System.Collections.Generic.List<IBatchedParticleViewModule>();
                    foreach (var item in this.root.GetComponent<EntityView>().GetAllModules().items) {
                        if (item.enabled == true && item.module is IBatchedParticleViewModule module) modules.Add(module);
                    }
                    this.modules = modules.ToArray();
                    foreach (var behaviour in this.root.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                    var systems = this.root.GetComponentsInChildren<ParticleSystem>(includeInactive: false);
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
                        renderer.enableGPUInstancing = false;
                        var hasTrails = simulation.trails.enabled;
                        var node = new Node { simulation = simulation, renderer = renderer, hasTrails = hasTrails };
                        this.nodes[i] = node;
                        if (this.resident == true) {
                            node.geometry = shared.nodes[i].geometry;
                            node.trails = shared.nodes[i].trails;
                        } else {
                            node.geometry = new ParticleGeometryBatch(parent, renderer);
                            if (hasTrails == true) node.trails = new ParticleGeometryBatch(parent, renderer, trails: true);
                        }
                        var ancestor = simulation.transform.parent;
                        while (ancestor != null && ancestor != this.transform.parent && ancestor.GetComponent<ParticleSystem>() == null) ancestor = ancestor.parent;
                        if (ancestor == null || ancestor == this.transform.parent) rootSystems.Add(simulation);
                        simulation.name = "[Simulation] " + simulation.name;
                    }
                    foreach (var renderer in this.root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    foreach (var light in this.root.GetComponentsInChildren<Light>(true)) light.enabled = false;
                    foreach (var renderer in this.root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
                    this.roots = rootSystems.ToArray();
                    if (this.resident == true) this.residentOwner = this.CreateOwner();
                } catch {
                    this.Dispose();
                    throw;
                }
            }

            internal Owner CreateOwner() {
                var owner = new Owner { simulation = this, nodes = new Snapshot[this.nodes.Length],
                    modules = new IBatchedParticleViewModule[this.modules.Length],
                    orbitCenters = new Vector3[this.orbits.Length], orbitPositions = new Vector3[this.orbits.Length] };
                try {
                    for (int j = 0; j < this.nodes.Length; ++j) owner.nodes[j] = new Snapshot();
                    for (int j = 0; j < this.modules.Length; ++j) owner.modules[j] = this.modules[j].CreateBatchInstance();
                } catch {
                    owner.Dispose();
                    throw;
                }
                return owner;
            }

            internal void SetPose(Vector3 position, Quaternion rotation, Vector3 scale) {
                this.transform.SetPositionAndRotation(position, rotation);
                this.transform.localScale = scale;
            }

            internal void Simulate(float dt, bool restart) {
                using var marker = SimulationMarker.Auto();
                foreach (var system in this.roots) {
                    if (system.gameObject.activeInHierarchy == true) system.Simulate(dt, withChildren: true, restart: restart, fixedTimeStep: false);
                }
            }

            internal void ApplyOrbits(Owner state, float age) {
                for (int i = 0; i < this.orbits.Length; ++i) {
                    var orbit = this.orbits[i];
                    var center = state.orbitCenters[i];
                    orbit.transform.position = new Vector3(center.x + Mathf.Cos(age * orbit.speed) * orbit.radius,
                        orbit.transform.position.y, center.z + Mathf.Sin(age * orbit.speed) * orbit.radius);
                }
            }

            internal void Stop() {
                foreach (var node in this.nodes) node.simulation.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
                this.meshes.Hide(this.meshSources);
            }

            public void Dispose() {
                this.residentOwner?.Dispose();
                if (this.resident == false) {
                    if (this.nodes != null) {
                        foreach (var node in this.nodes) {
                            node?.Dispose();
                        }
                    }
                    this.meshes?.Dispose();
                }
                if (this.root != null) UnityEngine.Object.DestroyImmediate(this.root);
            }
        }

        private readonly EntityView prefab;
        private readonly Transform parent;
        private readonly Simulation shared;
        private readonly System.Collections.Generic.List<Simulation> residentSimulations = new System.Collections.Generic.List<Simulation>();
        private readonly System.Collections.Generic.Stack<Owner> freeResidentOwners = new System.Collections.Generic.Stack<Owner>();
        private readonly System.Collections.Generic.List<Owner> owners = new System.Collections.Generic.List<Owner>();
        private readonly System.Collections.Generic.Stack<Owner> unusedOwners = new System.Collections.Generic.Stack<Owner>();
        private readonly bool meshInstance;
        private readonly ParticleMeshBatch meshes;
        internal uint ResidentSystemCost { get; }
        internal int ResidentCount => this.residentSimulations.Count;

        internal ParticleViewBatch(EntityView prefab, Transform parent, BatchedParticleView.Mode mode) {
            this.prefab = prefab;
            this.parent = parent;
            this.meshInstance = mode == BatchedParticleView.Mode.MeshInstance;
            if (this.meshInstance == true) {
                this.meshes = new ParticleMeshBatch(prefab.gameObject, parent);
                return;
            }
            this.shared = new Simulation(prefab, parent);
            this.meshes = this.shared.meshes;
            this.ResidentSystemCost = (uint)prefab.GetComponentsInChildren<ParticleSystem>(true).Length;
        }

        internal void PrepareResident() {
            var simulation = new Simulation(this.prefab, this.parent, this.shared);
            this.residentSimulations.Add(simulation);
            this.freeResidentOwners.Push(simulation.residentOwner);
        }

        internal void RemoveAtSwapBack(int index) {
            if (this.meshInstance == true || index >= this.owners.Count) return;
            var removed = this.owners[index];
            this.owners[index] = this.owners[this.owners.Count - 1];
            this.owners.RemoveAt(this.owners.Count - 1);
            if (removed.simulation.resident == true) {
                removed.simulation.Stop();
                this.freeResidentOwners.Push(removed);
            } else {
                this.unusedOwners.Push(removed);
            }
        }

        internal void Commit(NativeList<ParticlesProvider.ParticleInstanceData> instances, NativeList<Ent> entities, float dt, Camera camera) {
            this.meshes.Clear();
            if (this.shared != null) {
                foreach (var node in this.shared.nodes) {
                    node.geometry.Clear();
                    node.trails?.Clear();
                }
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
                    if (this.freeResidentOwners.Count > 0) {
                        owner = this.freeResidentOwners.Pop();
                    } else if (this.unusedOwners.Count > 0) {
                        owner = this.unusedOwners.Pop();
                    } else {
                        owner = this.shared.CreateOwner();
                    }
                    this.owners.Add(owner);
                }
                CommitOwner(this.owners[i], in instance, entities[i], dt, camera);
            }
            if (this.shared != null) {
                foreach (var node in this.shared.nodes) {
                    node.geometry.Commit();
                    node.trails?.Commit();
                }
            }
            this.meshes.Commit();
        }

        private static void CommitOwner(Owner state, in ParticlesProvider.ParticleInstanceData instance, Ent entity, float dt, Camera camera) {
            var graph = state.simulation;
            if (instance.pendingSpawn == true) {
                using var marker = InitializeMarker.Auto();
                for (int orbit = 0; orbit < graph.orbits.Length; ++orbit) graph.orbits[orbit].transform.localPosition = graph.orbitLocalPositions[orbit];
                graph.SetPose((Vector3)instance.position, (Quaternion)(Unity.Mathematics.quaternion)instance.rotation, instance.scale);
                state.age = 0f;
                for (int orbit = 0; orbit < graph.orbits.Length; ++orbit) state.orbitCenters[orbit] = graph.orbits[orbit].transform.position;
                for (int j = 0; j < graph.nodes.Length; ++j) {
                    graph.nodes[j].simulation.gameObject.SetActive(true);
                    graph.nodes[j].simulation.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    graph.nodes[j].simulation.randomSeed = Unity.Mathematics.math.hash(new Unity.Mathematics.uint2(instance.randomSeed, (uint)j + 1u)) | 1u;
                }
                PrepareModules(state, entity, dt, newOwner: true);
                graph.Simulate(0f, restart: true);
            } else if (graph.resident == false) {
                using var marker = RestoreMarker.Auto();
                for (int orbit = 0; orbit < graph.orbits.Length; ++orbit) graph.orbits[orbit].transform.localPosition = state.orbitPositions[orbit];
                graph.SetPose(instance.previousPosition, instance.previousRotation, state.scale);
                graph.ApplyOrbits(state, state.age);
                for (int j = 0; j < graph.nodes.Length; ++j) graph.nodes[j].simulation.gameObject.SetActive(state.nodes[j].activeSelf);
                foreach (var node in graph.nodes) node.simulation.Clear(withChildren: false);
                foreach (var node in graph.nodes) {
                    var simulation = node.simulation;
                    if (simulation.gameObject.activeInHierarchy == false) continue;
                    simulation.Play(withChildren: false);
                    simulation.Pause(withChildren: false);
                }
                for (int j = 0; j < graph.nodes.Length; ++j) {
                    var snapshot = state.nodes[j];
                    var simulation = graph.nodes[j].simulation;
                    if (simulation.gameObject.activeInHierarchy == false) continue;
                    simulation.SetPlaybackState(snapshot.playback);
                    simulation.SetParticles(snapshot.particles.AsArray(), snapshot.particles.Length);
                    if (graph.nodes[j].hasTrails == true) simulation.SetTrails(snapshot.trails);
                }
                // Shared graphs must prime history before moving to another owner.
                if (dt > 0f) graph.Simulate(0f, restart: false);
            } else if (dt > 0f && state.transformHistoryPending == true) {
                // A zero-delta commit can move the view without advancing native
                // transform history. Discard that paused motion before resuming.
                graph.SetPose(instance.previousPosition, instance.previousRotation, state.scale);
                graph.ApplyOrbits(state, state.age);
                graph.Simulate(0f, restart: false);
            }
            graph.SetPose((Vector3)instance.position, (Quaternion)(Unity.Mathematics.quaternion)instance.rotation, instance.scale);
            state.age += Mathf.Max(0f, dt);
            graph.ApplyOrbits(state, state.age);
            if (instance.pendingSpawn == false) PrepareModules(state, entity, dt, newOwner: false);
            foreach (var node in graph.nodes) {
                if (node.simulation.gameObject.activeInHierarchy == false) {
                    node.simulation.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            if (instance.pendingSpawn == false) {
                for (int j = 0; j < graph.nodes.Length; ++j) {
                    var simulation = graph.nodes[j].simulation;
                    if (simulation.gameObject.activeInHierarchy == true && state.nodes[j].active == false) {
                        simulation.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        simulation.randomSeed = Unity.Mathematics.math.hash(new Unity.Mathematics.uint2(instance.randomSeed, (uint)j + 1u)) | 1u;
                        simulation.Simulate(0f, withChildren: false, restart: true, fixedTimeStep: false);
                    }
                }
            }
            if (dt > 0f) graph.Simulate(dt, restart: false);
            state.transformHistoryPending = dt <= 0f;
            for (int orbit = 0; orbit < graph.orbits.Length; ++orbit) state.orbitPositions[orbit] = graph.orbits[orbit].transform.localPosition;
            graph.meshes.Append(graph.transform.localToWorldMatrix, instance.randomSeed, graph.meshSources);
            graph.meshes.Hide(graph.meshSources);
            state.scale = instance.scale;
            using (graph.resident == true ? RenderSyncMarker.Auto() : SnapshotMarker.Auto()) {
                for (int j = 0; j < graph.nodes.Length; ++j) {
                    var node = graph.nodes[j];
                    var snapshot = state.nodes[j];
                    snapshot.activeSelf = node.simulation.gameObject.activeSelf;
                    snapshot.active = node.simulation.gameObject.activeInHierarchy;
                    // Unity leaves inactive fields (notably angularVelocity) untouched.
                    // Keep zeroed staging and all-node SetParticles before native Bake.
                    snapshot.particles.Clear();
                    snapshot.particles.Resize(node.simulation.particleCount, NativeArrayOptions.ClearMemory);
                    var count = node.simulation.GetParticles(snapshot.particles.AsArray());
                    snapshot.particles.ResizeUninitialized(count);
                    if (graph.resident == false) {
                        if (node.hasTrails == true) node.simulation.GetTrails(ref snapshot.trails);
                        snapshot.playback = node.simulation.GetPlaybackState();
                    }
                }
                for (int j = 0; j < graph.nodes.Length; ++j) {
                    var snapshot = state.nodes[j];
                    graph.nodes[j].simulation.SetParticles(snapshot.particles.AsArray(), snapshot.particles.Length);
                }
            }
            foreach (var node in graph.nodes) {
                if (node.simulation.gameObject.activeInHierarchy == false) continue;
                if (node.hasTrails == true) node.trails.AppendFrom(node.simulation, node.renderer, camera);
                node.geometry.AppendFrom(node.simulation, node.renderer, camera);
            }
        }

        private static void PrepareModules(Owner state, Ent owner, float dt, bool newOwner) {
            using var marker = ModulesMarker.Auto();
            state.simulation.meshes.Prepare(state.simulation.meshSources);
            var viewData = new ViewData(owner, default);
            foreach (var module in state.modules) module.PrepareBatch(in viewData, dt, newOwner);
        }

        public void Dispose() {
            foreach (var owner in this.owners) {
                if (owner.simulation.resident == false) owner.Dispose();
            }
            foreach (var owner in this.unusedOwners) owner.Dispose();
            foreach (var simulation in this.residentSimulations) simulation.Dispose();
            this.shared?.Dispose();
            if (this.meshInstance == true) this.meshes.Dispose();
        }
    }
}
