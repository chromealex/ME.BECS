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
            return ref ent.GetAspect<TransformAspect>().parent;
        }

        [INLINE(256)]
        public static ref readonly Ent ReadParent(this in Ent ent) {
            return ref ent.GetAspect<TransformAspect>().parent;
        }

        [INLINE(256)]
        public static void SetParent(this in Ent ent, in Ent parent, bool worldPositionStay = false) {

            float3 prevPos = default;
            quaternion prevRot = default;
            if (worldPositionStay == true && parent.IsAlive() == true) {
                var aspect = ent.GetOrCreateAspect<TransformAspect>();
                prevPos = aspect.position;
                prevRot = aspect.rotation;
            }
            
            ent.SetParent_INTERNAL(in parent);
            
            if (worldPositionStay == true && parent.IsAlive() == true) {
                var aspect = ent.GetOrCreateAspect<TransformAspect>();
                aspect.position = prevPos;
                aspect.rotation = prevRot;
            }
            
        }

        [INLINE(256)]
        private static void SetParent_INTERNAL(this in Ent ent, in Ent parent) {

            ref var currentParent = ref ent.Get<ParentComponent>().value;
            if (currentParent.IsAlive() == true && ent.worldId == currentParent.worldId) {
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

            // Do not change children if worlds are not the same 
            if (ent.worldId == parent.worldId) {
                // Move to the new parent
                ref var parentChildren = ref parent.Get<ChildrenComponent>();
                parentChildren.lockSpinner.Lock();
                if (parentChildren.list.IsCreated == false) parentChildren.list = new ListAuto<Ent>(parent, 1u); 
                parentChildren.list.Add(ent);
                parentChildren.list.Sort<Ent>();
                parentChildren.lockSpinner.Unlock();
            }
            
            currentParent = parent;
            // if new parent has no parent component
            // set IsFirstLevelComponent
            if (parent.Has<ParentComponent>() == false) {
                ent.Set(new IsFirstLevelComponent());
            }

        }
        
        [INLINE(256)]
        public static void CalculateMatrix(in TransformAspect parent, in TransformAspect ent) {

            ent.worldMatrix = math.mul(parent.readWorldMatrix, ent.readLocalMatrix);

        }

        [INLINE(256)]
        public static void CalculateLocalMatrix(in TransformAspect ent) {

            ent.localMatrix = float4x4.TRS(ent.readLocalPosition, ent.readLocalRotation, ent.readLocalScale);

        }

        [INLINE(256)]
        public static void CalculateWorldMatrix(in TransformAspect ent) {

            ent.worldMatrix = ent.readLocalMatrix;
            ent.isWorldMatrixTickCalculated = 1;

        }

        [INLINE(256)]
        public static void CalculateWorldMatrixHierarchy(ref TransformAspect aspect) {
            CalculateWorldMatrixHierarchy(aspect.parent, aspect);
        }

        [INLINE(256)]
        public static void CalculateWorldMatrixHierarchy(in TransformAspect parent, in TransformAspect ent) {

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
                queue.Dispose();
                
            }

        }

        [INLINE(256)]
        public static void Clear(in TransformAspect ent) {

            ent.isWorldMatrixTickCalculated = 0;

        }

        [INLINE(256)]
        public static void CalculateWorldMatrixParent(in TransformAspect parent, in TransformAspect ent) {
            
            if (parent.readIsWorldMatrixTickCalculated == 0) {
                // Calculate parent matrix
                if (ent.ent.worldId != parent.ent.worldId) return;
                if (parent.parent.IsAlive() == true) CalculateWorldMatrixParent(parent.parent.GetAspect<TransformAspect>(), in parent);
            }

            if (ent.readIsWorldMatrixTickCalculated == 0) {
                ent.LockWorldMatrix();
                if (ent.readIsWorldMatrixTickCalculated == 0) {
                    CalculateMatrix(in parent, in ent);
                    ent.isWorldMatrixTickCalculated = 1;
                }
                ent.UnlockWorldMatrix();
            }

        }

    }

}