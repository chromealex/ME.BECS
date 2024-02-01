namespace ME.BECS {

    using static Cuts;
    using Unity.Jobs;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public class RequiredDependenciesAttribute : System.Attribute {

        public System.Type[] types;

        public RequiredDependenciesAttribute(params System.Type[] types) {
            this.types = types;
        }

    }

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

        public ushort updateType;
        public int graphId;
        private GroupData* data;
        internal ref Node* rootNode => ref this.data->rootNode;
        internal ref NodeData* nodes => ref this.data->nodes;
        internal ref uint index => ref this.data->index;
        internal ref uint count => ref this.data->count;
        private Queue runtimeQueue;

        [INLINE(256)]
        public static SystemGroup Create(ushort updateType = 0) {
            var group = new SystemGroup() {
                data = _make(new GroupData() {
                    rootNode = _make(new Node()),
                }),
                updateType = updateType,
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
        public JobHandle DrawGizmos(ref World world, JobHandle dependsOn = default) {

            return this.Run(ref world, 0f, Method.DrawGizmos, 0, dependsOn);

        }

        [INLINE(256)]
        public JobHandle Awake(ref World world, ushort subId = 0, JobHandle dependsOn = default) {

            return this.Run(ref world, 0f, Method.Awake, subId, dependsOn);

        }

        [INLINE(256)]
        public JobHandle Update(ref World world, float dt, ushort subId = 0, JobHandle dependsOn = default) {

            return this.Run(ref world, dt, Method.Update, subId, dependsOn);

        }

        [INLINE(256)]
        public JobHandle Destroy(ref World world, ushort subId = 0, JobHandle dependsOn = default) {
            
            return this.Run(ref world, 0f, Method.Destroy, subId, dependsOn);
            
        }

        internal static string DebugParent(Node* node) {
            var str = string.Empty;
            if (node->parentIndex == 1u) {
                str += node->parents[0].data->name;
            } else if (node->parentIndex == 2u) {
                str += node->parents[0].data->name + ", ";
                str += node->parents[1].data->name;
            } else if (node->parentIndex == 3u) {
                str += node->parents[0].data->name + ", ";
                str += node->parents[1].data->name + ", ";
                str += node->parents[2].data->name;
            } else if (node->parentIndex > 3u) {
                for (uint i = 0; i < node->parentIndex; ++i) {
                    str += node->parents[i].data->name + ", ";
                }
            }

            return str;
        }

        internal static string DebugHandle(JobHandle jobHandle) {
            var field = jobHandle.GetType().GetField("debugInfo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return ((int)(System.IntPtr)field.GetValue(jobHandle)).ToString();
        }

        [INLINE(256)]
        private JobHandle Run(ref World world, float dt, Method method, ushort subId, JobHandle dependsOn = default) {
            
            var list = new UnsafeList<NodeData>((int)this.index, Constants.ALLOCATOR_TEMP);
            ref var queue = ref this.runtimeQueue;
            queue.Clear();
            queue.Enqueue(this.rootNode);
            this.rootNode->dependsOn = Batches.Apply(dependsOn, world.state);
            try {
                while (queue.Count > 0) {

                    var node = queue.Dequeue();
                    
                    if (node->HasMethod(method) == true) {
                        if (node->isStarted == false) {
                            node->isStarted = true;
                            if (node->AllParentsStarted(method) == true) {
                                var dj = node->GetJobHandle();
                                var context = SystemContext.Create(dt, world, dj);
                                //context.SetDependency(Batches.Apply(context.dependsOn, world.state));
                                Journal.UpdateSystemStarted(world.id, node->name);
                                //UnityEngine.Debug.Log("Started: " + node->name + " :: " + DebugHandle(dj) + " => " + DebugHandle(node->dependsOn) + " :: " + DebugParent(node) + ", subId: " + subId);
                                list.Add(new NodeData() { data = node });
                                this.RunSystem(node->systemData, node->GetMethod(method), ref context);
                                Journal.UpdateSystemEnded(world.id, node->name);
                                node->dependsOn = Batches.Apply(context.dependsOn, world.state);
                                //UnityEngine.Debug.Log("Ended: " + node->name + " :: " + DebugHandle(dj) + " => " + DebugHandle(node->dependsOn));
                            } else {
                                node->isStarted = false;
                                queue.Enqueue(node);
                            }
                        }
                    } else if (node->graph != null) {
                        // Check if subId is valid for this graph
                        if (subId > 0 && node->graph->updateType > 0 && node->graph->updateType != subId) continue;
                        if (node->isStarted == false) {
                            var dj = node->GetJobHandle();
                            //UnityEngine.Debug.Log("Graph: " + node->name + " :: " + DebugHandle(dj));
                            node->isStarted = true;
                            node->dependsOn = node->graph->Run(ref world, dt, method, subId, dj);
                            list.Add(new NodeData() { data = node });
                        }
                    } else {
                        list.Add(new NodeData() { data = node });
                        node->dependsOn = node->GetJobHandle();
                        //UnityEngine.Debug.Log("Skip: " + node->name + " :: " + DebugHandle(node->dependsOn));
                    }

                    for (uint i = 0; i < node->childrenIndex; ++i) {
                        var child = node->children[i].data;
                        if (child->isStarted == false) queue.Enqueue(child);
                    }

                }
            } catch (System.Exception ex) {
                Logger.Core.Exception(ex, showCallstack: true);
            }
            
            var arrDepends = new Unity.Collections.NativeArray<JobHandle>(list.Length + 1, Constants.ALLOCATOR_TEMP);
            {
                int i = 0;
                for (i = 0; i < list.Length; ++i) {
                    list[i].data->isStarted = false;
                    //UnityEngine.Debug.Log("Complete: " + DebugHandle(list[i].data->dependsOn) + " :: " + list[i].data->dependsOn.IsCompleted + " :: " + list[i].data->name);
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