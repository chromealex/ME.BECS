using ME.BECS.Jobs;
using Unity.Collections;

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using System.Runtime.InteropServices;
    using static Cuts;
    
    public unsafe partial struct OneShotTasks {

        [StructLayout(LayoutKind.Sequential)]
        private struct ThreadItem {

            [StructLayout(LayoutKind.Sequential)]
            public struct TaskCollection {

                public List<Task> items;
                public LockSpinner lockIndex;

            }

            public TaskCollection currentTick;
            public TaskCollection nextTick;

        }
        
        private MemArrayThreadCacheLine<ThreadItem> threadItems;

        [INLINE(256)]
        [NotThreadSafe]
        public static OneShotTasks Create(safe_ptr<State> state, uint capacity) {
            var tasks = new OneShotTasks() {
                threadItems = new MemArrayThreadCacheLine<ThreadItem>(ref state.ptr->allocator),
            };
            for (uint i = 0; i < tasks.threadItems.Length; ++i) {
                ref var threadItem = ref tasks.threadItems[state, i];
                threadItem.currentTick = new ThreadItem.TaskCollection() { items = new List<Task>(ref state.ptr->allocator, capacity) };
                threadItem.nextTick = new ThreadItem.TaskCollection() { items = new List<Task>(ref state.ptr->allocator, capacity) };
            }
            return tasks;
        }

        [INLINE(256)]
        public static void Add<T>(safe_ptr<State> state, in Ent ent, in T data, ushort updateType, OneShotType type) where T : unmanaged, IComponent {

            E.IS_IN_TICK(state);
            var dataPtr = new MemAllocatorPtr();
            if (type == OneShotType.NextTick) dataPtr.Set(ref state.ptr->allocator, in data);
            Add(state, in ent, StaticTypes<T>.typeId, StaticTypes<T>.groupId, updateType, dataPtr, type);

        }

        [INLINE(256)]
        public static void Add(safe_ptr<State> state, in Ent ent, uint typeId, uint groupId, ushort updateType, MemAllocatorPtr data, OneShotType type) {

            E.IS_IN_TICK(state);
            var threadIndex = JobUtils.ThreadIndex;
            Journal.SetOneShotComponent(in ent, typeId, type);
            ref var threadItem = ref state.ptr->oneShotTasks.threadItems[state, threadIndex];
            var collection = _addressT(ref threadItem.currentTick);
            if (type == OneShotType.NextTick) {
                collection = _addressT(ref threadItem.nextTick);
            }

            {
                collection.ptr->lockIndex.Lock();
                collection.ptr->items.Add(ref state.ptr->allocator, new Task() {
                    typeId = typeId,
                    groupId = groupId,
                    ent = ent,
                    type = type,
                    data = data,
                    updateType = updateType,
                });
                collection.ptr->lockIndex.Unlock();
            }
            
        }

        [INLINE(256)]
        [NotThreadSafe]
        public static JobHandle Schedule(safe_ptr<State> state, OneShotType type, ushort updateType, JobHandle dependsOn) {

            E.THREAD_CHECK(nameof(ScheduleJobs));

            var job = new ResolveTasksParallelJob() {
                state = state,
                type = type,
                updateType = updateType,
            };
            var handle = job.Schedule((int)state.ptr->oneShotTasks.threadItems.Length, 1, dependsOn);
            
            return handle;

        }
        
        [INLINE(256)]
        public static void ResolveThread(safe_ptr<State> state, OneShotType type, ushort updateType, uint index) {

            ref var threadItem = ref state.ptr->oneShotTasks.threadItems[state, index];
            var collection = _addressT(ref threadItem.currentTick);
            if (type == OneShotType.NextTick) {
                collection = _addressT(ref threadItem.nextTick);
            }
            collection.ptr->lockIndex.Lock();
            for (uint j = collection.ptr->items.Count; j > 0u; --j) {

                var idx = j - 1u;
                var item = collection.ptr->items[state, idx];
                if (item.type == type && item.updateType == updateType) {
                    switch (type) {
                        case OneShotType.CurrentTick:
                            // if we are processing current tick - remove component
                            if (item.ent.IsAlive() == true) {
                                Journal.ResolveOneShotComponent(in item.ent, item.typeId, type);
                                item.ent.Remove(item.typeId);
                            }
                            item.Dispose(ref state.ptr->allocator);
                            break;

                        case OneShotType.NextTick:
                            if (item.ent.IsAlive() == true) {
                                {
                                    Journal.ResolveOneShotComponent(in item.ent, item.typeId, type);
                                    // if we are processing begin of tick - add component
                                    Batches.Set(in item.ent, item.typeId, item.GetData(state).ptr, state);
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
                                    _addressT(ref threadItem.currentTick).ptr->items.Add(ref state.ptr->allocator, newTask);
                                }
                                item.Dispose(ref state.ptr->allocator);
                            }
                            break;
                    }
                    collection.ptr->items.RemoveAtFast(in state.ptr->allocator, idx);
                }

            }
            collection.ptr->lockIndex.Unlock();

        }

    }

}