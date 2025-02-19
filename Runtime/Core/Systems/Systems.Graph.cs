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

            public safe_ptr<Node> rootNode;
            public safe_ptr<NodeData> nodes;
            public uint index;
            public uint count;
            
        }
        
        internal struct NodeData {

            public safe_ptr<Node> data;

        }

        public ushort updateType;
        public int graphId;
        private safe_ptr<GroupData> data;
        internal ref safe_ptr<Node> rootNode => ref this.data.ptr->rootNode;
        internal ref safe_ptr<NodeData> nodes => ref this.data.ptr->nodes;
        internal ref uint index => ref this.data.ptr->index;
        internal ref uint count => ref this.data.ptr->count;
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
        internal void RegisterNode(safe_ptr<Node> node) {
            
            if (this.index >= this.count) {
                var newLength = (this.index + 1u) * 2u;
                _resizeArray(ref this.nodes, ref this.count, newLength);
            }

            this.nodes[this.index++] = new NodeData() { data = node };
            node.ptr->id = this.index;

        }

        [INLINE(256)]
        internal ref safe_ptr<Node> GetNode(in SystemHandle dependsOn) {

            return ref this.GetNode(dependsOn.id);

        }

        [INLINE(256)]
        internal ref safe_ptr<Node> GetNode(uint id) {
            
            var idx = id - 1u;
            E.RANGE(idx, 0u, this.index);
            return ref this.nodes[idx].data;

        }

        [INLINE(256)]
        public SystemHandle Combine(SystemHandle handle1, SystemHandle handle2) {

            var node = Node.Create(isPointer: true);
            if (handle1.IsValid() == true) node.ptr->AddDependency(this.GetNode(handle1.id));
            if (handle2.IsValid() == true) node.ptr->AddDependency(this.GetNode(handle2.id));
            this.RegisterNode(node);
            return SystemHandle.Create(node.ptr->id);
            
        }

        [INLINE(256)]
        public SystemHandle Combine(SystemHandle handle1, SystemHandle handle2, SystemHandle handle3) {

            var node = Node.Create(isPointer: true);
            if (handle1.IsValid() == true) node.ptr->AddDependency(this.GetNode(handle1.id));
            if (handle2.IsValid() == true) node.ptr->AddDependency(this.GetNode(handle2.id));
            if (handle3.IsValid() == true) node.ptr->AddDependency(this.GetNode(handle3.id));
            this.RegisterNode(node);
            return SystemHandle.Create(node.ptr->id);
            
        }

        [INLINE(256)]
        public SystemHandle Combine(System.Collections.Generic.List<SystemHandle> handles) {

            var node = Node.Create(isPointer: true);
            foreach (var handle in handles) {
                if (handle.IsValid() == true) node.ptr->AddDependency(this.GetNode(handle.id));    
            }
            this.RegisterNode(node);
            return SystemHandle.Create(node.ptr->id);
            
        }

        [INLINE(256)]
        private void RunSystem(void* systemData, Node.Data* methodData, ref SystemContext context) {
            
            // call
            var func = new Unity.Burst.FunctionPointer<FunctionPointerDelegate>((System.IntPtr)methodData->methodPtr);
            func.Invoke(systemData, ref context);
            
        }

        [INLINE(256)]
        public JobHandle DrawGizmos(ref World world, JobHandle dependsOn = default) {

            return this.Run(ref world, 0u, Method.DrawGizmos, 0, dependsOn);

        }

        [INLINE(256)]
        public JobHandle Awake(ref World world, ushort subId = 0, JobHandle dependsOn = default) {

            return this.Run(ref world, 0u, Method.Awake, subId, dependsOn);

        }

        [INLINE(256)]
        public JobHandle Start(ref World world, ushort subId = 0, JobHandle dependsOn = default) {

            return this.Run(ref world, 0u, Method.Start, subId, dependsOn);

        }

        [INLINE(256)]
        public JobHandle Update(ref World world, uint deltaTimeMs, ushort subId = 0, JobHandle dependsOn = default) {

            return this.Run(ref world, deltaTimeMs, Method.Update, subId, dependsOn);

        }

        [INLINE(256)]
        public JobHandle Destroy(ref World world, ushort subId = 0, JobHandle dependsOn = default) {
            
            return this.Run(ref world, 0u, Method.Destroy, subId, dependsOn);
            
        }

        internal static string DebugParent(safe_ptr<Node> node) {
            var str = string.Empty;
            if (node.ptr->parentIndex == 1u) {
                str += node.ptr->parents[0].data.ptr->name;
            } else if (node.ptr->parentIndex == 2u) {
                str += node.ptr->parents[0].data.ptr->name + ", ";
                str += node.ptr->parents[1].data.ptr->name;
            } else if (node.ptr->parentIndex == 3u) {
                str += node.ptr->parents[0].data.ptr->name + ", ";
                str += node.ptr->parents[1].data.ptr->name + ", ";
                str += node.ptr->parents[2].data.ptr->name;
            } else if (node.ptr->parentIndex > 3u) {
                for (uint i = 0; i < node.ptr->parentIndex; ++i) {
                    str += node.ptr->parents[i].data.ptr->name + ", ";
                }
            }

            return str;
        }

        internal static string DebugHandle(JobHandle jobHandle) {
            var field = jobHandle.GetType().GetField("debugInfo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return ((int)(System.IntPtr)field.GetValue(jobHandle)).ToString();
        }

        [INLINE(256)]
        private JobHandle Run(ref World world, uint deltaTimeMs, Method method, ushort subId, JobHandle dependsOn = default) {
            
            //UnityEngine.Debug.LogWarning("You are trying to run obsolete method. Use graph code generator instead.");
            var list = new UnsafeList<NodeData>((int)this.index, Constants.ALLOCATOR_TEMP);
            ref var queue = ref this.runtimeQueue;
            queue.Clear();
            queue.Enqueue(this.rootNode);
            this.rootNode.ptr->dependsOn = Batches.Apply(dependsOn, world.state);
            try {
                while (queue.Count > 0) {

                    var node = queue.Dequeue();
                    
                    if (node.ptr->HasMethod(method) == true) {
                        if (node.ptr->isStarted == false) {
                            node.ptr->isStarted = true;
                            if (node.ptr->AllParentsStarted(method) == true) {
                                var dj = node.ptr->GetJobHandle();
                                var context = SystemContext.Create(deltaTimeMs, world, dj);
                                //context.SetDependency(Batches.Apply(context.dependsOn, world.state));
                                Journal.UpdateSystemStarted(world.id, node.ptr->name);
                                //UnityEngine.Debug.Log("Started: " + node.ptr->name + " :: " + DebugHandle(dj) + " => " + DebugHandle(node.ptr->dependsOn) + " :: " + DebugParent(node) + ", subId: " + subId);
                                list.Add(new NodeData() { data = node });
                                this.RunSystem(node.ptr->systemData.ptr, node.ptr->GetMethod(method).ptr, ref context);
                                Journal.UpdateSystemEnded(world.id, node.ptr->name);
                                node.ptr->dependsOn = Batches.Apply(context.dependsOn, world.state);
                                //UnityEngine.Debug.Log("Ended: " + node.ptr->name + " :: " + DebugHandle(dj) + " => " + DebugHandle(node.ptr->dependsOn));
                            } else {
                                node.ptr->isStarted = false;
                                queue.Enqueue(node);
                            }
                        }
                    } else if (node.ptr->graph.ptr != null) {
                        // Check if subId is valid for this graph
                        if (subId > 0 && node.ptr->graph.ptr->updateType > 0 && node.ptr->graph.ptr->updateType != subId) continue;
                        if (node.ptr->isStarted == false) {
                            var dj = node.ptr->GetJobHandle();
                            //UnityEngine.Debug.Log("Graph: " + node.ptr->name + " :: " + DebugHandle(dj));
                            node.ptr->isStarted = true;
                            node.ptr->dependsOn = node.ptr->graph.ptr->Run(ref world, deltaTimeMs, method, subId, dj);
                            list.Add(new NodeData() { data = node });
                        }
                    } else {
                        list.Add(new NodeData() { data = node });
                        node.ptr->dependsOn = node.ptr->GetJobHandle();
                        //UnityEngine.Debug.Log("Skip: " + node.ptr->name + " :: " + DebugHandle(node.ptr->dependsOn));
                    }

                    for (uint i = 0; i < node.ptr->childrenIndex; ++i) {
                        var child = node.ptr->children[i].data;
                        if (child.ptr->isStarted == false) queue.Enqueue(child);
                    }

                }
            } catch (System.Exception ex) {
                Logger.Core.Exception(ex, showCallstack: true);
            }
            
            var arrDepends = new Unity.Collections.NativeArray<JobHandle>(list.Length + 1, Constants.ALLOCATOR_TEMP);
            {
                int i = 0;
                for (i = 0; i < list.Length; ++i) {
                    list[i].data.ptr->isStarted = false;
                    //UnityEngine.Debug.Log("Complete: " + DebugHandle(list[i].data.ptr->dependsOn) + " :: " + list[i].data.ptr->dependsOn.IsCompleted + " :: " + list[i].data.ptr->name);
                    arrDepends[i] = list[i].data.ptr->dependsOn;
                }

                arrDepends[i] = dependsOn;
            }
            var resultDepends = JobHandle.CombineDependencies(arrDepends);
            list.Dispose();
            arrDepends.Dispose();
            JobUtils.RunScheduled();

            return resultDepends;

        }

        [INLINE(256)]
        public void Dispose() {

            {
                // To be sure all jobs has been complete
                for (int i = 0; i < this.index; ++i) {
                    var node = this.nodes[i];
                    if (node.data.ptr->isPointer == true) continue;
                    node.data.ptr->dependsOn.Complete();
                }
            }

            for (int i = 0; i < this.index; ++i) {
                var node = this.nodes[i];
                node.data.ptr->Dispose();
                _free(node.data);
            }

            this.rootNode.ptr->Dispose();
            _free(this.rootNode);
            if (this.nodes.ptr != null) _free(this.nodes);
            if (this.data.ptr != null) _free(this.data);
            this.runtimeQueue.Dispose();

        }

    }

}