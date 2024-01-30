
namespace ME.BECS.Bullets {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Bullet fly system")]
    public struct FlySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct FlyJob : ME.BECS.Jobs.IJobParallelForAspect<BulletAspect, TransformAspect> {

            public float dt;
            
            public void Execute(ref BulletAspect aspect, ref TransformAspect tr) {

                if (aspect.config.autoTarget == true && aspect.component.targetEnt.IsAlive() == true) {
                    aspect.component.targetWorldPos = aspect.component.targetEnt.GetAspect<TransformAspect>().GetWorldMatrixPosition();
                }

                var prevPos = tr.position;
                tr.position = Math.MoveTowards(prevPos, aspect.component.targetWorldPos, aspect.config.speed * this.dt);
                tr.rotation = quaternion.LookRotationSafe(tr.position - prevPos, math.up());
                if (math.lengthsq(tr.position - aspect.component.targetWorldPos) <= 0.01f) {
                    aspect.IsReached = true;
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = API.Query(in context).Without<TargetReachedComponent>().ScheduleParallelFor<FlyJob, BulletAspect, TransformAspect>(new FlyJob() {
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}