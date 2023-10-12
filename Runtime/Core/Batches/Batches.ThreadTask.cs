namespace ME.BECS {
    
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    using Jobs;
    
    public unsafe partial struct Batches {

        public const byte TASK_TYPE_CREATE_ENTITY = 1;
        public const byte TASK_TYPE_DESTROY_ENTITY = 2;
        public const byte TASK_TYPE_SET = 3;
        public const byte TASK_TYPE_REMOVE = 4;

        public const int THREAD_TASK_LIST_CAPACITY = 100;
        
        public struct ThreadTaskRemove {

            public uint typeId;
            public uint groupId;
            public uint entId;
            public ushort entGen;
            
        }

        public struct ThreadTaskSet {

            public uint typeId;
            public uint groupId;
            public uint entId;
            public ushort entGen;
            public void* data;
            public uint dataSize;

        }

        public struct ThreadTaskCreateEntity {

            public uint entId;
            public ushort entGen;
            
        }

        public struct ThreadTaskDestroyEntity {

            public uint entId;
            
        }

        public struct Task {

            public byte taskType; // TASK_TYPE_*
            public void* taskData;

            public void Dispose() => _free(ref this.taskData);

        }

        public struct ThreadTaskList {

            public struct Node {

                public UnsafeList<Task> list;
                public Node* next;

            }

            // root to enumerate all items
            public Node* root;
            // head to add new nodes
            public Node* head;
            
            // rover indicates which node we are read now
            public Node* rover;
            // roverIndex indicates from which index we have to start
            public int roverIndex;

            // Change counter after all elements are set
            // indicates how much elements being unread
            public int Count;
            public int CreateDestroyCount;

            // This method run on each thread
            // so we don't care about thread-safe write
            // but we have keep in mind that Apply job can read this struct 
            public void Add(in Task task) {

                if (this.root == null) {
                    this.root = _make(new Node() {
                        list = new UnsafeList<Task>(THREAD_TASK_LIST_CAPACITY, Constants.ALLOCATOR_PERSISTENT),
                    });
                    this.head = this.root;
                    this.rover = this.root;
                } else {
                    if (this.head->list.Length >= THREAD_TASK_LIST_CAPACITY) {
                        // new head
                        var node = _make(new Node() {
                            list = new UnsafeList<Task>(THREAD_TASK_LIST_CAPACITY, Constants.ALLOCATOR_PERSISTENT),
                        });
                        this.head->next = node;
                        this.head = node;
                    }
                    if (this.rover == null) this.rover = this.head;
                }

                // throw an exception if we need to resize
                this.head->list.AddNoResize(task);
                
                if (task.taskType == TASK_TYPE_CREATE_ENTITY ||
                    task.taskType == TASK_TYPE_DESTROY_ENTITY) {
                    JobUtils.Increment(ref this.CreateDestroyCount);
                }
                JobUtils.Increment(ref this.Count);

            }

            [BURST]
            private struct DisposeJob : IJobSingle {

                [NativeDisableUnsafePtrRestriction]
                public State* state;
                public int index;

                public void Execute() {

                    ref var list = ref this.state->batches.lists[in this.state->allocator, this.index];
                    list.Dispose();

                }

            }

            public void Dispose() {
                var root = this.root;
                while (root != null) {
                    var next = root->next;
                    root->list.Dispose();
                    _free(root);
                    root = next;
                }
                this = default;
            }

            public JobHandle Dispose(State* state, int index, JobHandle dependsOn) {
                return new DisposeJob() {
                    state = state,
                    index = index,
                }.ScheduleSingle(dependsOn);
            }

        }
        
        // ThreadId => ThreadTaskList
        private MemArray<ThreadTaskList> lists;
        private int availableTasksCount;

        private int isThreadWorking;
        private int isThreadCleanUpWorking;
        
        [INLINE(256)]
        public void InitializeThreadTasks(State* state) {

            // Create lists for each thread
            // We don't care about tasks order
            this.lists = new MemArray<ThreadTaskList>(ref state->allocator, (uint)JobsUtility.ThreadIndexCount);

        }

        [BURST]
        public struct BurstModeThreadTasksJob : IJobSingle {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            
            public void Execute() {
                
                //UnityEngine.Debug.Log("FRAME CLOSED BEGIN");
                JobUtils.Lock(ref this.state->batches.isThreadCleanUpWorking);
                {
                    // Clean up all nodes
                    for (int i = 0; i < this.state->batches.lists.Length; ++i) {
                        ref var list = ref this.state->batches.lists[in this.state->allocator, i];
                        list.Dispose();
                    }
                }
                JobUtils.Unlock(ref this.state->batches.isThreadCleanUpWorking);
                //UnityEngine.Debug.Log("FRAME CLOSED END");
                
            }

        }
        
        [INLINE(256)]
        public static JobHandle BurstModeThreadTasks(JobHandle dependsOn, State* state, bool mode) {

            if (mode == true) {
                // Frame starts - support for pointers are open
                // Nothing to do here because we have lazy collections init
            } else {
                dependsOn = new BurstModeThreadTasksJob() {
                    state = state,
                }.ScheduleSingle(dependsOn);
            }

            return dependsOn;

        }

        [INLINE(256)]
        public void ApplyThreadTasks(State* state, ushort worldId) {
            
            //UnityEngine.Debug.Log("ApplyThreadTasks BEGIN: " + state->tick + " :: " + JobsUtility.ThreadIndex + " :: " + this.availableTasksCount);
            // Apply all thread tasks if available
            if (this.availableTasksCount > 0u) {

                JobUtils.Lock(ref this.isThreadWorking);
                var updatedTasks = 0;
                var maxCount = this.availableTasksCount;
                {
                    for (int i = 0; i < this.lists.Length; ++i) {

                        ref var list = ref this.lists[in state->allocator, i];
                        var count = list.Count;
                        var createDestroyCount = list.CreateDestroyCount;
                        if (count > list.roverIndex) {
                            //UnityEngine.Debug.Log("CUR ROVER INDEX: " + list.roverIndex + ", count: " + count);
                            var idx = list.roverIndex;
                            this.ApplyTasks(state, worldId, count, createDestroyCount, ref list.roverIndex, ref list.rover, ref maxCount);
                            updatedTasks += list.roverIndex - idx;
                            if (maxCount == 0) break;
                            //UnityEngine.Debug.Log("NEXT ROVER INDEX: " + list.roverIndex + ", updatedTasks: " + updatedTasks);
                        }

                    }
                    JobUtils.Decrement(ref this.availableTasksCount, updatedTasks);
                }
                JobUtils.Unlock(ref this.isThreadWorking);
                UnityEngine.Assertions.Assert.AreEqual(0, maxCount);

            }
            //UnityEngine.Debug.Log("ApplyThreadTasks END: " + JobsUtility.ThreadIndex);

        }

        [INLINE(256)]
        private void ApplyTasks(State* state, ushort worldId, int count, int createDestroyCount, ref int readCount, ref ThreadTaskList.Node* rover, ref int maxCount) {

            var kCount = count - readCount;
            var tmpReadCount = readCount;
            var kIndex = tmpReadCount % THREAD_TASK_LIST_CAPACITY;
            if (kCount > maxCount) kCount = maxCount;
            tmpReadCount += kCount;
            
            var tmpRover = rover;
            if (createDestroyCount > 0) {
                // Apply create/destroy entities tasks
                // Collect all entities in a batch
                var maxId = 0u;
                var createEntities = UnsafeList<Ent>.Create(count, Constants.ALLOCATOR_TEMP);
                var destroyEntities = UnsafeList<uint>.Create(count, Constants.ALLOCATOR_TEMP);
                var k = kIndex;
                for (int j = 0; j < kCount; ++j) {
                    var task = tmpRover->list[k++];
                    var accepted = false;
                    if (task.taskType == Batches.TASK_TYPE_CREATE_ENTITY) {
                        accepted = true;
                        var taskData = (ThreadTaskCreateEntity*)task.taskData;
                        var ent = new Ent(taskData->entId, taskData->entGen, worldId);
                        createEntities->Add(ent);
                        if (ent.id > maxId) maxId = ent.id;
                    } else if (task.taskType == Batches.TASK_TYPE_DESTROY_ENTITY) {
                        accepted = true;
                        var taskData = (ThreadTaskDestroyEntity*)task.taskData;
                        var entId = taskData->entId;
                        destroyEntities->Add(entId);
                        state->components.ClearShared(state, entId);
                        state->archetypes.RemoveEntity(state, entId);
                    }

                    if (k == THREAD_TASK_LIST_CAPACITY) {
                        // move to next node
                        tmpRover = tmpRover->next;
                        k = 0;
                    }
                    
                    if (accepted == true) {
                        task.Dispose();
                        --maxCount;
                    }
                }

                if (destroyEntities->Length > 0) {
                    // Apply batch
                    state->entities.RemoveTaskComplete(state, destroyEntities);
                }

                if (createEntities->Length > 0) {
                    // Apply batch
                    state->entities.Initialize(state, createEntities, maxId);
                    state->archetypes.AddEntity(state, createEntities, maxId);
                }

                createEntities->Dispose();
                destroyEntities->Dispose();
            }

            tmpRover = rover;

            if (maxCount > 0) {
                // Apply Set/Remove components tasks
                var k = kIndex;
                for (int j = 0; j < kCount; ++j) {
                    var task = tmpRover->list[k++];
                    if (task.taskType == TASK_TYPE_SET) {
                        var taskData = (ThreadTaskSet*)task.taskData;
                        if (state->components.SetUnknownType(state, taskData->typeId, taskData->groupId, taskData->entId, taskData->entGen, taskData->data) == true) {
                            this.Set_INTERNAL(taskData->typeId, taskData->entId, state);
                        }

                        _free(ref taskData->data);
                    } else if (task.taskType == TASK_TYPE_REMOVE) {
                        var taskData = (ThreadTaskRemove*)task.taskData;
                        if (state->components.RemoveUnknownType(state, taskData->typeId, taskData->groupId, taskData->entId, taskData->entGen) == true) {
                            this.Remove_INTERNAL(taskData->typeId, taskData->entId, state);
                        }
                    }

                    task.Dispose();
                    if (k == THREAD_TASK_LIST_CAPACITY) {
                        // move to next node
                        tmpRover = tmpRover->next;
                        k = 0;
                    }
                    --maxCount;
                }
            }

            rover = tmpRover;
            readCount = tmpReadCount;
            
        }

        [INLINE(256)]
        public void AddThreadTask(State* state, ThreadTaskCreateEntity task) {
            
            ref var list = ref this.lists[in state->allocator, JobsUtility.ThreadIndex];
            list.Add(new Task() { taskData = _make(task), taskType = TASK_TYPE_CREATE_ENTITY });
            JobUtils.Increment(ref this.availableTasksCount);

        }

        [INLINE(256)]
        public void AddThreadTask(State* state, ThreadTaskDestroyEntity task) {
            
            ref var list = ref this.lists[in state->allocator, JobsUtility.ThreadIndex];
            list.Add(new Task() { taskData = _make(task), taskType = TASK_TYPE_DESTROY_ENTITY });
            JobUtils.Increment(ref this.availableTasksCount);
            
        }

        [INLINE(256)]
        public void AddThreadTask(State* state, ThreadTaskSet task) {

            ref var list = ref this.lists[in state->allocator, JobsUtility.ThreadIndex];
            list.Add(new Task() { taskData = _make(task), taskType = TASK_TYPE_SET });
            JobUtils.Increment(ref this.availableTasksCount);
            
        }

        [INLINE(256)]
        public void AddThreadTask(State* state, ThreadTaskRemove task) {
            
            ref var list = ref this.lists[in state->allocator, JobsUtility.ThreadIndex];
            list.Add(new Task() { taskData = _make(task), taskType = TASK_TYPE_REMOVE });
            JobUtils.Increment(ref this.availableTasksCount);
            
        }

    }

}