
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Transforms;
    using ME.BECS.Jobs;
    using ME.BECS.Bullets;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Fire system")]
    public struct FireSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct FireJob : IJobAspect<AttackAspect, TransformAspect, QuadTreeQueryAspect> {

            public void Execute(in JobInfo jobInfo, ref AttackAspect aspect, ref TransformAspect tr, ref QuadTreeQueryAspect query) {

                if (aspect.target.IsAlive() == true) {

                    var firePoint = ME.BECS.Bullets.BulletUtils.GetFirePoint(aspect.ent);
                    var pos = tr.GetWorldMatrixPosition();
                    var rot = tr.GetWorldMatrixRotation();
                    if (firePoint.IsAlive() == true) {
                        var firePointTr = firePoint.GetAspect<TransformAspect>();
                        pos = firePointTr.GetWorldMatrixPosition();
                        rot = firePointTr.GetWorldMatrixRotation();
                    }

                    BulletUtils.CreateBullet(aspect.ent, pos, rot, query.query.treeMask, aspect.target, default, aspect.component.bulletConfig, aspect.component.bulletView,
                                             aspect.component.muzzleView, jobInfo: jobInfo);

                    // fire - reset target and reload
                    aspect.IsReloaded = false;

                }

                // we remove target to initiate the new search (may be another target will be better)
                aspect.SetTarget(default);

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query()
                               .With<ReloadedComponent>()
                               .With<AttackTargetComponent>()
                               .Schedule<FireJob, AttackAspect, TransformAspect, QuadTreeQueryAspect>();
            context.SetDependency(dependsOn);

        }

    }

}