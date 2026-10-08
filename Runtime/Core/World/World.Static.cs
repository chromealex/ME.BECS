using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS {

    using static Cuts;
    using Internal;
    using BURST = Unity.Burst.BurstCompileAttribute;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    namespace Internal {

        /// <summary>
        /// Defines array cache line state and operations.
        /// </summary>
        [IgnoreProfiler]
        public unsafe struct ArrayCacheLine<T> where T : unmanaged {

            /// <summary>
            /// Cache line size constant used by <c>ArrayCacheLine</c>.
            /// </summary>
            public static readonly uint CACHE_LINE_SIZE = _align(TSize<T>.size, JobUtils.CacheLineSize);

            /// <summary>
            /// Number of elements exposed by this value.
            /// </summary>
            public readonly uint Length => JobUtils.ThreadsCount;
            internal safe_ptr ptr;

            /// <summary>
            /// Initializes array cache line state from the supplied context.
            /// </summary>
            [INLINE(256)]
            public void Initialize() {
                this.ptr = _make(CACHE_LINE_SIZE * this.Length);
            }

            /// <summary>
            /// Returns the requested entry from array cache line.
            /// </summary>
            [INLINE(256)]
            public ref T Get(int index) {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)(this.ptr + (uint)index * CACHE_LINE_SIZE).ptr;
            }

            /// <summary>
            /// Returns the requested entry from array cache line.
            /// </summary>
            [INLINE(256)]
            public ref T Get(uint index) {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)(this.ptr + index * CACHE_LINE_SIZE).ptr;
            }

            /// <summary>
            /// Releases the resources owned by this array cache line instance.
            /// </summary>
            [INLINE(256)]
            public void Dispose() {
                if (this.ptr.ptr != null) _free(this.ptr);
                this = default;
            }

        }
        
        /// <summary>
        /// Defines array state and operations.
        /// </summary>
        [IgnoreProfiler]
        public unsafe struct Array<T> : IIsCreated where T : unmanaged {

            /// <summary>
            /// Number of elements exposed by this value.
            /// </summary>
            public volatile uint Length;
            internal safe_ptr<T> ptr;
            
            /// <summary>
            /// Whether the backing state has been initialized.
            /// </summary>
            public bool IsCreated => this.ptr.ptr != null;

            /// <summary>
            /// Returns the requested entry from array.
            /// </summary>
            [INLINE(256)]
            public readonly ref T Get(int index) {
                E.RANGE(index, 0, this.Length);
                return ref *(this.ptr + index).ptr;
            }

            /// <summary>
            /// Returns the requested entry from array.
            /// </summary>
            [INLINE(256)]
            public readonly ref T Get(uint index) {
                E.RANGE(index, 0, this.Length);
                return ref *(this.ptr + index).ptr;
            }

            /// <summary>
            /// Returns the first matching entry, or the default value when none exists.
            /// </summary>
            [INLINE(256)]
            public readonly T FirstOrDefault() {
                if (this.Length > 0u) return this.Get(0u);
                return default;
            }

            /// <summary>
            /// Changes the storage size to the requested element count.
            /// </summary>
            [INLINE(256)]
            public void Resize(uint length) {

                var arr = this.ptr;
                var u = this.Length;
                _resizeArray(ref arr, ref u, length);
                this.ptr = arr;
                this.Length = u;

            }

            /// <summary>
            /// Releases the resources owned by this array instance.
            /// </summary>
            [INLINE(256)]
            public void Dispose() {
                if (this.ptr.ptr != null) _free(this.ptr);
                this = default;
            }

            /// <summary>
            /// Resolves the requested address to a native pointer.
            /// </summary>
            [INLINE(256)]
            public safe_ptr GetPtr() {
                return this.ptr;
            }

        }

        /// <summary>
        /// Defines list u short state and operations.
        /// </summary>
        [IgnoreProfiler]
        public unsafe struct ListUShort {

            /// <summary>
            /// Defines a node entry in the associated graph.
            /// </summary>
            public struct Node {

                /// <summary>
                /// Data consumed or produced by the containing operation.
                /// </summary>
                public ushort data;
                /// <summary>
                /// Link or index of the next entry in the sequence.
                /// </summary>
                public safe_ptr<Node> next;

            }

            /// <summary>
            /// Root entry of the represented hierarchy.
            /// </summary>
            public safe_ptr<Node> root;
            /// <summary>
            /// Number of entries currently tracked by this value.
            /// </summary>
            public uint Count;

            /// <summary>
            /// Whether the backing state has been initialized.
            /// </summary>
            public bool isCreated => this.root.ptr != null;

            /// <summary>
            /// Converts the value to array.
            /// </summary>
            [INLINE(256)]
            public ushort[] ToArray() {

                var result = new ushort[this.Count];
                var i = 0;
                var node = this.root;
                while (node.ptr != null) {
                    var n = node;
                    result[i++] = n.ptr->data;
                    node = node.ptr->next;
                }

                return result;

            }

            /// <summary>
            /// Adds the supplied entry to list u short.
            /// </summary>
            [INLINE(256)]
            public void Add(ushort value) {

                var newNode = _make(new Node() { data = value });
                if (this.root.ptr == null) {
                    newNode.ptr->next = default;
                    this.root = newNode;
                    ++this.Count;
                    return;
                }

                if (value < this.root.ptr->data) {
                    newNode.ptr->next = this.root;
                    this.root = newNode;
                    ++this.Count;
                    return;
                }

                var current = this.root;
                while (current.ptr->next.ptr != null && current.ptr->next.ptr->data < value) {
                    current = current.ptr->next;
                }

                newNode.ptr->next = current.ptr->next;
                current.ptr->next = newNode;
                ++this.Count;

            }

            /// <summary>
            /// Removes and returns the next entry according to this container's ordering.
            /// </summary>
            [INLINE(256)]
            public ushort Pop() {

                var root = this.root;
                var val = this.root.ptr->data;
                this.root = this.root.ptr->next;
                _free(root);
                --this.Count;
                return val;

            }

            /// <summary>
            /// Removes the specified entry from list u short.
            /// </summary>
            [INLINE(256)]
            public bool Remove(ushort value) {

                Node* prevNode = null;
                var node = this.root;
                while (node.ptr != null) {
                    if (node.ptr->data == value) {
                        if (prevNode == null) {
                            this.root = node.ptr->next;
                        } else {
                            prevNode->next = node.ptr->next;
                        }

                        _free(node);
                        --this.Count;
                        return true;
                    }

                    prevNode = node.ptr;
                    node = node.ptr->next;
                }

                return false;

            }

            /// <summary>
            /// Clears the current list u short contents.
            /// </summary>
            [INLINE(256)]
            public void Clear() {

                var node = this.root;
                while (node.ptr != null) {
                    var n = node;
                    node = node.ptr->next;
                    _free(n);
                }

                this.root = default;
                this.Count = 0u;

            }

            /// <summary>
            /// Releases the resources owned by this list u short instance.
            /// </summary>
            [INLINE(256)]
            public void Dispose() {
                this.Clear();
                this = default;
            }

        }

    }

    /// <summary>
    /// Defines world header state and operations.
    /// </summary>
    public struct WorldHeader {

        /// <summary>
        /// World used by the containing operation.
        /// </summary>
        public World world;
        /// <summary>
        /// Display or lookup name of this entry.
        /// </summary>
        public Unity.Collections.FixedString64Bytes name;
        /// <summary>
        /// End tick handles lock used by <c>WorldHeader</c>.
        /// </summary>
        public LockSpinner endTickHandlesLock;
        /// <summary>
        /// End tick handles used by <c>WorldHeader</c>.
        /// </summary>
        public Unity.Collections.LowLevel.Unsafe.UnsafeList<Unity.Jobs.JobHandle> endTickHandles;
        /// <summary>
        /// Src name used by <c>WorldHeader</c>.
        /// </summary>
        public Unity.Collections.FixedString64Bytes srcName;
        /// <summary>
        /// Elapsed simulation time supplied to this update.
        /// </summary>
        public uint deltaTime;

        /// <summary>
        /// Releases the resources owned by this world header instance.
        /// </summary>
        public void Dispose() {
            this.endTickHandles.Dispose();
        }

    }
    
    /// <summary>
    /// Stores and indexes worlds entries.
    /// </summary>
    [IgnoreProfiler]
    public struct WorldsStorage {

        private static readonly Unity.Burst.SharedStatic<Array<WorldHeader>> worldsArrBurst = Unity.Burst.SharedStatic<Array<WorldHeader>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsStorage>(TAlign<Array<WorldHeader>>.align, 10003);
        internal static ref Array<WorldHeader> worlds => ref worldsArrBurst.Data;
        
    }

    /// <summary>
    /// Stores and indexes worlds ID entries.
    /// </summary>
    [IgnoreProfiler]
    public struct WorldsIdStorage {

        private static readonly Unity.Burst.SharedStatic<ListUShort> worldIdsBurst = Unity.Burst.SharedStatic<ListUShort>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsIdStorage>(TAlign<ListUShort>.align, 10001);
        internal static ref ListUShort worldIds => ref worldIdsBurst.Data;

    }
    
    /// <summary>
    /// Stores per-world allocators whose backing storage follows the registered world lifecycle.
    /// </summary>
    [IgnoreProfiler]
    public struct WorldsDomainAllocator {

        #if UNITY_2023_1_OR_NEWER
        internal static bool allocatorDomainValid => false;
        #else
        private static readonly Unity.Burst.SharedStatic<Unity.Collections.AllocatorHelper<DomainAllocator>> allocatorDomainBurst = Unity.Burst.SharedStatic<Unity.Collections.AllocatorHelper<DomainAllocator>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsDomainAllocator>(TAlign<Unity.Collections.AllocatorHelper<DomainAllocator>>.align, 10008);
        internal static ref Unity.Collections.AllocatorHelper<DomainAllocator> allocatorDomain => ref allocatorDomainBurst.Data;

        private static readonly Unity.Burst.SharedStatic<Unity.Collections.NativeReference<bool>> allocatorDomainValidBurst = Unity.Burst.SharedStatic<Unity.Collections.NativeReference<bool>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsDomainAllocator>(TAlign<Unity.Collections.NativeReference<bool>>.align, 10009);
        internal static bool allocatorDomainValid => allocatorDomainValidBurst.Data.IsCreated == true && allocatorDomainValidBurst.Data.Value;
        #endif

        /// <summary>
        /// Initializes worlds domain allocator state from the supplied context.
        /// </summary>
        public static void Initialize() {

            #if !UNITY_2023_1_OR_NEWER
            var prevMode = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.GetLeakDetectionMode();
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(Unity.Collections.NativeLeakDetectionMode.Disabled);
            allocatorDomain = new Unity.Collections.AllocatorHelper<DomainAllocator>(Constants.ALLOCATOR_PERSISTENT);
            allocatorDomain.Allocator.Initialize(100);
            allocatorDomainValidBurst.Data = new Unity.Collections.NativeReference<bool>(true, Constants.ALLOCATOR_PERSISTENT);
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(prevMode);
            #endif

        }

        /// <summary>
        /// Releases the resources owned by this worlds domain allocator instance.
        /// </summary>
        public static void Dispose() {

            #if !UNITY_2023_1_OR_NEWER
            if (allocatorDomainValidBurst.Data.IsCreated == false || allocatorDomainValidBurst.Data.Value == false) return;
            allocatorDomainValidBurst.Data.Value = false;
            allocatorDomainValidBurst.Data.Dispose();
            allocatorDomain.Dispose();
            #endif
            
        }

    }

    /// <summary>
    /// Stores per-world allocators used for persistent native allocations.
    /// </summary>
    [IgnoreProfiler]
    public struct WorldsPersistentAllocator {

        private static readonly Unity.Burst.SharedStatic<Internal.Array<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>> allocatorPersistentBurst = Unity.Burst.SharedStatic<Internal.Array<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsPersistentAllocator>(TAlign<Internal.Array<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>>.align, 10006);
        /// <summary>
        /// Allocator persistent used by <c>WorldsPersistentAllocator</c>.
        /// </summary>
        public static ref Internal.Array<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>> allocatorPersistent => ref allocatorPersistentBurst.Data;

        private static readonly Unity.Burst.SharedStatic<Internal.Array<bool>> allocatorPersistentValidBurst = Unity.Burst.SharedStatic<Internal.Array<bool>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsPersistentAllocator>(TAlign<Internal.Array<bool>>.align, 10007);
        /// <summary>
        /// Allocator persistent valid used by <c>WorldsPersistentAllocator</c>.
        /// </summary>
        public static ref Internal.Array<bool> allocatorPersistentValid => ref allocatorPersistentValidBurst.Data;

        /// <summary>
        /// Initializes worlds persistent allocator state from the supplied context.
        /// </summary>
        public static void Initialize(ushort worldId) {

            var prevMode = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.GetLeakDetectionMode();
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(Unity.Collections.NativeLeakDetectionMode.Disabled);
            {
                {
                    allocatorPersistent.Resize(worldId + 1u);
                    allocatorPersistentValid.Resize(worldId + 1u);
                }
                {
                    var allocator = new Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>(Constants.ALLOCATOR_PERSISTENT);
                    allocator.Allocator.Initialize(128 * 1024);
                    allocatorPersistent.Get(worldId) = allocator;
                    allocatorPersistentValidBurst.Data.Get(worldId) = true;
                }
            }
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(prevMode);

        }

        /// <summary>
        /// Releases the resources owned by this worlds persistent allocator instance.
        /// </summary>
        public static void Dispose(ushort worldId) {

            if (worldId >= allocatorPersistentValidBurst.Data.Length || allocatorPersistentValidBurst.Data.Get(worldId) == false) return;
            allocatorPersistentValidBurst.Data.Get(worldId) = false;
            ref var allocator = ref allocatorPersistent.Get(worldId);
            allocator.Allocator.Dispose();
            allocator.Dispose();
            allocator = default;

        }

    }

    /// <summary>
    /// Stores per-world allocators used for temporary native allocations.
    /// </summary>
    [IgnoreProfiler]
    public struct WorldsTempAllocator {

        private static readonly Unity.Burst.SharedStatic<Internal.Array<Unity.Collections.AllocatorHelper<TempAllocator>>> allocatorTempBurst = Unity.Burst.SharedStatic<Internal.Array<Unity.Collections.AllocatorHelper<TempAllocator>>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsTempAllocator>(TAlign<Internal.Array<Unity.Collections.AllocatorHelper<TempAllocator>>>.align, 10005);
        /// <summary>
        /// Allocator temp used by <c>WorldsTempAllocator</c>.
        /// </summary>
        public static ref Internal.Array<Unity.Collections.AllocatorHelper<TempAllocator>> allocatorTemp => ref allocatorTempBurst.Data;

        private static readonly Unity.Burst.SharedStatic<Internal.Array<bool>> allocatorTempValidBurst = Unity.Burst.SharedStatic<Internal.Array<bool>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsTempAllocator>(TAlign<Internal.Array<bool>>.align, 10008);
        internal static ref Internal.Array<bool> allocatorTempValid => ref allocatorTempValidBurst.Data;

        /// <summary>
        /// Initializes worlds temp allocator state from the supplied context.
        /// </summary>
        public static void Initialize(ushort worldId) {

            var prevMode = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.GetLeakDetectionMode();
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(Unity.Collections.NativeLeakDetectionMode.Disabled);
            {
                {
                    allocatorTemp.Resize(worldId + 1u);
                    allocatorTempValid.Resize(worldId + 1u);
                }
                {
                    var allocator = new Unity.Collections.AllocatorHelper<TempAllocator>(Constants.ALLOCATOR_PERSISTENT);
                    allocator.Allocator.Initialize(128 * 1024);
                    allocatorTemp.Get(worldId) = allocator;
                    allocatorTempValidBurst.Data.Get(worldId) = true;
                }
            }
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(prevMode);

        }

        /// <summary>
        /// Releases the resources owned by this worlds temp allocator instance.
        /// </summary>
        public static void Dispose(ushort worldId) {
            
            if (worldId >= allocatorTempValidBurst.Data.Length || allocatorTempValidBurst.Data.Get(worldId) == false) return;
            allocatorTempValidBurst.Data.Get(worldId) = false;
            ref var allocator = ref allocatorTemp.Get(worldId);
            allocator.Allocator.Dispose();
            allocator.Dispose();
            allocator = default;
            
        }

        /// <summary>
        /// Restores the tracked state to its initial values.
        /// </summary>
        public static void Reset(ushort worldId) {
            
            allocatorTemp.Get(worldId).Allocator.Rewind();
            
        }

    }

    /// <summary>
    /// Stores and indexes handle entries.
    /// </summary>
    [IgnoreProfiler]
    public struct HandleStorage {

        internal static readonly Unity.Burst.SharedStatic<JobHandle> lastApplyHandleBurst = Unity.Burst.SharedStatic<JobHandle>.GetOrCreate<JobHandle>();
        internal static ref JobHandle lastApplyHandle => ref lastApplyHandleBurst.Data;

    }

    /// <summary>
    /// Registers worlds and resolves world identifiers to their current instances.
    /// </summary>
    [IgnoreProfiler]
    public unsafe struct Worlds {

        private static readonly Unity.Burst.SharedStatic<ushort> worldsCounterBurst = Unity.Burst.SharedStatic<ushort>.GetOrCreate<Worlds>();
        private static ref ushort counter => ref worldsCounterBurst.Data;

        /// <summary>
        /// Maximum world ID.
        /// </summary>
        public static uint MaxWorldId => counter;

        /// <summary>
        /// Initializes worlds state from the supplied context.
        /// </summary>
        public static void Initialize() {
            
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            StartParallelJob.safetyHandler.Data = AtomicSafetyHandle.Create();
            #endif
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeEditorChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeEditorChanged;
            #endif
            
            if (WorldsStorage.worlds.Length > 0u) Dispose();
            WorldsDomainAllocator.Initialize();
            
            FullFillBits.Initialize();
            StaticUtils.Initialize();

            if (WorldsStorage.worlds.Length > 0u) WorldsStorage.worlds.Dispose();
            ResetWorldsCounter();

        }

        #if UNITY_EDITOR
        private static void OnPlayModeEditorChanged(UnityEditor.PlayModeStateChange state) {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) {
                Dispose();
            }
        }
        #endif

        /// <summary>
        /// Releases the resources owned by this worlds instance.
        /// </summary>
        public static void Dispose() {

            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(StartParallelJob.safetyHandler.Data);
            #endif
            LocksCache.Dispose();
            WorldsDomainAllocator.Dispose();
            
        }

        /// <summary>
        /// Returns worlds.
        /// </summary>
        [INLINE(256)]
        public static Array<WorldHeader> GetWorlds() {
            return WorldsStorage.worlds;
        }

        /// <summary>
        /// Tests whether the referenced entity or world still matches its registered lifetime.
        /// </summary>
        [INLINE(256)]
        public static bool IsAlive(uint id) {

            if (id >= WorldsStorage.worlds.Length) return false;
            return WorldsStorage.worlds.Get(id).world.state.ptr != null;

        }
        
        /// <summary>
        /// Returns world.
        /// </summary>
        [INLINE(256)]
        public static ref readonly World GetWorld(ushort id) {

            var worldsStorage = WorldsStorage.worlds;
            if (id >= worldsStorage.Length) return ref StaticDefaultValue<World>.defaultValue;

            return ref worldsStorage.Get(id).world;

        }
        
        [INLINE(256)]
        internal static ushort GetNextWorldId() {

            ushort id = 0;
            ref var worldIds = ref WorldsIdStorage.worldIds;
            if (worldIds.Count > 0) {
                id = worldIds.Pop();
            } else {
                id = ++counter;
            }

            return id;

        }

        [INLINE(256)]
        internal static void ReleaseWorldId(ushort worldId) {

            ref var worldIds = ref WorldsIdStorage.worldIds;
            worldIds.Add(worldId);

        }

        /// <summary>
        /// Sets world delta time.
        /// </summary>
        [INLINE(256)]
        public static void SetWorldDeltaTime(ushort worldId, uint deltaTimeMs) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return;
            worldsStorage.Get(worldId).deltaTime = deltaTimeMs;

        }

        /// <summary>
        /// Returns world delta time.
        /// </summary>
        [INLINE(256)]
        public static uint GetWorldDeltaTime(ushort worldId) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return default;
            return worldsStorage.Get(worldId).deltaTime;

        }

        /// <summary>
        /// Returns world name.
        /// </summary>
        [INLINE(256)]
        public static Unity.Collections.FixedString64Bytes GetWorldName(ushort worldId) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return default;
            return worldsStorage.Get(worldId).name;

        }

        /// <summary>
        /// Returns world source name.
        /// </summary>
        [INLINE(256)]
        public static Unity.Collections.FixedString64Bytes GetWorldSourceName(ushort worldId) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return default;
            return worldsStorage.Get(worldId).srcName;

        }

        /// <summary>
        /// Adds end tick handle.
        /// </summary>
        [INLINE(256)]
        public static void AddEndTickHandle(ushort worldId, Unity.Jobs.JobHandle handle) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return;
            ref var storage = ref worldsStorage.Get(worldId);
            ref var arr = ref storage.endTickHandles;
            storage.endTickHandlesLock.Lock();
            if (arr.IsCreated == false) {
                arr = new Unity.Collections.LowLevel.Unsafe.UnsafeList<JobHandle>(10, Constants.ALLOCATOR_PERSISTENT);
            }
            arr.Add(handle);
            storage.endTickHandlesLock.Unlock();
            
        }
        
        /// <summary>
        /// Returns end tick handle.
        /// </summary>
        [INLINE(256)]
        public static Unity.Jobs.JobHandle GetEndTickHandle(ushort worldId) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return default;
            ref var storage = ref worldsStorage.Get(worldId);
            storage.endTickHandlesLock.Lock();
            ref var arr = ref storage.endTickHandles;
            if (arr.IsCreated == false || arr.Length == 0) {
                storage.endTickHandlesLock.Unlock();
                return default;
            }

            var tempArr = new Unity.Collections.NativeArray<JobHandle>(arr.Length, Constants.ALLOCATOR_TEMP);
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemCpy(tempArr.GetUnsafePtr(), arr.Ptr, arr.Length * TSize<JobHandle>.sizeInt);
            arr.Clear();
            storage.endTickHandlesLock.Unlock();

            var dependsOn = Unity.Jobs.JobHandle.CombineDependencies(tempArr);
            tempArr.Dispose();
            return dependsOn;

        }

        [Unity.Burst.BurstDiscardAttribute]
        private static void SetWorldName(ref World world, ref Unity.Collections.FixedString64Bytes name) {
            if (name.IsEmpty == true) {
                name = $"World #{world.id}";
            } else {
                name = $"#{world.id} {name.ToString()}";
            }
        }
        
        [INLINE(256)]
        internal static void AddWorld(ref World world, ushort worldId = 0, Unity.Collections.FixedString64Bytes name = default, bool raiseCallback = true) {

            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId == 0u) worldId = Worlds.GetNextWorldId();
            world.id = worldId;

            LocksCache.AddWorld(worldId);
            
            if (worldId >= worldsStorage.Length) {
                worldsStorage.Resize((worldId + 1u) * 2u);
            }
            
            WorldsParent.Resize(world.id);

            var srcName = name;
            SetWorldName(ref world, ref name);
            
            worldsStorage.Get(worldId) = new WorldHeader() {
                world = world,
                name = name,
                srcName = srcName,
            };
            
            WorldsTempAllocator.Initialize(worldId);
            WorldsPersistentAllocator.Initialize(worldId);
            
            WorldAspectStorage.AddWorld(in world);
            
            if (raiseCallback == true) WorldStaticCallbacks.RaiseCallback(ref world);

        }

        [INLINE(256)]
        internal static void ReleaseWorld(in World world) {

            RuntimeObjectReference.DisposeWorld(world.id);
            Worlds.ReleaseWorldId(world.id);
            LocksCache.DisposeWorld(world.id);
            ref var worldsStorage = ref WorldsStorage.worlds;
            worldsStorage.Get(world.id).Dispose();
            worldsStorage.Get(world.id) = default;
            
            WorldAspectStorage.DisposeWorld(in world);
            GlobalEvents.DisposeWorld(world.id);
            #if UNITY_EDITOR
            EntEditorName.Dispose(world.id);
            #endif
            WorldsParent.Clear(world.id);

            WorldsTempAllocator.Dispose(world.id);
            WorldsPersistentAllocator.Dispose(world.id);

        }

        [INLINE(256)]
        internal static void ResetWorldsCounter() {

            counter = 0;
            WorldsIdStorage.worldIds.Clear();
            
        }

    }

}
