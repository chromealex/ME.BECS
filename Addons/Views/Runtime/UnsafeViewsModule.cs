using System.Linq;
using Unity.Collections;

namespace ME.BECS.Views {

    using g = System.Collections.Generic;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;

    public unsafe interface IViewProvider<TEntityView> where TEntityView : IView {

        void Initialize(uint providerId, World viewsWorld, ViewsModuleProperties properties);
        JobHandle Spawn(ViewsModuleData* data, UnsafeList<SpawnInstanceInfo> list, JobHandle dependsOn);
        JobHandle Despawn(ViewsModuleData* data, UnsafeList<SceneInstanceInfo> list, JobHandle dependsOn);
        /// <summary>
        /// Apply Spawn/Despawn commands
        /// </summary>
        JobHandle Commit(ViewsModuleData* data, JobHandle dependsOn);
        void Dispose(State* state, ViewsModuleData* data);
        void ApplyState(in SceneInstanceInfo instanceInfo, in Ent ent);
        void OnUpdate(in SceneInstanceInfo instanceInfo, in Ent ent, float dt);

        public void Load(ViewsModuleData* viewsModuleData, ViewsRegistryData data);
        public ViewSource Register(ViewsModuleData* viewsModuleData, TEntityView prefab, uint prefabId = 0u, bool checkPrefab = true, bool sceneSource = false);

        void Query(ref QueryBuilder queryBuilder);

    }

    public struct HeapReference<T> {

        public System.Runtime.InteropServices.GCHandle handle;

        [INLINE(256)]
        public HeapReference(T obj) {
            this.handle = System.Runtime.InteropServices.GCHandle.Alloc(obj);
        }

        public T Value => (T)this.handle.Target;

        [INLINE(256)]
        public void Dispose() {
            if (this.handle.IsAllocated == true) this.handle.Free();
        }

    }
    
    [System.Serializable]
    public struct ViewsModuleProperties {

        public static ViewsModuleProperties Default => new ViewsModuleProperties() {
            instancesRegistryCapacity = 10u,
            renderingObjectsCapacity = 100u,
            viewsGameObjects = true,
            viewsDrawMeshes = true,
        };

        [UnityEngine.Tooltip("How many unique prefabs will be registered.")]
        public uint instancesRegistryCapacity;
        [UnityEngine.Tooltip("How many instances will be drawing on the scene at once.")]
        public uint renderingObjectsCapacity;

        [UnityEngine.Tooltip("Enable GameObjects Provider.")]
        public bool viewsGameObjects;
        [UnityEngine.Tooltip("Enable DrawMeshes Provider.")]
        public bool viewsDrawMeshes;

    }

    public interface IView {

        void DoInitialize(in Ent ent);
        void DoInitializeChildren(in Ent ent);
        void DoEnableFromPool(in Ent ent);
        void DoEnableFromPoolChildren(in Ent ent);
        void DoDeInitialize();

    }
    
    public unsafe struct SourceRegistry {

        public struct Info {

            public System.IntPtr prefabPtr;
            public uint prefabId;
            public ViewTypeInfo typeInfo;
            public bool sceneSource;
            
            public TypeFlags flags;

            public bool HasApplyStateModules {
                get => (this.flags & TypeFlags.ApplyState) != 0;
                set {
                    if (value == true) {
                        this.flags |= TypeFlags.ApplyState;
                    } else {
                        this.flags &= ~TypeFlags.ApplyState;
                    }
                }
            }

            public bool HasUpdateModules {
                get => (this.flags & TypeFlags.Update) != 0;
                set {
                    if (value == true) {
                        this.flags |= TypeFlags.Update;
                    } else {
                        this.flags &= ~TypeFlags.Update;
                    }
                }
            }

            public bool HasInitializeModules {
                get => (this.flags & TypeFlags.Initialize) != 0;
                set {
                    if (value == true) {
                        this.flags |= TypeFlags.Initialize;
                    } else {
                        this.flags &= ~TypeFlags.Initialize;
                    }
                }
            }

            public bool HasDeInitializeModules {
                get => (this.flags & TypeFlags.DeInitialize) != 0;
                set {
                    if (value == true) {
                        this.flags |= TypeFlags.DeInitialize;
                    } else {
                        this.flags &= ~TypeFlags.DeInitialize;
                    }
                }
            }

            public bool HasEnableFromPoolModules {
                get => (this.flags & TypeFlags.EnableFromPool) != 0;
                set {
                    if (value == true) {
                        this.flags |= TypeFlags.EnableFromPool;
                    } else {
                        this.flags &= ~TypeFlags.EnableFromPool;
                    }
                }
            }

            public bool HasDisableToPoolModules {
                get => (this.flags & TypeFlags.DisableToPool) != 0;
                set {
                    if (value == true) {
                        this.flags |= TypeFlags.DisableToPool;
                    } else {
                        this.flags &= ~TypeFlags.DisableToPool;
                    }
                }
            }

        }

        public struct InfoRef {

            public Info* info;

            public InfoRef(Info info) {
                this.info = _make(info);
            }

        }
        
    }

    public struct RenderingSparseList {

        public SparseSet sparseSet;
        public uint Count;

        public RenderingSparseList(ref MemoryAllocator allocator, uint capacity) {
            this.sparseSet = new SparseSet(ref allocator, capacity);
            this.Count = 0u;
        }

        public void Add(ref MemoryAllocator allocator, uint index) {
            this.sparseSet.Set(ref allocator, index, out _);
            ++this.Count;
        }

        public void Remove(in MemoryAllocator allocator, uint idx) {
            if (this.sparseSet.Remove(in allocator, idx, out var fromIndex, out var toIndex) == true) {
                --this.Count;
            }
        }

    }

    public struct SpawnInstanceInfo {

        public Ent ent;
        public SourceRegistry.InfoRef prefabInfo;

    }

    public unsafe struct SceneInstanceInfo {

        public System.IntPtr obj;
        public readonly SourceRegistry.Info* prefabInfo;
        public uint index;

        public SceneInstanceInfo(System.IntPtr obj, SourceRegistry.Info* prefabInfo) {
            this = default;
            this.obj = obj;
            this.prefabInfo = prefabInfo;
            this.index = 0u;
        }

    }

    public unsafe struct ViewsModuleData {

        public struct EntityData {

            public TransformAspect.TransformAspect element;
            public uint version;

        }
        
        public uint prefabId;
        public UIntDictionary<SourceRegistry.InfoRef> prefabIdToInfo;
        public UIntDictionary<uint> instanceIdToPrefabId;

        public TempBitArray renderingOnSceneBits;
        public UIntDictionary<uint> renderingOnSceneEntToRenderIndex;
        public UIntDictionary<uint> renderingOnSceneRenderIndexToEnt;
        public MemArray<uint> renderingOnSceneEntToPrefabId;
        public UnsafeParallelHashMap<uint, bool> toChange;
        public UnsafeParallelHashMap<uint, bool> toRemove;
        public UnsafeParallelHashMap<uint, bool> toAdd;
        public UnsafeParallelHashMap<uint, bool> dirty;
        public UnsafeList<EntityData> renderingOnSceneEnts;
        public List<SceneInstanceInfo> renderingOnScene;
        
        public RenderingSparseList renderingOnSceneApplyState;
        public RenderingSparseList renderingOnSceneUpdate;
        
        public uint renderingOnSceneCount;
        public UnsafeList<SceneInstanceInfo> toRemoveTemp;
        public UnsafeList<SpawnInstanceInfo> toAddTemp;

        public World connectedWorld;
        public World viewsWorld;
        
        public static ViewsModuleData Create(ref MemoryAllocator allocator, uint entitiesCapacity, ViewsModuleProperties properties) {

            return new ViewsModuleData() {
                prefabId = 0u,
                prefabIdToInfo = new UIntDictionary<SourceRegistry.InfoRef>(ref allocator, properties.instancesRegistryCapacity),
                instanceIdToPrefabId = new UIntDictionary<uint>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneCount = 0u,
                renderingOnScene = new List<SceneInstanceInfo>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneApplyState = new RenderingSparseList(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneUpdate = new RenderingSparseList(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneEnts = new UnsafeList<EntityData>((int)properties.renderingObjectsCapacity, Allocator.Persistent),
                renderingOnSceneBits = new TempBitArray(properties.renderingObjectsCapacity, allocator: Allocator.Persistent),
                renderingOnSceneEntToRenderIndex = new UIntDictionary<uint>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneRenderIndexToEnt = new UIntDictionary<uint>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneEntToPrefabId = new MemArray<uint>(ref allocator, entitiesCapacity),
                toChange = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, Allocator.Persistent),
                toRemove = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, Allocator.Persistent),
                toAdd = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, Allocator.Persistent),
                dirty = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, Allocator.Persistent),
                toRemoveTemp = new UnsafeList<SceneInstanceInfo>((int)properties.renderingObjectsCapacity, Allocator.Persistent),
                toAddTemp = new UnsafeList<SpawnInstanceInfo>((int)properties.renderingObjectsCapacity, Allocator.Persistent),
            };

        }

        public void Dispose(State* state) {
            
            if (this.renderingOnSceneEnts.IsCreated == true) this.renderingOnSceneEnts.Dispose();
            if (this.renderingOnSceneBits.isCreated == true) this.renderingOnSceneBits.Dispose();
            if (this.toRemove.IsCreated == true) this.toRemove.Dispose();
            if (this.toAdd.IsCreated == true) this.toAdd.Dispose();
            if (this.dirty.IsCreated == true) this.dirty.Dispose();
            if (this.toChange.IsCreated == true) this.toChange.Dispose();
            
        }

    }

    public delegate void ProviderInstantiateView(in Ent ent, in ViewSource viewSource);
    public delegate void ProviderDestroyView(in Ent ent);
    
    public unsafe struct UnsafeViewsModule {

        public struct ProviderInfo : IIsCreated {

            public bool isCreated { get; set; }
            public Unity.Burst.FunctionPointer<ProviderInstantiateView> instantiateMethod;
            public Unity.Burst.FunctionPointer<ProviderDestroyView> destroyMethod;

        }
        
        internal static readonly Unity.Burst.SharedStatic<UnsafeList<ProviderInfo>> registeredProviders = Unity.Burst.SharedStatic<UnsafeList<ProviderInfo>>.GetOrCreatePartiallyUnsafeWithHashCode<UnsafeViewsModule>(TAlign<UnsafeList<ProviderInfo>>.align, 20021);
        
        public static void RegisterProviderCallbacks(uint providerId, ProviderInstantiateView instantiateView, ProviderDestroyView destroyView) {

            if (registeredProviders.Data.IsCreated == false) {
                registeredProviders.Data = new UnsafeList<ProviderInfo>((int)providerId + 1, Allocator.Domain);
            }
            registeredProviders.Data.Resize((int)providerId + 1, NativeArrayOptions.ClearMemory);

            ref var item = ref *(registeredProviders.Data.Ptr + providerId);
            item.isCreated = true;
            item.instantiateMethod = Unity.Burst.BurstCompiler.CompileFunctionPointer(instantiateView);
            item.destroyMethod = Unity.Burst.BurstCompiler.CompileFunctionPointer(destroyView);

        }
        
        [INLINE(256)]
        public static void InstantiateView(in Ent ent, in ViewSource viewSource) {

            ent.Set(new ViewComponent() {
                source = viewSource,
            });
            ent.Set(new IsViewRequested());
            if (viewSource.providerId < registeredProviders.Data.Length) {
                ref var item = ref *(registeredProviders.Data.Ptr + viewSource.providerId);
                E.IS_CREATED(item);
                item.instantiateMethod.Invoke(in ent, in viewSource);
            }

        }

        [INLINE(256)]
        public static void DestroyView(in Ent ent) {

            ent.Remove<IsViewRequested>();
            
        }

    }

    [BURST]
    public unsafe struct UnsafeViewsModule<TEntityView> where TEntityView : IView {

        public ViewsModuleData* data;
        private IViewProvider<TEntityView> provider;
        
        public static UnsafeViewsModule<TEntityView> Create<T>(uint providerId, ref World connectedWorld, T provider, uint entitiesCapacity, ViewsModuleProperties properties) where T : IViewProvider<TEntityView> {
            
            ViewsRegistry.Initialize();

            var viewsWorldProperties = WorldProperties.Default;
            viewsWorldProperties.allocatorProperties.sizeInBytesCapacity = (uint)MemoryAllocator.MIN_ZONE_SIZE; // Use min allocator size
            viewsWorldProperties.name = ViewsModule.providerInfos[providerId].editorName;

            var viewsWorld = World.Create(viewsWorldProperties);
            provider.Initialize(providerId, viewsWorld, properties);

            var module = new UnsafeViewsModule<TEntityView> {
                data = _make(ViewsModuleData.Create(ref viewsWorld.state->allocator, entitiesCapacity, properties)),
                provider = provider,
            };
            module.data->connectedWorld = connectedWorld;
            module.data->viewsWorld = viewsWorld;
            WorldStaticCallbacks.RaiseCallback(ref *module.data);
            module.provider.Load(module.data, ViewsRegistry.data);
            Context.Switch(in connectedWorld);
            return module;

        }

        public void Dispose() {

            this.provider.Dispose(this.data->viewsWorld.state, this.data);
            this.data->Dispose(this.data->viewsWorld.state);
            _free(this.data);
            this.data->viewsWorld.Dispose();
            this = default;

        }
        
        public ViewSource RegisterViewSource(TEntityView prefab) {

            return this.provider.Register(this.data, prefab);

        }

        internal ViewSource RegisterViewSource(TEntityView prefab, bool checkPrefab, bool sceneSource = false) {

            return this.provider.Register(this.data, prefab, checkPrefab: checkPrefab, sceneSource: sceneSource);

        }

        public JobHandle Update(float dt) {
            return this.Update(dt, default);
        }

        public JobHandle Update(float dt, JobHandle dependsOn) {

            E.IS_CREATED(this.data->connectedWorld);
            E.IS_CREATED(this.data->viewsWorld);

            var toRemoveCounter = Jobs.Counter.Create();
            
            ref var allocator = ref this.data->viewsWorld.state->allocator;
            dependsOn = new Jobs.PrepareJob() {
                viewsModuleData = this.data,
                state = this.data->viewsWorld.state,
                connectedWorld = this.data->connectedWorld,
            }.Schedule(dependsOn);
            
            JobHandle toRemoveEntitiesJob;
            {
                // Update views
                JobHandle toRemoveJob;
                {
                    // DestroyView() case: Remove views from the scene which don't have ViewComponent, but contained in renderingOnSceneBits (DestroyView called)
                    var query = API.Query(in this.data->connectedWorld, dependsOn).With<ViewComponent>().Without<IsViewRequested>();
                    this.provider.Query(ref query);
                    toRemoveJob = query.ScheduleParallelFor(new Jobs.JobRemoveFromScene() {
                        viewsModuleData = this.data,
                        toRemove = this.data->toRemove.AsParallelWriter(),
                        toRemoveCounter = toRemoveCounter,
                        registeredProviders = UnsafeViewsModule.registeredProviders.Data,
                    });
                }
                JobHandle toAddJob;
                {
                    // InstantiateView() case: Add views to the scene which have ViewComponent, but not contained in renderingOnSceneBits
                    var query = API.Query(in this.data->connectedWorld, dependsOn).With<IsViewRequested>().WithAspect<TransformAspect.TransformAspect>();
                    this.provider.Query(ref query);
                    toAddJob = query.ScheduleParallelFor(new Jobs.JobAddToScene() {
                        state = this.data->viewsWorld.state,
                        viewsModuleData = this.data,
                        toAdd = this.data->toAdd.AsParallelWriter(),
                        toRemove = this.data->toRemove.AsParallelWriter(),
                        dirty = this.data->dirty.AsParallelWriter(),
                        toRemoveCounter = toRemoveCounter,
                    });
                }
                {
                    var handle = JobHandle.CombineDependencies(toRemoveJob, toAddJob);
                    // Add entities which has been destroyed, but contained in renderingOnScene
                    toRemoveEntitiesJob = new Jobs.JobRemoveEntitiesFromScene() {
                        world = this.data->connectedWorld,
                        viewsModuleData = this.data,
                        dirty = this.data->dirty,
                        toChange = this.data->toChange.AsParallelWriter(),
                        toRemove = this.data->toRemove.AsParallelWriter(),
                        toRemoveCounter = toRemoveCounter,
                    }.Schedule(this.data->renderingOnSceneEnts.Length, 64, handle);
                }
            }

            dependsOn = toRemoveEntitiesJob;

            {
                // Update views
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Update Remove Lists Schedule");
                    marker.Begin();
                    dependsOn = new Jobs.JobDespawnViews() {
                        viewsWorld = this.data->viewsWorld,
                        data = this.data,
                    }.Schedule(dependsOn);
                    marker.End();
                }

                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Update Add Lists Schedule");
                    marker.Begin();
                    dependsOn = new Jobs.JobSpawnViews() {
                        connectedWorld = this.data->connectedWorld,
                        viewsWorld = this.data->viewsWorld,
                        data = this.data,
                    }.Schedule(dependsOn);
                    marker.End();
                }

            }
            JobUtils.RunScheduled();
            dependsOn.Complete();
            
            {
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Provider::Despawn");
                    marker.Begin();
                    dependsOn = this.provider.Despawn(this.data, this.data->toRemoveTemp, dependsOn);
                    marker.End();
                }
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Provider::Spawn");
                    marker.Begin();
                    dependsOn = this.provider.Spawn(this.data, this.data->toAddTemp, dependsOn);
                    marker.End();
                }
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Provider::Commit");
                    marker.Begin();
                    dependsOn = this.provider.Commit(this.data, dependsOn);
                    marker.End();
                }
            }
            JobUtils.RunScheduled();
            
            {
                // Update views logic
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] ApplyState Views");
                    marker.Begin();
                    for (uint i = 0u; i < this.data->renderingOnSceneApplyState.Count; ++i) {
                        var entId = this.data->renderingOnSceneApplyState.sparseSet.dense[in allocator, i];
                        var idx = this.data->renderingOnSceneEntToRenderIndex.ReadValue(in allocator, entId);
                        ref var entData = ref *(this.data->renderingOnSceneEnts.Ptr + idx);
                        var view = this.data->renderingOnScene[in allocator, idx];
                        var ent = entData.element.ent;
                        if (entData.version != ent.Version) {
                            entData.version = ent.Version;
                            this.provider.ApplyState(in view, in ent);
                        }
                    }
                    marker.End();
                }

                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Update Views");
                    marker.Begin();
                    for (uint i = 0u; i < this.data->renderingOnSceneUpdate.Count; ++i) {
                        var entId = this.data->renderingOnSceneUpdate.sparseSet.dense[in allocator, i];
                        var idx = this.data->renderingOnSceneEntToRenderIndex.ReadValue(in allocator, entId);
                        ref var entData = ref *(this.data->renderingOnSceneEnts.Ptr + idx);
                        var view = this.data->renderingOnScene[in allocator, idx];
                        var ent = entData.element.ent;
                        if (view.prefabInfo->typeInfo.HasUpdate == true) {
                            this.provider.OnUpdate(in view, in ent, dt);
                        }
                    }
                    marker.End();
                }
            }
            
            {
                // Clean up
                this.data->toRemoveTemp.Clear();
                this.data->toAddTemp.Clear();
                this.data->toChange.Clear();
                this.data->toAdd.Clear();
                this.data->toRemove.Clear();
                this.data->dirty.Clear();
                toRemoveCounter->Dispose();
            }
            return dependsOn;

        }

    }

}