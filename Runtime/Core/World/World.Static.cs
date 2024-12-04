namespace ME.BECS {

    using static Cuts;
    using Internal;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    namespace Internal {

        public unsafe struct ArrayCacheLine<T> where T : unmanaged {

            public const uint CACHE_LINE_SIZE = JobUtils.CacheLineSize;

            public readonly uint Length => JobUtils.ThreadsCount;
            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute]
            internal T* ptr;

            [INLINE(256)]
            public void Initialize() {
                var size = TSize<T>.size;
                var length = JobUtils.ThreadsCount;
                this.ptr = (T*)_make(size * CACHE_LINE_SIZE * length);
            }

            [INLINE(256)]
            public ref T Get(int index) {
                E.RANGE(index, 0, this.Length);
                return ref *(this.ptr + index * CACHE_LINE_SIZE);
            }

            [INLINE(256)]
            public ref T Get(uint index) {
                E.RANGE(index, 0, this.Length);
                return ref *(this.ptr + index * CACHE_LINE_SIZE);
            }

            [INLINE(256)]
            public void Dispose() {
                _free(this.ptr);
                this = default;
            }

        }

        public unsafe struct Array<T> where T : unmanaged {

            public uint Length;
            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute]
            internal T* ptr;

            [INLINE(256)]
            public ref T Get(int index) {
                E.RANGE(index, 0, this.Length);
                return ref *(this.ptr + index);
            }

            [INLINE(256)]
            public ref T Get(uint index) {
                E.RANGE(index, 0, this.Length);
                return ref *(this.ptr + index);
            }

            [INLINE(256)]
            public void Resize(uint length) {

                _resizeArray(ref this.ptr, ref this.Length, length);
                
            }

            [INLINE(256)]
            public void Dispose() {
                _free(this.ptr);
                this = default;
            }

            [INLINE(256)]
            public void* GetPtr() {
                return this.ptr;
            }

        }

        public unsafe struct ListUShort {

            public struct Node {

                public ushort data;
                public Node* next;

            }

            public Node* root;
            public uint Count;

            public bool isCreated => this.root != null;

            [INLINE(256)]
            public ushort[] ToArray() {

                var result = new ushort[this.Count];
                var i = 0;
                var node = this.root;
                while (node != null) {
                    var n = node;
                    result[i++] = n->data;
                    node = node->next;
                }

                return result;

            }

            [INLINE(256)]
            public void Add(ushort value) {

                var node = _make(new Node() { data = value });
                node->next = this.root;
                this.root = node;
                ++this.Count;

            }

            [INLINE(256)]
            public ushort Pop() {

                var root = this.root;
                var val = this.root->data;
                this.root = this.root->next;
                _free(root);
                --this.Count;
                return val;

            }

            [INLINE(256)]
            public bool Remove(ushort value) {

                Node* prevNode = null;
                var node = this.root;
                while (node != null) {
                    if (node->data == value) {
                        if (prevNode == null) {
                            this.root = node->next;
                        } else {
                            prevNode->next = node->next;
                        }

                        _free(node);
                        --this.Count;
                        return true;
                    }

                    prevNode = node;
                    node = node->next;
                }

                return false;

            }

            [INLINE(256)]
            public void Clear() {

                var node = this.root;
                while (node != null) {
                    var n = node;
                    node = node->next;
                    _free(n);
                }

                this.root = null;
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

        private static readonly Unity.Burst.SharedStatic<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>> allocatorTempBurst = Unity.Burst.SharedStatic<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsTempAllocator>(TAlign<Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>>.align, 10005);
        internal static ref Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator> allocatorTemp => ref allocatorTempBurst.Data;

        private static readonly Unity.Burst.SharedStatic<Unity.Collections.NativeReference<bool>> allocatorTempValidBurst = Unity.Burst.SharedStatic<Unity.Collections.NativeReference<bool>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsPersistentAllocator>(TAlign<Unity.Collections.NativeReference<bool>>.align, 10007);
        internal static bool allocatorTempValid => allocatorTempValidBurst.Data.Value;

        public static Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator> Initialize() {

            var prevMode = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.GetLeakDetectionMode();
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(Unity.Collections.NativeLeakDetectionMode.Disabled);
            allocatorTemp = new Unity.Collections.AllocatorHelper<Unity.Collections.RewindableAllocator>(Constants.ALLOCATOR_PERSISTENT);
            allocatorTemp.Allocator.Initialize(128 * 1024, false);
            allocatorTempValidBurst.Data = new Unity.Collections.NativeReference<bool>(true, Constants.ALLOCATOR_PERSISTENT);
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SetLeakDetectionMode(prevMode);

            return allocatorTemp;

        }

        public static void Dispose() {
            
            if (allocatorTempValidBurst.Data.IsCreated == false || allocatorTempValidBurst.Data.Value == false) return;
            allocatorTempValidBurst.Data.Value = false;
            allocatorTempValidBurst.Data.Dispose();
            allocatorTemp.Dispose();
            
        }

        public static void Reset() {
            
            allocatorTemp.Allocator.Rewind();
            
        }

    }

    public unsafe struct Worlds {

        private static readonly Unity.Burst.SharedStatic<ushort> worldsCounterBurst = Unity.Burst.SharedStatic<ushort>.GetOrCreate<Worlds>();
        private static ref ushort counter => ref worldsCounterBurst.Data;

        public static uint MaxWorldId => counter;

        public static void Initialize() {

            #if UNITY_EDITOR
            UnityEngine.Application.quitting += OnQuit;
            #endif
            
            if (WorldsStorage.worlds.Length > 0u) Dispose();
            WorldsTempAllocator.Initialize();
            WorldsPersistentAllocator.Initialize();
            WorldsDomainAllocator.Initialize();

            if (WorldsStorage.worlds.Length > 0u) WorldsStorage.worlds.Dispose();
            ResetWorldsCounter();

        }

        private static void OnQuit() {
            #if UNITY_EDITOR
            UnityEngine.Application.quitting -= OnQuit;
            #endif

            Dispose();
        }

        public static void Dispose() {

            WorldsTempAllocator.Dispose();
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
            return WorldsStorage.worlds.Get(id).world.state != null;

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

            ref var worldIds = ref WorldsIdStorage.worldIds;
            worldIds.Add(worldId);

        }

        [INLINE(256)]
        public static Unity.Collections.FixedString64Bytes GetWorldName(ushort worldId) {
            
            ref var worldsStorage = ref WorldsStorage.worlds;
            if (worldId >= worldsStorage.Length) return default;
            return worldsStorage.Get(worldId).name;

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
            
            if (raiseCallback == true) WorldStaticCallbacks.RaiseCallback(ref world);

        }

        [INLINE(256)]
        internal static void ReleaseWorld(in World world) {

            Worlds.ReleaseWorldId(world.id);
            ref var worldsStorage = ref WorldsStorage.worlds;
            worldsStorage.Get(world.id) = default;
            
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