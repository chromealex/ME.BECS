using System;
using System.Runtime.InteropServices;
using ME.BECS.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

[assembly: ME.BECS.CodeGeneratorInclude(typeof(ME.BECS.Views.ParticlesProviderTag))]

namespace ME.BECS.Views {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using scg = System.Collections.Generic;
    using um = Unity.Mathematics;

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct ParticlesProviderTag : IComponent {}

    [BURST]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct ParticlesProvider : IViewProvider<EntityView>, IViewProviderRoot {

        // Kept for source compatibility with existing package consumers.
        public struct ParticleSystemInfo {
            public ParticleSystem particleSystem;
        }

        // Kept for source compatibility. Runtime state uses RuntimeInstanceData below.
        public struct ParticleInstanceData {
            public float3 position;
            public quaternion rotation;
        }

        // Kept for source compatibility with the previous provider implementation.
        public struct ObjectsPerPrefab {
            public NativeList<ParticleInstanceData> instances;
            public NativeList<Ent> entities;
            public bool isDirty;
        }

        private const float PROXY_PARTICLE_LIFETIME = 10_000f;
        private const float POSITION_GROUP_EPSILON_SQ = 0.00000001f;
        private const float ROTATION_GROUP_DOT_MIN = 0.999999f;

        private struct EmitterPose {
            public float3 position;
            public quaternion rotation;
        }

        private struct RuntimeInstanceData {
            public float3 position;
            public quaternion rotation;
            public float3 scale;
            public float3 previousPosition;
            public quaternion previousRotation;
            public float3 previousScale;
            public uint firstParticleSeed;
            public int renderIndex;
            public byte initialized;
            public byte pendingSpawn;
        }

        private sealed class EmitterGroup {
            public ParticleSystem particleSystem;
            public int[] manualSubEmitterIndices;
            public int particleOffset;
        }

        private sealed class EmitterGroupBuilder {
            public EmitterPose pose;
            public readonly scg.List<ParticleSystem> systems = new scg.List<ParticleSystem>();
        }

        private sealed class PrefabRuntime {
            public NativeList<RuntimeInstanceData> instances;
            public NativeList<Ent> entities;
            public NativeArray<EmitterPose> emitterPoses;
            public EmitterGroup[] emitterGroups;
            public int spawnedCount;
        }

        private static float3 ToParticleRotation3D(in quaternion rotation) {
            // Particle.rotation3D stores the inverse of Unity's ZXY Euler angles.
            // UnityEngine.Quaternion.Euler and Unity.Mathematics.EulerZXY use that same order.
            return -um.math.degrees(um.math.EulerZXY(rotation));
        }

        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        private struct UpdateInstancesJob : IJobParallelFor {

            [ReadOnly]
            public NativeArray<Ent> entities;
            public NativeArray<RuntimeInstanceData> instances;
            public safe_ptr<ViewsModuleData> data;
            public safe_ptr<State> beginFrameState;
            public float dtMs;
            public bool interpolateState;
            public bool interpolateNetwork;

            public void Execute(int index) {

                var ent = this.entities[index];
                var instance = this.instances[index];
                var tr = ent.GetAspect<TransformAspect>();

                var worldMatrix = tr.readWorldMatrix;
                var targetPosition = (float3)MatrixUtils.GetPosition(worldMatrix);
                var targetRotation = (quaternion)MatrixUtils.GetRotation(worldMatrix);
                var targetScale = this.interpolateState == true ? (float3)tr.readLocalScale : (float3)MatrixUtils.GetScale(worldMatrix);

                if (this.interpolateState == true) {
                    var canInterpolate = Components.Has<WorldMatrixComponent>(this.beginFrameState, ent.id, ent.gen, true);
                    if (canInterpolate == true) {
                        var sourceData = Components.Read<WorldMatrixComponent>(this.beginFrameState, ent.id, ent.gen);
                        canInterpolate = sourceData.isTickCalculated;
                        if (canInterpolate == true) {
                            var factor = this.data.ptr->interpolationFactor;
                            targetPosition = um.math.lerp(MatrixUtils.GetPosition(sourceData.value), targetPosition, factor);
                            targetRotation = Math.FastSlerp((quaternion)MatrixUtils.GetRotation(sourceData.value), targetRotation, factor);
                            targetScale = um.math.lerp(MatrixUtils.GetScale(sourceData.value), targetScale, factor);
                        }
                    }
                }

                if (instance.initialized == 0) {
                    instance.position = targetPosition;
                    instance.rotation = targetRotation;
                    instance.scale = targetScale;
                    instance.previousPosition = targetPosition;
                    instance.previousRotation = targetRotation;
                    instance.previousScale = targetScale;
                    instance.initialized = 1;
                } else {
                    instance.previousPosition = instance.position;
                    instance.previousRotation = instance.rotation;
                    instance.previousScale = instance.scale;

                    var networkFactor = 1f;
                    if (this.interpolateNetwork == true && (uint)instance.renderIndex < this.data.ptr->renderingOnSceneCount) {
                        var entityData = this.data.ptr->renderingOnSceneEnts[instance.renderIndex];
                        if (entityData.version != entityData.initialVersion && entityData.playerDelayMs != 0ul) {
                            networkFactor = um.math.saturate(this.dtMs / entityData.playerDelayMs);
                        }
                    }

                    instance.position = um.math.lerp(instance.position, targetPosition, networkFactor);
                    instance.rotation = Math.FastSlerp(instance.rotation, targetRotation, networkFactor);
                    instance.scale = um.math.lerp(instance.scale, targetScale, networkFactor);
                }

                this.instances[index] = instance;

            }

        }

        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        private struct BuildParticlesJob : IJobParallelFor {

            [ReadOnly]
            public NativeArray<RuntimeInstanceData> instances;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<ParticleSystem.Particle> particles;
            public EmitterPose emitterPose;
            public int emitterGroupIndex;
            public float inverseDeltaTime;

            public void Execute(int index) {

                var instance = this.instances[index];
                var position = instance.position + um.math.rotate(instance.rotation, this.emitterPose.position * instance.scale);
                var previousPosition = instance.previousPosition + um.math.rotate(instance.previousRotation, this.emitterPose.position * instance.previousScale);
                var rotation = um.math.normalize(um.math.mul(instance.rotation, this.emitterPose.rotation));
                var seed = instance.firstParticleSeed + (uint)this.emitterGroupIndex;
                var velocity = this.inverseDeltaTime > 0f ? (position - previousPosition) * this.inverseDeltaTime : float3.zero;

                this.particles[index] = new ParticleSystem.Particle() {
                    position = position,
                    velocity = velocity,
                    rotation3D = ToParticleRotation3D(in rotation),
                    startLifetime = PROXY_PARTICLE_LIFETIME,
                    remainingLifetime = PROXY_PARTICLE_LIFETIME,
                    startSize3D = Vector3.one,
                    startColor = Color.white,
                    randomSeed = seed == 0u ? 1u : seed,
                };

            }

        }

        private scg.Dictionary<uint, PrefabRuntime> runtimes;
        private scg.Dictionary<uint, uint> entityIdToPrefabId;
        private scg.Dictionary<uint, int> entityIdToInstanceIndex;
        private NativeList<JobHandle> jobHandles;
        private NativeList<ParticleSystem.Particle> particleScratch;
        private NativeList<uint> continueLoadingRequests;
        private ViewsModuleProperties properties;
        private Transform particlesRoot;
        private uint nextParticleSeed;
        private int activeInstanceCount;

        public Transform GetRoot() => this.particlesRoot;

        public void Initialize(uint providerId, World viewsWorld, ViewsModuleProperties properties) {

            UnsafeViewsModule.RegisterProviderType<ParticlesProviderTag>(providerId);

            this.properties = properties;
            this.runtimes = DictionaryPool<uint, PrefabRuntime>.Get();
            this.entityIdToPrefabId = DictionaryPool<uint, uint>.Get();
            this.entityIdToInstanceIndex = DictionaryPool<uint, int>.Get();
            this.jobHandles = new NativeList<JobHandle>((int)properties.instancesRegistryCapacity, Allocator.Persistent);
            this.particleScratch = new NativeList<ParticleSystem.Particle>(um.math.max(1, (int)properties.renderingObjectsCapacity), Allocator.Persistent);
            this.continueLoadingRequests = new NativeList<uint>((int)properties.instancesRegistryCapacity, Allocator.Persistent);
            this.nextParticleSeed = 1u;
            this.activeInstanceCount = 0;

            this.runtimes.EnsureCapacity((int)properties.instancesRegistryCapacity);
            this.entityIdToPrefabId.EnsureCapacity((int)properties.renderingObjectsCapacity);
            this.entityIdToInstanceIndex.EnsureCapacity((int)properties.renderingObjectsCapacity);

            this.particlesRoot = new GameObject("[Particles Provider] Root").transform;
            if (Application.isPlaying == true) GameObject.DontDestroyOnLoad(this.particlesRoot.gameObject);

        }

        private static void ConfigureProxySystem(ParticleSystem particleSystem, int capacity) {

            particleSystem.Pause(withChildren: false);
            particleSystem.Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.useAutoRandomSeed = false;
            particleSystem.randomSeed = 1u;

            var main = particleSystem.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = false;
            main.duration = PROXY_PARTICLE_LIFETIME;
            main.maxParticles = capacity;
            main.startLifetime = PROXY_PARTICLE_LIFETIME;
            main.startRotation3D = true;
            main.ringBufferMode = ParticleSystemRingBufferMode.PauseUntilReplaced;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = particleSystem.emission;
            emission.enabled = false;

            var shape = particleSystem.shape;
            shape.enabled = false;

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.enabled = false;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var subEmitters = particleSystem.subEmitters;
            subEmitters.enabled = true;

        }

        private static void ConfigureSourceSystem(ParticleSystem particleSystem) {

            particleSystem.Pause(withChildren: false);
            particleSystem.Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particleSystem.main;
            main.prewarm = false;
            main.playOnAwake = false;

            if (main.simulationSpace == ParticleSystemSimulationSpace.Local) {
                // A shared Transform cannot represent Local space for many entities. In World space,
                // Current inherit velocity keeps every child bound to its own proxy parent particle.
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var inheritVelocity = particleSystem.inheritVelocity;
                inheritVelocity.enabled = true;
                inheritVelocity.mode = ParticleSystemInheritVelocityMode.Current;
                inheritVelocity.curve = new ParticleSystem.MinMaxCurve(1f);
            }

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh) {
                // The source Transform is normalized under the shared proxy. Mesh alignment must
                // therefore use the particle rotation that already contains the authored pose.
                renderer.alignment = ParticleSystemRenderSpace.World;
            }

        }

        private static bool HasContinuousEmission(in ParticleSystem.MinMaxCurve curve) {

            switch (curve.mode) {
                case ParticleSystemCurveMode.Constant:
                    return um.math.abs(curve.constant) > um.math.EPSILON;
                case ParticleSystemCurveMode.TwoConstants:
                    return um.math.abs(curve.constantMin) > um.math.EPSILON ||
                           um.math.abs(curve.constantMax) > um.math.EPSILON;
                case ParticleSystemCurveMode.Curve:
                case ParticleSystemCurveMode.TwoCurves:
                    // Curves are kept on Birth even when currently zero: their authored value can
                    // be animated and Manual sub-emitters do not support rate emission.
                    return true;
                default:
                    return true;
            }

        }

        private static bool RequiresBirthSubEmitter(ParticleSystem particleSystem) {

            var emission = particleSystem.emission;
            return emission.enabled == true &&
                   (HasContinuousEmission(emission.rateOverTime) == true ||
                    HasContinuousEmission(emission.rateOverDistance) == true);

        }

        private static bool IsSamePose(in EmitterPose a, in EmitterPose b) {
            return um.math.lengthsq(a.position - b.position) <= POSITION_GROUP_EPSILON_SQ &&
                   um.math.abs(um.math.dot(a.rotation.value, b.rotation.value)) >= ROTATION_GROUP_DOT_MIN;
        }

        private static void ValidateSimulationSpace(EntityView prefab, uint prefabId) {

            var particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            foreach (var particleSystem in particleSystems) {
                if (particleSystem.main.simulationSpace != ParticleSystemSimulationSpace.Custom) continue;
                var message = $"ParticlesProvider does not support Custom simulation space. Prefab #{prefabId}, system '{particleSystem.name}' must use GameObjectProvider.";
                Logger.Views.Error(message);
                throw new InvalidOperationException(message);
            }

        }

        private PrefabRuntime GetOrCreateRuntime(uint prefabId, safe_ptr<SourceRegistry.Info> prefabInfo) {

            if (this.runtimes.TryGetValue(prefabId, out var runtime) == true) return runtime;
            if (prefabInfo.ptr->isLoaded == false) throw new InvalidOperationException("Prefab was not loaded, but ParticlesProvider tried to instantiate it.");

            var handle = GCHandle.FromIntPtr(prefabInfo.ptr->prefabPtr);
            if (handle.Target is not EntityView prefab) throw new InvalidOperationException($"ParticlesProvider prefab #{prefabId} is not an EntityView.");
            ValidateSimulationSpace(prefab, prefabId);

            var runtimeRoot = new GameObject($"ParticleSystem_{prefabId}");
            runtimeRoot.transform.SetParent(this.particlesRoot, false);

            var prefabInstance = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity, runtimeRoot.transform);
            var allParticleSystems = prefabInstance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            var referencedSubEmitters = HashSetPool<ParticleSystem>.Get();
            var builders = ListPool<EmitterGroupBuilder>.Get();
            EmitterGroup[] emitterGroups = null;
            NativeArray<EmitterPose> emitterPoses = default;

            try {
                foreach (var particleSystem in allParticleSystems) {
                    var sourceSubEmitters = particleSystem.subEmitters;
                    for (int i = 0; i < sourceSubEmitters.subEmittersCount; ++i) {
                        var referencedSystem = sourceSubEmitters.GetSubEmitterSystem(i);
                        if (referencedSystem != null) referencedSubEmitters.Add(referencedSystem);
                    }
                }

                foreach (var particleSystem in allParticleSystems) ConfigureSourceSystem(particleSystem);

                foreach (var particleSystem in allParticleSystems) {
                    if (referencedSubEmitters.Contains(particleSystem) == true) continue;

                    var pose = new EmitterPose() {
                        position = runtimeRoot.transform.InverseTransformPoint(particleSystem.transform.position),
                        rotation = um.math.normalize(um.math.mul(um.math.inverse((quaternion)runtimeRoot.transform.rotation), (quaternion)particleSystem.transform.rotation)),
                    };

                    EmitterGroupBuilder builder = null;
                    for (int i = 0; i < builders.Count; ++i) {
                        if (IsSamePose(builders[i].pose, pose) == true) {
                            builder = builders[i];
                            break;
                        }
                    }

                    if (builder == null) {
                        builder = new EmitterGroupBuilder() { pose = pose };
                        builders.Add(builder);
                    }
                    builder.systems.Add(particleSystem);
                }

                var capacity = um.math.max(1, (int)this.properties.renderingObjectsCapacity);
                emitterGroups = new EmitterGroup[builders.Count];
                emitterPoses = new NativeArray<EmitterPose>(builders.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                for (int groupIndex = 0; groupIndex < builders.Count; ++groupIndex) {
                    var builder = builders[groupIndex];
                    var groupObject = new GameObject($"Proxy_{prefabId}_{groupIndex}");
                    groupObject.transform.SetParent(runtimeRoot.transform, false);
                    var proxySystem = groupObject.AddComponent<ParticleSystem>();
                    ConfigureProxySystem(proxySystem, capacity);

                    var proxySubEmitters = proxySystem.subEmitters;
                    var manualSubEmitterIndices = ListPool<int>.Get();
                    try {
                        foreach (var sourceSystem in builder.systems) {
                            sourceSystem.transform.SetParent(proxySystem.transform, worldPositionStays: true);
                            sourceSystem.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                            var subEmitterIndex = proxySubEmitters.subEmittersCount;
                            if (RequiresBirthSubEmitter(sourceSystem) == true) {
                                // Rate-over-time and rate-over-distance are unsupported by Manual.
                                proxySubEmitters.AddSubEmitter(sourceSystem, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritRotation, emitProbability: 1f);
                            } else {
                                proxySubEmitters.AddSubEmitter(sourceSystem, ParticleSystemSubEmitterType.Manual, ParticleSystemSubEmitterProperties.InheritRotation, emitProbability: 1f);
                                manualSubEmitterIndices.Add(subEmitterIndex);
                            }
                        }

                        proxySystem.Play(withChildren: false);
                        emitterPoses[groupIndex] = builder.pose;
                        emitterGroups[groupIndex] = new EmitterGroup() {
                            particleSystem = proxySystem,
                            manualSubEmitterIndices = manualSubEmitterIndices.ToArray(),
                        };
                    } finally {
                        ListPool<int>.Release(manualSubEmitterIndices);
                    }
                }

                var renderers = runtimeRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
                foreach (var renderer in renderers) {
                    if (renderer is not ParticleSystemRenderer) renderer.enabled = false;
                }

                runtime = new PrefabRuntime() {
                    instances = new NativeList<RuntimeInstanceData>(capacity, Allocator.Persistent),
                    entities = new NativeList<Ent>(capacity, Allocator.Persistent),
                    emitterPoses = emitterPoses,
                    emitterGroups = emitterGroups,
                };
                this.runtimes.Add(prefabId, runtime);

                var requiredJobCapacity = 0;
                foreach (var registeredRuntime in this.runtimes.Values) {
                    requiredJobCapacity += um.math.max(1, registeredRuntime.emitterGroups.Length);
                }
                if (this.jobHandles.Capacity < requiredJobCapacity) this.jobHandles.Capacity = requiredJobCapacity;

                var maxGroupCount = 1;
                foreach (var registeredRuntime in this.runtimes.Values) {
                    maxGroupCount = um.math.max(maxGroupCount, registeredRuntime.emitterGroups.Length);
                }
                var requiredParticleCapacity = (long)um.math.max(1, (int)this.properties.renderingObjectsCapacity) * maxGroupCount;
                if (requiredParticleCapacity > int.MaxValue) throw new InvalidOperationException("ParticlesProvider particle capacity exceeds NativeList limits.");
                if (this.particleScratch.Capacity < requiredParticleCapacity) this.particleScratch.Capacity = (int)requiredParticleCapacity;
                return runtime;
            } catch {
                if (emitterPoses.IsCreated == true) emitterPoses.Dispose();
                if (Application.isPlaying == true) GameObject.Destroy(runtimeRoot);
                else GameObject.DestroyImmediate(runtimeRoot);
                throw;
            } finally {
                HashSetPool<ParticleSystem>.Release(referencedSubEmitters);
                ListPool<EmitterGroupBuilder>.Release(builders);
            }

        }

        private static void ValidateExistingSpawnRegistration(PrefabRuntime runtime, int instanceIndex, uint expectedEntId, uint expectedEntGen) {

            if (instanceIndex < 0 ||
                instanceIndex >= runtime.entities.Length ||
                instanceIndex >= runtime.instances.Length ||
                runtime.entities[instanceIndex].id != expectedEntId ||
                runtime.entities[instanceIndex].gen != expectedEntGen) {
                throw new InvalidOperationException($"ParticlesProvider cannot retry Spawn for entity #{expectedEntId}: existing provider state is incompatible.");
            }
        }

        public JobHandle Spawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {

            dependsOn.Complete();
            for (int i = 0; i < data.ptr->toAddTemp.Length; ++i) {
                var item = data.ptr->toAddTemp[i];
                var prefabId = (uint)item.prefabInfo.info.ptr->prefabId;
                var runtime = this.runtimes[prefabId];

                var hasPrefabMapping = this.entityIdToPrefabId.TryGetValue(item.ent.id, out var existingPrefabId);
                var hasInstanceMapping = this.entityIdToInstanceIndex.TryGetValue(item.ent.id, out var existingInstanceIndex);
                if (hasPrefabMapping != hasInstanceMapping) {
                    throw new InvalidOperationException($"ParticlesProvider found inconsistent Spawn mappings for entity #{item.ent.id}.");
                }
                if (hasPrefabMapping == true) {
                    if (existingPrefabId != prefabId || this.runtimes.TryGetValue(existingPrefabId, out var existingRuntime) == false) {
                        throw new InvalidOperationException($"ParticlesProvider cannot retry Spawn for entity #{item.ent.id}: existing provider state is incompatible.");
                    }
                    ValidateExistingSpawnRegistration(existingRuntime, existingInstanceIndex, item.ent.id, item.ent.gen);
                    // Commit may have failed after this Spawn completed but before the Views module
                    // cleared toAddTemp. The existing aligned instance is the retry result.
                    continue;
                }

                // ProcessAssignedEntities runs before Commit scheduling. If that Commit failed,
                // toAddTemp still refers to the source entity while the provider registration has
                // already moved to its assigned target. Treat it as the same completed Spawn.
                if (data.ptr->toAssign.TryGetValue(item.ent.id, out var assignedEntId) == true && assignedEntId != item.ent.id) {
                    var hasAssignedPrefabMapping = this.entityIdToPrefabId.TryGetValue(assignedEntId, out var assignedPrefabId);
                    var hasAssignedInstanceMapping = this.entityIdToInstanceIndex.TryGetValue(assignedEntId, out var assignedInstanceIndex);
                    if (hasAssignedPrefabMapping != hasAssignedInstanceMapping) {
                        throw new InvalidOperationException($"ParticlesProvider found inconsistent assigned Spawn mappings for entity #{assignedEntId}.");
                    }
                    if (hasAssignedPrefabMapping == true) {
                        if (assignedPrefabId != prefabId || this.runtimes.TryGetValue(assignedPrefabId, out var assignedRuntime) == false) {
                            throw new InvalidOperationException($"ParticlesProvider cannot retry assigned Spawn for entity #{assignedEntId}: existing provider state is incompatible.");
                        }
                        var assignedEnt = new Ent(assignedEntId, data.ptr->connectedWorld);
                        ValidateExistingSpawnRegistration(assignedRuntime, assignedInstanceIndex, assignedEnt.id, assignedEnt.gen);
                        continue;
                    }
                }

                ref var allocator = ref data.ptr->viewsWorld.state.ptr->allocator;
                if (data.ptr->renderingOnSceneEntToRenderIndex.TryGetValue(in allocator, item.ent.id, out var renderIndex) == false) {
                    throw new InvalidOperationException($"ParticlesProvider could not resolve render index for entity #{item.ent.id}.");
                }

                if (this.activeInstanceCount >= (int)this.properties.renderingObjectsCapacity) {
                    throw new InvalidOperationException($"ParticlesProvider reached renderingObjectsCapacity ({this.properties.renderingObjectsCapacity}). Increase ViewsModuleProperties.renderingObjectsCapacity.");
                }

                var groupCount = (uint)runtime.emitterGroups.Length;
                var firstSeed = this.nextParticleSeed;
                this.nextParticleSeed += um.math.max(1u, groupCount);
                if (this.nextParticleSeed == 0u) this.nextParticleSeed = 1u;

                runtime.entities.Add(item.ent);
                runtime.instances.Add(new RuntimeInstanceData() {
                    rotation = quaternion.identity,
                    previousRotation = quaternion.identity,
                    scale = new float3(1f),
                    previousScale = new float3(1f),
                    firstParticleSeed = firstSeed,
                    renderIndex = (int)renderIndex,
                    pendingSpawn = 1,
                });
                this.entityIdToPrefabId.Add(item.ent.id, prefabId);
                this.entityIdToInstanceIndex.Add(item.ent.id, runtime.entities.Length - 1);
                ++runtime.spawnedCount;
                ++this.activeInstanceCount;

                var instanceInfo = new SceneInstanceInfo((IntPtr)item.ent.ToULong(), item.prefabInfo.info, 0u, item.localData);
                data.ptr->renderingOnScene.Add(ref data.ptr->viewsWorld.state.ptr->allocator, instanceInfo);
            }
            return dependsOn;

        }

        public JobHandle Despawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {

            dependsOn.Complete();
            for (int i = 0; i < data.ptr->toRemoveTemp.Length; ++i) {
                var item = data.ptr->toRemoveTemp[i];
                var entToRemove = new Ent((ulong)item.obj);
                if (this.entityIdToInstanceIndex.TryGetValue(entToRemove.id, out var removeIndex) == false) continue;

                var prefabId = item.prefabInfo.ptr->prefabId;
                var runtime = this.runtimes[prefabId];
                var instanceCount = runtime.instances.Length;
                var removingPendingSpawn = runtime.instances[removeIndex].pendingSpawn != 0;

                if (removingPendingSpawn == true) {
                    var lastIndex = instanceCount - 1;
                    var movedEnt = runtime.entities[lastIndex];
                    runtime.entities.RemoveAtSwapBack(removeIndex);
                    runtime.instances.RemoveAtSwapBack(removeIndex);
                    --runtime.spawnedCount;
                    if (removeIndex != lastIndex) this.entityIdToInstanceIndex[movedEnt.id] = removeIndex;
                } else {
                    var lastRetainedIndex = instanceCount - runtime.spawnedCount - 1;
                    if (removeIndex != lastRetainedIndex) {
                        var movedRetainedEnt = runtime.entities[lastRetainedIndex];
                        runtime.entities[removeIndex] = movedRetainedEnt;
                        runtime.instances[removeIndex] = runtime.instances[lastRetainedIndex];
                        this.entityIdToInstanceIndex[movedRetainedEnt.id] = removeIndex;
                    }

                    var lastIndex = instanceCount - 1;
                    var movedPendingEnt = runtime.entities[lastIndex];
                    runtime.entities.RemoveAtSwapBack(lastRetainedIndex);
                    runtime.instances.RemoveAtSwapBack(lastRetainedIndex);
                    if (lastRetainedIndex != lastIndex) this.entityIdToInstanceIndex[movedPendingEnt.id] = lastRetainedIndex;
                }

                this.entityIdToPrefabId.Remove(entToRemove.id);
                this.entityIdToInstanceIndex.Remove(entToRemove.id);
                --this.activeInstanceCount;

                if (item.index < data.ptr->renderingOnSceneCount) {
                    var movedEntId = data.ptr->renderingOnSceneEnts[(int)item.index].element.id;
                    if (this.entityIdToPrefabId.TryGetValue(movedEntId, out var movedPrefabId) == true &&
                        this.entityIdToInstanceIndex.TryGetValue(movedEntId, out var movedInstanceIndex) == true) {
                        var movedRuntime = this.runtimes[movedPrefabId];
                        var movedInstance = movedRuntime.instances[movedInstanceIndex];
                        movedInstance.renderIndex = (int)item.index;
                        movedRuntime.instances[movedInstanceIndex] = movedInstance;
                    }
                }
            }
            return dependsOn;

        }

        private void ProcessAssignedEntities(safe_ptr<ViewsModuleData> data) {

            if (data.ptr->toAssign.Count() == 0) return;
            foreach (var item in data.ptr->toAssign) {
                var sourceEntId = item.Key;
                var targetEntId = item.Value;
                if (this.entityIdToPrefabId.TryGetValue(sourceEntId, out var prefabId) == false ||
                    this.entityIdToInstanceIndex.TryGetValue(sourceEntId, out var instanceIndex) == false) continue;

                var runtime = this.runtimes[prefabId];
                runtime.entities[instanceIndex] = new Ent(targetEntId, data.ptr->connectedWorld);
                this.entityIdToPrefabId.Remove(sourceEntId);
                this.entityIdToInstanceIndex.Remove(sourceEntId);
                this.entityIdToPrefabId[targetEntId] = prefabId;
                this.entityIdToInstanceIndex[targetEntId] = instanceIndex;
            }

        }

        private static ParticleSystem.EmitParams GetEmitParams(in ParticleSystem.Particle particle) {
            return new ParticleSystem.EmitParams() {
                position = particle.position,
                velocity = particle.velocity,
                rotation3D = particle.rotation3D,
                startLifetime = particle.startLifetime,
                startSize3D = particle.startSize3D,
                startColor = particle.startColor,
                randomSeed = particle.randomSeed,
            };
        }

        private void ScheduleParticleJobs(safe_ptr<ViewsModuleData> data, JobHandle dependsOn, float dt) {

            this.jobHandles.Clear();
            var totalParticleCount = 0;
            foreach (var runtime in this.runtimes.Values) {
                totalParticleCount += runtime.instances.Length * runtime.emitterGroups.Length;
            }
            this.particleScratch.ResizeUninitialized(totalParticleCount);
            var particleOffset = 0;
            var interpolationEnabled = data.ptr->properties.interpolateState == true &&
                                       data.ptr->beginFrameState.ptr->state.ptr != null &&
                                       data.ptr->beginFrameState.ptr->state.ptr->IsCreated == true;

            if (interpolationEnabled == true) {
                dependsOn = new Jobs.PrepareInterpolationFactorJob() {
                    data = data,
                    beginFrameState = data.ptr->beginFrameState.ptr->state,
                    currentTick = data.ptr->connectedWorld.CurrentTick,
                    tickTime = data.ptr->beginFrameState.ptr->tickTime,
                    currentTimeSinceStart = data.ptr->beginFrameState.ptr->timeSinceStart,
                }.Schedule(dependsOn);
            }

            var inverseDeltaTime = dt > um.math.EPSILON ? 1f / dt : 0f;
            foreach (var runtime in this.runtimes.Values) {
                var instanceCount = runtime.instances.Length;
                var updateHandle = new UpdateInstancesJob() {
                    entities = runtime.entities.AsArray(),
                    instances = runtime.instances.AsArray(),
                    data = data,
                    beginFrameState = interpolationEnabled == true ? data.ptr->beginFrameState.ptr->state : default,
                    dtMs = dt * 1000f,
                    interpolateState = interpolationEnabled,
                    interpolateNetwork = interpolationEnabled == true && data.ptr->properties.interpolateNetwork == true,
                }.Schedule(instanceCount, 32, dependsOn);

                if (runtime.emitterGroups.Length == 0) {
                    this.jobHandles.Add(updateHandle);
                    continue;
                }

                for (int groupIndex = 0; groupIndex < runtime.emitterGroups.Length; ++groupIndex) {
                    var group = runtime.emitterGroups[groupIndex];
                    group.particleOffset = particleOffset;
                    var buildHandle = new BuildParticlesJob() {
                        instances = runtime.instances.AsArray(),
                        particles = this.particleScratch.AsArray().GetSubArray(particleOffset, instanceCount),
                        emitterPose = runtime.emitterPoses[groupIndex],
                        emitterGroupIndex = groupIndex,
                        inverseDeltaTime = inverseDeltaTime,
                    }.Schedule(instanceCount, 32, updateHandle);
                    this.jobHandles.Add(buildHandle);
                    particleOffset += instanceCount;
                }
            }

        }

        private void ApplyParticles() {

            foreach (var runtime in this.runtimes.Values) {
                var instanceCount = runtime.instances.Length;
                var retainedCount = um.math.max(0, instanceCount - runtime.spawnedCount);

                foreach (var group in runtime.emitterGroups) {
                    var particles = this.particleScratch.AsArray().GetSubArray(group.particleOffset, instanceCount);
                    // Despawn can reduce the prefix while Spawn appends new instances. Emitting only
                    // the suffix guarantees every source trigger happens once for a new instance.
                    group.particleSystem.SetParticles(particles, retainedCount);
                    for (int i = retainedCount; i < instanceCount; ++i) {
                        var particle = particles[i];
                        var emitParams = GetEmitParams(in particle);
                        group.particleSystem.Emit(emitParams, 1);
                        foreach (var subEmitterIndex in group.manualSubEmitterIndices) {
                            var subEmitterParticle = particle;
                            group.particleSystem.TriggerSubEmitter(subEmitterIndex, ref subEmitterParticle);
                        }
                    }
                }
                for (int i = retainedCount; i < instanceCount; ++i) {
                    var instance = runtime.instances[i];
                    instance.pendingSpawn = 0;
                    runtime.instances[i] = instance;
                }
                runtime.spawnedCount = 0;
            }

        }

        private void ProcessLoadingRequests(safe_ptr<ViewsModuleData> data) {

            this.continueLoadingRequests.Clear();
            ref var allocator = ref data.ptr->viewsWorld.state.ptr->allocator;

            foreach (var prefabId in data.ptr->loadingRequests) {
                if (data.ptr->prefabIdToInfo.TryGetValue(in allocator, prefabId, out var prefabInfo) == false) continue;

                var handle = GCHandle.FromIntPtr(prefabInfo.info.ptr->prefabPtr);
                if (handle.Target is not AssetOp assetRef) {
                    Logger.Views.Error($"ParticlesProvider loading request #{prefabId} has an invalid AssetOp.");
                    continue;
                }

                if (assetRef.IsLoading() == false) {
                    assetRef.StartLoading();
                    this.continueLoadingRequests.Add(prefabId);
                    continue;
                }
                if (assetRef.IsLoaded() == false) {
                    this.continueLoadingRequests.Add(prefabId);
                    continue;
                }

                if (assetRef.handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ||
                    assetRef.assetReference.Asset is not GameObject go ||
                    go.TryGetComponent<EntityView>(out var entityView) == false) {
                    Logger.Views.Error($"ParticlesProvider failed to load prefab #{prefabId}.");
                    continue;
                }

                ValidateSimulationSpace(entityView, prefabId);
                prefabInfo.info.ptr->isLoaded = true;
                prefabInfo.info.ptr->loadedTick = data.ptr->viewsWorld.CurrentTick;
                var prefabHandle = new HeapReference<EntityView>(entityView).handle;
                prefabInfo.info.ptr->prefabPtr = GCHandle.ToIntPtr(prefabHandle);
                data.ptr->gcHandles.Add(ref allocator, prefabHandle);
                this.GetOrCreateRuntime(prefabId, prefabInfo.info);
            }

            data.ptr->loadingRequests.Clear();
            foreach (var prefabId in this.continueLoadingRequests) data.ptr->loadingRequests.Add(prefabId);

        }

        public JobHandle Commit(safe_ptr<ViewsModuleData> data, JobHandle dependsOn, float dt) {

            this.ProcessAssignedEntities(data);
            this.ScheduleParticleJobs(data, dependsOn, dt);

            if (this.jobHandles.Length > 0) {
                dependsOn = JobHandle.CombineDependencies(this.jobHandles.AsArray());
                JobUtils.RunScheduled();
            }
            dependsOn.Complete();

            this.ApplyParticles();
            this.ProcessLoadingRequests(data);
            return dependsOn;

        }

        public void Dispose(safe_ptr<State> state, safe_ptr<ViewsModuleData> data) {

            if (this.runtimes != null) {
                foreach (var runtime in this.runtimes.Values) {
                    if (runtime.instances.IsCreated == true) runtime.instances.Dispose();
                    if (runtime.entities.IsCreated == true) runtime.entities.Dispose();
                    if (runtime.emitterPoses.IsCreated == true) runtime.emitterPoses.Dispose();
                }
                this.runtimes.Clear();
            }

            if (this.jobHandles.IsCreated == true) this.jobHandles.Dispose();
            if (this.particleScratch.IsCreated == true) this.particleScratch.Dispose();
            if (this.continueLoadingRequests.IsCreated == true) this.continueLoadingRequests.Dispose();

            if (this.particlesRoot != null) {
                if (Application.isPlaying == true) GameObject.Destroy(this.particlesRoot.gameObject);
                else GameObject.DestroyImmediate(this.particlesRoot.gameObject);
                this.particlesRoot = null;
            }

            if (this.runtimes != null) DictionaryPool<uint, PrefabRuntime>.Release(this.runtimes);
            if (this.entityIdToPrefabId != null) {
                this.entityIdToPrefabId.Clear();
                DictionaryPool<uint, uint>.Release(this.entityIdToPrefabId);
            }
            if (this.entityIdToInstanceIndex != null) {
                this.entityIdToInstanceIndex.Clear();
                DictionaryPool<uint, int>.Release(this.entityIdToInstanceIndex);
            }

            this.runtimes = null;
            this.entityIdToPrefabId = null;
            this.entityIdToInstanceIndex = null;

        }

        public void ApplyStateParallel(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData) {}
        public void ApplyState(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData) {}
        public void OnUpdate(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData, float dt) {}
        public void OnUpdateParallel(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData, float dt) {}

        public void Load(safe_ptr<ViewsModuleData> viewsModuleData, ObjectReferenceRegistryData data) {

            viewsModuleData.ptr->prefabId = math.max(viewsModuleData.ptr->prefabId, data.GetSourceId());
            foreach (var item in data.objects) {
                var objectItem = new ObjectItem(item.data);
                if (objectItem.IsValid() == true && objectItem.Is<EntityView>() == true) this.Register(viewsModuleData, objectItem, item.data.sourceId);
            }

        }

        public ViewSource Register(safe_ptr<ViewsModuleData> viewsModuleData, EntityView prefab, uint prefabId = 0u, bool checkPrefab = true, bool sceneSource = false) {

            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var instanceId = prefab.GetInstanceID();
            if (checkPrefab == true && instanceId <= 0 && prefab.gameObject.scene.name != null && prefab.gameObject.scene.rootCount > 0) {
                throw new InvalidOperationException($"Value {prefab} is not a prefab");
            }
            ValidateSimulationSpace(prefab, prefabId);

            var id = (uint)instanceId;
            ViewSource viewSource;
            if (prefabId > 0u || viewsModuleData.ptr->instanceIdToPrefabId.TryGetValue(in viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, out prefabId) == false) {
                prefabId = prefabId > 0u ? prefabId : ++viewsModuleData.ptr->prefabId;
                viewSource = new ViewSource() { prefabId = prefabId, providerId = ViewsModule.PARTICLES_PROVIDER_ID };

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
                this.GetOrCreateRuntime(prefabId, prefabInfo.info);
                viewsModuleData.ptr->prefabIdToInfo.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, prefabId, prefabInfo);
            } else {
                viewSource = new ViewSource() { prefabId = prefabId, providerId = ViewsModule.PARTICLES_PROVIDER_ID };
            }

            if (sceneSource == true) UnityEngine.Object.Destroy(prefab.gameObject);
            return viewSource;

        }

        public void Register(safe_ptr<ViewsModuleData> viewsModuleData, ObjectItem prefab, uint prefabId) {

            if (prefab.IsValid() == false) throw new ArgumentNullException(nameof(prefab));
            if ((((ViewObjectItemData)prefab.data).info.supportedProviders & 1u << (int)ViewsModule.PARTICLES_PROVIDER_ID) == 0) return;

            if (prefab.source is EntityView sourcePrefab) ValidateSimulationSpace(sourcePrefab, prefabId);

            var instanceId = prefab.GetInstanceID();
            var id = (uint)instanceId;
            if (prefabId > 0u || viewsModuleData.ptr->instanceIdToPrefabId.TryGetValue(in viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, out prefabId) == false) {
                var objectData = (ViewObjectItemData)prefab.data;
                prefabId = prefabId > 0u ? prefabId : ++viewsModuleData.ptr->prefabId;

                viewsModuleData.ptr->instanceIdToPrefabId.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, prefabId);
                ViewsTypeInfo.types.TryGetValue(prefab.GetType(), out var typeInfo);
                typeInfo.cullingType = objectData.info.typeInfo.cullingType;

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
                    poolCount = objectData.info.poolCount,
                    supportedProviders = objectData.info.supportedProviders,
                    flags = objectData.info.flags,
                };

                var prefabInfo = new SourceRegistry.InfoRef(info);
                if (isLoaded == true) this.GetOrCreateRuntime(prefabId, prefabInfo.info);
                else viewsModuleData.ptr->loadingRequests.Add(prefabId);
                viewsModuleData.ptr->prefabIdToInfo.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, prefabId, prefabInfo);
            }

        }

        public void Query(ref QueryBuilder queryBuilder) => queryBuilder.With<ParticlesProviderTag>();
        public IView GetViewByEntity(safe_ptr<ViewsModuleData> data, in Ent entity) => null;

    }

}
