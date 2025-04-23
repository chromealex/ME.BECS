#if FIXED_POINT
using tfloat = sfloat;
#else
using tfloat = System.Single;
#endif

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Can Fire system")]
    [RequiredDependencies(typeof(ReloadSystem))]
    public struct CanFireSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForAspects<AttackAspect> {

            public tfloat dt;
            
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
                        var parent = ent.Read<ME.BECS.Transforms.ParentComponent>().value.GetAspect<ME.BECS.Units.UnitAspect>();
                        if (ent.HasTag<CanFireWhileMovesTag>(true) == false && parent is { IsHold: false, IsStatic: false }) {
                            aspect.CanFire = false;
                        } else {
                            aspect.CanFire = true;
                        }
                    }

                }
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel().With<ReloadedComponent>().WithAny<AttackTargetComponent, AttackTargetsComponent>().Schedule<Job, AttackAspect>(new Job() {
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}