namespace ME.BECS.TransformAspect {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using float3 = Unity.Mathematics.float3;
    using float4x4 = Unity.Mathematics.float4x4;
    using quaternion = Unity.Mathematics.quaternion;
    using tfloat = System.Single;
    using math = Unity.Mathematics.math;
    
    public static unsafe class Transform3DExt {

        [INLINE(256)]
        public static void SetParent(this in Ent ent, Ent parent) {

            var world = ent.World;
            ref var currentParent = ref ent.Get<ParentComponent>().value;
            if (currentParent.IsAlive() == true) {
                
                // Move out from current parent
                ref var children = ref currentParent.Get<ChildrenComponent>().list;
                children.Remove(ref world.state->allocator, ent);
                currentParent = default;

            }

            if (parent.IsAlive() == false) {
                
                // Leave currentParent as null
                ent.Remove<ParentComponent>();
                return;

            }

            {
                // Move to the new parent
                ref var parentChildren = ref parent.Get<ChildrenComponent>().list;
                if (parentChildren.isCreated == false) parentChildren = new List<Ent>(ref world.state->allocator, 1u); 
                parentChildren.Add(ref world.state->allocator, ent);
                currentParent = parent;
            }

            {
                // Mark as dirty
                ent.Set(new IsHierarchyDirtyComponent());
            }
            
        }
        
        [INLINE(256)]
        public static void CalculateMatrix(in TransformAspect parent, in TransformAspect ent) {

            var matrix = ent.localMatrix;
            if (parent.ent.IsAlive() == true) matrix = math.mul(parent.worldMatrix, matrix);
            ent.worldMatrix = matrix;

        }

        [INLINE(256)]
        public static void CalculateMatrixHierarchy(in CommandBufferJobParallel buffer) {
            var aspect = buffer.ent.GetAspect<TransformAspect>();
            CalculateMatrixHierarchy(buffer.buffer->state, aspect.parent, aspect);
        }

        [INLINE(256)]
        public static void CalculateMatrixHierarchy(State* state, ref TransformAspect aspect) {
            CalculateMatrixHierarchy(state, aspect.parent, aspect);
        }

        [INLINE(256)]
        public static void CalculateMatrixHierarchy(in CommandBufferJob buffer) {
            CalculateMatrixHierarchy(buffer.buffer->state, buffer.Read<ParentComponent>().value, buffer.ent);
        }

        [INLINE(256)]
        public static void CalculateMatrixHierarchy(State* state, in TransformAspect parent, in TransformAspect ent) {

            CalculateMatrix(parent, ent);
            ent.UnsetDirty();
            
            ref readonly var children = ref ent.children;
            for (uint i = 0; i < children.Count; ++i) {

                var child = children[in state->allocator, i];
                CalculateMatrixHierarchy(state, in ent, child);

            }
            
        }

    }

}