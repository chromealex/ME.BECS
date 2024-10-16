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
        JobHandle Spawn(ViewsModuleData* data, JobHandle dependsOn);
        JobHandle Despawn(ViewsModuleData* data, JobHandle dependsOn);
        /// <summary>
        /// Apply Spawn/Despawn commands
        /// </summary>
        JobHandle Commit(ViewsModuleData* data, JobHandle dependsOn);
        void Dispose(State* state, ViewsModuleData* data);
        void ApplyState(in SceneInstanceInfo instanceInfo, in Ent ent);
        void OnUpdate(in SceneInstanceInfo instanceInfo, in Ent ent, float dt);

        public void Load(ViewsModuleData* viewsModuleData, BECS.ObjectReferenceRegistryData data);
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

    public struct HeapReference {

        public System.Runtime.InteropServices.GCHandle handle;

        [INLINE(256)]
        public HeapReference(object obj) {
            this.handle = System.Runtime.InteropServices.GCHandle.Alloc(obj, System.Runtime.InteropServices.GCHandleType.Pinned);
        }

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
            interpolateState = true,
        };

        [UnityEngine.Tooltip("How many unique prefabs will be registered.")]
        public uint instancesRegistryCapacity;
        [UnityEngine.Tooltip("How many instances will be drawing on the scene at once.")]
        public uint renderingObjectsCapacity;

        [UnityEngine.Tooltip("Enable GameObjects Provider.")]
        public bool viewsGameObjects;
        [UnityEngine.Tooltip("Enable DrawMeshes Provider.")]
        public bool viewsDrawMeshes;

        [UnityEngine.Tooltip("Use automatic state interpolation between start and end of the frame. Useful with Network Module only.")]
        public bool interpolateState;

    }

    public interface IView {

        void DoInitialize(in EntRO ent);
        void DoInitializeChildren(in EntRO ent);
        void DoEnableFromPool(in EntRO ent);
        void DoEnableFromPoolChildren(in EntRO ent);
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

    public unsafe struct BeginFrameState {

        public State* state;
        public float tickTime;
        public double timeSinceStart;

    }

    public unsafe struct ViewsModuleData {

        public struct EntityData {

            public Ent element;
            public uint version;

        }
        
        public uint prefabId;
        public UIntDictionary<SourceRegistry.InfoRef> prefabIdToInfo;
        public UIntDictionary<uint> instanceIdToPrefabId;

        public TempBitArray renderingOnSceneBits;
        public UIntDictionary<uint> renderingOnSceneEntToRenderIndex;
        public UIntDictionary<uint> renderingOnSceneRenderIndexToEnt;
        public MemArray<uint> renderingOnSceneEntToPrefabId;
        public UnsafeParallelHashMap<uint, uint> toAssign;
        public UnsafeParallelHashMap<uint, bool> toChange;
        public UnsafeParallelHashMap<uint, bool> toRemove;
        public UnsafeParallelHashMap<uint, bool> toAdd;
        public UnsafeList<bool> dirty;
        public UnsafeList<EntityData> renderingOnSceneEnts;
        public List<SceneInstanceInfo> renderingOnScene;
        
        public RenderingSparseList renderingOnSceneApplyState;
        public RenderingSparseList renderingOnSceneUpdate;
        public uint* applyStateCounter;
        public uint* updateCounter;

        public MemArray<bool> renderingOnSceneApplyStateCulling;
        public MemArray<bool> renderingOnSceneUpdateCulling;

        public uint renderingOnSceneCount;
        public UnsafeList<SceneInstanceInfo> toRemoveTemp;
        public UnsafeList<SpawnInstanceInfo> toAddTemp;

        public ViewsModuleProperties properties;
        
        public World connectedWorld;
        public World viewsWorld;
        public BeginFrameState* beginFrameState;

        public Ent camera;
        
        public static ViewsModuleData Create(ref MemoryAllocator allocator, uint entitiesCapacity, ViewsModuleProperties properties) {

            return new ViewsModuleData() {
                prefabId = 0u,
                properties = properties,
                beginFrameState = _make(new BeginFrameState()),
                prefabIdToInfo = new UIntDictionary<SourceRegistry.InfoRef>(ref allocator, properties.instancesRegistryCapacity),
                instanceIdToPrefabId = new UIntDictionary<uint>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneCount = 0u,
                renderingOnScene = new List<SceneInstanceInfo>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneApplyState = new RenderingSparseList(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneUpdate = new RenderingSparseList(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneApplyStateCulling = new MemArray<bool>(ref allocator, entitiesCapacity),
                renderingOnSceneUpdateCulling = new MemArray<bool>(ref allocator, entitiesCapacity),
                renderingOnSceneEnts = new UnsafeList<EntityData>((int)properties.renderingObjectsCapacity, Constants.ALLOCATOR_DOMAIN),
                renderingOnSceneBits = new TempBitArray(properties.renderingObjectsCapacity, allocator: Constants.ALLOCATOR_DOMAIN),
                renderingOnSceneEntToRenderIndex = new UIntDictionary<uint>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneRenderIndexToEnt = new UIntDictionary<uint>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneEntToPrefabId = new MemArray<uint>(ref allocator, entitiesCapacity),
                applyStateCounter = _make<uint>(0u),
                updateCounter = _make<uint>(0u),
                toAssign = new UnsafeParallelHashMap<uint, uint>((int)properties.renderingObjectsCapacity, Constants.ALLOCATOR_DOMAIN),
                toChange = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, Constants.ALLOCATOR_DOMAIN),
                toRemove = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, Constants.ALLOCATOR_DOMAIN),
                toAdd = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, Constants.ALLOCATOR_DOMAIN),
                dirty = new UnsafeList<bool>((int)properties.renderingObjectsCapacity, Constants.ALLOCATOR_DOMAIN),
                toRemoveTemp = new UnsafeList<SceneInstanceInfo>((int)properties.renderingObjectsCapacity, Constants.ALLOCATOR_DOMAIN),
                toAddTemp = new UnsafeList<SpawnInstanceInfo>((int)properties.renderingObjectsCapacity, Constants.ALLOCATOR_DOMAIN),
            };

        }

        public void SetCamera(in CameraAspect camera) {
            this.camera = camera.ent;
        }

        public void Dispose(State* state) {
            
            _free(ref this.beginFrameState);
            _free(ref this.applyStateCounter);
            _free(ref this.updateCounter);
            if (this.renderingOnSceneEnts.IsCreated == true) this.renderingOnSceneEnts.Dispose();
            if (this.renderingOnSceneBits.IsCreated == true) this.renderingOnSceneBits.Dispose();
            if (this.toRemove.IsCreated == true) this.toRemove.Dispose();
            if (this.toAdd.IsCreated == true) this.toAdd.Dispose();
            if (this.dirty.IsCreated == true) this.dirty.Dispose();
            if (this.toAssign.IsCreated == true) this.toAssign.Dispose();
            if (this.toChange.IsCreated == true) this.toChange.Dispose();
            
        }

    }

    public unsafe struct UnsafeViewsModule {

        public struct ProviderInfo : IIsCreated {

            public bool IsCreated { get; set; }
            public uint typeId;

        }
        
        internal static readonly Unity.Burst.SharedStatic<UnsafeList<ProviderInfo>> registeredProviders = Unity.Burst.SharedStatic<UnsafeList<ProviderInfo>>.GetOrCreatePartiallyUnsafeWithHashCode<UnsafeViewsModule>(TAlign<UnsafeList<ProviderInfo>>.align, 20021);
        
        public static void RegisterProviderType<T>(uint providerId) where T : unmanaged, IComponent {

            if (registeredProviders.Data.IsCreated == false) {
                registeredProviders.Data = new UnsafeList<ProviderInfo>((int)providerId + 1, Constants.ALLOCATOR_DOMAIN);
            }
            registeredProviders.Data.Resize((int)providerId + 1, NativeArrayOptions.ClearMemory);

            ref var item = ref *(registeredProviders.Data.Ptr + providerId);
            item.IsCreated = true;
            item.typeId = StaticTypes<T>.typeId;

        }
        
        [INLINE(256)]
        public static bool InstantiateView(in Ent ent, in ViewSource viewSource) {

            if (viewSource.IsValid == false) return false;
            
            ent.Set(new ViewComponent() {
                source = viewSource,
            });
            ent.Set(new IsViewRequested());
            if (viewSource.providerId < registeredProviders.Data.Length) {
                ref var item = ref *(registeredProviders.Data.Ptr + viewSource.providerId);
                E.IS_CREATED(item);
                ent.Set(item.typeId, null);
            }
            
            return true;

        }

        [INLINE(256)]
        public static bool AssignView(in Ent ent, in Ent sourceEnt) {

            if (sourceEnt.TryRead(out ViewComponent viewComponent) == true &&
                ent.Has<ViewComponent>() == false) {

                // Clean up source entity
                sourceEnt.Remove<ViewComponent>();
                sourceEnt.Remove<IsViewRequested>();

                // Assign ent to the current view
                ent.Set(new AssignViewComponent() {
                    source = viewComponent.source,
                    sourceEnt = sourceEnt,
                });
                ent.Set(viewComponent);
                ent.Set(new IsViewRequested());
                if (viewComponent.source.providerId < registeredProviders.Data.Length) {
                    ref var item = ref *(registeredProviders.Data.Ptr + viewComponent.source.providerId);
                    E.IS_CREATED(item);
                    ent.Set(item.typeId, null);
                }
                return true;
                
            }

            return false;

        }

        [INLINE(256)]
        public static void DestroyView(in Ent ent) {

            ent.Remove<IsViewRequested>();
            
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct UnsafeViewsModule<TEntityView> where TEntityView : IView {

        public ViewsModuleData* data;
        private IViewProvider<TEntityView> provider;
        
        public static UnsafeViewsModule<TEntityView> Create<T>(uint providerId, ref World connectedWorld, T provider, uint entitiesCapacity, ViewsModuleProperties properties) where T : IViewProvider<TEntityView> {
            
            var viewsWorldProperties = WorldProperties.Default;
            viewsWorldProperties.allocatorProperties.sizeInBytesCapacity = (uint)MemoryAllocator.MIN_ZONE_SIZE; // Use min allocator size
            viewsWorldProperties.name = ViewsModule.providerInfos[providerId].editorName;
            viewsWorldProperties.stateProperties.mode = WorldMode.Visual;

            var viewsWorld = World.Create(viewsWorldProperties, false);
            var prevContext = Context.world;
            Context.Switch(in viewsWorld);
            provider.Initialize(providerId, viewsWorld, properties);

            var module = new UnsafeViewsModule<TEntityView> {
                data = _make(ViewsModuleData.Create(ref viewsWorld.state->allocator, entitiesCapacity, properties)),
                provider = provider,
            };
            module.data->connectedWorld = connectedWorld;
            module.data->viewsWorld = viewsWorld;
            WorldStaticCallbacks.RaiseCallback(ref *module.data);
            module.provider.Load(module.data, BECS.ObjectReferenceRegistry.data);
            Context.Switch(in prevContext);
            return module;

        }

        public void Dispose() {

            this.provider.Dispose(this.data->viewsWorld.state, this.data);
            this.data->Dispose(this.data->viewsWorld.state);
            _free(this.data);
            this.data->viewsWorld.Dispose();
            this = default;

        }

        public void SetCamera(in CameraAspect camera) {

            this.data->SetCamera(in camera);

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

            WorldStaticCallbacks.RaiseCallback(ref *this.data, 1);
            
            ref var allocator = ref this.data->viewsWorld.state->allocator;
            dependsOn = new Jobs.PrepareJob() {
                viewsModuleData = this.data,
                state = this.data->viewsWorld.state,
                connectedWorld = this.data->connectedWorld,
            }.Schedule(dependsOn);
            
            JobHandle toRemoveEntitiesJob;
            {
                // Update views
                {
                    // Assign views first
                    var query = API.Query(in this.data->connectedWorld, dependsOn).With<AssignViewComponent>();
                    this.provider.Query(ref query);
                    var toAssignJob = query.ScheduleParallelFor(new Jobs.JobAssignViews() {
                        viewsWorld = this.data->viewsWorld,
                        viewsModuleData = this.data,
                        registeredProviders = UnsafeViewsModule.registeredProviders.Data,
                        toAssign = this.data->toAssign.AsParallelWriter(),
                    });
                    dependsOn = toAssignJob;
                }
                JobHandle toRemoveJob;
                {
                    // DestroyView() case: Remove views from the scene which don't have ViewComponent, but contained in renderingOnSceneBits (DestroyView called)
                    var query = API.Query(in this.data->connectedWorld, dependsOn).With<ViewComponent>().Without<IsViewRequested>();
                    this.provider.Query(ref query);
                    toRemoveJob = query.ScheduleParallelFor(new Jobs.JobRemoveFromScene() {
                        viewsModuleData = this.data,
                        toRemove = this.data->toRemove.AsParallelWriter(),
                        registeredProviders = UnsafeViewsModule.registeredProviders.Data,
                    });
                }
                JobHandle toAddJob;
                {
                    // InstantiateView() case: Add views to the scene which have ViewComponent, but not contained in renderingOnSceneBits
                    var query = API.Query(in this.data->connectedWorld, dependsOn).With<IsViewRequested>().WithAspect<Transforms.TransformAspect>();
                    this.provider.Query(ref query);
                    toAddJob = query.ScheduleParallelFor(new Jobs.JobAddToScene() {
                        state = this.data->viewsWorld.state,
                        viewsModuleData = this.data,
                        toAdd = this.data->toAdd.AsParallelWriter(),
                        toRemove = this.data->toRemove.AsParallelWriter(),
                    });
                }
                {
                    var handle = JobHandle.CombineDependencies(toRemoveJob, toAddJob);
                    // Add entities which has been destroyed, but contained in renderingOnScene
                    toRemoveEntitiesJob = new Jobs.JobRemoveEntitiesFromScene() {
                        world = this.data->connectedWorld,
                        viewsModuleData = this.data,
                        toChange = this.data->toChange.AsParallelWriter(),
                        toRemove = this.data->toRemove.AsParallelWriter(),
                    }.Schedule(this.data->renderingOnSceneEnts.Length, JobUtils.GetScheduleBatchCount(this.data->renderingOnSceneEnts.Length), handle);
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
            
            if (this.data->camera.IsAlive() == true) { // Update culling

                var cullingApplyState = new Jobs.UpdateCullingApplyStateJob() {
                    state = this.data->viewsWorld.state,
                    viewsModuleData = this.data,
                }.Schedule((int*)this.data->applyStateCounter, 64, dependsOn);

                var cullingUpdateState = new Jobs.UpdateCullingUpdateJob() {
                    state = this.data->viewsWorld.state,
                    viewsModuleData = this.data,
                }.Schedule((int*)this.data->updateCounter, 64, dependsOn);

                dependsOn = JobHandle.CombineDependencies(cullingApplyState, cullingUpdateState);

            }

            JobUtils.RunScheduled();
            
            {
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Provider::Despawn");
                    marker.Begin();
                    dependsOn = this.provider.Despawn(this.data, dependsOn);
                    marker.End();
                }
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Provider::Spawn");
                    marker.Begin();
                    dependsOn = this.provider.Spawn(this.data, dependsOn);
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
                // Complete all previous systems to be sure that
                // renderingOnSceneApplyState and renderingOnSceneUpdate set up has been complete
                dependsOn.Complete();
                // Update views logic
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] ApplyState Views");
                    marker.Begin();
                    for (uint i = 0u; i < this.data->renderingOnSceneApplyState.Count; ++i) {
                        var entId = this.data->renderingOnSceneApplyState.sparseSet.dense[in allocator, i];
                        if (this.data->renderingOnSceneApplyStateCulling[in allocator, entId] == true) continue;
                        var idx = this.data->renderingOnSceneEntToRenderIndex.ReadValue(in allocator, entId);
                        ref var entData = ref *(this.data->renderingOnSceneEnts.Ptr + idx);
                        var view = this.data->renderingOnScene[in allocator, idx];
                        var ent = entData.element;
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
                        if (this.data->renderingOnSceneUpdateCulling[in allocator, entId] == true) continue;
                        var idx = this.data->renderingOnSceneEntToRenderIndex.ReadValue(in allocator, entId);
                        ref var entData = ref *(this.data->renderingOnSceneEnts.Ptr + idx);
                        var view = this.data->renderingOnScene[in allocator, idx];
                        var ent = entData.element;
                        if (view.prefabInfo->typeInfo.HasUpdate == true || view.prefabInfo->HasUpdateModules == true) {
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
                this.data->toAssign.Clear();
                this.data->toChange.Clear();
                this.data->toAdd.Clear();
                this.data->toRemove.Clear();
                this.data->dirty.Clear();
            }
            return dependsOn;

        }

    }

}