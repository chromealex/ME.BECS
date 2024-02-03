using Unity.Collections;

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    
    public unsafe partial struct OneShotTasks {

        private struct ThreadItem {

            public List<Task> items;
            public LockSpinner lockIndex;

        }
        
        private MemArrayThreadCacheLine<ThreadItem> threadItems;

        [INLINE(256)]
        public void Add<T>(State* state, in Ent ent, in T data, ushort updateType, OneShotType type) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            var dataPtr = new MemAllocatorPtr();
            if (type == OneShotType.NextTick) dataPtr.Set(ref state->allocator, in data);
            this.Add(state, in ent, StaticTypes<T>.typeId, StaticTypes<T>.groupId, updateType, dataPtr, type);

        }

        [INLINE(256)]
        public void Add(State* state, in Ent ent, uint typeId, uint groupId, ushort updateType, MemAllocatorPtr data, OneShotType type) {

            E.IS_IN_TICK(state);
            var threadIndex = Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex;
            Journal.SetOneShotComponent(in ent, typeId, type);
            ref var threadItem = ref this.threadItems[state, threadIndex];
            threadItem.lockIndex.Lock();
            threadItem.items.Add(ref state->allocator, new Task() {
                typeId = typeId,
                groupId = groupId,
                ent = ent,
                type = type,
                data = data,
                updateType = updateType,
            });
            threadItem.lockIndex.Unlock();

        }

        [INLINE(256)]
        [NotThreadSafe]
        public void ResolveTasks(State* state, OneShotType type, ushort updateType) {

            E.THREAD_CHECK("ResolveTasks");
            
            var capacity = (int)this.threadItems.Length;
            var temp = new UnsafeList<Task>(capacity, Constants.ALLOCATOR_TEMP);
            var tempContains = new UnsafeHashSet<Task>(capacity, Constants.ALLOCATOR_TEMP);
            for (uint i = 0; i < this.threadItems.Length; ++i) {
                
                ref var threadItem = ref this.threadItems[state, i];
                threadItem.lockIndex.Lock();
                for (int j = (int)threadItem.items.Count - 1; j >= 0; --j) {

                    var item = threadItem.items[state, (uint)j];
                    if (item.type == type && item.updateType == updateType) {
                        switch (type) {
                            case OneShotType.CurrentTick:
                                // if we processing current tick - remove component
                                if (item.ent.IsAlive() == true) {
                                    Journal.ResolveOneShotComponent(in item.ent, item.typeId, type);
                                    state->batches.Remove(in item.ent, item.typeId, item.groupId, state);
                                }
                                item.Dispose(ref state->allocator);
                                break;

                            case OneShotType.NextTick:
                                if (item.ent.IsAlive() == true && tempContains.Add(item) == true) {
                                    temp.Add(item);
                                }
                                break;
                        }
                        threadItem.items.RemoveAtFast(in state->allocator, (uint)j);
                    }

                }
                threadItem.lockIndex.Unlock();

            }

            { // sort
                temp.Sort();
            }

            foreach (var item in temp) {

                {
                    Journal.ResolveOneShotComponent(in item.ent, item.typeId, type);
                    // if we processing begin of tick - add component
                    state->batches.Set(in item.ent, item.typeId, item.GetData(state), state);
                    // add new task to remove at the end of the tick
                    this.Add(state, in item.ent, item.typeId, item.groupId, item.updateType, default, OneShotType.CurrentTick);
                }
                item.Dispose(ref state->allocator);

            }

        }

        [INLINE(256)]
        [NotThreadSafe]
        public static OneShotTasks Create(State* state, uint capacity) {
            var tasks = new OneShotTasks() {
                threadItems = new MemArrayThreadCacheLine<ThreadItem>(ref state->allocator),
            };
            for (uint i = 0; i < tasks.threadItems.Length; ++i) {
                ref var threadItem = ref tasks.threadItems[state, i];
                threadItem.items = new List<Task>(ref state->allocator, capacity);
            }
            return tasks;
        }

    }

}