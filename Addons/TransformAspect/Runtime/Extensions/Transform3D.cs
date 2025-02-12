#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Transforms {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public static unsafe class Transform3DExt {

        [INLINE(256)]
        public static ref readonly Ent GetParent(this in Ent ent) {
            return ref ent.Read<ParentComponent>().value;
        }

        [INLINE(256)]
        public static ref readonly Ent ReadParent(this in Ent ent) {
            return ref ent.Read<ParentComponent>().value;
        }

        [INLINE(256)]
        public static void SetParent(this in Ent ent, in Ent parent, bool worldPositionStay = false) {

            float3 prevPos = default;
            quaternion prevRot = default;
            if (parent.IsAlive() == true && worldPositionStay == true) {
                var aspect = ent.GetOrCreateAspect<TransformAspect>();
                prevPos = aspect.position;
                prevRot = aspect.rotation;
            }
            
            ent.SetParent_INTERNAL(in parent);
            
            if (parent.IsAlive() == true && worldPositionStay == true) {
                var aspect = ent.GetOrCreateAspect<TransformAspect>();
                aspect.position = prevPos;
                aspect.rotation = prevRot;
            }
            
        }

        [INLINE(256)]
        private static void SetParent_INTERNAL(this in Ent ent, in Ent parent) {

            ref var currentParent = ref ent.Get<ParentComponent>().value;
            if (currentParent.IsAlive() == true) {
                
                // Move out from current parent
                ref var children = ref currentParent.Get<ChildrenComponent>();
                children.lockSpinner.Lock();
                children.list.Remove(ent);
                children.lockSpinner.Unlock();
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
                ref var parentChildren = ref parent.Get<ChildrenComponent>();
                parentChildren.lockSpinner.Lock();
                if (parentChildren.list.IsCreated == false) parentChildren.list = new ListAuto<Ent>(parent, 1u); 
                parentChildren.list.Add(ent);
                parentChildren.lockSpinner.Unlock();
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
        public static void CalculateMatrixHierarchy(ref TransformAspect aspect) {
            CalculateMatrixHierarchy(aspect.parent, aspect);
        }

        [INLINE(256)]
        public static void CalculateMatrixHierarchy(in TransformAspect parent, in TransformAspect ent) {

            CalculateMatrix(in parent, in ent);
            
            var cnt = ent.children.Count;
            if (cnt > 0u) {

                var queue = new Unity.Collections.LowLevel.Unsafe.UnsafeList<TransformAspect>((int)cnt, Constants.ALLOCATOR_TEMP);
                queue.Add(ent);
                while (queue.Length > 0) {
                    var entData = queue[0];
                    queue.RemoveAtSwapBack(0);
                    cnt = entData.children.Count;
                    if (cnt > 0u) {
                        var children = (safe_ptr<Ent>)entData.children.GetUnsafePtr(in entData.ent.World.state.ptr->allocator);
                        for (uint i = 0; i < cnt; ++i) {
                            var child = *(children + i).ptr;
                            var tr = child.GetAspect<TransformAspect>();
                            CalculateMatrix(in entData, in tr);
                            queue.Add(tr);
                        }
                    }
                }
                
            }

        }

    }

}