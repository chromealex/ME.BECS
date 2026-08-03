
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using UnityEngine.Pool;
    using um = Unity.Mathematics;

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct ParticlesProviderTag : IComponent {}

    [BURST]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct ParticlesProvider : IViewProvider<EntityView> {

        public struct ParticleSystemInfo {
            public ParticleSystem particleSystem;
        }

        public struct ParticleInstanceData {
            public float3 position;
            public quaternion rotation;
            public float3 prevPos;
            public ulong prevTick;
            public float3 velocity;
        }

        public struct ObjectsPerPrefab {

            public NativeList<ParticleInstanceData> instances;
            public NativeList<Ent> entities;
            public bool isDirty;

        }

        private Dictionary<uint, ObjectsPerPrefab> objectsPerPrefab;
        private Dictionary<uint, ParticleSystemInfo> systemForPrefab;
        private ViewsModuleProperties properties;

        private Dictionary<Ent, uint> entityToPrefabId;
        private Dictionary<Ent, int> entityToInstanceIndex;

        private Transform particlesRoot;

        public void Initialize(uint providerId, World viewsWorld, ViewsModuleProperties properties) {

            UnsafeViewsModule.RegisterProviderType<ParticlesProviderTag>(providerId);

            this.properties = properties;
            this.objectsPerPrefab = DictionaryPool<uint, ObjectsPerPrefab>.Get();
            this.systemForPrefab = DictionaryPool<uint, ParticleSystemInfo>.Get();
            this.entityToPrefabId = DictionaryPool<Ent, uint>.Get();
            this.entityToInstanceIndex = DictionaryPool<Ent, int>.Get();

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
            GameObject objInstance;
            if (prefabInfo.ptr->isLoaded == false) {
                throw new System.Exception("Prefab was not loaded, but we are trying to instantiate it.");
            } else {
                objInstance = new GameObject($"ParticleSystem_{prefabId}");
                objInstance.transform.SetParent(this.particlesRoot, false);
            }

            var rootParticleSystem = objInstance.AddComponent<ParticleSystem>();

            rootParticleSystem.Pause(withChildren: true);
            rootParticleSystem.Stop(withChildren: true);
            rootParticleSystem.useAutoRandomSeed = false;
            rootParticleSystem.randomSeed = 1u;

            var subEmitters = rootParticleSystem.subEmitters;
            subEmitters.enabled = true;
            {

                var main = rootParticleSystem.main;
                main.loop = false;
                main.prewarm = true;
                main.playOnAwake = false;
                main.duration = 10_000f;
                main.maxParticles = int.MaxValue;
                main.startLifetime = 10_000f;
                main.ringBufferMode = UnityEngine.ParticleSystemRingBufferMode.PauseUntilReplaced;
                main.simulationSpace = UnityEngine.ParticleSystemSimulationSpace.World;

                var emission = rootParticleSystem.emission;
                emission.enabled = false;

                var shape = rootParticleSystem.shape;
                shape.enabled = false;

                var renderer = rootParticleSystem.GetComponent<ParticleSystemRenderer>();
                renderer.enabled = false;
                renderer.alignment = UnityEngine.ParticleSystemRenderSpace.World;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

            }

            var prefab = (EntityView)handle.Target;

            var prefabInstance = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity, rootParticleSystem.transform);
            var requiredParticleSystems = prefabInstance.GetComponentsInChildren<ParticleSystem>(includeInactive: false);
            foreach (var requiredParticleSystem in requiredParticleSystems) {

                var main = requiredParticleSystem.main;
                main.prewarm = false;

                subEmitters.AddSubEmitter(requiredParticleSystem, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing, emitProbability: 1f);

            }

            var systemInfo = new ParticleSystemInfo {
                particleSystem = rootParticleSystem,
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
                    prevPos = pos,
                    prevTick = data.ptr->connectedWorld.CurrentTick,
                    velocity = float3.zero,
                });
                objects.isDirty = true;

                this.entityToInstanceIndex.Add(item.ent, objects.entities.Length - 1);

                this.objectsPerPrefab[prefabId] = objects;

                var instanceInfo = new SceneInstanceInfo((System.IntPtr)item.ent.ToULong(), data.ptr->toAddTemp[i].prefabInfo.info, 0u, data.ptr->toAddTemp[i].localData);
                data.ptr->renderingOnScene.Add(ref data.ptr->viewsWorld.state.ptr->allocator, instanceInfo);

            }

            return dependsOn;

        }

        public JobHandle Despawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {

            dependsOn.Complete();

            for (int i = 0; i < data.ptr->toRemoveTemp.Length; i++) {

                var item = data.ptr->toRemoveTemp[i];
                var entToRemove = new Ent((ulong)item.obj);
                var prefabId = item.prefabInfo.ptr->prefabId;

                this.entityToPrefabId.Remove(entToRemove);

                var objects = this.objectsPerPrefab[prefabId];

                var lastEnt = objects.entities[^1];

                var removeIndex = this.entityToInstanceIndex[entToRemove];

                objects.entities.RemoveAtSwapBack(removeIndex);
                objects.instances.RemoveAtSwapBack(removeIndex);
                objects.isDirty = true;

                this.entityToPrefabId.Remove(entToRemove);
                this.entityToInstanceIndex.Remove(entToRemove);

                if (lastEnt != entToRemove) {
                    this.entityToInstanceIndex[lastEnt] = removeIndex;
                }

                this.objectsPerPrefab[prefabId] = objects;

            }

            return dependsOn;

        }

        private float GetInterpolationFactor(safe_ptr<State> beginFrameState, ulong currentTick, double tickTime, double currentTimeSinceStart) {

            if (beginFrameState.ptr == null || beginFrameState.ptr->IsCreated == false) {
                return 1f;
            }

            var prevTick = beginFrameState.ptr->tick;
            var prevTime = prevTick * tickTime;
            var currentTime = currentTick * tickTime;
            var val = um::math.unlerp(prevTime, currentTime, currentTimeSinceStart);
            return (float)um::math.select(0d, um::math.clamp(val, 0d, 1d), prevTick != currentTick);

        }

        public JobHandle Commit(safe_ptr<ViewsModuleData> data, JobHandle dependsOn, float dt) {

            dependsOn.Complete();

            {

                var marker = new Unity.Profiling.ProfilerMarker("[Particles Provider] Move transforms");
                marker.Begin();
                // move

                var currentTick = data.ptr->connectedWorld.CurrentTick;
                var beginFrameState = data.ptr->beginFrameState.ptr->state;
                var tickTime = data.ptr->beginFrameState.ptr->tickTime;
                var currentTimeSinceStart = data.ptr->beginFrameState.ptr->timeSinceStart;

                var factor = this.GetInterpolationFactor(beginFrameState, currentTick, tickTime / 1000, currentTimeSinceStart / 1000);

                // UnityEngine.Debug.Log($"Factor {factor}");

                foreach (var kv in this.entityToPrefabId) {

                    var ent = kv.Key;
                    var prefabId = kv.Value;
                    var objects = this.objectsPerPrefab[prefabId];
                    var instanceIndex = this.entityToInstanceIndex[ent];

                    var instance = objects.instances[instanceIndex];

                    var worldMatrix = ent.Read<ME.BECS.Transforms.WorldMatrixComponent>().value;
                    var nextPos = math.lerp(instance.position, worldMatrix.c3.xyz, factor);
                    if (data.ptr->connectedWorld.CurrentTick > instance.prevTick && tickTime > 0) {
                        instance.velocity = (nextPos - instance.prevPos) * 1000 / tickTime;
                        instance.prevPos = nextPos;
                        instance.prevTick = data.ptr->connectedWorld.CurrentTick;
                    }
                    instance.position = nextPos;
                    var targetRotation = quaternion.LookRotationSafe(worldMatrix.c2.xyz, worldMatrix.c1.xyz);
                    instance.rotation = math.slerp(instance.rotation, targetRotation, factor);
                    objects.instances[instanceIndex] = instance;

                    objects.isDirty = true;
                    this.objectsPerPrefab[prefabId] = objects;

                }

                marker.End();

            }

            foreach (var kv in this.systemForPrefab) {

                var prefabId = kv.Key;
                var ps = kv.Value;
                var objects = this.objectsPerPrefab[prefabId];

                if (objects.isDirty == false) continue;

                var particlesRequired = objects.entities.Length;
                var particlesHas = ps.particleSystem.particleCount;
                if (particlesHas < particlesRequired) {
                    ps.particleSystem.Emit(new ParticleSystem.EmitParams(), particlesRequired - particlesHas);
                }

                var particlesArr = new NativeArray<ParticleSystem.Particle>(particlesRequired, Allocator.TempJob);
                ps.particleSystem.GetParticles(particlesArr);

                for (int i = 0; i < objects.instances.Length; i++) {

                    var particle = particlesArr[i];

                    var instance = objects.instances[i];
                    particle.position = (Vector3)instance.position;
                    particle.rotation3D = (Vector3)instance.rotation.ToEuler();
                    particle.velocity = (Vector3)instance.velocity;
                    particlesArr[i] = particle;

                }

                ps.particleSystem.SetParticles(particlesArr, particlesRequired);
                objects.isDirty = false;
                this.objectsPerPrefab[prefabId] = objects;

                particlesArr.Dispose();

            }

            {

                ref var allocator = ref data.ptr->viewsWorld.state.ptr->allocator;
                var continueLoadingRequests = new Unity.Collections.LowLevel.Unsafe.UnsafeList<uint>(data.ptr->loadingRequests.Count, Constants.ALLOCATOR_TEMPJOB);
                foreach (var prefabId in data.ptr->loadingRequests) {
                    if (data.ptr->prefabIdToInfo.TryGetValue(in allocator, prefabId, out var prefabInfo) == true) {
                        var handle = GCHandle.FromIntPtr(prefabInfo.info.ptr->prefabPtr);
                        var assetRef = (AssetOp)handle.Target;
                        if (assetRef.IsLoading() == false) {
                            assetRef.StartLoading();
                        } else if (assetRef.IsLoaded() == true) {
                            prefabInfo.info.ptr->isLoaded = true;
                            var go = (UnityEngine.GameObject)assetRef.assetReference.Asset;
                            handle = new HeapReference<EntityView>(go.GetComponent<EntityView>()).handle;
                            prefabInfo.info.ptr->prefabPtr = GCHandle.ToIntPtr(handle);
                            data.ptr->gcHandles.Add(ref data.ptr->viewsWorld.state.ptr->allocator, handle);
                            this.GetOrCreateSystem(prefabId, prefabInfo.info);
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

        public void Dispose(safe_ptr<State> state, safe_ptr<ViewsModuleData> data) {

            foreach (var kv in this.objectsPerPrefab) {
                var objects = kv.Value;
                objects.instances.Dispose();
                objects.entities.Dispose();
            }

            foreach (var kv in this.systemForPrefab) {

                var systemInfo = kv.Value;

                if (systemInfo.particleSystem != null) {
                    GameObject.DestroyImmediate(systemInfo.particleSystem.gameObject);
                }

            }

            this.objectsPerPrefab.Clear();
            this.entityToPrefabId.Clear();
            this.entityToInstanceIndex.Clear();

            if (this.particlesRoot != null) {
                GameObject.DestroyImmediate(this.particlesRoot.gameObject);
            }

            DictionaryPool<uint, ObjectsPerPrefab>.Release(this.objectsPerPrefab);
            DictionaryPool<Ent, uint>.Release(this.entityToPrefabId);
            DictionaryPool<Ent, int>.Release(this.entityToInstanceIndex);

        }

        public void ApplyStateParallel(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData) {

            return;

        }

        public void ApplyState(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData) {

            return;

        }

        public void OnUpdate(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData, float dt) {

        }

        public void OnUpdateParallel(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData, float dt) {

        }

        public void Load(safe_ptr<ViewsModuleData> viewsModuleData, ObjectReferenceRegistryData data) {

            viewsModuleData.ptr->prefabId = math.max(viewsModuleData.ptr->prefabId, data.GetSourceId());
            foreach (var item in data.objects) {
                var objectItem = new ObjectItem(item.data);
                if (objectItem.IsValid() == true && objectItem.Is<EntityView>() == true) {
                    this.Register(viewsModuleData, objectItem, item.data.sourceId);
                }
            }

        }

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
                this.GetOrCreateSystem(viewsModuleData.ptr->prefabId, prefabInfo.info);

                viewsModuleData.ptr->prefabIdToInfo.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, prefabId, new SourceRegistry.InfoRef(info));

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
                    this.GetOrCreateSystem(viewsModuleData.ptr->prefabId, prefabInfo.info);
                } else {
                    viewsModuleData.ptr->loadingRequests.Add(prefabId);
                }

                viewsModuleData.ptr->prefabIdToInfo.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, prefabId, prefabInfo);

            }

        }

        public void Query(ref QueryBuilder queryBuilder) {
            queryBuilder.With<ParticlesProviderTag>();
        }

        public IView GetViewByEntity(safe_ptr<ViewsModuleData> data, in Ent entity) => null;

    }

}
