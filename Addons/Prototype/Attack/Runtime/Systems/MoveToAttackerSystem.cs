
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Transforms;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using ME.BECS.Pathfinding;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Move unit if it was damaged and is not attacking and without hold")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct MoveToAttackerSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobAspect<UnitAspect, TransformAspect> {

            public BuildGraphSystem buildGraphSystem;
            
            // TODO: Make this job as parallel
            // may be we need to group by DamageTookComponent.sourceUnit first?
            public void Execute(in JobInfo jobInfo, ref UnitAspect unit, ref TransformAspect tr) {

                var attacker = unit.ent.Read<DamageTookComponent>().sourceUnit;
                if (attacker.IsAlive() == false) return;
                
                // move to attacker
                if (AttackUtils.GetPositionToAttack(in unit, in attacker, out var worldPos) == true) {
                    ME.BECS.Commands.CommandsUtils.SetCommand(in this.buildGraphSystem, in unit, new ME.BECS.Commands.CommandMove() {
                        targetPosition = worldPos,
                    }, jobInfo);
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                                   .Without<IsUnitStaticComponent>()
                                   .Without<PathFollowComponent>()
                                   .With<DamageTookComponent>()
                                   .Without<AttackTargetComponent>()
                                   .Without<UnitHoldComponent>()
                                   .Schedule<Job, UnitAspect, TransformAspect>(new Job() {
                                       buildGraphSystem = context.world.GetSystem<BuildGraphSystem>(),
                                   });
            context.SetDependency(dependsOn);

        }

    }

}