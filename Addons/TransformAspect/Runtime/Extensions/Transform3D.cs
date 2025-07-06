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
            if (ent.IsStatic == true) ent.ent.SetTag<IsTransformStaticCalculatedComponent>(true);
            
        }

        [INLINE(256)]
        public static void CalculateLocalMatrix(in TransformAspect ent) {

            var t = ent.readLocalPosition;
            var s = ent.readLocalScale;
            var r = ent.readLocalRotation.value;
            ref var matrix = ref ent.localMatrix;
            matrix.c0.x = (1.0f - 2.0f * (r.y * r.y + r.z * r.z)) * s.x;
            matrix.c0.y = (r.x * r.y + r.z * r.w) * s.x * 2.0f;
            matrix.c0.z = (r.x * r.z - r.y * r.w) * s.x * 2.0f;
            matrix.c0.w = 0.0f;
            matrix.c1.x = (r.x * r.y - r.z * r.w) * s.y * 2.0f;
            matrix.c1.y = (1.0f - 2.0f * (r.x * r.x + r.z * r.z)) * s.y;
            matrix.c1.z = (r.y * r.z + r.x * r.w) * s.y * 2.0f;
            matrix.c1.w = 0.0f;
            matrix.c2.x = (r.x * r.z + r.y * r.w) * s.z * 2.0f;
            matrix.c2.y = (r.y * r.z - r.x * r.w) * s.z * 2.0f;
            matrix.c2.z = (1.0f - 2.0f * (r.x * r.x + r.y * r.y)) * s.z;
            matrix.c2.w = 0.0f;
            matrix.c3.x = t.x;
            matrix.c3.y = t.y;
            matrix.c3.z = t.z;
            matrix.c3.w = 1.0f;
            
            //ent.localMatrix = (float4x4)UnityEngine.Matrix4x4.TRS((UnityEngine.Vector3)ent.readLocalPosition, (UnityEngine.Quaternion)ent.readLocalRotation, (UnityEngine.Vector3)ent.readLocalScale); //float4x4.TRS(ent.readLocalPosition, ent.readLocalRotation, ent.readLocalScale);

        }

        [INLINE(256)]
        public static void CalculateWorldMatrix(in TransformAspect ent) {

            ent.worldMatrix = ent.readLocalMatrix;
            ent.IsWorldMatrixTickCalculated = true;

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

            ent.IsWorldMatrixTickCalculated = false;

        }

        [INLINE(256)]
        public static void CalculateWorldMatrixParent(in TransformAspect parent, in TransformAspect ent) {
            
            if (parent.IsWorldMatrixTickCalculated == false) {
                // Calculate parent matrix
                if (ent.ent.worldId != parent.ent.worldId) return;
                if (parent.parent.IsAlive() == true) CalculateWorldMatrixParent(parent.parent.GetAspect<TransformAspect>(), in parent);
            }

            if (ent.IsWorldMatrixTickCalculated == false) {
                ent.LockWorldMatrix();
                if (ent.IsWorldMatrixTickCalculated == false) {
                    CalculateMatrix(in parent, in ent);
                    ent.IsWorldMatrixTickCalculated = true;
                }
                ent.UnlockWorldMatrix();
            }

        }

    }

}