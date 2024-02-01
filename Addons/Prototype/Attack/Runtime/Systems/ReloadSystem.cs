
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Reload system")]
    public struct ReloadSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct ReloadJob : ME.BECS.Jobs.IJobParallelForAspect<AttackAspect> {

            public float dt;
            
            public void Execute(ref AttackAspect aspect) {

                aspect.component.reloadTimer += this.dt;
                if (aspect.component.reloadTimer >= aspect.component.reloadTime) {

                    aspect.IsReloaded = true;

                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Without<ReloadedComponent>().ScheduleParallelFor<ReloadJob, AttackAspect>(new ReloadJob() {
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}