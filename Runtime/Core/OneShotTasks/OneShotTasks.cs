namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct OneShotTasks {

        private struct ThreadItem {

            public List<Task> items;

        }
        
        private MemArrayThreadCacheLine<ThreadItem> threadItems;

        [INLINE(256)]
        public void Add<T>(State* state, in Ent ent, in T data, OneShotType type) where T : unmanaged {

            E.IS_IN_TICK(state);
            var dataPtr = new MemAllocatorPtr();
            if (type == OneShotType.NextTick) dataPtr.Set(ref state->allocator, in data);
            this.Add(state, in ent, StaticTypes<T>.typeId, StaticTypes<T>.groupId, dataPtr, type);

        }

        [INLINE(256)]
        public void Add(State* state, in Ent ent, uint typeId, uint groupId, MemAllocatorPtr data, OneShotType type) {

            E.IS_IN_TICK(state);
            var threadIndex = Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex;
            Journal.SetOneShotComponent(ent.World.id, in ent, typeId, type);
            this.threadItems[state, threadIndex].items.Add(ref state->allocator, new Task() {
                typeId = typeId,
                groupId = groupId,
                ent = ent,
                type = type,
                data = data,
            });

        }

        [INLINE(256)]
        [NotThreadSafe]
        public void ResolveTasks(State* state, OneShotType type) {

            for (uint i = 0; i < this.threadItems.Length; ++i) {
                
                ref var threadItem = ref this.threadItems[state, i];
                for (int j = (int)threadItem.items.Count - 1; j >= 0; --j) {

                    var item = threadItem.items[state, (uint)j];
                    if (item.type == type) {
                        switch (type) {
                            case OneShotType.CurrentTick:
                                // if we processing current tick - remove component
                                Journal.ResolveOneShotComponent(item.ent.World.id, in item.ent, item.typeId, type);
                                state->batches.Remove(item.ent.id, item.ent.gen, item.typeId, item.groupId, state);
                                break;

                            case OneShotType.NextTick:
                                Journal.ResolveOneShotComponent(item.ent.World.id, in item.ent, item.typeId, type);
                                // if we processing begin of tick - add component
                                state->batches.Set(item.ent.id, item.ent.gen, item.typeId, item.GetData(state), state);
                                // add new task to remove at the end of the tick
                                this.Add(state, in item.ent, item.typeId, item.groupId, default, OneShotType.CurrentTick);
                                item.Dispose(ref state->allocator);
                                break;
                        }
                        threadItem.items.RemoveAtFast(in state->allocator, (uint)j);
                    }

                }

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