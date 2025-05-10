#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

using ME.BECS.Transforms;

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Rotate unit while attacking")]
    public struct RotateWhileAttackSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct IdleJob : IJobForAspects<UnitAspect, TransformAspect> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit, ref TransformAspect transformAspect) {

                var attack = unit.readComponentRuntime.attackSensor.GetAspect<AttackAspect>();
                if (attack.target.IsAlive() == true) {
                    UnitUtils.LookToTarget(in transformAspect, in unit, attack.target.GetAspect<TransformAspect>().position, this.dt);
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct RotateAttackSensorJob : IJobForAspects<AttackAspect, TransformAspect> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect attack, ref TransformAspect transformAspect) {

                var speedFactor = attack.ent.Read<RotateAttackSensorComponent>().speedFactor;
                if (attack.target.IsAlive() == true) {
                    var lookDir = attack.target.GetAspect<TransformAspect>().GetWorldMatrixPosition() - transformAspect.GetWorldMatrixPosition();
                    transformAspect.rotation = math.slerp(transformAspect.rotation, quaternion.LookRotation(lookDir, math.up()), this.dt * speedFactor);
                } else if (attack.targets.Count > 0u) {
                    var lookDir = attack.targets[0u].GetAspect<TransformAspect>().GetWorldMatrixPosition() - transformAspect.GetWorldMatrixPosition();
                    transformAspect.rotation = math.slerp(transformAspect.rotation, quaternion.LookRotation(lookDir, math.up()), this.dt * speedFactor);
                } else {
                    transformAspect.localRotation = math.slerp(transformAspect.readLocalRotation, quaternion.identity, this.dt * speedFactor);
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel()
                                   .With<RotateToAttackWhileIdleComponent>()
                                   .Without<PathFollowComponent>()
                                   .AsUnsafe()
                                   .Schedule<IdleJob, UnitAspect, TransformAspect>(new IdleJob() {
                                       dt = context.deltaTime,
                                   });
            var dependsOnAttackSensor = context.Query().AsParallel()
                                   .With<RotateAttackSensorComponent>()
                                   .AsUnsafe()
                                   .Schedule<RotateAttackSensorJob, AttackAspect, TransformAspect>(new RotateAttackSensorJob() {
                                       dt = context.deltaTime,
                                   });
            context.SetDependency(dependsOn, dependsOnAttackSensor);

        }

    }

}