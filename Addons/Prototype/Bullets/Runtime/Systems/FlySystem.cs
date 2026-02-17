
using NativeTrees;
using UnityEngine;
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.Bullets {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    [BURST]
    [UnityEngine.Tooltip("Bullet fly system")]
    public struct FlySystem : IUpdate {
        
        public bool continuousTargetCheck;
        
        [BURST]
        public struct FlyJob : IJobForAspects<BulletAspect, TransformAspect> {
            
            public bool continuousTargetCheck;
            public QuadTreeInsertSystem qt;
            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref BulletAspect aspect, ref TransformAspect tr) {

                if (aspect.readConfig.autoTarget == true) {
                    if (aspect.readComponent.targetEnt.IsAlive() == true && aspect.readComponent.sourceUnit.IsAlive() == true) {
                        aspect.component.targetWorldPos = ME.BECS.Units.UnitUtils.GetTargetBulletPosition(in aspect.readComponent.sourceUnit, in aspect.readComponent.targetEnt); 
                    } else if (aspect.readComponent.targetEnt.IsAlive() == true) {
                        aspect.component.targetWorldPos = ME.BECS.Units.UnitUtils.GetTargetBulletPosition(in ent, in aspect.readComponent.targetEnt);
                    }
                }

                var prevPos = tr.position;
                tr.position = Math.MoveTowards(prevPos, aspect.readComponent.targetWorldPos, aspect.readConfig.speed * this.dt);
                tr.rotation = quaternion.LookRotationSafe(tr.position - prevPos, math.up());
                if (math.lengthsq(tr.position - aspect.readComponent.targetWorldPos) <= 0.01f) {
                    aspect.IsReached = true;
                }

                if (this.continuousTargetCheck == true) {
                    var vector = tr.position - prevPos;
                    var direction = math.normalizesafe(vector);
                    var distance = math.length(vector);
                
                    var ray = new Ray2D((Vector2)prevPos.xz, (Vector2)direction.xz);
                    var mask = ent.GetAspect<QuadTreeQueryAspect>().readQuery.treeMask;
                    if (this.qt.Raycast(ray, mask, distance, out var hitResult) == true) {
                        aspect.component.targetEnt = hitResult.obj;
                        tr.position = new float3(hitResult.point.x, 0f, hitResult.point.y);
                        aspect.IsReached = true;
                    }
                    
                }
            }

        }

        [BURST]
        public struct FlySpatialJob : IJobForAspects<BulletAspect, TransformAspect> {
            
            public bool continuousTargetCheck;
            public SpatialInsertSystem qt;
            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref BulletAspect aspect, ref TransformAspect tr) {

                if (aspect.readConfig.autoTarget == true) {
                    if (aspect.readComponent.targetEnt.IsAlive() == true && aspect.readComponent.sourceUnit.IsAlive() == true) {
                        aspect.component.targetWorldPos = ME.BECS.Units.UnitUtils.GetTargetBulletPosition(in aspect.readComponent.sourceUnit, in aspect.readComponent.targetEnt); 
                    } else if (aspect.readComponent.targetEnt.IsAlive() == true) {
                        aspect.component.targetWorldPos = ME.BECS.Units.UnitUtils.GetTargetBulletPosition(in ent, in aspect.readComponent.targetEnt);
                    }
                }

                var prevPos = tr.position;
                tr.position = Math.MoveTowards(prevPos, aspect.readComponent.targetWorldPos, aspect.readConfig.speed * this.dt);
                tr.rotation = quaternion.LookRotationSafe(tr.position - prevPos, math.up());
                if (math.lengthsq(tr.position - aspect.readComponent.targetWorldPos) <= 0.01f) {
                    aspect.IsReached = true;
                }

                if (this.continuousTargetCheck == true) {
                    var vector = tr.position - prevPos;
                    var direction = math.normalizesafe(vector);
                    var distance = math.length(vector);
                
                    var ray = new Ray2D((Vector2)prevPos.xz, (Vector2)direction.xz);
                    var mask = ent.GetAspect<SpatialQueryAspect>().readQuery.treeMask;
                    if (this.qt.Raycast(ray, mask, distance, out var hitResult) == true) {
                        aspect.component.targetEnt = hitResult.obj;
                        tr.position = new float3(hitResult.point.x, 0f, hitResult.point.y);
                        aspect.IsReached = true;
                    }
                    
                }
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel().WithAspect<QuadTreeQueryAspect>().Without<TargetReachedComponent>().Schedule<FlyJob, BulletAspect, TransformAspect>(new FlyJob() {
                continuousTargetCheck = this.continuousTargetCheck,
                qt = context.world.GetSystem<QuadTreeInsertSystem>(),
                dt = context.deltaTime,
            });
            var dependsOnSpatial = context.Query().AsParallel().WithAspect<SpatialQueryAspect>().Without<TargetReachedComponent>().Schedule<FlySpatialJob, BulletAspect, TransformAspect>(new FlySpatialJob() {
                continuousTargetCheck = this.continuousTargetCheck,
                qt = context.world.GetSystem<SpatialInsertSystem>(),
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn, dependsOnSpatial);

        }

    }

}
