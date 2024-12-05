
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
        public struct IdleJob : IJobParallelForAspect<UnitAspect, TransformAspect> {

            public float dt;
            
            public void Execute(in JobInfo jobInfo, ref UnitAspect unit, ref TransformAspect transformAspect) {

                var attack = unit.readComponentRuntime.attackSensor.GetAspect<AttackAspect>();
                if (attack.target.IsAlive() == true) {
                    UnitUtils.LookToTarget(in transformAspect, in unit, attack.target.GetAspect<TransformAspect>().position, this.dt);
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct RotateAttackSensorJob : IJobParallelForAspect<AttackAspect, TransformAspect> {

            public float dt;
            
            public void Execute(in JobInfo jobInfo, ref AttackAspect attack, ref TransformAspect transformAspect) {

                var speedFactor = attack.ent.Read<RotateAttackSensorComponent>().speedFactor;
                if (attack.target.IsAlive() == true) {
                    var tr = attack.ent.GetAspect<TransformAspect>();
                    var lookDir = attack.target.GetAspect<TransformAspect>().GetWorldMatrixPosition() - transformAspect.GetWorldMatrixPosition();
                    tr.rotation = math.slerp(tr.rotation, quaternion.LookRotation(lookDir, math.up()), this.dt * speedFactor);
                } else {
                    var tr = attack.ent.GetAspect<TransformAspect>();
                    tr.localRotation = math.slerp(tr.readLocalRotation, quaternion.identity, this.dt * speedFactor);
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                                   .With<RotateToAttackWhileIdleComponent>()
                                   .Without<PathFollowComponent>()
                                   .AsUnsafe()
                                   .Schedule<IdleJob, UnitAspect, TransformAspect>(new IdleJob() {
                                       dt = context.deltaTime,
                                   });
            var dependsOnAttackSensor = context.Query()
                                   .With<RotateAttackSensorComponent>()
                                   .AsUnsafe()
                                   .Schedule<RotateAttackSensorJob, AttackAspect, TransformAspect>(new RotateAttackSensorJob() {
                                       dt = context.deltaTime,
                                   });
            context.SetDependency(dependsOn, dependsOnAttackSensor);

        }

    }

}