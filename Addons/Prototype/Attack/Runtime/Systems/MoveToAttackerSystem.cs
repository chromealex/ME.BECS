
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
        public struct Job : ME.BECS.Jobs.IJobAspect<UnitAspect, TransformAspect> {

            public BuildGraphSystem buildGraphSystem;
            
            public void Execute(ref UnitAspect unit, ref TransformAspect tr) {

                var attacker = unit.ent.Read<DamageTookComponent>().sourceUnit;
                if (attacker.IsAlive() == false) return;
                
                // move to attacker
                // remove from current group
                PathUtils.RemoveUnitFromGroup(in unit);
                // create new group
                var group = UnitUtils.CreateCommandGroup(this.buildGraphSystem.GetTargetsCapacity());
                group.Add(in unit);

                if (AttackUtils.GetPositionToAttack(in unit, in attacker, out var worldPos) == true) {
                    // move unit to target
                    PathUtils.UpdateTarget(this.buildGraphSystem, in group, worldPos);
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
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