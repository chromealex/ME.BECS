
using ME.BECS.Transforms;

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Rotate unit while attacking")]
    public struct RotateWhileAttackSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<UnitAspect, TransformAspect> {

            public float dt;
            
            public void Execute(in JobInfo jobInfo, ref UnitAspect unit, ref TransformAspect transformAspect) {

                var attack = unit.readComponentRuntime.attackSensor.GetAspect<AttackAspect>();
                if (attack.target.IsAlive() == true) {
                    UnitUtils.LookToTarget(ref transformAspect, in unit, attack.target.GetAspect<TransformAspect>().position, this.dt);
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                                   .With<RotateToAttackWhileIdleComponent>()
                                   .Without<PathFollowComponent>()
                                   .Schedule<Job, UnitAspect, TransformAspect>(new Job() {
                                       dt = context.deltaTime,
                                   });
            context.SetDependency(dependsOn);

        }

    }

}