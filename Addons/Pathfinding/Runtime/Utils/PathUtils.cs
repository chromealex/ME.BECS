namespace ME.BECS.Pathfinding {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
    using ME.BECS.Units;
    
    public static class PathUtils {

        public const float DEFAULT_VOLUME_RADIUS = 2f;
        public const float RADIUS_FACTOR = math.PI;
        
        [INLINE(256)]
        public static unsafe void UpdateTarget(in BuildGraphSystem buildGraphSystem, in UnitCommandGroupAspect unitCommandGroup, in float3 position) {
            
            var typeIds = new Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<uint>(buildGraphSystem.graphs.Length, Unity.Collections.Allocator.Temp);
            for (uint i = 0; i < unitCommandGroup.units.Count; ++i) {
                var unit = unitCommandGroup.units[i].GetAspect<UnitAspect>();
                typeIds.Add(unit.typeId);
            }
            
            // clamp position for each graph to find middle point
            var middlePoint = float3.zero;
            if (typeIds.Count > 0) {
                foreach (var typeId in typeIds) {
                    var graph = buildGraphSystem.GetGraphByTypeId(typeId);
                    var pos = GraphUtils.GetNearestNodeByFilter(in graph, position, default);
                    middlePoint += pos;
                }
                middlePoint /= typeIds.Count;
            } else {
                middlePoint = position;
            }
            
            // Update only last chunk if target chunk is equal with previous
            var nodeChanged = true;
            var chunkChanged = true;
            if (unitCommandGroup.targets.isCreated == true) {
                chunkChanged = false;
                nodeChanged = false;
                var state = unitCommandGroup.ent.World.state;
                foreach (var typeId in typeIds) {
                    var target = unitCommandGroup.targets[typeId];
                    if (target.IsAlive() == true) {
                        var root = buildGraphSystem.GetGraphByTypeId(typeId).Read<RootGraphComponent>();
                        var chunkIndex = Graph.GetChunkIndex(in root, position);
                        ref var prevPath = ref target.Get<TargetPathComponent>().path;
                        var prevRoot = prevPath.graph.Read<RootGraphComponent>();
                        prevPath.chunks[state, chunkIndex].flowField.Dispose(ref state->allocator);
                        var prevChunkIndex = Graph.GetChunkIndex(in prevRoot, prevPath.to);
                        if (chunkIndex != prevChunkIndex) {
                            // chunk changed - repath
                            chunkChanged = true;
                            nodeChanged = true;
                            break;
                        }
                        var nodeIndex = Graph.GetNodeIndex(in root, in root.chunks[state, chunkIndex], position);
                        var prevNodeIndex = Graph.GetNodeIndex(in prevRoot, in prevRoot.chunks[state, prevChunkIndex], prevPath.to);
                        if (nodeIndex != prevNodeIndex) {
                            nodeChanged = true;
                        }
                    } else {
                        chunkChanged = true;
                        nodeChanged = true;
                        break;
                    }
                }
            }

            if (chunkChanged == false) {

                if (nodeChanged == true) {

                    // we don't need to repath - use previous path
                    // so we just need to change last path chunk
                    foreach (var typeId in typeIds) {
                        var target = unitCommandGroup.targets[typeId];
                        if (target.IsAlive() == true) {
                            target.Read<TargetComponent>().target.Get<TargetInfoComponent>().position = middlePoint;
                            ref var prevPath = ref target.Get<TargetPathComponent>().path;
                            prevPath.to = middlePoint;
                        }
                    }

                } else {

                    // we need to update last chunk only if last node has been changed
                    return;

                }

            } else {

                // destroy previous target if set
                PathUtils.DestroyTargets(in unitCommandGroup);

                // set target for each unique type id in group
                {
                    var targetInfo = CreateTargetInfo(in middlePoint);
                    foreach (var typeId in typeIds) {
                        PathUtils.AddTarget(in buildGraphSystem, in unitCommandGroup, typeId, in targetInfo);
                    }
                }

            }
            
            // set path follow flag
            for (uint i = 0; i < unitCommandGroup.units.Count; ++i) {
                var unit = unitCommandGroup.units[i];
                var aspect = unit.GetAspect<UnitAspect>();
                aspect.IsPathFollow = true;
                aspect.collideWithEnd = false;
            }

        }

        [INLINE(256)]
        private static void AddTarget(in BuildGraphSystem buildGraphSystem, in UnitCommandGroupAspect unitCommandGroup, uint typeId, in Ent targetInfo) {

            var targetEnt = Ent.New();
            targetEnt.Set(TargetComponent.Create(in targetInfo, buildGraphSystem.GetGraphByTypeId(typeId)));
            unitCommandGroup.targets[typeId] = targetEnt;

        }

        [INLINE(256)]
        public static void DestroyTargets(in UnitCommandGroupAspect unitCommandGroup) {
            for (uint i = 0; i < unitCommandGroup.targets.Length; ++i) {
                ref var target = ref unitCommandGroup.targets[i];
                if (target.IsAlive() == false) continue;
                var targetComponent = target.Read<TargetComponent>();
                if (targetComponent.target.IsAlive() == false) continue;
                ref var pathComponent = ref target.Get<TargetPathComponent>();
                pathComponent.path.Dispose(in unitCommandGroup.ent.World);
            }

            for (uint i = 0; i < unitCommandGroup.targets.Length; ++i) {
                ref var target = ref unitCommandGroup.targets[i];
                if (target.IsAlive() == false) continue;
                var targetComponent = target.Read<TargetComponent>();
                if (targetComponent.target.IsAlive() == false) continue;
                targetComponent.target.Destroy();
                target.Destroy();
                target = default;
            }
        }
        
        [INLINE(256)]
        public static Ent CreateTargetInfo(in float3 position) {
            var ent = Ent.New();
            ent.Set(new TargetInfoComponent() {
                position = position,
                volume = (int)(DEFAULT_VOLUME_RADIUS * UnitUtils.FLOAT_TO_UINT),
            });
            return ent;
        }

        [INLINE(256)]
        public static void SetArrived(in UnitAspect unit) {

            ref var target = ref unit.unitCommandGroup.GetAspect<UnitCommandGroupAspect>().targets[unit.typeId].Read<TargetComponent>().target.Get<TargetInfoComponent>();
            JobUtils.Increment(ref target.volume, Units.UnitUtils.GetVolume(in unit));

        }

        [INLINE(256)]
        public static bool HasArrived(in Transforms.TransformAspect tr, in UnitAspect unit) {

            var group = unit.unitCommandGroup.GetAspect<UnitCommandGroupAspect>();
            var targetComponent = group.targets[unit.typeId].Read<TargetComponent>();
            var target = targetComponent.target.Read<TargetInfoComponent>();
            var targetRadiusSqr = GetTargetRadiusSqr(in targetComponent);
            return math.lengthsq(target.position - tr.position) <= targetRadiusSqr;

        }

        [INLINE(256)]
        public static float GetGroupRadiusSqr(in UnitCommandGroupAspect commandGroup) {

            return commandGroup.readVolume / math.PI * RADIUS_FACTOR;

        }
        
        [INLINE(256)]
        public static float GetTargetRadiusSqr(in TargetComponent target) {

            return target.target.Read<TargetInfoComponent>().volume * UnitUtils.UINT_TO_FLOAT / math.PI * RADIUS_FACTOR;

        }

        [INLINE(256)]
        public static void RemoveUnitFromGroup(in UnitAspect unit) {

            var group = unit.unitCommandGroup.GetAspect<UnitCommandGroupAspect>();
            group.Lock();
            if (unit.WillRemoveCommandGroup() == true) {
                // group will be removed - remove path
                PathUtils.DestroyTargets(in group);
            }

            unit.RemoveFromCommandGroup();
            group.Unlock();

        }

    }

}