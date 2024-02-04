namespace ME.BECS.Transforms {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using float3 = Unity.Mathematics.float3;
    using float4x4 = Unity.Mathematics.float4x4;
    using quaternion = Unity.Mathematics.quaternion;
    using tfloat = System.Single;
    using math = Unity.Mathematics.math;
    
    public static unsafe class Transform3DExt {

        [INLINE(256)]
        public static Ent GetParent(this in Ent ent) {
            return ent.Read<ParentComponent>().value;
        }

        [INLINE(256)]
        public static void SetParent(this in Ent ent, Ent parent) {

            ref var currentParent = ref ent.Get<ParentComponent>().value;
            if (currentParent.IsAlive() == true) {
                
                // Move out from current parent
                ref var children = ref currentParent.Get<ChildrenComponent>().list;
                children.Remove(ent);
                ent.Remove<IsFirstLevelComponent>();
                currentParent = default;

            }

            if (parent.IsAlive() == false) {
                
                // Clean up parent component
                ent.Remove<ParentComponent>();
                return;

            }

            {
                // Move to the new parent
                ref var parentChildren = ref parent.Get<ChildrenComponent>().list;
                if (parentChildren.isCreated == false) parentChildren = new ListAuto<Ent>(parent, 1u); 
                parentChildren.Add(ent);
                currentParent = parent;
                // if new parent has no parent component
                // set IsFirstLevelComponent
                if (parent.Has<ParentComponent>() == false) {
                    ent.Set(new IsFirstLevelComponent());
                }
            }

        }
        
        [INLINE(256)]
        public static void CalculateMatrix(in TransformAspect parent, in TransformAspect ent) {

            var matrix = ent.localMatrix;
            if (parent.ent.IsAlive() == true) matrix = math.mul(parent.readWorldMatrix, matrix);
            ent.worldMatrix = matrix;

        }

        [INLINE(256)]
        public static void CalculateMatrix(in TransformAspect ent) {

            ent.worldMatrix = ent.localMatrix;

        }

        [INLINE(256)]
        public static void CalculateMatrixHierarchy(in CommandBufferJobParallel buffer) {
            var aspect = buffer.ent.GetAspect<TransformAspect>();
            CalculateMatrixHierarchy(aspect.parent, aspect);
        }

        [INLINE(256)]
        public static void CalculateMatrixHierarchy(ref TransformAspect aspect) {
            CalculateMatrixHierarchy(aspect.parent, aspect);
        }

        [INLINE(256)]
        public static void CalculateMatrixHierarchy(in CommandBufferJob buffer) {
            CalculateMatrixHierarchy(buffer.Read<ParentComponent>().value, buffer.ent);
        }

        [INLINE(256)]
        public static void CalculateMatrixHierarchy(in TransformAspect parent, in TransformAspect ent) {

            CalculateMatrix(in parent, in ent);
            
            var cnt = ent.children.Count;
            if (cnt > 0u) {

                var queue = new Unity.Collections.NativeQueue<TransformAspect>(Constants.ALLOCATOR_TEMP);
                queue.Enqueue(ent);
                while (queue.Count > 0) {
                    var entData = queue.Dequeue();
                    cnt = entData.children.Count;
                    if (cnt > 0u) {
                        var children = (Ent*)entData.children.GetUnsafePtr(in entData.ent.World.state->allocator);
                        for (uint i = 0; i < cnt; ++i) {
                            var child = *(children + i);
                            var tr = child.GetAspect<TransformAspect>();
                            CalculateMatrix(in entData, in tr);
                            queue.Enqueue(tr);
                        }
                    }
                }
                
            }

        }

    }

}