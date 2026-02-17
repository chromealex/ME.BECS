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

    public partial struct FireSystem : IUpdate {

        [BURST]
        public struct SpatialFireTargetJob : IJobForAspects<AttackAspect, TransformAspect, SpatialQueryAspect> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect, ref TransformAspect tr, ref SpatialQueryAspect query) {

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

                        var bullet = AttackUtils.CreateBullet(aspect, pos, rot, query.readQuery.treeMask, in target, in targetPosition, aspect.readComponentVisual.bulletConfig,
                                                 aspect.readComponentVisual.muzzleView, jobInfo: jobInfo);
                        if (ent.TryRead(out MaxHitCountComponent maxHitCountComponent) == true) {
                            var attack = bullet.ent.GetAspect<SpatialQueryAspect>();
                            attack.query.nearestCount = maxHitCountComponent.value;
                        }
                        
                        aspect.UseFire();

                    }

                }

            }

        }

        [BURST]
        public struct SpatialFireTargetsJob : IJobForAspects<AttackAspect, TransformAspect, SpatialQueryAspect> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect, ref TransformAspect tr, ref SpatialQueryAspect query) {

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

                            var bullet = AttackUtils.CreateBullet(aspect, pos, rot, query.readQuery.treeMask, in target, in targetPosition, aspect.readComponentVisual.bulletConfig,
                                                     aspect.readComponentVisual.muzzleView, jobInfo: in jobInfo);
                            if (ent.TryRead(out MaxHitCountComponent maxHitCountComponent) == true) {
                                var attack = bullet.ent.GetAspect<SpatialQueryAspect>();
                                attack.query.nearestCount = maxHitCountComponent.value;
                            }
                        }

                        aspect.UseFire();

                    }

                }

            }

        }

        public Unity.Jobs.JobHandle UpdateSpatial(ref SystemContext context, Unity.Jobs.JobHandle jobHandle) {

            var target = context.Query(jobHandle)
                                .With<ReloadedComponent>()
                                .With<CanFireComponent>()
                                .Without<FireUsedComponent>()
                                .With<AttackTargetComponent>()
                                .Schedule<SpatialFireTargetJob, AttackAspect, TransformAspect, SpatialQueryAspect>(new SpatialFireTargetJob() {
                                    dt = context.deltaTime,
                                });
            var targets = context.Query(target)
                                .With<ReloadedComponent>()
                                .With<CanFireComponent>()
                                .Without<FireUsedComponent>()
                                .With<AttackTargetsComponent>()
                                .Schedule<SpatialFireTargetsJob, AttackAspect, TransformAspect, SpatialQueryAspect>(new SpatialFireTargetsJob() {
                                    dt = context.deltaTime,
                                });
            return targets;

        }

    }

}