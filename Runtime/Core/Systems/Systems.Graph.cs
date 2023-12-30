namespace ME.BECS {

    using static Cuts;
    using Unity.Jobs;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public readonly struct SystemHandle {

        internal readonly uint id;

        [INLINE(256)]
        public readonly bool IsValid() => this.id > 0u;

        [INLINE(256)]
        private SystemHandle(uint id) {
            this.id = id;
        }

        [INLINE(256)]
        public static SystemHandle Create(uint id) {
            return new SystemHandle(id);
        }

        [INLINE(256)]
        public override string ToString() {
            return $"ID: {this.id}";
        }

    }

    public unsafe struct SystemGroup {

        internal struct GroupData {

            public Node* rootNode;
            public NodeData* nodes;
            public uint index;
            public uint count;
            
        }
        
        internal struct NodeData {

            public Node* data;

        }

        private GroupData* data;
        internal ref Node* rootNode => ref this.data->rootNode;
        internal ref NodeData* nodes => ref this.data->nodes;
        internal ref uint index => ref this.data->index;
        internal ref uint count => ref this.data->count;
        private Queue runtimeQueue;

        [INLINE(256)]
        public static SystemGroup Create() {
            var group = new SystemGroup() {
                data = _make(new GroupData() {
                    rootNode = _make(new Node()),
                }),
            };
            return group;
        }

        [INLINE(256)]
        internal void RegisterNode(Node* node) {
            
            if (this.index >= this.count) {
                var newLength = (this.index + 1u) * 2u;
                _resizeArray(ref this.nodes, ref this.count, newLength);
            }

            this.nodes[this.index++] = new NodeData() { data = node };
            node->id = this.index;

        }

        [INLINE(256)]
        internal ref Node* GetNode(in SystemHandle dependsOn) {

            return ref this.GetNode(dependsOn.id);

        }

        [INLINE(256)]
        internal ref Node* GetNode(uint id) {
            
            var idx = id - 1u;
            E.RANGE(idx, 0u, this.index);
            return ref this.nodes[idx].data;

        }

        [INLINE(256)]
        public SystemHandle Combine(SystemHandle handle1, SystemHandle handle2) {

            var node = Node.Create(isPointer: true);
            if (handle1.IsValid() == true) node->AddDependency(this.GetNode(handle1.id));
            if (handle2.IsValid() == true) node->AddDependency(this.GetNode(handle2.id));
            this.RegisterNode(node);
            return SystemHandle.Create(node->id);
            
        }

        [INLINE(256)]
        public SystemHandle Combine(SystemHandle handle1, SystemHandle handle2, SystemHandle handle3) {

            var node = Node.Create(isPointer: true);
            if (handle1.IsValid() == true) node->AddDependency(this.GetNode(handle1.id));
            if (handle2.IsValid() == true) node->AddDependency(this.GetNode(handle2.id));
            if (handle3.IsValid() == true) node->AddDependency(this.GetNode(handle3.id));
            this.RegisterNode(node);
            return SystemHandle.Create(node->id);
            
        }

        [INLINE(256)]
        public SystemHandle Combine(System.Collections.Generic.List<SystemHandle> handles) {

            var node = Node.Create(isPointer: true);
            foreach (var handle in handles) {
                if (handle.IsValid() == true) node->AddDependency(this.GetNode(handle.id));    
            }
            this.RegisterNode(node);
            return SystemHandle.Create(node->id);
            
        }

        [INLINE(256)]
        private void RunSystem(void* systemData, Node.Data* methodData, ref SystemContext context) {
            
            // call
            var func = new Unity.Burst.FunctionPointer<FunctionPointerDelegate>((System.IntPtr)methodData->methodPtr);
            func.Invoke(systemData, ref context);
            
        }

        [INLINE(256)]
        public JobHandle Awake(ref World world, JobHandle dependsOn = default) {

            return this.Run(ref world, 0f, Method.Awake, dependsOn);

        }

        [INLINE(256)]
        public JobHandle Update(ref World world, float dt, JobHandle dependsOn = default) {

            return this.Run(ref world, dt, Method.Update, dependsOn);

        }

        [INLINE(256)]
        public JobHandle Destroy(ref World world, JobHandle dependsOn = default) {
            
            return this.Run(ref world, 0f, Method.Destroy, dependsOn);
            
        }

        private static string Debug(JobHandle jobHandle) {
            var field = jobHandle.GetType().GetField("debugInfo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return ((int)(System.IntPtr)field.GetValue(jobHandle)).ToString();
        }

        [INLINE(256)]
        private JobHandle Run(ref World world, float dt, Method method, JobHandle dependsOn = default) {
            
            var list = new UnsafeList<NodeData>((int)this.index, Constants.ALLOCATOR_TEMP);
            ref var queue = ref this.runtimeQueue;
            queue.Clear();
            queue.Enqueue(this.rootNode);
            this.rootNode->dependsOn = Batches.Apply(dependsOn, world.state);
            while (queue.Count > 0) {

                var node = queue.Dequeue();
                if (node->HasMethod(method) == true) {
                    if (node->isStarted == false) {
                        node->isStarted = true;
                        if (node->AllParentsStarted(method) == true) {
                            var context = SystemContext.Create(dt, world, node->GetJobHandle());
                            context.SetDependency(Batches.Apply(context.dependsOn, world.state));
                            Journal.UpdateSystemStarted(world.id, node->name);
                            this.RunSystem(node->systemData, node->GetMethod(method), ref context);
                            Journal.UpdateSystemEnded(world.id, node->name);
                            node->dependsOn = Batches.Apply(context.dependsOn, world.state);
                            list.Add(new NodeData() { data = node });
                        } else {
                            node->isStarted = false;
                            queue.Enqueue(node);
                        }
                    }
                } else if (node->graph != null) {
                    if (node->isStarted == false) {
                        node->isStarted = true;
                        node->dependsOn = node->graph->Run(ref world, dt, method, node->GetJobHandle());
                        list.Add(new NodeData() { data = node });
                    }
                } else {
                    list.Add(new NodeData() { data = node });
                    node->dependsOn = node->GetJobHandle();
                }

                for (uint i = 0; i < node->childrenIndex; ++i) {
                    var child = node->children[i].data;
                    queue.Enqueue(child);
                }

            }

            var arrDepends = new Unity.Collections.NativeArray<JobHandle>(list.Length + 1, Constants.ALLOCATOR_TEMP);
            {
                int i = 0;
                for (i = 0; i < list.Length; ++i) {
                    list[i].data->isStarted = false;
                    arrDepends[i] = list[i].data->dependsOn;
                }
                arrDepends[i] = dependsOn;
            }
            
            var resultDepends = JobHandle.CombineDependencies(arrDepends);
            JobUtils.RunScheduled();
            return resultDepends;

        }

        [INLINE(256)]
        public void Dispose() {

            {
                // To be sure all jobs has been complete
                for (int i = 0; i < this.index; ++i) {
                    var node = this.nodes[i];
                    if (node.data->isPointer == true) continue;
                    node.data->dependsOn.Complete();
                }
            }

            for (int i = 0; i < this.index; ++i) {
                var node = this.nodes[i];
                node.data->Dispose();
                _free(node.data);
            }

            this.rootNode->Dispose();
            _free(this.rootNode);
            _free(this.nodes);
            this.runtimeQueue.Dispose();

        }

    }

}