#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
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
        public struct FireTargetJob : IJobForAspects<AttackAspect, TransformAspect, QuadTreeQueryAspect> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect, ref TransformAspect tr, ref QuadTreeQueryAspect query) {

                if (aspect.target.IsAlive() == true) {

                    if (aspect.RateFire(this.dt) == true) {

                        var firePoint = BulletUtils.GetNextFirePoint(aspect.ent);
                        var pos = tr.GetWorldMatrixPosition();
                        var rot = tr.GetWorldMatrixRotation();
                        if (firePoint.IsAlive() == true) {
                            var firePointTr = firePoint.GetAspect<TransformAspect>();
                            pos = firePointTr.GetWorldMatrixPosition();
                            rot = firePointTr.GetWorldMatrixRotation();
                        }

                        var target = aspect.target;
                        float3 targetPosition = default;
                        if (aspect.componentRuntimeFire.targets.Length > 0u) {
                            target = default;
                            targetPosition = aspect.componentRuntimeFire.targets[0u];
                        }

                        AttackUtils.CreateBullet(aspect, pos, rot, query.readQuery.treeMask, in target, in targetPosition, aspect.readComponentVisual.bulletConfig,
                                                 aspect.readComponentVisual.muzzleView, jobInfo: jobInfo);

                        aspect.UseFire();

                    }

                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct FireTargetsJob : IJobForAspects<AttackAspect, TransformAspect, QuadTreeQueryAspect> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect, ref TransformAspect tr, ref QuadTreeQueryAspect query) {

                if (aspect.targets.IsCreated == true) {

                    if (aspect.RateFire(this.dt) == true) {

                        var firePoint = BulletUtils.GetNextFirePoint(aspect.ent);
                        var pos = tr.GetWorldMatrixPosition();
                        var rot = tr.GetWorldMatrixRotation();
                        if (firePoint.IsAlive() == true) {
                            var firePointTr = firePoint.GetAspect<TransformAspect>();
                            pos = firePointTr.GetWorldMatrixPosition();
                            rot = firePointTr.GetWorldMatrixRotation();
                        }

                        for (uint i = 0u; i < aspect.targets.Count; ++i) {

                            var unit = aspect.targets[i];
                            if (unit.IsAlive() == false) continue;
                            
                            var target = unit;
                            float3 targetPosition = default;
                            if (aspect.componentRuntimeFire.targets.Length > 0u) {
                                target = default;
                                targetPosition = aspect.componentRuntimeFire.targets[i];
                            }

                            AttackUtils.CreateBullet(aspect, pos, rot, query.readQuery.treeMask, in target, in targetPosition, aspect.readComponentVisual.bulletConfig,
                                                     aspect.readComponentVisual.muzzleView, jobInfo: in jobInfo);
                        }

                        aspect.UseFire();

                    }

                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var target = context.Query()
                                .With<ReloadedComponent>()
                                .With<CanFireComponent>()
                                .Without<FireUsedComponent>()
                                .With<AttackTargetComponent>()
                                .Schedule<FireTargetJob, AttackAspect, TransformAspect, QuadTreeQueryAspect>(new FireTargetJob() {
                                    dt = context.deltaTime,
                                });
            var targets = context.Query(target)
                                .With<ReloadedComponent>()
                                .With<CanFireComponent>()
                                .Without<FireUsedComponent>()
                                .With<AttackTargetsComponent>()
                                .Schedule<FireTargetsJob, AttackAspect, TransformAspect, QuadTreeQueryAspect>(new FireTargetsJob() {
                                    dt = context.deltaTime,
                                });
            context.SetDependency(targets);

        }

    }

}