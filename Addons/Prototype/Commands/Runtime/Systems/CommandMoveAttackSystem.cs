using ME.BECS.Transforms;

namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;

    [BURST]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct CommandMoveAttackSystem : IUpdate {

        [BURST]
        public struct CleanUpJob : IJobForAspects<UnitAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit) {

                if (unit.readUnitCommandGroup.IsAlive() == false || unit.readUnitCommandGroup.Has<CommandMoveAttack>() == false) {
                    unit.ent.Remove<UnitAttackOnMoveCommandComponent>();
                    unit.IsHold = false;
                }
                
            }

        }

        [BURST]
        public struct Job : IJobForAspects<UnitCommandGroupAspect> {

            public BuildGraphSystem buildGraphSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitCommandGroupAspect commandGroup) {

                var move = commandGroup.ent.Read<CommandMoveAttack>();
                var target = Path.Target.Create(move.targetPosition);
                if (move.targets.IsCreated == true) Path.Target.Create(move.targets);
                PathUtils.UpdateTarget(in this.buildGraphSystem, in commandGroup, in target, in jobInfo);
                
                for (uint i = 0u; i < commandGroup.readUnits.Count; ++i) {
                    var u = commandGroup.readUnits[i];
                    if (u.IsAlive() == false) continue;
                    var unit = u.GetAspect<UnitAspect>();
                    unit.IsHold = false;
                }
                
                commandGroup.ent.SetTag<IsCommandGroupDirty>(false);
                
            }

        }

        [BURST]
        public struct StopToAttackJob : IJobForAspects<UnitCommandGroupAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitCommandGroupAspect commandGroup) {

                var isDirty = false;
                for (uint i = 0u; i < commandGroup.readUnits.Count; ++i) {
                    var unit = commandGroup.readUnits[i];
                    if (unit.IsAlive() == false) continue;
                    Ent target = default;
                    var attackSensor = unit.GetAspect<UnitAspect>().readComponentRuntime.attackSensor;
                    if (attackSensor.Has<QuadTreeQuery>() == true) {
                        var query = attackSensor.GetAspect<QuadTreeQueryAspect>();
                        if (query.readResults.results.Count > 0u) {
                            target = query.readResults.results[0];
                        }
                    } else if (attackSensor.Has<OctreeQuery>() == true) {
                        var query = attackSensor.GetAspect<OctreeQueryAspect>();
                        if (query.readResults.results.Count > 0u) {
                            target = query.readResults.results[0];
                        }
                    } else if (attackSensor.Has<SpatialQuery>() == true) {
                        var query = attackSensor.GetAspect<SpatialQueryAspect>();
                        if (query.readResults.results.Count > 0u) {
                            target = query.readResults.results[0];
                        }
                    }

                    if (target.IsAlive() == true) {
                        ref var data = ref unit.Get<UnitAttackOnMoveCommandComponent>();
                        if (data.target != target) {
                            data.target = target;
                            isDirty = true;
                        }
                    } else {
                        if (unit.Remove<UnitAttackOnMoveCommandComponent>() == true) {
                            isDirty = true;
                        }
                    }
                }

                if (isDirty == true) {
                    ent.SetTag<IsCommandGroupDirty>(true);
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
            context.Query().With<UnitAttackOnMoveCommandComponent>().AsParallel().Schedule<CleanUpJob, UnitAspect>().AddDependency(ref context);
            context.Query().AsUnsafe().With<CommandMoveAttack>().With<IsCommandGroupDirty>().Schedule<Job, UnitCommandGroupAspect>(new Job() {
                buildGraphSystem = buildGraphSystem,
            }).AddDependency(ref context);
            context.Query().AsUnsafe().With<CommandMoveAttack>().Schedule<StopToAttackJob, UnitCommandGroupAspect>().AddDependency(ref context);
            
        }

    }

}