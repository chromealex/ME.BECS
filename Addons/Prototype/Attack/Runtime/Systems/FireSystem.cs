#if FIXED_POINT
using tfloat = sfloat;
#else
using tfloat = System.Single;
#endif

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Transforms;
    using ME.BECS.Jobs;
    using ME.BECS.Bullets;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Fire system")]
    public struct FireSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct FireJob : IJobForAspects<AttackAspect, TransformAspect, QuadTreeQueryAspect> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect, ref TransformAspect tr, ref QuadTreeQueryAspect query) {

                if (aspect.target.IsAlive() == true) {

                    if (aspect.RateFire(this.dt) == true) {

                        var firePoint = ME.BECS.Bullets.BulletUtils.GetNextFirePoint(aspect.ent);
                        var pos = tr.GetWorldMatrixPosition();
                        var rot = tr.GetWorldMatrixRotation();
                        if (firePoint.IsAlive() == true) {
                            var firePointTr = firePoint.GetAspect<TransformAspect>();
                            pos = firePointTr.GetWorldMatrixPosition();
                            rot = firePointTr.GetWorldMatrixRotation();
                        }

                        BulletUtils.CreateBullet(aspect.ent.GetParent(), pos, rot, query.readQuery.treeMask, aspect.target, default, aspect.readComponent.bulletConfig,
                                                 aspect.readComponent.muzzleView, jobInfo: jobInfo);

                        aspect.UseFire();

                    }

                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                               .With<ReloadedComponent>()
                               .With<CanFireComponent>()
                               .Without<FireUsedComponent>()
                               .With<AttackTargetComponent>()
                               .Schedule<FireJob, AttackAspect, TransformAspect, QuadTreeQueryAspect>(new FireJob() {
                                   dt = context.deltaTime,
                               });
            context.SetDependency(dependsOn);

        }

    }

}