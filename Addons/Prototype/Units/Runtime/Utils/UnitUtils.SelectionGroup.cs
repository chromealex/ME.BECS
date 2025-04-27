#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

using ME.BECS.Transforms;

namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public struct RangeMoveableAABBUniqueVisitor : NativeTrees.IOctreeRangeVisitor<Ent> {
        public Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<Ent> results;
        public tfloat rangeSqr;
        public uint max;
        public MathSector sector;

        public bool OnVisit(Ent obj, NativeTrees.AABB objBounds, NativeTrees.AABB queryRange) {
            if (this.sector.IsValid(objBounds.Center) == true) {
                // check if our object's AABB overlaps with the query AABB
                if (obj.Has<IsUnitStaticComponent>() == true) return true;
                if (objBounds.Overlaps(queryRange) == true &&
                    queryRange.DistanceSquared(objBounds.Center) <= this.rangeSqr) {
                    this.results.Add(obj);
                    if (this.max > 0u && this.results.Count == this.max) return false;
                }
            }

            return true; // keep iterating
        }
    }

    public struct OctreeNearestMoveableAABBVisitor : NativeTrees.IOctreeNearestVisitor<Ent> {

        public Ent nearest;
        public bool found;
        public MathSector sector;
        public bool ignoreSelf;
        public Ent ignore;

        public bool OnVisit(Ent obj, NativeTrees.AABB bounds) {

            if (this.sector.IsValid(bounds.Center) == false) {
                return true;
            }

            if (this.ignoreSelf == true) {
                if (this.ignore.Equals(obj) == true) return true;
            }
            
            if (obj.Has<IsUnitStaticComponent>() == true) return true;
            
            this.found = true;
            this.nearest = obj;
        
            return false; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }
    
    public static partial class UnitUtils {

        [INLINE(256)]
        public static UnitSelectionGroupAspect CreateSelectionGroup(uint capacity = 10u, in JobInfo jobInfo = default) {

            var ent = Ent.New(in jobInfo, editorName: "SelectionGroup");
            var aspect = ent.GetOrCreateAspect<UnitSelectionGroupAspect>();
            aspect.units = new ListAuto<Ent>(in ent, capacity);
            return aspect;

        }

        [INLINE(256)]
        public static UnitSelectionTempGroupAspect CreateSelectionTempGroup(uint capacity = 10u, in JobInfo jobInfo = default) {

            var ent = Ent.New(in jobInfo, editorName: "SelectionGroup");
            var aspect = ent.GetOrCreateAspect<UnitSelectionTempGroupAspect>();
            aspect.units = new ListAuto<Ent>(in ent, capacity);
            return aspect;

        }

        [INLINE(256)]
        public static void DestroySelectionGroup(in Ent group) {
            
            if (group.IsAlive() == true) group.DestroyHierarchy();
            
        }

        [INLINE(256)]
        public static void DestroySelectionGroup(in UnitSelectionGroupAspect group) {
            
            if (group.ent.IsAlive() == true) group.ent.DestroyHierarchy();
            
        }

        [INLINE(256)]
        public static uint AddToSelectionGroup(in UnitSelectionGroupAspect selectionGroup, in UnitAspect unit) {

            E.THREAD_CHECK("AddToSelectionGroup");

            RemoveFromSelectionGroup(in unit);
            unit.unitSelectionGroup = selectionGroup.ent;
            return selectionGroup.units.Add(unit.ent) + 1u;

        }

        [INLINE(256)]
        public static bool WillRemoveSelectionGroup(in UnitAspect unit) {
            if (unit.unitSelectionGroup.IsAlive() == true) {
                var aspect = unit.unitSelectionGroup.GetAspect<UnitSelectionGroupAspect>();
                return aspect.units.Count == 1u;
            }
            return false;
        }

        [INLINE(256)]
        public static bool RemoveFromSelectionGroup(in UnitAspect unit) {

            E.THREAD_CHECK("RemoveFromSelectionGroup");

            if (unit.unitSelectionGroup.IsAlive() == true) {
                var aspect = unit.unitSelectionGroup.GetAspect<UnitSelectionGroupAspect>();
                aspect.units.Remove(unit.ent);
                if (aspect.units.Count == 0u) {
                    // destroy group
                    aspect.ent.DestroyHierarchy();
                    return true;
                }
                unit.unitSelectionGroup = default;
            }

            return false;

        }

        [INLINE(256)]
        public static bool RemoveFromSelectionGroup(in UnitSelectionGroupAspect selectionGroup, in UnitAspect unit) {

            if (unit.readUnitSelectionGroup != selectionGroup.ent) return false;
            
            return RemoveFromSelectionGroup(in unit);

        }

        [INLINE(256)]
        public static unsafe UnitSelectionGroupAspect CreateSelectionGroupByTypeInPoint(in QuadTreeInsertSystem trees, int treeIndex, float3 position, tfloat? minRange = null,
                                                                                                tfloat? maxRange = null, JobInfo jobInfo = default) {
            return CreateSelectionGroupByTypeInPoint(in trees, treeIndex, position, minRange != null ? minRange.Value : 0f, maxRange != null ? maxRange.Value : 5f, jobInfo);
        }

        [INLINE(256)]
        public static unsafe UnitSelectionGroupAspect CreateSelectionGroupByTypeInPoint(in QuadTreeInsertSystem trees, int treeIndex, float3 position, tfloat minRange, tfloat maxRange, JobInfo jobInfo = default) {

            var tree = trees.GetTree(treeIndex);
            var group = UnitUtils.CreateSelectionGroup(1u, jobInfo);

            var visitor = new OctreeNearestAABBVisitor<Ent, AlwaysTrueSubFilter>();
            tree.ptr->Nearest(position, minRange, maxRange, ref visitor, new AABBDistanceSquaredProvider<Ent>());
            if (visitor.found == true) {
                if (visitor.nearest.IsAlive() == true) group.Add(visitor.nearest.GetAspect<UnitAspect>());
            }
            
            return group;

        }

        [INLINE(256)]
        public static unsafe UnitSelectionTempGroupAspect CreateSelectionGroupByTypeInPointTemp(in QuadTreeInsertSystem trees, int treeIndex, float3 position, tfloat? minRange = null,
                                                                                                tfloat? maxRange = null, JobInfo jobInfo = default) {
            return CreateSelectionGroupByTypeInPointTemp(in trees, treeIndex, position, minRange != null ? minRange.Value : 0f, maxRange != null ? maxRange.Value : 5f, jobInfo);
        }

        [INLINE(256)]
        public static unsafe UnitSelectionTempGroupAspect CreateSelectionGroupByTypeInPointTemp(in QuadTreeInsertSystem trees, int treeIndex, float3 position, tfloat minRange, tfloat maxRange, JobInfo jobInfo = default) {

            var tree = trees.GetTree(treeIndex);
            var group = UnitUtils.CreateSelectionTempGroup(1u, jobInfo);

            var visitor = new OctreeNearestAABBVisitor<Ent, AlwaysTrueSubFilter>();
            tree.ptr->Nearest(position, minRange, maxRange, ref visitor, new AABBDistanceSquaredProvider<Ent>());
            if (visitor.found == true) {
                if (visitor.nearest.IsAlive() == true) group.Add(visitor.nearest.GetAspect<UnitAspect>());
            }
            
            return group;

        }

        [INLINE(256)]
        public static unsafe UnitSelectionGroupAspect CreateSelectionGroupByTypeInRange(in SystemContext context, int treeIndex, float3 position, uint unitTypeId, tfloat range, JobInfo jobInfo = default) {

            var tree = context.world.GetSystem<QuadTreeInsertSystem>().GetTree(treeIndex);
            var visitor = new RangeAABBUniqueVisitor<Ent, AlwaysTrueSubFilter>() {
                results = new Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<Ent>(10, Unity.Collections.Allocator.Temp),
                rangeSqr = range * range,
            };
            tree.ptr->Range(new NativeTrees.AABB(position - range, position + range), ref visitor);
            
            var group = UnitUtils.CreateSelectionGroup((uint)visitor.results.Count, jobInfo);
            foreach (var unit in visitor.results) {

                if (unit.IsAlive() == false) continue;
                if (unit.GetAspect<UnitAspect>().agentProperties.typeId == unitTypeId) {
                    group.Add(unit.GetAspect<UnitAspect>());
                }

            }

            return group;

        }

        [INLINE(256)]
        public static unsafe UnitSelectionTempGroupAspect CreateSelectionGroupByTypeInRangeTemp(in SystemContext context, int treeIndex, float3 position, uint unitTypeId, tfloat range, JobInfo jobInfo = default) {

            var tree = context.world.GetSystem<QuadTreeInsertSystem>().GetTree(treeIndex);
            var visitor = new RangeAABBUniqueVisitor<Ent, AlwaysTrueSubFilter>() {
                results = new Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<Ent>(10, Unity.Collections.Allocator.Temp),
                rangeSqr = range * range,
            };
            tree.ptr->Range(new NativeTrees.AABB(position - range, position + range), ref visitor);
            
            var group = UnitUtils.CreateSelectionTempGroup((uint)visitor.results.Count, jobInfo);
            foreach (var unit in visitor.results) {

                if (unit.IsAlive() == false) continue;
                if (unit.GetAspect<UnitAspect>().agentProperties.typeId == unitTypeId) {
                    group.Add(unit.GetAspect<UnitAspect>());
                }

            }

            return group;

        }

        [INLINE(256)]
        public static (float3 p1, float3 p2, float3 p3, float3 p4) GetScreenPoints(float3 screenPos1, float3 screenPos2) {

            var min = math.min(screenPos1, screenPos2);
            var max = math.max(screenPos1, screenPos2);
            
            var sp1 = new float3(min.x, max.y, 0f);
            var sp2 = new float3(max.x, max.y, 0f);
            var sp3 = new float3(max.x, min.y, 0f);
            var sp4 = new float3(min.x, min.y, 0f);
            
            return (sp1, sp2, sp3, sp4);

        }

        [INLINE(256)]
        public static (float3 p1, float3 p2, float3 p3, float3 p4) GetPoints(UnityEngine.Camera camera, int layersMask, float distance, float3 screenPos1, float3 screenPos2) {

            var p1 = float3.zero;
            var p2 = float3.zero;
            var p3 = float3.zero;
            var p4 = float3.zero;
            var min = math.min(screenPos1, screenPos2);
            var max = math.max(screenPos1, screenPos2);

            {
                var sp = new float3(min.x, max.y, 0f);
                var ray = camera.ScreenPointToRay((UnityEngine.Vector3)sp);
                if (UnityEngine.Physics.Raycast(ray, out var hit, distance, layersMask) == true) {
                    p1 = (float3)hit.point;
                }
            }
            {
                var sp = new float3(max.x, max.y, 0f);
                var ray = camera.ScreenPointToRay((UnityEngine.Vector3)sp);
                if (UnityEngine.Physics.Raycast(ray, out var hit, distance, layersMask) == true) {
                    p2 = (float3)hit.point;
                }
            }
            {
                var sp = new float3(max.x, min.y, 0f);
                var ray = camera.ScreenPointToRay((UnityEngine.Vector3)sp);
                if (UnityEngine.Physics.Raycast(ray, out var hit, distance, layersMask) == true) {
                    p3 = (float3)hit.point;
                }
            }
            {
                var sp = new float3(min.x, min.y, 0f);
                var ray = camera.ScreenPointToRay((UnityEngine.Vector3)sp);
                if (UnityEngine.Physics.Raycast(ray, out var hit, distance, layersMask) == true) {
                    p4 = (float3)hit.point;
                }
            }
            
            return (p1, p2, p3, p4);

        }

        [INLINE(256)]
        public static unsafe UnitSelectionTempGroupAspect CreateSelectionGroupByRectTemp(in QuadTreeInsertSystem trees, int treeIndex, float3 p1, float3 p2, float3 p3, float3 p4, in JobInfo jobInfo = default, bool selectMoveableOnly = false) {

            /*
            UnityEngine.Debug.DrawLine(p1, p2, UnityEngine.Color.cyan, 3f);
            UnityEngine.Debug.DrawLine(p2, p3, UnityEngine.Color.cyan, 3f);
            UnityEngine.Debug.DrawLine(p3, p4, UnityEngine.Color.cyan, 3f);
            UnityEngine.Debug.DrawLine(p4, p1, UnityEngine.Color.cyan, 3f);
            */
            
            var center = (p1 + p3) * 0.5f;
            var range = math.length(p3 - center);
            
            var tree = trees.GetTree(treeIndex);
            var results = new Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<Ent>(10, Unity.Collections.Allocator.Temp);
            if (selectMoveableOnly == true) {
                var visitor = new RangeMoveableAABBUniqueVisitor() {
                    results = results,
                    rangeSqr = range * range,
                };
                tree.ptr->Range(new NativeTrees.AABB(center - range, center + range), ref visitor);
                results = visitor.results;
            } else {
                var visitor = new RangeAABBUniqueVisitor<Ent, AlwaysTrueSubFilter>() {
                    results = results,
                    rangeSqr = range * range,
                };
                tree.ptr->Range(new NativeTrees.AABB(center - range, center + range), ref visitor);
                results = visitor.results;
            }
            
            var group = UnitUtils.CreateSelectionTempGroup((uint)results.Count, in jobInfo);
            foreach (var unit in results) {

                if (unit.IsAlive() == false) continue;
                var unitAspect = unit.GetAspect<UnitAspect>();
                var tr = unit.GetAspect<TransformAspect>();
                if (Math.IsInPolygon(tr.position, p1, p2, p3, p4) == true) {
                    
                    group.Add(unitAspect);
                    
                }

            }

            return group;

        }

        [INLINE(256)]
        public static unsafe UnitSelectionGroupAspect CreateSelectionGroupByRect(in QuadTreeInsertSystem trees, int treeIndex, float3 p1, float3 p2, float3 p3, float3 p4, in JobInfo jobInfo = default, bool selectMoveableOnly = false) {

            /*
            UnityEngine.Debug.DrawLine(p1, p2, UnityEngine.Color.cyan, 3f);
            UnityEngine.Debug.DrawLine(p2, p3, UnityEngine.Color.cyan, 3f);
            UnityEngine.Debug.DrawLine(p3, p4, UnityEngine.Color.cyan, 3f);
            UnityEngine.Debug.DrawLine(p4, p1, UnityEngine.Color.cyan, 3f);
            */
            
            var center = (p1 + p3) * 0.5f;
            var range = math.length(p3 - center);
            
            var tree = trees.GetTree(treeIndex);
            var results = new Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<Ent>(10, Unity.Collections.Allocator.Temp);
            if (selectMoveableOnly == true) {
                var visitor = new RangeMoveableAABBUniqueVisitor() {
                    results = results,
                    rangeSqr = range * range,
                };
                tree.ptr->Range(new NativeTrees.AABB(center - range, center + range), ref visitor);
                results = visitor.results;
            } else {
                var visitor = new RangeAABBUniqueVisitor<Ent, AlwaysTrueSubFilter>() {
                    results = results,
                    rangeSqr = range * range,
                };
                tree.ptr->Range(new NativeTrees.AABB(center - range, center + range), ref visitor);
                results = visitor.results;
            }
            
            var group = UnitUtils.CreateSelectionGroup((uint)results.Count, in jobInfo);
            foreach (var unit in results) {

                if (unit.IsAlive() == false) continue;
                var unitAspect = unit.GetAspect<UnitAspect>();
                var tr = unit.GetAspect<TransformAspect>();
                if (Math.IsInPolygon(tr.position, p1, p2, p3, p4) == true) {
                    
                    group.Add(unitAspect);
                    
                }

            }

            return group;

        }

        /// <summary>
        /// Set current selection to player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="selectionGroup"></param>
        /// <param name="addToCurrentGroup">Shift. Always move unique units from current selection to the new one.</param>
        /// <param name="removeAddConcrete">Control. If unit is in current selection - remove, if unit is not in current selection - add</param>
        /// <param name="jobInfo"></param>
        [INLINE(256)]
        public static void SetSelectionGroup(in ME.BECS.Players.PlayerAspect player, in UnitSelectionTempGroupAspect selectionGroup, bool addToCurrentGroup, bool removeAddConcrete, in JobInfo jobInfo) {

            var groupEnt = player.currentSelection;
            if (groupEnt.IsAlive() == false) {
                // create selection group
                player.currentSelection = UnitUtils.CreateSelectionGroup(selectionGroup.units.Count, in jobInfo).ent;
                groupEnt = player.currentSelection;
            }

            var group = groupEnt.GetAspect<UnitSelectionGroupAspect>();
            if (removeAddConcrete == true) {
                // get all units from new group
                // if unit is in current group - remove
                // if unit is not in current group - add
                ref var units = ref group.units;
                for (uint i = 0u; i < selectionGroup.units.Count; ++i) {
                    var unit = selectionGroup.units[i];
                    if (units.Contains(unit) == false) {
                        group.Add(unit.GetAspect<UnitAspect>());
                    } else {
                        group.Remove(unit.GetAspect<UnitAspect>());
                    }
                }
            }
            if (addToCurrentGroup == true) {
                // merge groups
                ref var units = ref group.units;
                for (uint i = 0u; i < selectionGroup.units.Count; ++i) {
                    var unit = selectionGroup.units[i];
                    if (units.Contains(unit) == false) {
                        group.Add(unit.GetAspect<UnitAspect>());
                    }
                }
            }

            if (addToCurrentGroup == false && removeAddConcrete == false) {
                // replace all units in current group with selection group
                group.Replace(selectionGroup);
            }

            selectionGroup.Destroy();
            
        }

        [INLINE(256)]
        public static bool IsSelected(ME.BECS.Players.PlayerAspect activePlayer, in EntRO ent) {
            return activePlayer.readCurrentSelection.IsAlive() == true && ent.GetAspect<UnitAspect>().readUnitSelectionGroup == activePlayer.readCurrentSelection;
        }

    }

}