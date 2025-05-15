
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

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Bullet fly system")]
    public struct FlySystem : IUpdate {
        
        public bool continuousTargetCheck;
        
        [BURST(CompileSynchronously = true)]
        public unsafe struct FlyJob : IJobForAspects<BulletAspect, TransformAspect> {
            
            public bool continuousTargetCheck;
            public QuadTreeInsertSystem qt;
            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref BulletAspect aspect, ref TransformAspect tr) {

                if (aspect.readConfig.autoTarget == 1 && aspect.readComponent.targetEnt.IsAlive() == true) {
                    aspect.component.targetWorldPos = aspect.readComponent.targetEnt.GetAspect<TransformAspect>().GetWorldMatrixPosition();
                }

                var prevPos = tr.position;
                tr.position = Math.MoveTowards(prevPos, aspect.readComponent.targetWorldPos, aspect.readConfig.speed * this.dt);
                tr.rotation = quaternion.LookRotationSafe(tr.position - prevPos, math.up());
                if (math.lengthsq(tr.position - aspect.readComponent.targetWorldPos) <= 0.01f) {
                    aspect.IsReached = true;
                }

                if (this.continuousTargetCheck == true) {
                    var vector = prevPos - tr.position;
                    var direction = math.normalize(vector);
                    var distance = math.length(vector);
                
                    var mask = ent.GetAspect<QuadTreeQueryAspect>().query.treeMask;
                    for (int i = 0; i < this.qt.treesCount; i++) {
                        if ((mask & (1 << i)) == 0) {
                            continue;
                        }
                    
                        ref var tree = ref *qt.GetTree(i).ptr;
                        var ray = new Ray((Vector3)tr.position, (Vector3)direction);
                        if (tree.RaycastAABB(ray, out var hitResult, distance) == true) {
                            aspect.component.targetEnt = hitResult.obj;
                            aspect.IsReached = true;
                        }
                    }
                }
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var qt = context.world.GetSystem<QuadTreeInsertSystem>();
            var dependsOn = context.Query().AsParallel().Without<TargetReachedComponent>().Schedule<FlyJob, BulletAspect, TransformAspect>(new FlyJob() {
                continuousTargetCheck = this.continuousTargetCheck,
                qt = qt,
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}
