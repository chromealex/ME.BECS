
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using ME.BECS.Pathfinding;
    using ME.BECS.Commands;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Move unit if it was damaged and is not attacking and without hold")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct MoveToAttackerSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct MoveToAttackerJob : IJobAspect<UnitAspect, TransformAspect> {

            public BuildGraphSystem buildGraphSystem;
            
            public void Execute(in JobInfo jobInfo, ref UnitAspect unit, ref TransformAspect transform) {

                var component = unit.ent.Read<DamageTookEvent>();
                if (component.source.IsAlive() == false) return;

                // move to attacker
                var result = AttackUtils.GetPositionToAttack(in unit, in component.source, this.buildGraphSystem.GetNodeSize(), out var worldPos);
                if (result == AttackUtils.PositionToAttack.MoveToPoint) {
                    CommandsUtils.SetCommand(in this.buildGraphSystem, in unit, new ME.BECS.Commands.CommandMove() {
                        targetPosition = worldPos,
                    }, jobInfo);
                } else if (result == AttackUtils.PositionToAttack.RotateToTarget) {
                    unit.ent.Set(new UnitLookAtComponent() {
                        target = component.source.GetAspect<TransformAspect>().GetWorldMatrixPosition(),
                    });
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct StopOnTargetJob : IJobAspect<UnitAspect> {

            public BuildGraphSystem buildGraphSystem;

            public void Execute(in JobInfo jobInfo, ref UnitAspect unit) {

                var target = unit.ent.Read<UnitAttackCommandComponent>();
                if (target.target.IsAlive() == true) {
                    var result = AttackUtils.GetPositionToAttack(in unit, in target.target, this.buildGraphSystem.GetNodeSize(), out var worldPos);
                    if (result == AttackUtils.PositionToAttack.RotateToTarget) {
                        // Stop unit to attack
                        unit.ent.Set(new UnitLookAtComponent() {
                            target = target.target.GetAspect<TransformAspect>().GetWorldMatrixPosition(),
                        });
                    } else {
                        PathUtils.UpdateTarget(in this.buildGraphSystem, unit.readUnitCommandGroup.GetAspect<UnitCommandGroupAspect>(), in worldPos, in jobInfo);
                    }
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
            var dependsOnMoveToTarget = context.Query()
                                               .Without<IsUnitStaticComponent>()
                                               .Without<PathFollowComponent>()
                                               .Without<AttackTargetComponent>()
                                               .Without<UnitHoldComponent>()
                                               .With<DamageTookEvent>()
                                               .Schedule<MoveToAttackerJob, UnitAspect, TransformAspect>(new MoveToAttackerJob() {
                                                   buildGraphSystem = context.world.GetSystem<BuildGraphSystem>(),
                                               });
            var dependsOnStop = context.Query(dependsOnMoveToTarget)
                            .With<UnitAttackCommandComponent>()
                            .Schedule<StopOnTargetJob, UnitAspect>(new StopOnTargetJob() {
                                buildGraphSystem = buildGraphSystem,
                            });
            context.SetDependency(dependsOnStop);

        }

    }

}