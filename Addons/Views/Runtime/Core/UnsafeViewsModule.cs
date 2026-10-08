using ME.BECS.Addons.Views.Runtime.Providers;
using Unity.Collections;

namespace ME.BECS.Views {

    using BURST = Unity.Burst.BurstCompileAttribute;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Collections.LowLevel.Unsafe;
    using ME.BECS.Jobs;
    using Unity.Jobs;
    using static Cuts;
    using System.Runtime.InteropServices;
    using ME.BECS.NativeCollections;

    /// <summary>
    /// Defines the operations required by view provider.
    /// </summary>
    public unsafe interface IViewProvider<TEntityView> where TEntityView : IView {

        /// <summary>
        /// Initializes i view provider state from the supplied context.
        /// </summary>
        void Initialize(uint providerId, World viewsWorld, ViewsModuleProperties properties);
        /// <summary>
        /// Creates or reuses a presentation instance for the requested entity.
        /// </summary>
        JobHandle Spawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn);
        /// <summary>
        /// Removes an active presentation instance and returns it to its provider.
        /// </summary>
        JobHandle Despawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn);
        /// <summary>
        /// Apply Spawn/Despawn commands
        /// </summary>
        JobHandle Commit(safe_ptr<ViewsModuleData> data, JobHandle dependsOn, float dt);
        /// <summary>
        /// Releases the resources owned by this i view provider instance.
        /// </summary>
        void Dispose(safe_ptr<State> state, safe_ptr<ViewsModuleData> data);
        /// <summary>
        /// Applies logic state during the parallel phase of view processing.
        /// </summary>
        void ApplyStateParallel(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData);
        /// <summary>
        /// Applies the current logic state to the presentation instance.
        /// </summary>
        void ApplyState(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData);
        /// <summary>
        /// Updates i view provider using the current inputs and execution context.
        /// </summary>
        void OnUpdate(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData, float dt);
        /// <summary>
        /// Updates presentation during the parallel phase of view processing.
        /// </summary>
        void OnUpdateParallel(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in ViewData viewData, float dt);

        /// <summary>
        /// Loads the registered data required by this operation.
        /// </summary>
        public void Load(safe_ptr<ViewsModuleData> viewsModuleData, BECS.ObjectReferenceRegistryData data);
        /// <summary>
        /// Registers the supplied instance or type for subsequent lookup.
        /// </summary>
        public ViewSource Register(safe_ptr<ViewsModuleData> viewsModuleData, TEntityView prefab, uint prefabId = 0u, bool checkPrefab = true, bool sceneSource = false);

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        void Query(ref QueryBuilder queryBuilder);

        /// <summary>
        /// Returns view by entity.
        /// </summary>
        IView GetViewByEntity(safe_ptr<ViewsModuleData> data, in Ent entity);

    }

    /// <summary>
    /// Defines the operations required by view provider root.
    /// </summary>
    public interface IViewProviderRoot {

        /// <summary>
        /// Returns root.
        /// </summary>
        public UnityEngine.Transform GetRoot();

    }

    /// <summary>
    /// Configures views module behavior and storage.
    /// </summary>
    [System.Serializable]
    public struct ViewsModuleProperties {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static ViewsModuleProperties Default => new ViewsModuleProperties() {
            instancesRegistryCapacity = 10u,
            renderingObjectsCapacity = 100u,
            spawnLimitPerFrame = 5u,
            viewsGameObjects = true,
            viewsDrawMeshes = true,
            interpolateState = true,
            interpolateNetwork = true,
            useUnityHierarchy = false,
        };

        /// <summary>
        /// How many unique prefabs will be registered.
        /// </summary>
        [UnityEngine.Tooltip("How many unique prefabs will be registered.")]
        public uint instancesRegistryCapacity;
        /// <summary>
        /// How many instances will be drawing on the scene at once.
        /// </summary>
        [UnityEngine.Tooltip("How many instances will be drawing on the scene at once.")]
        public uint renderingObjectsCapacity;

        /// <summary>
        /// Limits spawn view instances per frame. 0 = unlimited.
        /// </summary>
        [UnityEngine.Tooltip("Limits spawn view instances per frame. 0 = unlimited.")]
        public uint spawnLimitPerFrame;

        /// <summary>
        /// Enable GameObjects Provider.
        /// </summary>
        [UnityEngine.Tooltip("Enable GameObjects Provider.")]
        public bool viewsGameObjects;
        /// <summary>
        /// Enable DrawMeshes Provider.
        /// </summary>
        [UnityEngine.Tooltip("Enable DrawMeshes Provider.")]
        public bool viewsDrawMeshes;
        /// <summary>
        /// Enable Particles Provider.
        /// </summary>
        [UnityEngine.Tooltip("Enable Particles Provider.")]
        public bool viewsParticles;

        /// <summary>
        /// Use automatic state interpolation between start and end of the frame. Useful with Network Module only.
        /// </summary>
        [UnityEngine.Tooltip("Use automatic state interpolation between start and end of the frame. Useful with Network Module only.")]
        public bool interpolateState;

        /// <summary>
        /// Interpolate view according to network input delay.
        /// </summary>
        [UnityEngine.Tooltip("Interpolate view according to network input delay")]
        public bool interpolateNetwork;

        /// <summary>
        /// Use Unity hierarchy for objects. All transforms on scene will be added into their parents.
        /// </summary>
        [UnityEngine.Tooltip("Use Unity hierarchy for objects. All transforms on scene will be added into their parents.")]
        public bool useUnityHierarchy;
        
    }

    internal class AssetOp {

        public UnityEngine.AddressableAssets.AssetReference assetReference;
        public UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle handle;

        public AssetOp(UnityEngine.AddressableAssets.AssetReference assetReference) {
            this.assetReference = assetReference;
            this.handle = default;
        }
            
        public bool IsLoading() {
            return this.handle.IsValid();
        }

        public void StartLoading() {
            if (this.assetReference.OperationHandle.IsValid() == true) {
                this.handle = this.assetReference.OperationHandle;
            } else {
                this.handle = this.assetReference.LoadAssetAsync<UnityEngine.GameObject>();
            }
        }

        public bool IsLoaded() {
            return this.handle.IsValid() == true && this.handle.IsDone == true;
        }

        public void Dispose() {
            if (this.handle.IsValid() == true) this.assetReference.ReleaseAsset();
        }

    }

    /// <summary>
    /// Defines the operations required by view.
    /// </summary>
    public interface IView {

        /// <summary>
        /// Returns view data.
        /// </summary>
        public ViewData GetViewData();

        /// <summary>
        /// Invokes initialize.
        /// </summary>
        void DoInitialize();
        /// <summary>
        /// Invokes de initialize.
        /// </summary>
        void DoDeInitialize();
        /// <summary>
        /// Invokes enable from pool.
        /// </summary>
        void DoEnableFromPool(in ViewData viewData);
        /// <summary>
        /// Invokes disable to pool.
        /// </summary>
        void DoDisableToPool();
        /// <summary>
        /// Invokes apply state.
        /// </summary>
        void DoApplyState(in ViewData viewData);
        /// <summary>
        /// Invokes on update.
        /// </summary>
        void DoOnUpdate(in ViewData viewData, float dt);

    }
    
    /// <summary>
    /// Registers and resolves source entries.
    /// </summary>
    public struct SourceRegistry {

        /// <summary>
        /// Stores a info record used by <c>SourceRegistry</c>.
        /// </summary>
        [System.Serializable]
        public struct Info {

            /// <summary>
            /// Native pointer or typed storage accessor for prefab.
            /// </summary>
            public System.IntPtr prefabPtr;
            /// <summary>
            /// Registered prefab identifier used to resolve a view source.
            /// </summary>
            public uint prefabId;
            /// <summary>
            /// Type info used by <c>SourceRegistry.Info</c>.
            /// </summary>
            public ViewTypeInfo typeInfo;
            /// <summary>
            /// Whether scene source behavior or state is selected.
            /// </summary>
            public bbool sceneSource;
            /// <summary>
            /// Indicates is loaded.
            /// </summary>
            public bbool isLoaded;
            /// <summary>
            /// Loaded tick used by <c>SourceRegistry.Info</c>.
            /// </summary>
            public ulong loadedTick;
            /// <summary>
            /// Pool count for the associated storage.
            /// </summary>
            public uint poolCount;
            /// <summary>
            /// Supported providers used by <c>SourceRegistry.Info</c>.
            /// </summary>
            [ViewsProviderMask]
            public uint supportedProviders;
            
            /// <summary>
            /// Bit flags controlling the associated behavior.
            /// </summary>
            public TypeFlags flags;

            /// <summary>
            /// Indicates has apply state modules.
            /// </summary>
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

            /// <summary>
            /// Indicates has apply state parallel modules.
            /// </summary>
            public bool HasApplyStateParallelModules {
                get => (this.flags & TypeFlags.ApplyStateParallel) != 0;
                set {
                    if (value == true) {
                        this.flags |= TypeFlags.ApplyStateParallel;
                    } else {
                        this.flags &= ~TypeFlags.ApplyStateParallel;
                    }
                }
            }

            /// <summary>
            /// Indicates has update modules.
            /// </summary>
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

            /// <summary>
            /// Indicates has update parallel modules.
            /// </summary>
            public bool HasUpdateParallelModules {
                get => (this.flags & TypeFlags.UpdateParallel) != 0;
                set {
                    if (value == true) {
                        this.flags |= TypeFlags.UpdateParallel;
                    } else {
                        this.flags &= ~TypeFlags.UpdateParallel;
                    }
                }
            }

            /// <summary>
            /// Indicates has initialize modules.
            /// </summary>
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

            /// <summary>
            /// Indicates has de initialize modules.
            /// </summary>
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

            /// <summary>
            /// Indicates has enable from pool modules.
            /// </summary>
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

            /// <summary>
            /// Indicates has disable to pool modules.
            /// </summary>
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

        /// <summary>
        /// Defines info ref state and operations for <c>SourceRegistry</c>.
        /// </summary>
        public struct InfoRef {

            /// <summary>
            /// Metadata describing the associated entry.
            /// </summary>
            public safe_ptr<Info> info;

            /// <summary>
            /// Initializes <c>InfoRef</c> from the supplied info.
            /// </summary>
            public InfoRef(Info info) {
                this.info = _make(info);
            }

            /// <summary>
            /// Releases the resources owned by this info ref instance.
            /// </summary>
            public void Dispose() {
                _free(this.info);
                this = default;
            }

        }
        
    }

    /// <summary>
    /// Maintains sparse entity membership for a view callback phase.
    /// </summary>
    public struct RenderingSparseList {

        /// <summary>
        /// Sparse set used by <c>RenderingSparseList</c>.
        /// </summary>
        public SparseSet sparseSet;
        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count;

        /// <summary>
        /// Initializes <c>RenderingSparseList</c> from the supplied allocator, capacity.
        /// </summary>
        public RenderingSparseList(ref MemoryAllocator allocator, uint capacity) {
            this.sparseSet = new SparseSet(ref allocator, capacity);
            this.Count = 0u;
        }

        /// <summary>
        /// Adds the supplied entry to rendering sparse list.
        /// </summary>
        public void Add(ref MemoryAllocator allocator, uint index) {
            this.sparseSet.Set(ref allocator, index, out _);
            ++this.Count;
        }

        /// <summary>
        /// Removes the specified entry from rendering sparse list.
        /// </summary>
        public bool Remove(in MemoryAllocator allocator, uint idx) {
            if (this.sparseSet.Remove(in allocator, idx, out var fromIndex, out var toIndex) == true) {
                --this.Count;
                return true;
            }

            return false;
        }

    }

    /// <summary>
    /// Stores spawn instance info for the associated views API.
    /// </summary>
    public struct SpawnInstanceInfo {

        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent;
        /// <summary>
        /// Local data used by <c>SpawnInstanceInfo</c>.
        /// </summary>
        public Ent localData;
        /// <summary>
        /// Prefab info used by <c>SpawnInstanceInfo</c>.
        /// </summary>
        public SourceRegistry.InfoRef prefabInfo;

    }

    /// <summary>
    /// Stores scene instance info for the associated views API.
    /// </summary>
    public struct SceneInstanceInfo {

        /// <summary>
        /// Object represented by this entry.
        /// </summary>
        public System.IntPtr obj;
        /// <summary>
        /// Prefab info used by <c>SceneInstanceInfo</c>.
        /// </summary>
        public readonly safe_ptr<SourceRegistry.Info> prefabInfo;
        /// <summary>
        /// Local data used by <c>SceneInstanceInfo</c>.
        /// </summary>
        public Ent localData;
        /// <summary>
        /// Identifier used to distinguish this entry from other entries.
        /// </summary>
        public uint uniqueId;
        /// <summary>
        /// Index of this entry within its containing storage.
        /// </summary>
        public uint index;

        /// <summary>
        /// Initializes <c>SceneInstanceInfo</c> from the supplied obj, prefab info, unique ID, local data.
        /// </summary>
        public SceneInstanceInfo(System.IntPtr obj, safe_ptr<SourceRegistry.Info> prefabInfo, uint uniqueId, Ent localData) {
            this = default;
            this.obj = obj;
            this.prefabInfo = prefabInfo;
            this.localData = localData;
            this.uniqueId = uniqueId;
            this.index = 0u;
        }

    }

    /// <summary>
    /// Defines begin frame state state and operations.
    /// </summary>
    public struct BeginFrameState {

        /// <summary>
        /// State accessed by the containing operation.
        /// </summary>
        public safe_ptr<State> state;
        /// <summary>
        /// Tick time in the time units used by the containing API.
        /// </summary>
        public float tickTime;
        /// <summary>
        /// Time since start used by <c>BeginFrameState</c>.
        /// </summary>
        public double timeSinceStart;

    }

    /// <summary>
    /// Owns the collections and state shared by a views module.
    /// </summary>
    public struct ViewsModuleData {

        /// <summary>
        /// Stores entity data for <c>ViewsModuleData</c>.
        /// </summary>
        public struct EntityData {

            /// <summary>
            /// Element used by <c>ViewsModuleData.EntityData</c>.
            /// </summary>
            public Ent element;
            /// <summary>
            /// Local data used by <c>ViewsModuleData.EntityData</c>.
            /// </summary>
            public Ent localData;
            /// <summary>
            /// Initial version used by <c>ViewsModuleData.EntityData</c>.
            /// </summary>
            public uint initialVersion;
            /// <summary>
            /// Change version used to detect stale state.
            /// </summary>
            public uint version;
            /// <summary>
            /// Version parallel used by <c>ViewsModuleData.EntityData</c>.
            /// </summary>
            public uint versionParallel;
            /// <summary>
            /// Player delay expressed in milliseconds.
            /// </summary>
            public ulong playerDelayMs;

            /// <summary>
            /// View data used by <c>ViewsModuleData.EntityData</c>.
            /// </summary>
            public ViewData ViewData => new ViewData(this.element, this.localData);

        }
        
        /// <summary>
        /// Provider id used to locate the associated entry.
        /// </summary>
        public uint providerId;
        /// <summary>
        /// Registered prefab identifier used to resolve a view source.
        /// </summary>
        public uint prefabId;
        /// <summary>
        /// Prefab id to info used by <c>ViewsModuleData</c>.
        /// </summary>
        public UIntDictionary<SourceRegistry.InfoRef> prefabIdToInfo;
        /// <summary>
        /// Instance id to prefab id used to locate the associated entry.
        /// </summary>
        public UIntDictionary<uint> instanceIdToPrefabId;

        /// <summary>
        /// Rendering on scene bits used by <c>ViewsModuleData</c>.
        /// </summary>
        public TempBitArray renderingOnSceneBits;
        /// <summary>
        /// Rendering on scene ent to render index used to locate the associated entry.
        /// </summary>
        public UIntDictionary<uint> renderingOnSceneEntToRenderIndex;
        /// <summary>
        /// Rendering on scene render index to ent used by <c>ViewsModuleData</c>.
        /// </summary>
        public UIntDictionary<uint> renderingOnSceneRenderIndexToEnt;
        /// <summary>
        /// Rendering on scene ent to prefab id used to locate the associated entry.
        /// </summary>
        public MemArray<uint> renderingOnSceneEntToPrefabId;
        /// <summary>
        /// Pending entries to assign during the next processing phase.
        /// </summary>
        public UnsafeParallelHashMap<uint, uint> toAssign;
        /// <summary>
        /// Pending entries to change during the next processing phase.
        /// </summary>
        public UnsafeParallelHashMap<uint, bool> toChange;
        /// <summary>
        /// Pending entries to remove during the next processing phase.
        /// </summary>
        public UnsafeParallelHashMap<uint, bool> toRemove;
        /// <summary>
        /// Pending entries to add during the next processing phase.
        /// </summary>
        public UnsafeParallelHashMap<uint, bool> toAdd;
        /// <summary>
        /// Whether dirty behavior or state is selected.
        /// </summary>
        public UnsafeList<byte> dirty;
        /// <summary>
        /// Entity handles with active scene rendering entries.
        /// </summary>
        public UnsafeList<EntityData> renderingOnSceneEnts;
        /// <summary>
        /// Rendering on scene used by <c>ViewsModuleData</c>.
        /// </summary>
        public List<SceneInstanceInfo> renderingOnScene;
        /// <summary>
        /// Gc handles used by <c>ViewsModuleData</c>.
        /// </summary>
        public List<System.Runtime.InteropServices.GCHandle> gcHandles;
        
        /// <summary>
        /// Rendering on scene apply state used by <c>ViewsModuleData</c>.
        /// </summary>
        public RenderingSparseList renderingOnSceneApplyState;
        /// <summary>
        /// Rendering on scene apply state parallel used by <c>ViewsModuleData</c>.
        /// </summary>
        public RenderingSparseList renderingOnSceneApplyStateParallel;
        /// <summary>
        /// Rendering on scene update used by <c>ViewsModuleData</c>.
        /// </summary>
        public RenderingSparseList renderingOnSceneUpdate;
        /// <summary>
        /// Rendering on scene update parallel used by <c>ViewsModuleData</c>.
        /// </summary>
        public RenderingSparseList renderingOnSceneUpdateParallel;
        /// <summary>
        /// Apply state counter used by <c>ViewsModuleData</c>.
        /// </summary>
        public safe_ptr<DeferJobCounter> applyStateCounter;
        /// <summary>
        /// Apply state parallel counter used by <c>ViewsModuleData</c>.
        /// </summary>
        public safe_ptr<DeferJobCounter> applyStateParallelCounter;
        /// <summary>
        /// Update counter used by <c>ViewsModuleData</c>.
        /// </summary>
        public safe_ptr<DeferJobCounter> updateCounter;
        /// <summary>
        /// Update parallel counter used by <c>ViewsModuleData</c>.
        /// </summary>
        public safe_ptr<DeferJobCounter> updateParallelCounter;

        /// <summary>
        /// Rendering on scene apply state culling used by <c>ViewsModuleData</c>.
        /// </summary>
        public MemArray<ibool> renderingOnSceneApplyStateCulling;
        /// <summary>
        /// Rendering on scene apply state parallel culling used by <c>ViewsModuleData</c>.
        /// </summary>
        public MemArray<ibool> renderingOnSceneApplyStateParallelCulling;
        /// <summary>
        /// Rendering on scene update culling used by <c>ViewsModuleData</c>.
        /// </summary>
        public MemArray<ibool> renderingOnSceneUpdateCulling;
        /// <summary>
        /// Rendering on scene update parallel culling used by <c>ViewsModuleData</c>.
        /// </summary>
        public MemArray<ibool> renderingOnSceneUpdateParallelCulling;

        /// <summary>
        /// Rendering on scene count for the associated storage.
        /// </summary>
        public uint renderingOnSceneCount;
        /// <summary>
        /// Pending entries to remove temp during the next processing phase.
        /// </summary>
        public UnsafeList<SceneInstanceInfo> toRemoveTemp;
        /// <summary>
        /// Pending entries to add temp during the next processing phase.
        /// </summary>
        public UnsafeList<SpawnInstanceInfo> toAddTemp;
        /// <summary>
        /// Loading requests used by <c>ViewsModuleData</c>.
        /// </summary>
        public UnsafeHashSet<uint> loadingRequests;

        /// <summary>
        /// Configuration values used by this operation.
        /// </summary>
        public ViewsModuleProperties properties;
        
        /// <summary>
        /// World associated with this connection.
        /// </summary>
        public World connectedWorld;
        /// <summary>
        /// World containing the presentation-side entities.
        /// </summary>
        public World viewsWorld;
        /// <summary>
        /// Begin frame state used by <c>ViewsModuleData</c>.
        /// </summary>
        public safe_ptr<BeginFrameState> beginFrameState;

        /// <summary>
        /// Camera used by <c>ViewsModuleData</c>.
        /// </summary>
        public Ent camera;
        /// <summary>
        /// Culling snapshot used by <c>ViewsModuleData</c>.
        /// </summary>
        public CameraUtils.CullingSnapshot cullingSnapshot;
        /// <summary>
        /// Interpolation factor controlling the associated calculation.
        /// </summary>
        public float interpolationFactor;
        
        /// <summary>
        /// Creates <c>ViewsModuleData</c> using the supplied creation arguments.
        /// </summary>
        public static ViewsModuleData Create(ref MemoryAllocator allocator, ushort worldId, uint entitiesCapacity, ViewsModuleProperties properties) {
            
            var allocatorPersistent = WorldsPersistentAllocator.allocatorPersistent.Get(worldId).Allocator.ToAllocator;
            return new ViewsModuleData() {
                prefabId = 0u,
                properties = properties,
                beginFrameState = _make(new BeginFrameState()),
                applyStateCounter = _make<DeferJobCounter>(default),
                applyStateParallelCounter = _make<DeferJobCounter>(default),
                updateCounter = _make<DeferJobCounter>(default),
                updateParallelCounter = _make<DeferJobCounter>(default),
                prefabIdToInfo = new UIntDictionary<SourceRegistry.InfoRef>(ref allocator, properties.instancesRegistryCapacity),
                instanceIdToPrefabId = new UIntDictionary<uint>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneCount = 0u,
                renderingOnScene = new List<SceneInstanceInfo>(ref allocator, properties.renderingObjectsCapacity),
                gcHandles = new List<GCHandle>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneApplyState = new RenderingSparseList(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneApplyStateParallel = new RenderingSparseList(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneUpdate = new RenderingSparseList(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneUpdateParallel = new RenderingSparseList(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneApplyStateCulling = new MemArray<ibool>(ref allocator, entitiesCapacity),
                renderingOnSceneApplyStateParallelCulling = new MemArray<ibool>(ref allocator, entitiesCapacity),
                renderingOnSceneUpdateCulling = new MemArray<ibool>(ref allocator, entitiesCapacity),
                renderingOnSceneUpdateParallelCulling = new MemArray<ibool>(ref allocator, entitiesCapacity),
                renderingOnSceneEnts = new UnsafeList<EntityData>((int)properties.renderingObjectsCapacity, allocatorPersistent),
                renderingOnSceneBits = new TempBitArray(properties.renderingObjectsCapacity, allocator: allocatorPersistent),
                renderingOnSceneEntToRenderIndex = new UIntDictionary<uint>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneRenderIndexToEnt = new UIntDictionary<uint>(ref allocator, properties.renderingObjectsCapacity),
                renderingOnSceneEntToPrefabId = new MemArray<uint>(ref allocator, entitiesCapacity),
                toAssign = new UnsafeParallelHashMap<uint, uint>((int)properties.renderingObjectsCapacity, allocatorPersistent),
                toChange = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, allocatorPersistent),
                toRemove = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, allocatorPersistent),
                toAdd = new UnsafeParallelHashMap<uint, bool>((int)properties.renderingObjectsCapacity, allocatorPersistent),
                dirty = new UnsafeList<byte>((int)properties.renderingObjectsCapacity, allocatorPersistent),
                toRemoveTemp = new UnsafeList<SceneInstanceInfo>((int)properties.renderingObjectsCapacity, allocatorPersistent),
                toAddTemp = new UnsafeList<SpawnInstanceInfo>((int)properties.renderingObjectsCapacity, allocatorPersistent),
                loadingRequests = new UnsafeHashSet<uint>((int)properties.renderingObjectsCapacity, allocatorPersistent),
            };

        }

        /// <summary>
        /// Sets camera.
        /// </summary>
        public void SetCamera(in CameraAspect camera) {
            this.camera = camera.ent;
        }

        /// <summary>
        /// Releases the resources owned by this views module data instance.
        /// </summary>
        public void Dispose(safe_ptr<State> state) {

            var e = this.prefabIdToInfo.GetEnumerator(this.viewsWorld);
            while (e.MoveNext() == true) {
                var kv = e.Current;
                kv.value.Dispose();
            }

            for (uint i = 0u; i < this.gcHandles.Count; ++i) {
                var handle = this.gcHandles[state, i];
                if (handle.IsAllocated == true) {
                    if (handle.Target is AssetOp assetOp) {
                        assetOp.Dispose();
                    }
                    handle.Free();
                }
            }
            
            _free(ref this.beginFrameState);
            _free(ref this.applyStateCounter);
            _free(ref this.applyStateParallelCounter);
            _free(ref this.updateCounter);
            _free(ref this.updateParallelCounter);
            if (this.renderingOnSceneEnts.IsCreated == true) this.renderingOnSceneEnts.Dispose();
            if (this.renderingOnSceneBits.IsCreated == true) this.renderingOnSceneBits.Dispose();
            if (this.toRemove.IsCreated == true) this.toRemove.Dispose();
            if (this.toAdd.IsCreated == true) this.toAdd.Dispose();
            if (this.dirty.IsCreated == true) this.dirty.Dispose();
            if (this.toAssign.IsCreated == true) this.toAssign.Dispose();
            if (this.toChange.IsCreated == true) this.toChange.Dispose();
            if (this.toRemoveTemp.IsCreated == true) this.toRemoveTemp.Dispose();
            if (this.toAddTemp.IsCreated == true) this.toAddTemp.Dispose();
            if (this.loadingRequests.IsCreated == true) this.loadingRequests.Dispose();

            this = default;

        }

    }

    /// <summary>
    /// Provides lifecycle integration for the unsafe views feature.
    /// </summary>
    public unsafe struct UnsafeViewsModule {

        /// <summary>
        /// Stores provider info for <c>UnsafeViewsModule</c>.
        /// </summary>
        public struct ProviderInfo : IIsCreated {

            /// <summary>
            /// Whether the backing state has been initialized.
            /// </summary>
            public bool IsCreated { get; set; }
            /// <summary>
            /// Type id used to locate the associated entry.
            /// </summary>
            public uint typeId;

        }
        
        internal static readonly Unity.Burst.SharedStatic<UnsafeList<ProviderInfo>> registeredProviders = Unity.Burst.SharedStatic<UnsafeList<ProviderInfo>>.GetOrCreatePartiallyUnsafeWithHashCode<UnsafeViewsModule>(TAlign<UnsafeList<ProviderInfo>>.align, 20021);
        
        /// <summary>
        /// Registers provider type.
        /// </summary>
        public static void RegisterProviderType<T>(uint providerId) where T : unmanaged, IComponent {

            if (registeredProviders.Data.IsCreated == false) {
                registeredProviders.Data = new UnsafeList<ProviderInfo>((int)providerId + 1, Constants.ALLOCATOR_DOMAIN);
            }
            if (providerId >= registeredProviders.Data.Length) {
                registeredProviders.Data.Resize((int)providerId + 1, NativeArrayOptions.ClearMemory);
            }

            ref var item = ref *(registeredProviders.Data.Ptr + providerId);
            item.IsCreated = true;
            item.typeId = StaticTypes<T>.typeId;

        }
        
        /// <summary>
        /// Instantiates view.
        /// </summary>
        [INLINE(256)]
        public static bool InstantiateView(in Ent ent, in ViewSource viewSource) {

            if (viewSource.IsValid == false) return false;
            
            if (ent.TryRead(out ViewComponent previous) == true && previous.source.providerId != viewSource.providerId &&
                previous.source.providerId < registeredProviders.Data.Length) {
                var previousProvider = registeredProviders.Data[(int)previous.source.providerId];
                if (previousProvider.IsCreated == true) ent.Remove(previousProvider.typeId);
            }
            ent.Remove<AssignViewComponent>();

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

        /// <summary>
        /// Requests transfer of an existing view between logic entities.
        /// </summary>
        [INLINE(256)]
        public static bool AssignView(in Ent ent, in Ent sourceEnt) {

            if (ent.worldId != sourceEnt.worldId) return false;
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
                    sourceEnt.Remove(item.typeId);
                    ent.Set(item.typeId, null);
                }
                return true;
                
            }

            return false;

        }

        /// <summary>
        /// Clears the view request; a subsequent views update performs the removal.
        /// </summary>
        [INLINE(256)]
        public static void DestroyView(in Ent ent) {

            ent.Remove<IsViewRequested>();
            
        }

    }

    /// <summary>
    /// Provides lifecycle integration for the unsafe views feature.
    /// </summary>
    [BURST]
    public unsafe struct UnsafeViewsModule<TEntityView> where TEntityView : IView {

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public safe_ptr<ViewsModuleData> data;
        /// <summary>
        /// Provider responsible for the associated presentation or service.
        /// </summary>
        public ClassPtr<IViewProvider<TEntityView>> provider;
        
        /// <summary>
        /// Creates <c>UnsafeViewsModule&lt;TEntityView&gt;</c> using the supplied creation arguments.
        /// </summary>
        public static UnsafeViewsModule<TEntityView> Create<T>(uint providerId, ref World connectedWorld, T provider, uint entitiesCapacity, ViewsModuleProperties properties) where T : IViewProvider<TEntityView> {
            
            var viewsWorldProperties = WorldProperties.Default;
            viewsWorldProperties.allocatorProperties.sizeInBytesCapacity = (uint)MemoryAllocator.MIN_ZONE_SIZE; // Use min allocator size
            viewsWorldProperties.name = ViewsModule.providerInfos[providerId].editorName;
            viewsWorldProperties.stateProperties.mode = WorldMode.Visual;
            viewsWorldProperties.stateProperties.entitiesCapacity = entitiesCapacity;

            var viewsWorld = World.Create(viewsWorldProperties, switchContext: false);
            var prevContext = Context.world;
            Context.Switch(in viewsWorld);
            provider.Initialize(providerId, viewsWorld, properties);

            var module = new UnsafeViewsModule<TEntityView> {
                data = _make(ViewsModuleData.Create(ref viewsWorld.state.ptr->allocator, viewsWorld.id, entitiesCapacity, properties)),
                provider = new ClassPtr<IViewProvider<TEntityView>>(provider),
            };
            module.data.ptr->providerId = providerId;
            module.data.ptr->connectedWorld = connectedWorld;
            module.data.ptr->viewsWorld = viewsWorld;
            WorldStaticCallbacks.RaiseCallback(ref *module.data.ptr);
            module.provider.Value.Load(module.data, ObjectReferenceRegistry.data);
            Context.Switch(in prevContext);
            return module;

        }

        /// <summary>
        /// Releases the resources owned by this unsafe views module instance.
        /// </summary>
        public void Dispose() {

            var world = this.data.ptr->viewsWorld;
            this.provider.Value.Dispose(this.data.ptr->viewsWorld.state, this.data);
            this.provider.Dispose();
            this.data.ptr->Dispose(this.data.ptr->viewsWorld.state);
            _free(this.data);
            world.Dispose();
            this = default;

        }

        /// <summary>
        /// Sets camera.
        /// </summary>
        public void SetCamera(in CameraAspect camera) {

            this.data.ptr->SetCamera(in camera);

        }
        
        /// <summary>
        /// Registers view source.
        /// </summary>
        public ViewSource RegisterViewSource(TEntityView prefab) {

            return this.provider.Value.Register(this.data, prefab);

        }

        internal ViewSource RegisterViewSource(TEntityView prefab, bool checkPrefab, bool sceneSource = false) {

            return this.provider.Value.Register(this.data, prefab, checkPrefab: checkPrefab, sceneSource: sceneSource);

        }

        /// <summary>
        /// Updates unsafe views module using the current inputs and execution context.
        /// </summary>
        public JobHandle Update(float dt) {
            return this.Update(dt, default);
        }

        /// <summary>
        /// Updates unsafe views module using the current inputs and execution context.
        /// </summary>
        public JobHandle Update(float dt, JobHandle dependsOn) {

            E.IS_CREATED(this.data.ptr->connectedWorld);
            E.IS_CREATED(this.data.ptr->viewsWorld);

            WorldStaticCallbacks.RaiseCallback(ref *this.data.ptr, 1);

            var mode = this.data.ptr->connectedWorld.state.ptr->Mode;
            ref var allocator = ref this.data.ptr->viewsWorld.state.ptr->allocator;
            dependsOn = new Jobs.PrepareJob() {
                worldId = this.data.ptr->viewsWorld.id,
                viewsModuleData = this.data,
                state = this.data.ptr->viewsWorld.state,
                connectedWorld = this.data.ptr->connectedWorld,
            }.Schedule(dependsOn);
            
            JobHandle toRemoveEntitiesJob;
            {
                // Update views
                {
                    // Assign views first
                    var query = API.Query(in this.data.ptr->connectedWorld, dependsOn).AsReadonly();
                    this.provider.Value.Query(ref query);
                    var toAssignJob = query.Schedule<Jobs.JobAssignViews, AssignViewComponent>(new Jobs.JobAssignViews() {
                        viewsWorld = this.data.ptr->viewsWorld,
                        viewsModuleData = this.data,
                        registeredProviders = UnsafeViewsModule.registeredProviders.Data,
                        toAssign = this.data.ptr->toAssign.AsParallelWriter(),
                    });
                    dependsOn = new Jobs.JobApplyViewAssignments() {
                        data = this.data,
                    }.Schedule(toAssignJob);
                }
                JobHandle toRemoveJob;
                {
                    // DestroyView() case: Remove views from the scene which don't have ViewComponent, but contained in renderingOnSceneBits (DestroyView called)
                    var query = API.Query(in this.data.ptr->connectedWorld, dependsOn).AsReadonly().Without<IsViewRequested>();
                    this.provider.Value.Query(ref query);
                    toRemoveJob = query.AsParallel().Schedule<Jobs.JobRemoveFromScene, ViewComponent>(new Jobs.JobRemoveFromScene() {
                        viewsModuleData = this.data,
                        toRemove = this.data.ptr->toRemove.AsParallelWriter(),
                        registeredProviders = UnsafeViewsModule.registeredProviders.Data,
                    });
                }
                JobHandle toAddJob;
                {
                    // InstantiateView() case: Add views to the scene which have ViewComponent, but not contained in renderingOnSceneBits
                    var query = API.Query(in this.data.ptr->connectedWorld, dependsOn).AsReadonly().WithAspect<Transforms.TransformAspect>();
                    this.provider.Value.Query(ref query);
                    toAddJob = query.AsParallel().Schedule<Jobs.JobAddToScene, IsViewRequested>(new Jobs.JobAddToScene() {
                        state = this.data.ptr->viewsWorld.state,
                        viewsModuleData = this.data,
                        toAdd = this.data.ptr->toAdd.AsParallelWriter(),
                        toRemove = this.data.ptr->toRemove.AsParallelWriter(),
                    });
                }
                {
                    var handle = JobHandle.CombineDependencies(toRemoveJob, toAddJob);
                    // Add entities which has been destroyed, but contained in renderingOnScene
                    toRemoveEntitiesJob = new Jobs.JobRemoveEntitiesFromScene() {
                        world = this.data.ptr->connectedWorld,
                        viewsModuleData = this.data,
                        toChange = this.data.ptr->toChange.AsParallelWriter(),
                        toRemove = this.data.ptr->toRemove.AsParallelWriter(),
                    }.Schedule(this.data.ptr->renderingOnSceneEnts.Length, JobUtils.GetScheduleBatchCount(this.data.ptr->renderingOnSceneEnts.Length), handle);
                }
            }

            dependsOn = toRemoveEntitiesJob;

            {
                // Update views
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Update Remove Lists Schedule");
                    marker.Begin();
                    dependsOn = new Jobs.JobDespawnViews() {
                        viewsWorld = this.data.ptr->viewsWorld,
                        data = this.data,
                    }.ScheduleSingle(dependsOn);
                    marker.End();
                }

                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Update Add Lists Schedule");
                    marker.Begin();
                    dependsOn = new Jobs.JobSpawnViews() {
                        connectedWorld = this.data.ptr->connectedWorld,
                        viewsWorld = this.data.ptr->viewsWorld,
                        data = this.data,
                    }.ScheduleSingle(dependsOn);
                    marker.End();
                }

            }
            
            if (this.data.ptr->camera.IsAlive() == true) { // Update culling

                dependsOn = new Jobs.PrepareCullingJob() {
                    viewsModuleData = this.data,
                }.Schedule(dependsOn);

                dependsOn = new Jobs.UpdateCullingJob() {
                    state = this.data.ptr->viewsWorld.state,
                    viewsModuleData = this.data,
                }.Schedule((int*)&this.data.ptr->renderingOnSceneCount, 64, dependsOn);

            }

            JobUtils.RunScheduled();
            
            {
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Provider::Despawn");
                    marker.Begin();
                    dependsOn = this.provider.Value.Despawn(this.data, dependsOn);
                    marker.End();
                }
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Provider::Spawn");
                    marker.Begin();
                    dependsOn = this.provider.Value.Spawn(this.data, dependsOn);
                    marker.End();
                }
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Provider::Commit");
                    marker.Begin();
                    dependsOn = this.provider.Value.Commit(this.data, dependsOn, dt);
                    marker.End();
                }
            }
            JobUtils.RunScheduled();

            {
                var provider = this.provider.Value;
                {
                    // Update views logic in parallel mode
                    var dependsOnApplyState = new Jobs.ApplyStateParallelJob<TEntityView>() {
                        data = this.data,
                        allocator = allocator,
                        provider = this.provider,
                    }.Schedule(&this.data.ptr->applyStateParallelCounter.ptr->count, 4, dependsOn);
                    var dependsOnUpdate = new Jobs.UpdateParallelJob<TEntityView>() {
                        data = this.data,
                        allocator = allocator,
                        provider = this.provider,
                        dt = dt,
                    }.Schedule(&this.data.ptr->updateParallelCounter.ptr->count, 4, dependsOn);
                    dependsOn = JobHandle.CombineDependencies(dependsOnApplyState, dependsOnUpdate);
                }

                {
                    // Complete all previous systems to be sure that
                    // renderingOnSceneApplyState and renderingOnSceneUpdate set up has been complete
                    dependsOn.Complete();
                }

                // Update views logic
                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] ApplyState Views");
                    marker.Begin();
                    for (uint i = 0u; i < this.data.ptr->renderingOnSceneApplyState.Count; ++i) {
                        var entId = this.data.ptr->renderingOnSceneApplyState.sparseSet.dense[in allocator, i];
                        if (this.data.ptr->renderingOnSceneApplyStateCulling[in allocator, entId] == true) continue;
                        var idx = this.data.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in allocator, entId);
                        ref var entData = ref *(this.data.ptr->renderingOnSceneEnts.Ptr + idx);
                        var view = this.data.ptr->renderingOnScene[in allocator, idx];
                        var ent = entData.element;
                        if (entData.version != ent.Version) {
                            entData.version = ent.Version;
                            provider.ApplyState(this.data, in view, entData.ViewData);
                        }
                    }
                    marker.End();
                }

                {
                    var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Update Views");
                    marker.Begin();
                    for (uint i = 0u; i < this.data.ptr->renderingOnSceneUpdate.Count; ++i) {
                        var entId = this.data.ptr->renderingOnSceneUpdate.sparseSet.dense[in allocator, i];
                        if (this.data.ptr->renderingOnSceneUpdateCulling[in allocator, entId] == true) continue;
                        var idx = this.data.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in allocator, entId);
                        ref var entData = ref *(this.data.ptr->renderingOnSceneEnts.Ptr + idx);
                        var view = this.data.ptr->renderingOnScene[in allocator, idx];
                        if (view.prefabInfo.ptr->typeInfo.HasUpdate == true || view.prefabInfo.ptr->HasUpdateModules == true) {
                            provider.OnUpdate(this.data, in view, entData.ViewData, dt);
                        }
                    }
                    marker.End();
                }
            }
            
            dependsOn = new Jobs.CompleteJob() {
                mode = mode,
                viewsModuleData = this.data,
            }.Schedule(dependsOn);
            
            return dependsOn;

        }

        /// <summary>
        /// Returns local data by entity.
        /// </summary>
        public Ent GetLocalDataByEntity(in Ent entity) {
            return EntityViewProvider.GetLocalDataByEntity(this.data, in entity);
        }

        /// <summary>
        /// Returns view by entity.
        /// </summary>
        public IView GetViewByEntity(in Ent entity) {
            return this.provider.Value.GetViewByEntity(this.data, in entity);
        }

    }

}
