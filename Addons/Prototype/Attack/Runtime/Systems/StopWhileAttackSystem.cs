
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
        public struct JobSet : IJobParallelForAspectsComponents<AttackAspect, ParentComponent> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect sensor, ref ParentComponent parent) {

                var unit = parent.value;
                if (sensor.target.IsAlive() == true) {
                    unit.GetAspect<UnitAspect>().IsHold = true;
                }

            }

        }
        
        [BURST(CompileSynchronously = true)]
        public struct JobRotate : IJobParallelForAspects<AttackAspect, TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect sensor, ref TransformAspect transformAspect) {

                var unit = sensor.target;
                if (unit.IsAlive() == true) {
                    if (unit.GetAspect<UnitAspect>().IsPathFollow == true) return; 
                    transformAspect.rotation = quaternion.LookRotationSafe(unit.GetAspect<TransformAspect>().position - transformAspect.position, math.up());
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobRemove : IJobParallelForAspects<AttackAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect sensor) {

                var unit = sensor.ent.GetParent();
                if (sensor.target.IsAlive() == false) unit.GetAspect<UnitAspect>().IsHold = false;

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                                   .With<AttackTargetComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobSet, AttackAspect, ParentComponent>();
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