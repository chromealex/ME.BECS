
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Can Fire system")]
    [RequiredDependencies(typeof(ReloadSystem))]
    public struct CanFireSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<AttackAspect> {

            public float dt;
            
            public void Execute(in JobInfo jobInfo, ref AttackAspect aspect) {

                aspect.component.fireTimer += this.dt;
                if (aspect.component.fireTimer >= aspect.component.fireTime) {

                    aspect.CanFire = true;

                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().With<ReloadedComponent>().Without<CanFireComponent>().Schedule<Job, AttackAspect>(new Job() {
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}