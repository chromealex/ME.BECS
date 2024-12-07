namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Can Fire system")]
    [RequiredDependencies(typeof(ReloadSystem))]
    public struct CanFireSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForAspects<AttackAspect> {

            public float dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect) {

                aspect.componentRuntimeFire.fireTimer += this.dt;
                if (aspect.readComponentRuntimeFire.fireTimer >= aspect.readComponent.fireTime) {

                    // time to attack is up
                    // fire - reset target and reload
                    aspect.IsReloaded = false;
                    aspect.CanFire = false;

                }
                
                if (aspect.IsFireUsed() == false) {

                    // use default attack time
                    if (aspect.readComponentRuntimeFire.fireTimer >= aspect.readComponent.attackTime) {
                        // Fire
                        aspect.CanFire = true;
                    }

                }
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel().With<ReloadedComponent>().With<AttackTargetComponent>().Schedule<Job, AttackAspect>(new Job() {
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}