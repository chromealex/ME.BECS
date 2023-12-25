using Unity.Collections.LowLevel.Unsafe;

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Unity.Collections;

    public struct PoolTypeInfo<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<PoolTypeInfo<T>>();
        public static ref uint typeId => ref value.Data;

    }

    public struct PoolTypeInfo {

        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<PoolTypeInfo>();
        public static ref uint counter => ref counterBurst.Data;

    }

    public struct PoolsLock {

        public static readonly Unity.Burst.SharedStatic<LockSpinner> lockIndex = Unity.Burst.SharedStatic<LockSpinner>.GetOrCreate<PoolsLock>();

    }

    /*public unsafe struct Pools {

        private struct StackItem {

            public void* data;
            public uint elementsCount;

        }
        
        private struct Item {

            [NativeDisableUnsafePtrRestriction]
            public StackItem* stack;
            public uint count;
            public LockSpinner lockIndex;

        }

        private struct ThreadItem {

            public NativeArray<Item> items;

        }

        private const uint MAX_CAPACITY = 1000u;
        private const int MAX_ITEMS_CAPACITY = 50;
        private static readonly Unity.Burst.SharedStatic<NativeArray<ThreadItem>> itemsPerThread = Unity.Burst.SharedStatic<NativeArray<ThreadItem>>.GetOrCreate<Pools>();

        #if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethodAttribute]
        #endif
        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            Pools.itemsPerThread.Data = new NativeArray<ThreadItem>(Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndexCount, Cuts.ALLOCATOR);
            
        }

        [INLINE(256)]
        public static void Push<T>(ref T* ptr) where T : unmanaged {
            Push(ptr, 1u);
            ptr = null;
        }

        [INLINE(256)]
        public static void Push<T>(T* ptr) where T : unmanaged {
            Push(ptr, 1u);
        }

        [INLINE(256)]
        public static void Push<T>(T* ptr, uint elementsCount) where T : unmanaged {

            if (PoolTypeInfo<T>.typeId == 0u) {
                JobUtils.Lock(ref PoolsLock.lockIndex.Data);
                if (PoolTypeInfo<T>.typeId == 0u) {
                    PoolTypeInfo<T>.typeId = ++PoolTypeInfo.counter;
                }
                JobUtils.Unlock(ref PoolsLock.lockIndex.Data);
            }

            const int threadIndex = 0;
            var idx = PoolTypeInfo<T>.typeId;
            ref var threadItem = ref *((ThreadItem*)Pools.itemsPerThread.Data.GetUnsafePtr() + threadIndex);
            if (threadItem.items.IsCreated == false) {
                JobUtils.Lock(ref PoolsLock.lockIndex.Data);
                if (threadItem.items.IsCreated == false) {
                    threadItem.items = new NativeArray<Item>(MAX_ITEMS_CAPACITY, ALLOCATOR);
                    for (int i = 0; i < threadItem.items.Length; ++i) {
                        ref var item = ref *((Item*)threadItem.items.GetUnsafePtr() + i);
                        item.stack = _makeArray<StackItem>(Pools.MAX_CAPACITY);
                        item.count = 0u;
                    }
                }
                JobUtils.Unlock(ref PoolsLock.lockIndex.Data);
            }

            if (idx >= threadItem.items.Length) {
                _free(ptr);
                return;
            }
            
            ref var itemsStack = ref *((Item*)threadItem.items.GetUnsafePtr() + idx);
            if (itemsStack.count >= Pools.MAX_CAPACITY) {
                _free(ptr);
                return;
            }

            JobUtils.Lock(ref itemsStack.lockIndex);
            if (itemsStack.stack == null) {
                itemsStack.stack = _makeArray<StackItem>(Pools.MAX_CAPACITY);
                itemsStack.count = 0u;
            }
            var stackItem = itemsStack.stack + itemsStack.count;
            stackItem->data = ptr;
            stackItem->elementsCount = elementsCount;
            ++itemsStack.count;
            JobUtils.Unlock(ref itemsStack.lockIndex);

        }

        [INLINE(256)]
        public static T* Pop<T>() where T : unmanaged {
            return Pop<T>(default);
        }

        [INLINE(256)]
        public static T* Pop<T>(uint elementsCount) where T : unmanaged {
            return Pop<T>(default, elementsCount);
        }

        [INLINE(256)]
        public static T* Pop<T>(in T data) where T : unmanaged {
            return Pop<T>(in data, 1u);
        }

        [INLINE(256)]
        public static T* Pop<T>(in T data, uint elementsCount) where T : unmanaged {

            if (PoolTypeInfo<T>.typeId == 0u) {
                JobUtils.Lock(ref PoolsLock.lockIndex.Data);
                if (PoolTypeInfo<T>.typeId == 0u) {
                    PoolTypeInfo<T>.typeId = ++PoolTypeInfo.counter;
                }
                JobUtils.Unlock(ref PoolsLock.lockIndex.Data);
            }

            const int threadIndex = 0;
            var idx = PoolTypeInfo<T>.typeId;
            ref var threadItem = ref *((ThreadItem*)Pools.itemsPerThread.Data.GetUnsafePtr() + threadIndex);
            if (threadItem.items.IsCreated == false) {
                return _makeArray(in data, elementsCount);
            }

            if (idx >= threadItem.items.Length) {
                return _makeArray(in data, elementsCount);
            }
            
            ref var item = ref *((Item*)threadItem.items.GetUnsafePtr() + idx);
            if (item.stack == null) {
                return _makeArray(in data, elementsCount);
            }

            if (item.count > 0u) {

                JobUtils.Lock(ref item.lockIndex);
                --item.count;
                var elem = item.stack + item.count;
                var ptr = (T*)elem->data;
                if (elementsCount > elem->elementsCount) {
                    _free(ref ptr);
                    ptr = _makeArray(in data, elementsCount);
                } else {
                    *ptr = data;
                }
                JobUtils.Unlock(ref item.lockIndex);

                return ptr;

            }

            return _makeArray(in data, elementsCount);

        }

    }*/

}