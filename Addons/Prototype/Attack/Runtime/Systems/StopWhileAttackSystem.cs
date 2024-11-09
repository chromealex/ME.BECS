
using ME.BECS.Transforms;

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Stop unit while attacking")]
    public struct StopWhileAttackSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct JobSet : IJobParallelForAspect<AttackAspect> {

            public void Execute(in JobInfo jobInfo, ref AttackAspect sensor) {

                var unit = sensor.ent.GetParent();
                if (sensor.target.IsAlive() == true) {
                    unit.GetAspect<UnitAspect>().IsHold = true;
                }

            }

        }
        
        [BURST(CompileSynchronously = true)]
        public struct JobRotate : IJobParallelForAspect<AttackAspect, TransformAspect> {

            public void Execute(in JobInfo jobInfo, ref AttackAspect sensor, ref TransformAspect transformAspect) {

                var unit = sensor.target;
                if (unit.IsAlive() == true) {
                    if (unit.GetAspect<UnitAspect>().IsPathFollow == true) return; 
                    transformAspect.rotation = quaternion.LookRotationSafe(unit.GetAspect<TransformAspect>().position - transformAspect.position, math.up());
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobRemove : IJobParallelForAspect<AttackAspect> {

            public void Execute(in JobInfo jobInfo, ref AttackAspect sensor) {

                var unit = sensor.ent.GetParent();
                if (sensor.target.IsAlive() == false) unit.GetAspect<UnitAspect>().IsHold = false;

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                                   .With<AttackTargetComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobSet, AttackAspect>();
            dependsOn = context.Query(dependsOn)
                                   .With<AttackTargetComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobRotate, AttackAspect, TransformAspect>();
            dependsOn = context.Query(dependsOn)
                                   .Without<AttackTargetComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobRemove, AttackAspect>();
            context.SetDependency(dependsOn);

        }

    }

}