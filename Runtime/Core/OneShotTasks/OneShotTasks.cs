using ME.BECS.Jobs;
using Unity.Collections;

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using System.Runtime.InteropServices;
    
    public unsafe partial struct OneShotTasks {

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct ThreadItem {

            [FieldOffset(0)]
            public List<Task> items;
            [FieldOffset(List<Task>.SIZE)]
            public LockSpinner lockIndex;

        }
        
        private MemArrayThreadCacheLine<ThreadItem> threadItems;
        private MemArray<uint> nextTickCount;

        [INLINE(256)]
        [NotThreadSafe]
        public static OneShotTasks Create(State* state, uint capacity) {
            var tasks = new OneShotTasks() {
                threadItems = new MemArrayThreadCacheLine<ThreadItem>(ref state->allocator),
                nextTickCount = new MemArray<uint>(ref state->allocator, UpdateType.MAX),
            };
            for (uint i = 0; i < tasks.threadItems.Length; ++i) {
                ref var threadItem = ref tasks.threadItems[state, i];
                threadItem.items = new List<Task>(ref state->allocator, capacity);
            }
            return tasks;
        }

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
            var threadIndex = JobUtils.ThreadIndex;
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
            if (type == OneShotType.NextTick) JobUtils.Increment(ref this.nextTickCount[state, (uint)updateType]);
            
        }

        [INLINE(256)]
        [NotThreadSafe]
        public JobHandle ResolveTasksJobs(State* state, OneShotType type, ushort updateType, JobHandle dependsOn) {

            E.THREAD_CHECK(nameof(this.ResolveTasksJobs));

            var results = new NativeList<Task>((int)this.nextTickCount[state, (uint)updateType], Allocator.TempJob);
            var job = new ResolveTasksParallelJob() {
                state = state,
                type = type,
                updateType = updateType,
                results = results.AsParallelWriter(),
            };
            var handle = job.Schedule((int)this.threadItems.Length, 1, dependsOn);

            var jobResult = new ResolveTasksComplete() {
                state = state,
                type = type,
                items = results,
            };
            handle = jobResult.Schedule(results, 64, handle);
            handle = results.Dispose(handle);
            
            return handle;

        }
        
        [INLINE(256)]
        public void ResolveThread(State* state, OneShotType type, ushort updateType, uint index, NativeList<Task>.ParallelWriter results) {

            ref var threadItem = ref this.threadItems[state, index];
            threadItem.lockIndex.Lock();
            var removedNextCount = 0u;
            for (uint j = threadItem.items.Count; j > 0u; --j) {

                var idx = j - 1u;
                var item = threadItem.items[state, idx];
                if (item.type == type && item.updateType == updateType) {
                    switch (type) {
                        case OneShotType.CurrentTick:
                            // if we are processing current tick - remove component
                            if (item.ent.IsAlive() == true) {
                                Journal.ResolveOneShotComponent(in item.ent, item.typeId, type);
                                item.ent.Remove(item.typeId);
                            }
                            item.Dispose(ref state->allocator);
                            break;

                        case OneShotType.NextTick:
                            if (item.ent.IsAlive() == true) {
                                results.AddNoResize(item);
                                --removedNextCount;
                            }
                            break;
                    }
                    threadItem.items.RemoveAtFast(in state->allocator, idx);
                }

            }
            threadItem.lockIndex.Unlock();
            if (removedNextCount > 0u) JobUtils.Decrement(ref this.nextTickCount[state, (uint)updateType], removedNextCount);

        }

        [INLINE(256)]
        public void ResolveCompleteThread(State* state, OneShotType type, NativeList<Task> items, int index) {

            var item = items[index];
            {
                Journal.ResolveOneShotComponent(in item.ent, item.typeId, type);
                // if we are processing begin of tick - add component
                state->batches.Set(in item.ent, item.typeId, item.GetData(state), state);
                // add new task to remove at the end of the tick
                this.Add(state, in item.ent, item.typeId, item.groupId, item.updateType, default, OneShotType.CurrentTick);
            }
            item.Dispose(ref state->allocator);

        }

        [INLINE(256)]
        [NotThreadSafe]
        public void ResolveTasks(State* state, OneShotType type, ushort updateType) {

            E.THREAD_CHECK(nameof(ResolveTasks));
            
            var capacity = (int)this.threadItems.Length;
            var temp = new UnsafeList<Task>(capacity, Constants.ALLOCATOR_TEMP);
            var tempContains = new UnsafeHashSet<Task>(capacity, Constants.ALLOCATOR_TEMP);
            for (uint i = 0; i < this.threadItems.Length; ++i) {
                
                ref var threadItem = ref this.threadItems[state, i];
                threadItem.lockIndex.Lock();
                for (uint j = threadItem.items.Count; j > 0u; --j) {

                    var idx = j - 1u;
                    var item = threadItem.items[state, idx];
                    if (item.type == type && item.updateType == updateType) {
                        switch (type) {
                            case OneShotType.CurrentTick:
                                // if we are processing current tick - remove component
                                if (item.ent.IsAlive() == true) {
                                    Journal.ResolveOneShotComponent(in item.ent, item.typeId, type);
                                    item.ent.Remove(item.typeId);
                                }
                                item.Dispose(ref state->allocator);
                                break;

                            case OneShotType.NextTick:
                                if (item.ent.IsAlive() == true && tempContains.Add(item) == true) {
                                    temp.Add(item);
                                }
                                break;
                        }
                        threadItem.items.RemoveAtFast(in state->allocator, idx);
                    }

                }
                threadItem.lockIndex.Unlock();

            }

            foreach (var item in temp) {

                {
                    Journal.ResolveOneShotComponent(in item.ent, item.typeId, type);
                    // if we are processing begin of tick - add component
                    state->batches.Set(in item.ent, item.typeId, item.GetData(state), state);
                    // add new task to remove at the end of the tick
                    this.Add(state, in item.ent, item.typeId, item.groupId, item.updateType, default, OneShotType.CurrentTick);
                }
                item.Dispose(ref state->allocator);

            }

        }

    }

}