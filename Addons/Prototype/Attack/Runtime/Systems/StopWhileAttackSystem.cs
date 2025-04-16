#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

using ME.BECS.Transforms;

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Stop unit while attacking")]
    public struct StopWhileAttackSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct JobSet : IJobFor1Aspects1Components<AttackAspect, ParentComponent> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect sensor, ref ParentComponent parent) {

                var unit = parent.value;
                if (sensor.target.IsAlive() == true) {
                    var unitAspect = unit.GetAspect<UnitAspect>();
                    if (unitAspect.IsPathFollow == false) unitAspect.IsHold = true;
                }

            }

        }
        
        [BURST(CompileSynchronously = true)]
        public struct JobRotate : IJobForAspects<AttackAspect, TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect sensor, ref TransformAspect transformAspect) {

                var unit = sensor.target;
                if (unit.IsAlive() == true) {
                    if (unit.GetAspect<UnitAspect>().IsPathFollow == true) return; 
                    transformAspect.rotation = quaternion.LookRotationSafe(unit.GetAspect<TransformAspect>().position - transformAspect.position, math.up());
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobRemove : IJobForAspects<AttackAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect sensor) {

                var unit = sensor.ent.GetParent();
                if (sensor.target.IsAlive() == false) unit.GetAspect<UnitAspect>().IsHold = false;

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                                   .AsParallel()
                                   .WithAny<AttackTargetComponent, AttackTargetsComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobSet, AttackAspect, ParentComponent>();
            dependsOn = context.Query(dependsOn)
                                   .AsParallel()
                                   .WithAny<AttackTargetComponent, AttackTargetsComponent>()
                                   .With<RotateAttackSensorComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobRotate, AttackAspect, TransformAspect>();
            dependsOn = context.Query(dependsOn)
                                   .AsParallel()
                                   .WithAny<AttackTargetComponent, AttackTargetsComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobRemove, AttackAspect>();
            context.SetDependency(dependsOn);

        }

    }

}