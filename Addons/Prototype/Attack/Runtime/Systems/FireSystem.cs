
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Transforms;
    using ME.BECS.Jobs;
    using ME.BECS.Bullets;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Fire system")]
    public struct FireSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct FireJob : ME.BECS.Jobs.IJobParallelForAspect<AttackAspect, TransformAspect, QuadTreeQueryAspect> {

            public void Execute(ref AttackAspect aspect, ref TransformAspect tr, ref QuadTreeQueryAspect query) {

                if (aspect.target.IsAlive() == true) {

                    var firePoint = ME.BECS.Bullets.BulletUtils.GetFirePoint(aspect.ent);
                    var pos = tr.position;
                    var rot = tr.rotation;
                    if (firePoint.IsAlive() == true) {
                        var firePointTr = firePoint.GetAspect<TransformAspect>();
                        pos = firePointTr.position;
                        rot = firePointTr.rotation;
                    }

                    BulletUtils.CreateBullet(pos, rot, query.query.treeMask, aspect.target, default, aspect.component.bulletConfig, aspect.component.bulletView, aspect.component.muzzleView);

                    // fire - reset target and reload
                    aspect.IsReloaded = false;

                }

                // we remove target to initiate the new search (may be another target will be better)
                aspect.SetTarget(default);

            }

        }
        
        public void OnUpdate(ref SystemContext context) {

            var dependsOn = API.Query(in context)
                               .With<ReloadedComponent>()
                               .With<AttackTargetComponent>()
                               .ScheduleParallelFor<FireJob, AttackAspect, TransformAspect, QuadTreeQueryAspect>();
            context.SetDependency(dependsOn);

        }

    }

}