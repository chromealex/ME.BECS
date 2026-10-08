using NativeTrees;
using UnityEngine;
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Ray = ME.BECS.FixedPoint.Ray;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Ray = UnityEngine.Ray;
#endif

namespace ME.BECS.Bullets {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    /// <summary>
    /// Coordinates fly3 d during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [UnityEngine.Tooltip("Bullet fly system")]
    public partial struct Fly3DSystem : IUpdate {
        
        /// <summary>
        /// Whether continuous target check behavior or state is selected.
        /// </summary>
        public bbool continuousTargetCheck;
        
        /// <summary>
        /// Executes fly work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct FlyJob : IJobForAspects<BulletAspect, TransformAspect> {
            
            /// <summary>
            /// Whether continuous target check behavior or state is selected.
            /// </summary>
            public bbool continuousTargetCheck;
            /// <summary>
            /// Qt used by <c>Fly3DSystem.FlyJob</c>.
            /// </summary>
            public OctreeInsertSystem qt;
            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            public tfloat dt;
            
            /// <summary>
            /// Processes fly using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref BulletAspect aspect, ref TransformAspect tr) {

                if (aspect.readConfig.autoTarget == true) {
                    if (aspect.readComponent.targetEnt.IsAlive() == true) {
                        aspect.component.targetWorldPos = ME.BECS.Units.UnitUtils.GetTargetBulletPosition(in ent, in aspect.readComponent.targetEnt);
                    }
                }

                var prevPos = tr.position;
                aspect.component.prevWorldPos = prevPos;
                tr.position = Math.MoveTowards(prevPos, aspect.readComponent.targetWorldPos, aspect.readConfig.speed * this.dt);
                tr.rotation = quaternion.LookRotationSafe(tr.position - prevPos, math.up());
                if (math.lengthsq(tr.position - aspect.readComponent.targetWorldPos) <= 0.01f) {
                    aspect.IsReached = true;
                    aspect.component.targetEnt = default;
                }

                if (this.continuousTargetCheck == true) {
                    var vector = tr.position - prevPos;
                    var direction = math.normalizesafe(vector);
                    var distance = math.length(vector);
                
                    var ray = new Ray(prevPos, direction);
                    var mask = ent.GetAspect<OctreeQueryAspect>().readQuery.treeMask;
                    if (this.qt.Raycast(ray, mask, distance, out var hitResult) == true) {
                        aspect.component.targetEnt = hitResult.obj;
                        tr.position = hitResult.point;
                        aspect.IsReached = true;
                    }
                }
            }

        }

        /// <summary>
        /// Updates fly3 d system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var qt = context.world.GetSystem<OctreeInsertSystem>();
            var dependsOn = context.Query().AsUnsafe().AsParallel().WithAspect<OctreeQueryAspect>().Without<TargetReachedComponent>().Without<IsBulletCustomFlyComponent>().Schedule<FlyJob, BulletAspect, TransformAspect>(new FlyJob() {
                continuousTargetCheck = this.continuousTargetCheck,
                qt = qt,
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}
