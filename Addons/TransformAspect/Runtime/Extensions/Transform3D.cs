#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Transforms {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    [IgnoreProfiler]
    public static unsafe class Transform3DExt {

        public struct HierarchyItem {

            public Ent ent;
            public bbool parentDirty;

        }

        [INLINE(256)]
        public static Ent ReadParent(this in EntRO ent) {
            return ent.Read<ParentComponent>().value;
        }

        [INLINE(256)]
        public static ref Ent GetParent(this in Ent ent) {
            return ref ent.Get<ParentComponent>().value;
        }

        [INLINE(256)]
        public static ref readonly Ent ReadParent(this in Ent ent) {
            return ref ent.Read<ParentComponent>().value;
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
                currentParent = default;
            }

            if (parent.IsAlive() == false) {
                // Clean up parent component
                ent.Remove<ParentComponent>();
                MarkTransformDirty(in ent);
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
            MarkTransformDirty(in ent);
        }

        [INLINE(256)]
        private static void MarkTransformDirty(in Ent ent) {

            if (ent.Has<LocalPositionComponent>() == false || ent.Has<LocalRotationComponent>() == false ||
                ent.Has<LocalMatrixComponent>() == false || ent.Has<WorldMatrixComponent>() == false) return;
            var tr = ent.GetAspect<TransformAspect>();
            tr.SetDirty();
            tr.IsWorldMatrixTickCalculated = false;

        }
        
        [INLINE(256)]
        public static void CalculateMatrix(in TransformAspect parent, in TransformAspect ent) {

            ent.worldMatrix = math.mul(parent.readWorldMatrix, ent.readLocalMatrix);
            
        }

        [INLINE(256)]
        public static void CalculateMatrixStatic(in TransformAspect parent, in TransformAspect ent) {

            ent.worldMatrix = math.mul(parent.readWorldMatrix, ent.readLocalMatrix);
            ent.ent.SetTag<IsTransformStaticCalculatedComponent>(true);
            
        }

        [INLINE(256)]
        public static void CalculateLocalMatrix(in TransformAspect ent) {

            ent.localMatrix = CreateLocalMatrix(in ent);

        }

        [INLINE(256)]
        private static float4x4 CreateLocalMatrix(in TransformAspect ent) {

            var t = ent.readLocalPosition;
            var s = ent.readLocalScale;
            var r = ent.readLocalRotation.value;
            float4x4 matrix = default;
            matrix.c0.x = (1 - 2 * (r.y * r.y + r.z * r.z)) * s.x;
            matrix.c0.y = (r.x * r.y + r.z * r.w) * s.x * 2;
            matrix.c0.z = (r.x * r.z - r.y * r.w) * s.x * 2;
            matrix.c0.w = 0;
            matrix.c1.x = (r.x * r.y - r.z * r.w) * s.y * 2;
            matrix.c1.y = (1 - 2 * (r.x * r.x + r.z * r.z)) * s.y;
            matrix.c1.z = (r.y * r.z + r.x * r.w) * s.y * 2;
            matrix.c1.w = 0;
            matrix.c2.x = (r.x * r.z + r.y * r.w) * s.z * 2;
            matrix.c2.y = (r.y * r.z - r.x * r.w) * s.z * 2;
            matrix.c2.z = (1 - 2 * (r.x * r.x + r.y * r.y)) * s.z;
            matrix.c2.w = 0;
            matrix.c3.x = t.x;
            matrix.c3.y = t.y;
            matrix.c3.z = t.z;
            matrix.c3.w = 1;
            
            return matrix;

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
        public static void CalculateLocalMatrixAndMarkDirty(in TransformAspect ent) {

            var current = CreateLocalMatrix(in ent);
            ref readonly var previous = ref ent.readLocalMatrix;
            if (math.all(previous.c0 == current.c0) == false ||
                math.all(previous.c1 == current.c1) == false ||
                math.all(previous.c2 == current.c2) == false ||
                math.all(previous.c3 == current.c3) == false) {
                ent.localMatrix = current;
                ent.SetDirty();
            }

        }

        [INLINE(256)]
        public static void CalculateWorldMatrixHierarchy(in TransformAspect root, ref Unity.Collections.LowLevel.Unsafe.UnsafeList<HierarchyItem> stack) {

            var rootDirty = root.IsDirty || root.IsWorldMatrixTickCalculated == false ||
                            (root.IsStatic == true && root.ent.Has<IsTransformStaticCalculatedComponent>() == false);
            if (rootDirty == true) {
                CalculateWorldMatrix(in root);
                if (root.IsStatic == true) root.ent.SetTag<IsTransformStaticCalculatedComponent>(true);
            }

            AddChildrenReverse(in root, rootDirty, ref stack);
            while (stack.Length > 0) {
                var index = stack.Length - 1;
                var item = stack[index];
                stack.RemoveAtSwapBack(index);
                var ent = item.ent;
                if (ent.IsAlive() == false || ent.worldId != root.ent.worldId) continue;
                if (ent.Has<LocalPositionComponent>() == false || ent.Has<LocalRotationComponent>() == false ||
                    ent.Has<LocalMatrixComponent>() == false || ent.Has<WorldMatrixComponent>() == false) continue;

                var tr = ent.GetAspect<TransformAspect>();
                var parent = tr.parent;
                if (parent.IsAlive() == false || parent.worldId != ent.worldId) continue;

                var worldDirty = item.parentDirty || tr.IsDirty || tr.IsWorldMatrixTickCalculated == false ||
                                 (tr.IsStatic == true && ent.Has<IsTransformStaticCalculatedComponent>() == false);
                if (worldDirty == true) {
                    var parentTr = parent.GetAspect<TransformAspect>();
                    if (tr.IsStatic == true) {
                        CalculateMatrixStatic(in parentTr, in tr);
                    } else {
                        CalculateMatrix(in parentTr, in tr);
                    }
                    tr.IsWorldMatrixTickCalculated = true;
                }

                AddChildrenReverse(in tr, worldDirty, ref stack);
            }

        }

        [INLINE(256)]
        private static void AddChildrenReverse(in TransformAspect ent, bool parentDirty, ref Unity.Collections.LowLevel.Unsafe.UnsafeList<HierarchyItem> stack) {

            if (ent.ent.Has<ChildrenComponent>() == false) return;
            ref readonly var children = ref ent.children;
            for (var i = (int)children.Count - 1; i >= 0; --i) {
                stack.Add(new HierarchyItem() {
                    ent = children[(uint)i],
                    parentDirty = parentDirty,
                });
            }

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
        public static void CalculateWorldMatrixLevel(in TransformAspect parent, in TransformAspect ent) {
            
            if (parent.IsWorldMatrixTickCalculated == false) {
                // Calculate parent matrix
                if (ent.ent.worldId != parent.ent.worldId) return;
            }

            CalculateMatrix(in parent, in ent);
            ent.IsWorldMatrixTickCalculated = true;
            
        }

        [INLINE(256)]
        public static void CalculateWorldMatrixLevelStatic(in TransformAspect parent, in TransformAspect ent) {
            
            if (parent.IsWorldMatrixTickCalculated == false) {
                // Calculate parent matrix
                if (ent.ent.worldId != parent.ent.worldId) return;
            }

            CalculateMatrixStatic(in parent, in ent);
            ent.IsWorldMatrixTickCalculated = true;
            
        }

        [INLINE(256)]
        public static void CalculateWorldMatrixParent(in TransformAspect parent, in TransformAspect ent) {
            
            /*var stack = new Unity.Collections.LowLevel.Unsafe.UnsafeList<TransformAspect>(8, Constants.ALLOCATOR_TEMP);

            stack.Add(ent);
            var current = parent;
            while (current.IsWorldMatrixTickCalculated == false) {
                if (ent.ent.worldId != current.ent.worldId) {
                    break;
                }
                stack.Add(current);
                if (current.parent.IsAlive() == false) break;
                current = current.parent.GetAspect<TransformAspect>();
            }

            for (int i = stack.Length - 1; i >= 0; --i) {
                var p = stack[i];
                p.LockWorldMatrix();
                if (p.IsWorldMatrixTickCalculated == false) {
                    if (p.parent.IsAlive() == true) {
                        CalculateMatrix(p.parent.GetAspect<TransformAspect>(), in p);
                    }
                    p.IsWorldMatrixTickCalculated = true;
                }
                p.UnlockWorldMatrix();
            }*/
            
            if (parent.IsWorldMatrixTickCalculated == false) {
                // Calculate parent matrix
                if (ent.ent.worldId != parent.ent.worldId) return;
                if (parent.parent.IsAlive() == true) CalculateWorldMatrixParent(parent.parent.GetAspect<TransformAspect>(), in parent);
            }

            if (ent.IsWorldMatrixTickCalculated == false) {
                ent.LockWorldMatrix();
                if (ent.IsWorldMatrixTickCalculated == false) {
                    if (ent.IsStatic == true) {
                        CalculateMatrixStatic(in parent, in ent);
                    } else {
                        CalculateMatrix(in parent, in ent);
                    }
                    ent.IsWorldMatrixTickCalculated = true;
                }
                ent.UnlockWorldMatrix();
            }

        }

    }

}
