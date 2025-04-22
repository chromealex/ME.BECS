#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

using ME.BECS.Transforms;

namespace ME.BECS {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [UnityEngine.Tooltip("Update entities with lifetime component (ent.Destroy(float lifetime) API).")]
    [BURST(CompileSynchronously = true)]
    public struct DestroyWithLifetimeSystem : IUpdate {
        
        [BURST(CompileSynchronously = true)]
        public struct LifetimeJob : IJobForComponents<DestroyWithLifetime> {

            public tfloat deltaTime;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref DestroyWithLifetime component) {
                component.lifetime -= this.deltaTime;
                if (component.lifetime <= 0f) {
                    ent.DestroyHierarchy();
                }
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct LifetimeMsJob : IJobForComponents<DestroyWithLifetimeMs> {

            public uint deltaTimeMs;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref DestroyWithLifetimeMs component) {
                if (component.lifetime >= this.deltaTimeMs) {
                    component.lifetime -= this.deltaTimeMs;
                } else {
                    component.lifetime = 0u;
                }
                if (component.lifetime <= 0u) {
                    ent.DestroyHierarchy();
                }
            }

        }

        public void OnUpdate(ref SystemContext context) {
            
            var childHandle = context.Query().AsParallel().Schedule<LifetimeJob, DestroyWithLifetime>(new LifetimeJob() {
                deltaTime = context.deltaTime,
            });
            var childHandleMs = context.Query().AsParallel().Schedule<LifetimeMsJob, DestroyWithLifetimeMs>(new LifetimeMsJob() {
                deltaTimeMs = context.deltaTimeMs,
            });
            context.SetDependency(Unity.Jobs.JobHandle.CombineDependencies(childHandleMs, childHandle));
            
        }

    }

}