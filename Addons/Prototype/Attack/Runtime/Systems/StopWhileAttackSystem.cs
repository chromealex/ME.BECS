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
using Unity.Jobs;

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;

    [BURST]
    [UnityEngine.Tooltip("Stop unit while attacking")]
    public struct StopWhileAttackSystem : IUpdate {

        [BURST]
        public struct JobSet : IJobFor1Aspects1Components<AttackAspect, ParentComponent> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect sensor, ref ParentComponent parent) {

                var unit = parent.value;
                if (sensor.target.IsAlive() == true) {
                    var unitAspect = unit.GetAspect<UnitAspect>();
                    if (unitAspect.IsPathFollow == false && sensor.readComponentRuntimeFire.fireTimer > 0f) {
                        unitAspect.IsHold = true;
                    }
                }

            }

        }
        
        [BURST]
        public struct JobRemove : IJobForAspects<AttackAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect sensor) {

                var unit = sensor.ent.GetParent();
                if (sensor.target.IsAlive() == false || sensor.readComponentRuntimeFire.fireTimer <= 0f) unit.GetAspect<UnitAspect>().IsHold = false;

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOnSet = context.Query()
                                   .AsParallel()
                                   .AsUnsafe()
                                   .WithAny<AttackTargetComponent, AttackTargetsComponent>()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobSet, AttackAspect, ParentComponent>();
            var dependsOnRemove = context.Query()
                                   .AsParallel()
                                   .AsUnsafe()
                                   .Without<CanFireWhileMovesTag>()
                                   .Schedule<JobRemove, AttackAspect>();
            context.SetDependency(dependsOnSet, dependsOnRemove);

        }

    }

}