using ME.BECS.Jobs;
using Unity.Collections;

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using System.Runtime.InteropServices;
    using static Cuts;
    
    public unsafe partial struct OneShotTasks {

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct ThreadItem {

            [StructLayout(LayoutKind.Explicit, Size = TaskCollection.SIZE)]
            public struct TaskCollection {

                public const int SIZE = 20;

                [FieldOffset(0)]
                public List<Task> items;
                [FieldOffset(List<Task>.SIZE)]
                public LockSpinner lockIndex;

            }

            [FieldOffset(0)]
            public TaskCollection currentTick;
            [FieldOffset(TaskCollection.SIZE)]
            public TaskCollection nextTick;

        }
        
        private MemArrayThreadCacheLine<ThreadItem> threadItems;

        [INLINE(256)]
        [NotThreadSafe]
        public static OneShotTasks Create(State* state, uint capacity) {
            var tasks = new OneShotTasks() {
                threadItems = new MemArrayThreadCacheLine<ThreadItem>(ref state->allocator),
            };
            for (uint i = 0; i < tasks.threadItems.Length; ++i) {
                ref var threadItem = ref tasks.threadItems[state, i];
                threadItem.currentTick = new ThreadItem.TaskCollection() { items = new List<Task>(ref state->allocator, capacity) };
                threadItem.nextTick = new ThreadItem.TaskCollection() { items = new List<Task>(ref state->allocator, capacity) };
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
            var collection = _addressT(ref threadItem.currentTick);
            if (type == OneShotType.NextTick) {
                collection = _addressT(ref threadItem.nextTick);
            }

            {
                collection->lockIndex.Lock();
                collection->items.Add(ref state->allocator, new Task() {
                    typeId = typeId,
                    groupId = groupId,
                    ent = ent,
                    type = type,
                    data = data,
                    updateType = updateType,
                });
                collection->lockIndex.Unlock();
            }
            
        }

        [INLINE(256)]
        [NotThreadSafe]
        public JobHandle Schedule(State* state, OneShotType type, ushort updateType, JobHandle dependsOn) {

            E.THREAD_CHECK(nameof(this.ScheduleJobs));

            var job = new ResolveTasksParallelJob() {
                state = state,
                type = type,
                updateType = updateType,
            };
            var handle = job.Schedule((int)this.threadItems.Length, 1, dependsOn);
            
            return handle;

        }
        
        [INLINE(256)]
        public void ResolveThread(State* state, OneShotType type, ushort updateType, uint index) {

            ref var threadItem = ref this.threadItems[state, index];
            var collection = _addressT(ref threadItem.currentTick);
            if (type == OneShotType.NextTick) {
                collection = _addressT(ref threadItem.nextTick);
            }
            collection->lockIndex.Lock();
            for (uint j = collection->items.Count; j > 0u; --j) {

                var idx = j - 1u;
                var item = collection->items[state, idx];
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
                                {
                                    Journal.ResolveOneShotComponent(in item.ent, item.typeId, type);
                                    // if we are processing begin of tick - add component
                                    state->batches.Set(in item.ent, item.typeId, item.GetData(state), state);
                                    // add new task to remove at the end of the tick
                                    var newTask = new Task() {
                                        typeId = item.typeId,
                                        groupId = item.groupId,
                                        ent = item.ent,
                                        type = OneShotType.CurrentTick,
                                        data = default,
                                        updateType = item.updateType,
                                    };
                                    Journal.SetOneShotComponent(in item.ent, item.typeId, OneShotType.CurrentTick);
                                    _addressT(ref threadItem.currentTick)->items.Add(ref state->allocator, newTask);
                                }
                                item.Dispose(ref state->allocator);
                            }
                            break;
                    }
                    collection->items.RemoveAtFast(in state->allocator, idx);
                }

            }
            collection->lockIndex.Unlock();

        }

    }

}