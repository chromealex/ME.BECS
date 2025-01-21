
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
        public struct MoveToAttackerJob : IJobFor2Aspects1Components<UnitAspect, TransformAspect, DamageTookEvent> {

            public BuildGraphSystem buildGraphSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit, ref TransformAspect transform, ref DamageTookEvent component) {

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
        public struct StopOnTargetJob : IJobFor1Aspects1Components<UnitAspect, UnitAttackCommandComponent> {

            public BuildGraphSystem buildGraphSystem;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit, ref UnitAttackCommandComponent target) {

                if (target.target.IsAlive() == true && AttackUtils.CanAttack(in unit, in target.target) == true) {
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
                                               .Schedule<MoveToAttackerJob, UnitAspect, TransformAspect, DamageTookEvent>(new MoveToAttackerJob() {
                                                   buildGraphSystem = context.world.GetSystem<BuildGraphSystem>(),
                                               });
            var dependsOnStop = context.Query(dependsOnMoveToTarget)
                            .Schedule<StopOnTargetJob, UnitAspect, UnitAttackCommandComponent>(new StopOnTargetJob() {
                                buildGraphSystem = buildGraphSystem,
                            });
            context.SetDependency(dependsOnStop);

        }

    }

}