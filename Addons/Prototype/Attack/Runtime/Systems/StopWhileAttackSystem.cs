
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
                    unit.Set(new IsUnitStaticComponent());
                    var tr = unit.GetAspect<TransformAspect>();
                    tr.rotation = quaternion.LookRotationSafe(sensor.target.GetAspect<TransformAspect>().position - tr.position, math.up());
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobRemove : IJobParallelForAspect<AttackAspect> {

            public void Execute(in JobInfo jobInfo, ref AttackAspect sensor) {

                var unit = sensor.ent.GetParent();
                if (sensor.target.IsAlive() == false) unit.Remove<IsUnitStaticComponent>();

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                                   .With<AttackTargetComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobSet, AttackAspect>();
            dependsOn = context.Query(dependsOn)
                                   .Without<AttackTargetComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobRemove, AttackAspect>();
            context.SetDependency(dependsOn);

        }

    }

}