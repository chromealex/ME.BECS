
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ME.BECS.Transforms;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Jobs;
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

[assembly: ME.BECS.CodeGeneratorInclude(typeof(ME.BECS.Views.ParticlesProviderTag))]

namespace ME.BECS.Views {

    using Unity.Collections;
    using Unity.Jobs;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using UnityEngine.Pool;
    using um = Unity.Mathematics;

    /// <summary>
    /// Stores per-entity state for particles provider tag.
    /// </summary>
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct ParticlesProviderTag : IComponent {}

    /// <summary>
    /// Updates view presentation for particles provider.
    /// </summary>
    [BURST]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct ParticlesProvider : IViewProvider<EntityView>, IViewProviderRoot {
        private static readonly Unity.Profiling.ProfilerMarker CommitMarker = new Unity.Profiling.ProfilerMarker("[Particles Provider] Commit");
        private static readonly Unity.Profiling.ProfilerMarker MoveMarker = new Unity.Profiling.ProfilerMarker("[Particles Provider] Move transforms");

        /// <summary>
        /// Stores particle system info for <c>ParticlesProvider</c>.
        /// </summary>
        public struct ParticleSystemInfo {
            /// <summary>
            /// Particle system used by <c>ParticlesProvider.ParticleSystemInfo</c>.
            /// </summary>
            public ParticleSystem particleSystem;
            public NativeList<ParticleSystem.Particle> particles;
            internal ParticleEmitter[] emitters;
            internal ParticleViewBatch batch;
        }

        internal sealed class ParticleEmitter {
            internal ParticleSystem proxy;
            internal NativeList<ParticleSystem.Particle> particles;
            internal Vector3 localPosition;
            internal Quaternion localRotation;
            internal ParticleSystem burstSystem;
            internal ParticleSystem.Burst burst;
            internal NativeList<ParticleSystem.Particle> burstParticles;
        }

        /// <summary>
        /// Stores particle instance data for <c>ParticlesProvider</c>.
        /// </summary>
        public struct ParticleInstanceData {
            /// <summary>
            /// Position in the coordinate space used by the containing API.
            /// </summary>
            public float3 position;
            /// <summary>
            /// Orientation in the coordinate space used by the containing API.
            /// </summary>
            public quaternion rotation;
            public Vector3 previousPosition;
            public Quaternion previousRotation;
            public uint randomSeed;
            public bool pendingSpawn;
            public Vector3 scale;
        }

        /// <summary>
        /// Defines objects per prefab state and operations for <c>ParticlesProvider</c>.
        /// </summary>
        public struct ObjectsPerPrefab {

            /// <summary>
            /// Instances used by <c>ParticlesProvider.ObjectsPerPrefab</c>.
            /// </summary>
            public NativeList<ParticleInstanceData> instances;
            /// <summary>
            /// Entity handles processed or stored by this operation.
            /// </summary>
            public NativeList<Ent> entities;
            /// <summary>
            /// Indicates is dirty.
            /// </summary>
            public bool isDirty;

        }

        private Dictionary<uint, ObjectsPerPrefab> objectsPerPrefab;
        private Dictionary<uint, ParticleSystemInfo> systemForPrefab;
        private ViewsModuleProperties properties;

        private Dictionary<Ent, uint> entityToPrefabId;
        private Dictionary<Ent, int> entityToInstanceIndex;

        private Transform particlesRoot;
        private NativeList<Jobs.InterpolationTempData> interpolationResults;
        private uint nextParticleSeed;
        private Camera batchCamera;

        /// <summary>
        /// Returns root.
        /// </summary>
        public Transform GetRoot() => this.particlesRoot;

        /// <summary>
        /// Initializes particles provider state from the supplied context.
        /// </summary>
        public void Initialize(uint providerId, World viewsWorld, ViewsModuleProperties properties) {

            UnsafeViewsModule.RegisterProviderType<ParticlesProviderTag>(providerId);

            this.properties = properties;
            this.objectsPerPrefab = DictionaryPool<uint, ObjectsPerPrefab>.Get();
            this.systemForPrefab = DictionaryPool<uint, ParticleSystemInfo>.Get();
            this.entityToPrefabId = DictionaryPool<Ent, uint>.Get();
            this.entityToInstanceIndex = DictionaryPool<Ent, int>.Get();
            this.interpolationResults = new NativeList<Jobs.InterpolationTempData>(Allocator.Persistent);
            this.nextParticleSeed = 1u;

            this.particlesRoot = new GameObject("[Particles Provider] Root").transform;
            if (Application.isPlaying) GameObject.DontDestroyOnLoad(this.particlesRoot.gameObject);

            this.objectsPerPrefab.EnsureCapacity((int)properties.instancesRegistryCapacity);
            this.systemForPrefab.EnsureCapacity((int)properties.instancesRegistryCapacity);

        }

        private ObjectsPerPrefab GetOrCreateSystem(uint prefabId, safe_ptr<SourceRegistry.Info> prefabInfo) {

            if (this.objectsPerPrefab.TryGetValue(prefabId, out var objects)) {
                return objects;
            }

            var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(prefabInfo.ptr->prefabPtr);
            if (prefabInfo.ptr->isLoaded == false) {
                throw new System.Exception("Prefab was not loaded, but we are trying to instantiate it.");
            }
            var rootParticleSystem = CreateEmitterProxy(this.particlesRoot);
            rootParticleSystem.name = $"ParticleSystem_{prefabId}";

            var prefab = (EntityView)handle.Target;

            var batchSettings = prefab.GetComponent<BatchedParticleView>();
            if (batchSettings != null) {
                var batch = new ParticleViewBatch(prefab, rootParticleSystem.transform, batchSettings.mode);
                this.systemForPrefab.Add(prefabId, new ParticleSystemInfo() {
                    particleSystem = rootParticleSystem,
                    emitters = Array.Empty<ParticleEmitter>(),
                    batch = batch,
                });
                objects = new ObjectsPerPrefab() {
                    instances = new NativeList<ParticleInstanceData>((int)this.properties.renderingObjectsCapacity, Allocator.Persistent),
                    entities = new NativeList<Ent>((int)this.properties.renderingObjectsCapacity, Allocator.Persistent),
                    isDirty = true,
                };
                this.objectsPerPrefab.Add(prefabId, objects);
                return objects;
            }

            var prefabInstance = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity, rootParticleSystem.transform);
            var requiredParticleSystems = prefabInstance.GetComponentsInChildren<ParticleSystem>(includeInactive: false);
            var referencedSystems = new System.Collections.Generic.HashSet<ParticleSystem>();
            foreach (var source in requiredParticleSystems) {
                var sub = source.subEmitters;
                if (sub.enabled == false) continue;
                for (int i = 0; i < sub.subEmittersCount; ++i) referencedSystems.Add(sub.GetSubEmitterSystem(i));
            }
            var emitters = new System.Collections.Generic.List<ParticleEmitter>();
            var sourcePoses = new System.Collections.Generic.List<(ParticleSystem source, Vector3 position, Quaternion rotation)>();
            foreach (var source in requiredParticleSystems) {
                if (referencedSystems.Contains(source) == true) continue;
                sourcePoses.Add((source, rootParticleSystem.transform.InverseTransformPoint(source.transform.position),
                    Quaternion.Inverse(rootParticleSystem.transform.rotation) * source.transform.rotation));
            }
            foreach (var requiredParticleSystem in requiredParticleSystems) {

                requiredParticleSystem.Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = requiredParticleSystem.main;
                main.prewarm = false;
                main.playOnAwake = false;

                main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
                main.simulationSpace = UnityEngine.ParticleSystemSimulationSpace.World;

                var inheritVelocity = requiredParticleSystem.inheritVelocity;
                inheritVelocity.enabled = true;
                inheritVelocity.mode = ParticleSystemInheritVelocityMode.Current;
                inheritVelocity.curveMultiplier = 1;

            }

            foreach (var sourcePose in sourcePoses) {
                var proxy = emitters.Count == 0 ? rootParticleSystem : CreateEmitterProxy(rootParticleSystem.transform);
                var emitter = new ParticleEmitter() {
                    proxy = proxy,
                    particles = new NativeList<ParticleSystem.Particle>(Allocator.Persistent),
                    localPosition = sourcePose.position,
                    localRotation = sourcePose.rotation,
                };
                var sourceRenderer = sourcePose.source.GetComponent<ParticleSystemRenderer>();
                if (sourceRenderer != null && sourceRenderer.renderMode == ParticleSystemRenderMode.Mesh) {
                    var main = sourcePose.source.main;
                    if (TryGetInstantBurst(sourcePose.source, out var burst) == true) {
                        // Emit the authored burst natively, then compose every sampled rotation.
                        // Native InheritRotation only adds Euler angles and cannot do this.
                        emitter.burstSystem = sourcePose.source;
                        emitter.burst = burst;
                        emitter.burstParticles = new NativeList<ParticleSystem.Particle>(Allocator.Persistent);
                        var emission = sourcePose.source.emission;
                        emission.enabled = false;
                        var inheritVelocity = sourcePose.source.inheritVelocity;
                        inheritVelocity.enabled = false;
                        main.stopAction = ParticleSystemStopAction.None;
                    } else if (main.startRotation3D == false) {
                        // Local mesh alignment used the emitter transform for all three axes.
                        // World alignment needs those axes in each inherited particle rotation.
                        // X/Y curves are inactive authoring data in 1D mode; do not enable them.
                        main.startRotationX = 0f;
                        main.startRotationY = 0f;
                        main.startRotation3D = true;
                    }
                    if (emitter.burstSystem == null && main.startRotation3D == true) {
                        var x = main.startRotationX; var y = main.startRotationY;
                        if (x.mode == ParticleSystemCurveMode.Constant && y.mode == ParticleSystemCurveMode.Constant &&
                            (x.constant == 0f && y.constant == 0f || main.flipRotation == 0f && sourcePose.source.shape.enabled == false)) {
                            // Native inheritance adds Euler components. Keep per-particle Z rotation
                            // native, and compose the constant XY part in quaternion space first.
                            emitter.localRotation *= Quaternion.Euler(x.constant * Mathf.Rad2Deg, y.constant * Mathf.Rad2Deg, 0f);
                            main.startRotationX = 0f; main.startRotationY = 0f;
                        } else {
                            Logger.Views.Warning($"ParticlesProvider mesh '{sourcePose.source.name}' needs per-particle XY rotation; keep this effect on GameObjectProvider for exact orientation.");
                        }
                    }
                    sourceRenderer.alignment = ParticleSystemRenderSpace.World;
                }
                sourcePose.source.transform.SetParent(proxy.transform, worldPositionStays: true);
                sourcePose.source.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (emitter.burstSystem == null) {
                    var subEmitters = proxy.subEmitters;
                    subEmitters.enabled = true;
                    subEmitters.AddSubEmitter(sourcePose.source, ParticleSystemSubEmitterType.Birth,
                        ParticleSystemSubEmitterProperties.InheritRotation, emitProbability: 1f);
                } else {
                    emitter.burstSystem.Play(withChildren: false);
                }
                proxy.Play(withChildren: false);
                emitters.Add(emitter);
            }
            if (emitters.Count == 0) {
                rootParticleSystem.Play(withChildren: false);
                emitters.Add(new ParticleEmitter() {
                    proxy = rootParticleSystem,
                    localRotation = Quaternion.identity,
                    particles = new NativeList<ParticleSystem.Particle>(Allocator.Persistent),
                });
            }

            var renderers = rootParticleSystem.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers) {
                if (renderer is not ParticleSystemRenderer) {
                    renderer.enabled = false;
                }
            }

            var systemInfo = new ParticleSystemInfo {
                particleSystem = rootParticleSystem,
                particles = emitters[0].particles,
                emitters = emitters.ToArray(),
            };

            objects = new ObjectsPerPrefab {
                instances = new NativeList<ParticleInstanceData>((int)this.properties.renderingObjectsCapacity, Allocator.Persistent),
                entities = new NativeList<Ent>((int)this.properties.renderingObjectsCapacity, Allocator.Persistent),
                isDirty = true,
            };

            this.objectsPerPrefab.Add(prefabId, objects);
            this.systemForPrefab.Add(prefabId, systemInfo);

            return objects;

        }

        private static ParticleSystem CreateEmitterProxy(Transform parent) {
            var go = new GameObject("Emitter proxy");
            go.transform.SetParent(parent, worldPositionStays: false);
            var proxy = go.AddComponent<ParticleSystem>();
            proxy.Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
            proxy.useAutoRandomSeed = false; proxy.randomSeed = 1u;
            var main = proxy.main;
            main.playOnAwake = false; main.loop = false; main.duration = 10_000f;
            main.startLifetime = 10_000f; main.startRotation3D = true; main.startSpeed = 0f;
            main.maxParticles = 1; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.ringBufferMode = ParticleSystemRingBufferMode.PauseUntilReplaced;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = proxy.emission; emission.enabled = false;
            var shape = proxy.shape; shape.enabled = false;
            proxy.GetComponent<ParticleSystemRenderer>().enabled = false;
            return proxy;
        }

        private static bool IsZero(ParticleSystem.MinMaxCurve curve) {
            return curve.mode == ParticleSystemCurveMode.Constant && curve.constant == 0f ||
                   curve.mode == ParticleSystemCurveMode.TwoConstants && curve.constantMin == 0f && curve.constantMax == 0f;
        }

        private static bool TryGetInstantBurst(ParticleSystem source, out ParticleSystem.Burst burst) {
            burst = default;
            var main = source.main;
            var emission = source.emission;
            if (main.loop == true || main.ringBufferMode != ParticleSystemRingBufferMode.Disabled ||
                IsZero(main.startDelay) == false || emission.enabled == false ||
                IsZero(emission.rateOverTime) == false || IsZero(emission.rateOverDistance) == false ||
                emission.burstCount != 1 || source.subEmitters.enabled == true || source.collision.enabled == true ||
                source.trigger.enabled == true || source.colorBySpeed.enabled == true || source.sizeBySpeed.enabled == true ||
                source.rotationBySpeed.enabled == true || source.shape.alignToDirection == true ||
                source.GetComponent<ParticleSystemRenderer>().alignment != ParticleSystemRenderSpace.Local) return false;
            var rotation = source.rotationOverLifetime;
            if (rotation.enabled == true && rotation.separateAxes == true &&
                (IsZero(rotation.x) == false || IsZero(rotation.y) == false)) return false;
            burst = emission.GetBurst(0);
            return burst.time == 0f && burst.cycleCount == 1;
        }

        private static void EmitBurst(ParticleEmitter emitter, in ParticleInstanceData instance) {
            var source = emitter.burstSystem;
            var random = um::Random.CreateFromIndex(um::math.hash(new um::uint2(instance.randomSeed, source.randomSeed)));
            if (random.NextFloat() >= emitter.burst.probability) return;
            var curve = emitter.burst.count;
            var count = curve.mode == ParticleSystemCurveMode.TwoConstants
                ? random.NextInt((int)curve.constantMin, (int)curve.constantMax + 1)
                : Mathf.RoundToInt(curve.Evaluate(0f, random.NextFloat()));
            if (count <= 0) return;
            var rotation = (Quaternion)(um::quaternion)instance.rotation * emitter.localRotation;
            var position = (Vector3)instance.position + (Quaternion)(um::quaternion)instance.rotation * emitter.localPosition;
            var sourceTransform = source.transform;
            sourceTransform.SetPositionAndRotation(position, rotation);
            var previousTime = source.time;
            try {
                source.Play(withChildren: false);
                // Main-module curves belong to a newly started effect, not the shared clock.
                source.time = 0f;
                var previousCount = source.particleCount;
                source.Emit(count);
                var emittedCount = source.particleCount - previousCount;
                if (emittedCount <= 0) return;
                emitter.burstParticles.Clear();
                emitter.burstParticles.Resize(emittedCount, NativeArrayOptions.ClearMemory);
                var particles = emitter.burstParticles.AsArray();
                emittedCount = source.GetParticles(particles, emittedCount, previousCount);
                for (int i = 0; i < emittedCount; ++i) {
                    var particle = particles[i];
                    particle.rotation3D = Quaternion.Normalize(rotation * Quaternion.Euler(particle.rotation3D)).eulerAngles;
                    particles[i] = particle;
                }
                source.SetParticles(particles, emittedCount, previousCount);
            } finally {
                source.time = previousTime;
                sourceTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }

        /// <summary>
        /// Creates or reuses a presentation instance for the requested entity.
        /// </summary>
        public JobHandle Spawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {

            dependsOn.Complete();

            for (int i = 0; i < data.ptr->toAddTemp.Length; ++i) {

                var item = data.ptr->toAddTemp[i];
                var prefabId = (uint)item.prefabInfo.info.ptr->prefabId;

                this.entityToPrefabId.Add(item.ent, prefabId);

                var objects = this.objectsPerPrefab[prefabId];

                objects.entities.Add(item.ent);
                var pos = item.ent.Read<ME.BECS.Transforms.WorldMatrixComponent>().value.c3.xyz;
                objects.instances.Add(new ParticleInstanceData() {
                    position = pos,
                    rotation = item.ent.GetAspect<TransformAspect>().rotation,
                    randomSeed = this.nextParticleSeed++,
                    pendingSpawn = true,
                    scale = Vector3.one,
                });
                foreach (var emitter in this.systemForPrefab[prefabId].emitters) emitter.particles.Add(default);
                objects.isDirty = true;

                this.entityToInstanceIndex.Add(item.ent, objects.entities.Length - 1);

                this.objectsPerPrefab[prefabId] = objects;

                var instanceInfo = new SceneInstanceInfo((System.IntPtr)item.ent.ToULong(), data.ptr->toAddTemp[i].prefabInfo.info, 0u, data.ptr->toAddTemp[i].localData);
                data.ptr->renderingOnScene.Add(ref data.ptr->viewsWorld.state.ptr->allocator, instanceInfo);

            }

            return dependsOn;

        }

        // Remove dead provider instances first, then transfer surviving owners as a
        // batch. Every lookup below refers to the original owner, independent of order.
        private void ApplyAssignments(safe_ptr<ViewsModuleData> data) {
            if (data.ptr->toAssign.Count() == 0) return;
            var transfers = DictionaryPool<Ent, (Ent destination, uint prefabId, int instanceIndex, uint renderIndex)>.Get();
            try {
                ref var allocator = ref data.ptr->viewsWorld.state.ptr->allocator;
                foreach (var assignment in data.ptr->toAssign) {
                    if (data.ptr->renderingOnSceneEntToRenderIndex.TryGetValue(in allocator, assignment.Value, out var index) == false ||
                        index >= data.ptr->renderingOnScene.Count) continue;
                    ref var instance = ref data.ptr->renderingOnScene[in allocator, index];
                    var source = new Ent((ulong)instance.obj);
                    var destination = data.ptr->renderingOnSceneEnts[(int)index].element;
                    if (source == destination) continue;
                    transfers[source] = (destination, this.entityToPrefabId[source], this.entityToInstanceIndex[source], index);
                }
                foreach (var transfer in transfers) {
                    this.entityToPrefabId.Remove(transfer.Key);
                    this.entityToInstanceIndex.Remove(transfer.Key);
                }
                foreach (var transfer in transfers) {
                    var item = transfer.Value;
                    this.entityToPrefabId.Add(item.destination, item.prefabId);
                    this.entityToInstanceIndex.Add(item.destination, item.instanceIndex);
                    var objects = this.objectsPerPrefab[item.prefabId];
                    objects.entities[item.instanceIndex] = item.destination;
                    objects.isDirty = true;
                    this.objectsPerPrefab[item.prefabId] = objects;
                    data.ptr->renderingOnScene[in allocator, item.renderIndex].obj = (System.IntPtr)item.destination.ToULong();
                }
            } finally {
                DictionaryPool<Ent, (Ent destination, uint prefabId, int instanceIndex, uint renderIndex)>.Release(transfers);
            }
        }

        /// <summary>
        /// Removes an active presentation instance and returns it to its provider.
        /// </summary>
        public JobHandle Despawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {

            dependsOn.Complete();

            // Read the simulated age before compacting the dense owner/particle arrays.
            // Birth sub-emitters use that age for delayed bursts and continuous emission.
            foreach (var system in this.systemForPrefab.Values) {
                foreach (var emitter in system.emitters) {
                    if (emitter.particles.Length > 0) emitter.proxy.GetParticles(emitter.particles.AsArray());
                }
            }

            for (int i = 0; i < data.ptr->toRemoveTemp.Length; i++) {

                var item = data.ptr->toRemoveTemp[i];
                var entToRemove = new Ent((ulong)item.obj);
                var prefabId = item.prefabInfo.ptr->prefabId;

                this.entityToPrefabId.Remove(entToRemove);

                var objects = this.objectsPerPrefab[prefabId];

                var lastEnt = objects.entities[^1];

                var removeIndex = this.entityToInstanceIndex[entToRemove];

                this.systemForPrefab[prefabId].batch?.RemoveAtSwapBack(removeIndex);

                objects.entities.RemoveAtSwapBack(removeIndex);
                objects.instances.RemoveAtSwapBack(removeIndex);
                foreach (var emitter in this.systemForPrefab[prefabId].emitters) emitter.particles.RemoveAtSwapBack(removeIndex);
                objects.isDirty = true;

                this.entityToPrefabId.Remove(entToRemove);
                this.entityToInstanceIndex.Remove(entToRemove);

                if (lastEnt != entToRemove) {
                    this.entityToInstanceIndex[lastEnt] = removeIndex;
                }

                this.objectsPerPrefab[prefabId] = objects;

            }

            this.ApplyAssignments(data);
            return dependsOn;

        }

        /// <summary>
        /// Commits the accumulated work to its destination.
        /// </summary>
        public JobHandle Commit(safe_ptr<ViewsModuleData> data, JobHandle dependsOn, float dt) {
            using var commitMarker = CommitMarker.Auto();

            // The provider's render count and dense entity list are produced by these jobs.
            dependsOn.Complete();
            var interpolate = data.ptr->properties.interpolateState == true &&
                              data.ptr->beginFrameState.ptr->state.ptr != null &&
                              data.ptr->beginFrameState.ptr->state.ptr->IsCreated == true;
            if (interpolate == true) {
                this.interpolationResults.ResizeUninitialized((int)data.ptr->renderingOnSceneCount);
                dependsOn = new Jobs.PrepareInterpolationFactorJob() {
                    data = data,
                    beginFrameState = data.ptr->beginFrameState.ptr->state,
                    currentTick = data.ptr->connectedWorld.CurrentTick,
                    tickTime = data.ptr->beginFrameState.ptr->tickTime,
                    currentTimeSinceStart = data.ptr->beginFrameState.ptr->timeSinceStart,
                }.Schedule(dependsOn);
                dependsOn = new Jobs.JobUpdateTransformsInterpolationNoHierarchyPrepare() {
                    renderingOnSceneEnts = data.ptr->renderingOnSceneEnts,
                    beginFrameState = data.ptr->beginFrameState.ptr->state,
                    data = data,
                    results = this.interpolationResults.AsArray(),
                }.Schedule(this.interpolationResults.Length, 32, dependsOn);
            }
            dependsOn.Complete();

            {

                using var marker = MoveMarker.Auto();
                // move

                for (int renderIndex = 0; renderIndex < data.ptr->renderingOnSceneCount; ++renderIndex) {

                    var entityData = data.ptr->renderingOnSceneEnts[renderIndex];
                    var ent = entityData.element;
                    var prefabId = this.entityToPrefabId[ent];
                    var objects = this.objectsPerPrefab[prefabId];
                    var instanceIndex = this.entityToInstanceIndex[ent];

                    var instance = objects.instances[instanceIndex];

                    Vector3 position;
                    Quaternion rotation;
                    Vector3 scale;
                    if (interpolate == true) {
                        var pose = this.interpolationResults[renderIndex];
                        position = pose.position;
                        rotation = pose.rotation;
                        scale = pose.localScale;
                        if (data.ptr->properties.interpolateNetwork == true && instance.pendingSpawn == false) {
                            var factor = entityData.version != entityData.initialVersion && entityData.playerDelayMs != 0ul
                                ? dt * 1000f / entityData.playerDelayMs : 1f;
                            position = Vector3.Lerp((Vector3)instance.position, position, factor);
                            rotation = Quaternion.Lerp((Quaternion)(um::quaternion)instance.rotation, rotation, factor);
                        }
                    } else {
                        var matrix = ent.GetAspect<TransformAspect>().readWorldMatrix;
                        position = (Vector3)MatrixUtils.GetPosition(matrix);
                        rotation = (Quaternion)(um::quaternion)MatrixUtils.GetRotation(matrix);
                        scale = (Vector3)MatrixUtils.GetScale(matrix);
                    }
                    // Transform.SetRotation normalizes the GameObject provider's quaternion.
                    // Euler conversion for particles needs the same unit-quaternion input.
                    rotation = Quaternion.Normalize(rotation);
                    instance.previousPosition = instance.pendingSpawn == true ? position : (Vector3)instance.position;
                    instance.previousRotation = instance.pendingSpawn == true ? rotation : (Quaternion)(um::quaternion)instance.rotation;
                    instance.position = (um::float3)position;
                    instance.rotation = (um::quaternion)rotation;
                    instance.scale = scale;

                    objects.instances[instanceIndex] = instance;

                    objects.isDirty = true;
                    this.objectsPerPrefab[prefabId] = objects;

                }

            }

            Camera camera = null;
            var cameraPrepared = false;
            foreach (var kv in this.systemForPrefab) {

                var prefabId = kv.Key;
                var ps = kv.Value;
                var objects = this.objectsPerPrefab[prefabId];

                if (objects.isDirty == false) continue;

                var particlesRequired = objects.entities.Length;
                if (ps.batch != null) {
                    if (cameraPrepared == false) {
                        camera = this.GetBatchCamera(data.ptr->camera);
                        cameraPrepared = true;
                    }
                    ps.batch.Commit(objects.instances, objects.entities, dt, camera);
                }
                foreach (var emitter in ps.emitters) {
                    emitter.particles.ResizeUninitialized(particlesRequired);
                    var particlesArr = emitter.particles.AsArray();
                    var retainedCount = particlesRequired;

                    for (int i = 0; i < objects.instances.Length; i++) {

                        var instance = objects.instances[i];
                        if (instance.pendingSpawn == true && retainedCount == particlesRequired) retainedCount = i;
                        var particle = particlesArr[i];
                        if (instance.pendingSpawn == true) {
                            particle = new ParticleSystem.Particle() {
                                startSize = 1f,
                                startLifetime = 10_000f,
                                remainingLifetime = 10_000f,
                                randomSeed = instance.randomSeed,
                                startColor = Color.white,
                            };
                        }
                        var rotation = (Quaternion)(um::quaternion)instance.rotation * emitter.localRotation;
                        particle.position = (Vector3)instance.position + (Quaternion)(um::quaternion)instance.rotation * emitter.localPosition;
                        var previousPosition = instance.previousPosition + instance.previousRotation * emitter.localPosition;
                        particle.velocity = dt > 0f ? (particle.position - previousPosition) / dt : Vector3.zero;
                        // Match the actual Mesh renderer's ZXY rotation (covered by BakeMesh regression).
                        particle.rotation3D = rotation.eulerAngles;
                        particlesArr[i] = particle;

                    }

                    var main = emitter.proxy.main;
                    if (main.maxParticles < particlesRequired) main.maxParticles = emitter.particles.Capacity;
                    // Birth sub-emitters must see the final pose on the first emission.
                    emitter.proxy.SetParticles(particlesArr, retainedCount);
                    for (int i = retainedCount; i < particlesRequired; ++i) {
                        var particle = particlesArr[i];
                        emitter.proxy.Emit(new ParticleSystem.EmitParams() {
                            position = particle.position,
                            velocity = particle.velocity,
                            rotation3D = particle.rotation3D,
                            startSize = particle.startSize,
                            startLifetime = particle.startLifetime,
                            startColor = particle.startColor,
                            randomSeed = particle.randomSeed,
                        }, 1);
                        if (emitter.burstSystem != null) EmitBurst(emitter, objects.instances[i]);
                    }
                }
                for (int i = 0; i < objects.instances.Length; ++i) {
                    var instance = objects.instances[i];
                    instance.pendingSpawn = false;
                    objects.instances[i] = instance;
                }
                objects.isDirty = false;
                this.objectsPerPrefab[prefabId] = objects;

            }

            if (data.ptr->loadingRequests.Count > 0) {

                ref var allocator = ref data.ptr->viewsWorld.state.ptr->allocator;
                var continueLoadingRequests = new Unity.Collections.LowLevel.Unsafe.UnsafeList<uint>(data.ptr->loadingRequests.Count, Constants.ALLOCATOR_TEMPJOB);
                foreach (var prefabId in data.ptr->loadingRequests) {
                    if (data.ptr->prefabIdToInfo.TryGetValue(in allocator, prefabId, out var prefabInfo) == true) {
                        var handle = GCHandle.FromIntPtr(prefabInfo.info.ptr->prefabPtr);
                        var assetRef = (AssetOp)handle.Target;
                        if (assetRef.IsLoading() == false) {
                            assetRef.StartLoading();
                            continueLoadingRequests.Add(prefabId);
                        } else if (assetRef.IsLoaded() == true) {
                            prefabInfo.info.ptr->isLoaded = true;
                            var go = (UnityEngine.GameObject)assetRef.assetReference.Asset;
                            handle = new HeapReference<EntityView>(go.GetComponent<EntityView>()).handle;
                            prefabInfo.info.ptr->prefabPtr = GCHandle.ToIntPtr(handle);
                            data.ptr->gcHandles.Add(ref data.ptr->viewsWorld.state.ptr->allocator, handle);
                            this.GetOrCreateSystem(prefabId, prefabInfo.info);
                        } else {
                            continueLoadingRequests.Add(prefabId);
                        }
                    }
                }
                data.ptr->loadingRequests.Clear();
                foreach (var prefabId in continueLoadingRequests) {
                    data.ptr->loadingRequests.Add(prefabId);
                }
                continueLoadingRequests.Dispose();

            }

            return dependsOn;

        }

        /// <summary>
        /// Releases the resources owned by this particles provider instance.
        /// </summary>
        private Camera GetBatchCamera(Ent cameraEntity) {
            if (cameraEntity.IsAlive() == false) return Camera.main;
            if (this.batchCamera == null) {
                var go = new GameObject("[Particles Provider] Bake camera");
                go.transform.SetParent(this.particlesRoot, worldPositionStays: false);
                this.batchCamera = go.AddComponent<Camera>();
                this.batchCamera.enabled = false;
            }
            var aspect = cameraEntity.GetAspect<CameraAspect>();
            ref readonly var component = ref aspect.readComponent;
            var tr = cameraEntity.GetAspect<TransformAspect>();
            this.batchCamera.transform.SetPositionAndRotation((Vector3)tr.position, (Quaternion)(um::quaternion)tr.rotation);
            this.batchCamera.nearClipPlane = (float)component.nearClipPlane;
            this.batchCamera.farClipPlane = (float)component.farClipPlane;
            this.batchCamera.fieldOfView = (float)component.fieldOfViewVertical * Mathf.Rad2Deg;
            this.batchCamera.aspect = (float)component.aspect;
            this.batchCamera.orthographic = component.orthographic == true;
            this.batchCamera.orthographicSize = (float)component.orthographicSize;
            return this.batchCamera;
        }

        public void Dispose(safe_ptr<State> state, safe_ptr<ViewsModuleData> data) {

            foreach (var kv in this.objectsPerPrefab) {
                var objects = kv.Value;
                objects.instances.Dispose();
                objects.entities.Dispose();
            }

            foreach (var kv in this.systemForPrefab) {

                var systemInfo = kv.Value;
                systemInfo.batch?.Dispose();
                foreach (var emitter in systemInfo.emitters) {
                    emitter.particles.Dispose();
                    if (emitter.burstParticles.IsCreated == true) emitter.burstParticles.Dispose();
                }

                if (systemInfo.particleSystem != null) {
                    GameObject.DestroyImmediate(systemInfo.particleSystem.gameObject);
                }

            }

            this.objectsPerPrefab.Clear();
            this.entityToPrefabId.Clear();
            this.entityToInstanceIndex.Clear();
            this.interpolationResults.Dispose();

            if (this.particlesRoot != null) {
                GameObject.DestroyImmediate(this.particlesRoot.gameObject);
            }

            DictionaryPool<uint, ObjectsPerPrefab>.Release(this.objectsPerPrefab);
            DictionaryPool<uint, ParticleSystemInfo>.Release(this.systemForPrefab);
            DictionaryPool<Ent, uint>.Release(this.entityToPrefabId);
            DictionaryPool<Ent, int>.Release(this.entityToInstanceIndex);

        }

        /// <summary>
        /// Applies logic state during the parallel phase of view processing.
        /// </summary>
        public void ApplyStateParallel(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData) {

            return;

        }

        /// <summary>
        /// Applies the current logic state to the presentation instance.
        /// </summary>
        public void ApplyState(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData) {

            return;

        }

        /// <summary>
        /// Provides the <c>OnUpdate</c> callback; this implementation performs no work.
        /// </summary>
        public void OnUpdate(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData, float dt) {

        }

        /// <summary>
        /// Provides the <c>OnUpdateParallel</c> callback; this implementation performs no work.
        /// </summary>
        public void OnUpdateParallel(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData, float dt) {

        }

        /// <summary>
        /// Loads the registered data required by this operation.
        /// </summary>
        public void Load(safe_ptr<ViewsModuleData> viewsModuleData, ObjectReferenceRegistryData data) {

            viewsModuleData.ptr->prefabId = math.max(viewsModuleData.ptr->prefabId, data.GetSourceId());
            foreach (var item in data.objects) {
                var objectItem = new ObjectItem(item.data);
                if (objectItem.IsValid() == true && objectItem.Is<EntityView>() == true) {
                    this.Register(viewsModuleData, objectItem, item.data.sourceId);
                }
            }

        }

        /// <summary>
        /// Registers the supplied instance or type for subsequent lookup.
        /// </summary>
        public ViewSource Register(safe_ptr<ViewsModuleData> viewsModuleData, EntityView prefab, uint prefabId = 0, bool checkPrefab = true, bool sceneSource = false) {

            ViewSource viewSource;

            if (prefab == null) {
                throw new System.Exception("Prefab is null");
            }

            var instanceId = prefab.GetInstanceID();
            if (checkPrefab && instanceId <= 0 && prefab.gameObject.scene.name != null && prefab.gameObject.scene.rootCount > 0) {
                throw new System.Exception($"Value {prefab} is not a prefab");
            }

            var id = (uint)instanceId;
            if (prefabId > 0u || viewsModuleData.ptr->instanceIdToPrefabId.TryGetValue(in viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, out prefabId) == false) {

                prefabId = prefabId > 0u ? prefabId : ++viewsModuleData.ptr->prefabId;

                viewSource = new ViewSource() {
                    prefabId = prefabId,
                    providerId = ViewsModule.PARTICLES_PROVIDER_ID,
                };

                viewsModuleData.ptr->instanceIdToPrefabId.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, prefabId);
                ViewsTypeInfo.types.TryGetValue(prefab.GetType(), out var typeInfo);
                typeInfo.cullingType = prefab.cullingType;

                var handle = new HeapReference<EntityView>(prefab).handle;
                viewsModuleData.ptr->gcHandles.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, handle);
                var info = new SourceRegistry.Info() {
                    prefabPtr = GCHandle.ToIntPtr(handle),
                    prefabId = prefabId,
                    typeInfo = typeInfo,
                    sceneSource = sceneSource,
                    flags = 0,
                    isLoaded = true,
                };

                info.HasUpdateModules = ProvidersHelper.HasAny<IViewUpdate>(prefab.modules);
                info.HasUpdateParallelModules = ProvidersHelper.HasAny<IViewUpdateParallel>(prefab.modules);
                info.HasApplyStateModules = ProvidersHelper.HasAny<IViewApplyState>(prefab.modules);
                info.HasApplyStateParallelModules = ProvidersHelper.HasAny<IViewApplyStateParallel>(prefab.modules);
                info.HasInitializeModules = ProvidersHelper.HasAny<IViewInitialize>(prefab.modules);
                info.HasDeInitializeModules = ProvidersHelper.HasAny<IViewDeInitialize>(prefab.modules);
                info.HasEnableFromPoolModules = ProvidersHelper.HasAny<IViewEnableFromPool>(prefab.modules);
                info.HasDisableToPoolModules = ProvidersHelper.HasAny<IViewDisableToPool>(prefab.modules);

                var prefabInfo = new SourceRegistry.InfoRef(info);
                this.GetOrCreateSystem(prefabId, prefabInfo.info);

                viewsModuleData.ptr->prefabIdToInfo.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, prefabId, prefabInfo);

            } else {

                viewSource = new ViewSource() {
                    prefabId = prefabId,
                    providerId = ViewsModule.PARTICLES_PROVIDER_ID,
                };

            }

            if (sceneSource == true) {
                UnityEngine.Object.Destroy(prefab.gameObject);
            }

            return viewSource;

        }

        /// <summary>
        /// Registers the supplied instance or type for subsequent lookup.
        /// </summary>
        public void Register(safe_ptr<ViewsModuleData> viewsModuleData, ObjectItem prefab, uint prefabId) {

            if (prefab.IsValid() == false) {
                throw new System.Exception("Prefab is null");
            }

            if ((((ViewObjectItemData)prefab.data).info.supportedProviders & 1u << (int)ViewsModule.PARTICLES_PROVIDER_ID) == 0) {
                return;
            }

            var instanceId = prefab.GetInstanceID();

            var id = (uint)instanceId;
            if (prefabId > 0u || viewsModuleData.ptr->instanceIdToPrefabId.TryGetValue(in viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, out prefabId) == false) {

                var data = (ViewObjectItemData)prefab.data;

                prefabId = prefabId > 0u ? prefabId : ++viewsModuleData.ptr->prefabId;

                viewsModuleData.ptr->instanceIdToPrefabId.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, prefabId);
                ViewsTypeInfo.types.TryGetValue(prefab.GetType(), out var typeInfo);
                typeInfo.cullingType = data.info.typeInfo.cullingType;

                GCHandle handle;
                bool isLoaded;
                if (prefab.source != null) {
                    handle = new HeapReference<EntityView>((EntityView)prefab.source).handle;
                    isLoaded = true;
                } else {
                    handle = new HeapReference<AssetOp>(new AssetOp(prefab.sourceReference)).handle;
                    isLoaded = false;
                }

                viewsModuleData.ptr->gcHandles.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, handle);
                var info = new SourceRegistry.Info() {
                    prefabPtr = GCHandle.ToIntPtr(handle),
                    prefabId = prefabId,
                    typeInfo = typeInfo,
                    sceneSource = false,
                    isLoaded = isLoaded,
                    poolCount = data.info.poolCount,
                    supportedProviders = data.info.supportedProviders,
                    flags = data.info.flags,
                };

                var prefabInfo = new SourceRegistry.InfoRef(info);
                if (isLoaded == true) {
                    this.GetOrCreateSystem(prefabId, prefabInfo.info);
                } else {
                    viewsModuleData.ptr->loadingRequests.Add(prefabId);
                }

                viewsModuleData.ptr->prefabIdToInfo.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, prefabId, prefabInfo);

            }

        }

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        public void Query(ref QueryBuilder queryBuilder) {
            queryBuilder.With<ParticlesProviderTag>();
        }

        /// <summary>
        /// Returns view by entity.
        /// </summary>
        public IView GetViewByEntity(safe_ptr<ViewsModuleData> data, in Ent entity) => null;

    }

}
