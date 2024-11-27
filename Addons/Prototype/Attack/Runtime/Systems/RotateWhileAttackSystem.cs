
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
        public struct RotateAttackSensorJob : IJobParallelForAspect<UnitAspect, TransformAspect> {

            public float dt;
            
            public void Execute(in JobInfo jobInfo, ref UnitAspect unit, ref TransformAspect transformAspect) {

                var speedFactor = unit.ent.Read<RotateAttackSensorComponent>().speedFactor;
                var attack = unit.readComponentRuntime.attackSensor.GetAspect<AttackAspect>();
                if (attack.target.IsAlive() == true) {
                    UnitUtils.LookToTarget(attack.ent.GetAspect<TransformAspect>(), in unit, attack.target.GetAspect<TransformAspect>().position, this.dt * speedFactor);
                } else {
                    var speed = unit.rotationSpeed;
                    var tr = attack.ent.GetAspect<TransformAspect>();
                    tr.localRotation = math.slerp(tr.localRotation, quaternion.identity, this.dt * speed * speedFactor);
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                                   .With<RotateToAttackWhileIdleComponent>()
                                   .Without<PathFollowComponent>()
                                   .Schedule<IdleJob, UnitAspect, TransformAspect>(new IdleJob() {
                                       dt = context.deltaTime,
                                   });
            var dependsOnAttackSensor = context.Query()
                                   .With<RotateAttackSensorComponent>()
                                   .Schedule<RotateAttackSensorJob, UnitAspect, TransformAspect>(new RotateAttackSensorJob() {
                                       dt = context.deltaTime,
                                   });
            context.SetDependency(dependsOn, dependsOnAttackSensor);

        }

    }

}