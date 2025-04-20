using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS {

    using static Cuts;
    using Internal;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    namespace Internal {

        public unsafe struct ArrayCacheLine<T> where T : unmanaged {

            public static readonly uint CACHE_LINE_SIZE = _align(TSize<T>.size, JobUtils.CacheLineSize);

            public readonly uint Length => JobUtils.ThreadsCount;
            internal safe_ptr ptr;

            [INLINE(256)]
            public void Initialize() {
                this.ptr = _make(CACHE_LINE_SIZE * this.Length);
            }

            [INLINE(256)]
            public ref T Get(int index) {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)(this.ptr + (uint)index * CACHE_LINE_SIZE).ptr;
            }

            [INLINE(256)]
            public ref T Get(uint index) {
                E.RANGE(index, 0, this.Length);
                return ref *(T*)(this.ptr + index * CACHE_LINE_SIZE).ptr;
            }

            [INLINE(256)]
            public void Dispose() {
                if (this.ptr.ptr != null) _free(this.ptr);
                this = default;
            }

        }

        public unsafe struct Array<T> : IIsCreated where T : unmanaged {

            public volatile uint Length;
            internal safe_ptr<T> ptr;
            
            public bool IsCreated => this.ptr.ptr != null;

            [INLINE(256)]
            public ref T Get(int index) {
                E.RANGE(index, 0, this.Length);
                return ref *(this.ptr + index).ptr;
            }

            [INLINE(256)]
            public ref T Get(uint index) {
                E.RANGE(index, 0, this.Length);
                return ref *(this.ptr + index).ptr;
            }

            [INLINE(256)]
            public void Resize(uint length) {

                var arr = this.ptr;
                var u = this.Length;
                _resizeArray(ref arr, ref u, length);
                this.ptr = arr;
                this.Length = u;

            }

            [INLINE(256)]
            public void Dispose() {
                if (this.ptr.ptr != null) _free(this.ptr);
                this = default;
            }

            [INLINE(256)]
            public safe_ptr GetPtr() {
                return this.ptr;
            }

        }

        public unsafe struct ListUShort {

            public struct Node {

                public ushort data;
                public safe_ptr<Node> next;

            }

            public safe_ptr<Node> root;
            public uint Count;

            public bool isCreated => this.root.ptr != null;

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

            [INLINE(256)]
            public void Add(ushort value) {

                var node = _make(new Node() { data = value });
                node.ptr->next = this.root;
                this.root = node;
                ++this.Count;

            }

            [INLINE(256)]
            public ushort Pop() {

                var root = this.root;
                var val = this.root.ptr->data;
                this.root = this.root.ptr->next;
                _free(root);
                --this.Count;
                return val;

            }

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

            [INLINE(256)]
            public void Dispose() {
                this.Clear();
                this = default;
            }

        }

    }

    public struct WorldHeader {

        public World world;
        public Unity.Collections.FixedString64Bytes name;
        public LockSpinner endTickHandlesLock;
        public Unity.Collections.LowLevel.Unsafe.UnsafeList<Unity.Jobs.JobHandle> endTickHandles;

        public void Dispose() {
            this.endTickHandles.Dispose();
        }

    }
    
    public struct WorldsStorage {

        private static readonly Unity.Burst.SharedStatic<Array<WorldHeader>> worldsArrBurst = Unity.Burst.SharedStatic<Array<WorldHeader>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsStorage>(TAlign<Array<WorldHeader>>.align, 10003);
        internal static ref Array<WorldHeader> worlds => ref worldsArrBurst.Data;
        
    }

    public struct WorldsIdStorage {

        private static readonly Unity.Burst.SharedStatic<ListUShort> worldIdsBurst = Unity.Burst.SharedStatic<ListUShort>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsIdStorage>(TAlign<ListUShort>.align, 10001);
        internal static ref ListUShort worldIds => ref worldIdsBurst.Data;

    }
    
    public struct WorldsDomainAllocator {

        private static readonly Unity.Burst.SharedStatic<Unity.Collections.AllocatorHelper<DomainAllocator>> allocatorDomainBurst = Unity.Burst.SharedStatic<Unity.Collections.AllocatorHelper<DomainAllocator>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsDomainAllocator>(TAlign<Unity.Collections.AllocatorHelper<DomainAllocator>>.align, 10008);
        internal static ref Unity.Collections.AllocatorHelper<DomainAllocator> allocatorDomain => ref allocatorDomainBurst.Data;

        private static readonly Unity.Burst.SharedStatic<Unity.Collections.NativeReference<bool>> allocatorDomainValidBurst = Unity.Burst.SharedStatic<Unity.Collections.NativeReference<bool>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsDomainAllocator>(TAlign<Unity.Collections.NativeReference<bool>>.align, 10009);
        internal static bool allocatorDomainValid => allocatorDomainValidBurst.Data.IsCreated == true && allocatorDomainValidBurst.Data.Value;

        public static Unity.Collections.AllocatorHelper<DomainAllocator> Initialize() {

            var prevMode = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.GetLeakDetectionMode();
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(Unity.Collections.NativeLeakDetectionMode.Disabled);
            allocatorDomain = new Unity.Collections.AllocatorHelper<DomainAllocator>(Constants.ALLOCATOR_PERSISTENT);
            allocatorDomain.Allocator.Initialize(100);
            allocatorDomainValidBurst.Data = new Unity.Collections.NativeReference<bool>(true, Constants.ALLOCATOR_PERSISTENT);
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(prevMode);

            return allocatorDomain;

        }

        public static void Dispose() {

            if (allocatorDomainValidBurst.Data.IsCreated == false || allocatorDomainValidBurst.Data.Value == false) return;
            allocatorDomainValidBurst.Data.Value = false;
            allocatorDomainValidBurst.Data.Dispose();
            allocatorDomain.Dispose();
            
        }

    }

    public struct WorldsPersistentAllocator {

        private static readonly Unity.Burst.SharedStatic<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>> allocatorPersistentBurst = Unity.Burst.SharedStatic<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsPersistentAllocator>(TAlign<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>.align, 10006);
        internal static ref Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator> allocatorPersistent => ref allocatorPersistentBurst.Data;

        private static readonly Unity.Burst.SharedStatic<Unity.Collections.NativeReference<bool>> allocatorPersistentValidBurst = Unity.Burst.SharedStatic<Unity.Collections.NativeReference<bool>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsPersistentAllocator>(TAlign<Unity.Collections.NativeReference<bool>>.align, 10007);
        internal static bool allocatorPersistentValid => allocatorPersistentValidBurst.Data.IsCreated == true && allocatorPersistentValidBurst.Data.Value;

        public static Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator> Initialize() {

            var prevMode = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.GetLeakDetectionMode();
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(Unity.Collections.NativeLeakDetectionMode.Disabled);
            allocatorPersistent = new Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>(Constants.ALLOCATOR_PERSISTENT);
            allocatorPersistent.Allocator.Initialize(128 * 1024, true);
            allocatorPersistentValidBurst.Data = new Unity.Collections.NativeReference<bool>(true, Constants.ALLOCATOR_PERSISTENT);
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(prevMode);

            return allocatorPersistent;

        }

        public static void Dispose() {

            if (allocatorPersistentValidBurst.Data.IsCreated == false || allocatorPersistentValidBurst.Data.Value == false) return;
            allocatorPersistentValidBurst.Data.Value = false;
            allocatorPersistentValidBurst.Data.Dispose();
            allocatorPersistent.Dispose();
            
        }

        public static void Reset() {

            allocatorPersistent.Allocator.Rewind();
            
        }

    }

    public struct WorldsTempAllocator {

        private static readonly Unity.Burst.SharedStatic<Internal.Array<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>> allocatorTempBurst = Unity.Burst.SharedStatic<Internal.Array<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsTempAllocator>(TAlign<Internal.Array<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>>.align, 10005);
        internal static ref Internal.Array<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>> allocatorTemp => ref allocatorTempBurst.Data;

        private static readonly Unity.Burst.SharedStatic<Internal.Array<bool>> allocatorTempValidBurst = Unity.Burst.SharedStatic<Internal.Array<bool>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsTempAllocator>(TAlign<Internal.Array<bool>>.align, 10007);
        internal static ref Internal.Array<bool> allocatorTempValid => ref allocatorTempValidBurst.Data;

        public static void Initialize(ushort worldId) {

            var prevMode = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.GetLeakDetectionMode();
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(Unity.Collections.NativeLeakDetectionMode.Disabled);
            {
                {
                    allocatorTemp.Resize(worldId + 1u);
                    allocatorTempValid.Resize(worldId + 1u);
                }
                {
                    var allocator = new Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>(Constants.ALLOCATOR_PERSISTENT);
                    allocator.Allocator.Initialize(128 * 1024, false);
                    allocatorTemp.Get(worldId) = allocator;
                    allocatorTempValidBurst.Data.Get(worldId) = true;
                }
            }
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(prevMode);

        }

        public static void Dispose(ushort worldId) {
            
            if (worldId >= allocatorTempValidBurst.Data.Length || allocatorTempValidBurst.Data.Get(worldId) == false) return;
            allocatorTempValidBurst.Data.Get(worldId) = false;
            allocatorTemp.Get(worldId).Dispose();
            
        }

        public static void Reset(ushort worldId) {
            
            allocatorTemp.Get(worldId).Allocator.Rewind();
            
        }

    }

    public unsafe struct Worlds {

        private static readonly Unity.Burst.SharedStatic<ushort> worldsCounterBurst = Unity.Burst.SharedStatic<ushort>.GetOrCreate<Worlds>();
        private static ref ushort counter => ref worldsCounterBurst.Data;

        public static uint MaxWorldId => counter;

        public static void Initialize() {

            #if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeEditorChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeEditorChanged;
            #endif
            
            if (WorldsStorage.worlds.Length > 0u) Dispose();
            WorldsPersistentAllocator.Initialize();
            WorldsDomainAllocator.Initialize();

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

        public static void Dispose() {

            WorldsPersistentAllocator.Dispose();
            WorldsDomainAllocator.Dispose();
            
        }

        [INLINE(256)]
        public static Array<WorldHeader> GetWorlds() {
            return WorldsStorage.worlds;
        }

        [INLINE(256)]
        public static bool IsAlive(uint id) {

            if (id >= WorldsStorage.worlds.Length) return false;
            return WorldsStorage.worlds.Get(id).world.state.ptr != null;

        }
        
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

            WorldsTempAllocator.Dispose(worldId);
            
            ref var worldIds = ref WorldsIdStorage.worldIds;
            worldIds.Add(worldId);

        }

        [INLINE(256)]
        public static Unity.Collections.FixedString64Bytes GetWorldName(ushort worldId) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return default;
            return worldsStorage.Get(worldId).name;

        }

        [BURST]
        public struct ClearEndTickHandlesJob : Unity.Jobs.IJob {

            public Array<WorldHeader> worldsStorage;
            public ushort worldId;

            public void Execute() {

                ref var storage = ref this.worldsStorage.Get(this.worldId);
                ref var arr = ref storage.endTickHandles;
                if (arr.IsCreated == true) {
                    storage.endTickHandlesLock.Lock();
                    arr.Clear();
                    storage.endTickHandlesLock.Unlock();
                }
                
            }

        }
        
        [INLINE(256)]
        public static void AddEndTickHandle(ushort worldId, Unity.Jobs.JobHandle handle) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return;
            ref var storage = ref worldsStorage.Get(worldId);
            ref var arr = ref storage.endTickHandles;
            storage.endTickHandlesLock.Lock();
            if (arr.IsCreated == false) {
                arr = new Unity.Collections.LowLevel.Unsafe.UnsafeList<JobHandle>(10, Constants.ALLOCATOR_DOMAIN);
            }
            arr.Add(handle);
            storage.endTickHandlesLock.Unlock();
            
        }
        
        [INLINE(256)]
        public static Unity.Jobs.JobHandle GetEndTickHandle(ushort worldId) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return default;
            ref var arr = ref worldsStorage.Get(worldId).endTickHandles;
            if (arr.IsCreated == false) return default;
            var tempArr = new Unity.Collections.NativeArray<JobHandle>(arr.Length, Constants.ALLOCATOR_TEMP);
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemCpy(tempArr.GetUnsafePtr(), arr.Ptr, arr.Length * TSize<JobHandle>.sizeInt);
            var dependsOn = Unity.Jobs.JobHandle.CombineDependencies(tempArr);//Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Unity.Jobs.JobHandle>(arr.Ptr, arr.Length, Unity.Collections.Allocator.None));
            dependsOn = Unity.Jobs.JobHandle.CombineDependencies(dependsOn, new ClearEndTickHandlesJob() { worldsStorage = worldsStorage, worldId = worldId, }.Schedule());
            return dependsOn;

        }
        
        [INLINE(256)]
        internal static void AddWorld(ref World world, ushort worldId = 0, Unity.Collections.FixedString64Bytes name = default, bool raiseCallback = true) {

            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId == 0u) worldId = Worlds.GetNextWorldId();
            world.id = worldId;
            
            if (worldId >= worldsStorage.Length) {
                worldsStorage.Resize((worldId + 1u) * 2u);
            }
            
            WorldsParent.Resize(world.id);

            if (name.IsEmpty == true) {
                name = $"World #{world.id}";
            } else {
                name = $"{name.ToString()} (World #{world.id})";
            }
            
            worldsStorage.Get(worldId) = new WorldHeader() {
                world = world,
                name = name,
            };
            
            WorldsTempAllocator.Initialize(worldId);
            
            if (raiseCallback == true) WorldStaticCallbacks.RaiseCallback(ref world);

        }

        [INLINE(256)]
        internal static void ReleaseWorld(in World world) {

            Worlds.ReleaseWorldId(world.id);
            ref var worldsStorage = ref WorldsStorage.worlds;
            worldsStorage.Get(world.id).Dispose();
            worldsStorage.Get(world.id) = default;
            
            GlobalEvents.DisposeWorld(world.id);
            #if UNITY_EDITOR
            EntEditorName.Dispose(world.id);
            #endif
            WorldsParent.Clear(world.id);

        }

        [INLINE(256)]
        internal static void ResetWorldsCounter() {

            counter = 0;
            WorldsIdStorage.worldIds.Clear();
            
        }

    }

}