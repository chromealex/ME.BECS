
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Reload system")]
    public struct ReloadSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct ReloadJob : IJobParallelForAspect<AttackAspect> {

            public float dt;
            
            public void Execute(in JobInfo jobInfo, ref AttackAspect aspect) {

                aspect.componentRuntime.reloadTimer += this.dt;
                if (aspect.componentRuntime.reloadTimer >= aspect.component.reloadTime) {

                    aspect.IsReloaded = true;

                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Without<ReloadedComponent>().Schedule<ReloadJob, AttackAspect>(new ReloadJob() {
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}