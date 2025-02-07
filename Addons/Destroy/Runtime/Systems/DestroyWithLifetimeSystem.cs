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
        public struct Job : IJobForComponents<DestroyWithLifetime> {

            public tfloat deltaTime;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref DestroyWithLifetime component) {
                component.lifetime -= this.deltaTime;
                if (component.lifetime <= 0u) {
                    ent.DestroyHierarchy();
                }
            }

        }

        public void OnUpdate(ref SystemContext context) {
            
            var childHandle = context.Query().AsParallel().Schedule<Job, DestroyWithLifetime>(new Job() {
                deltaTime = context.deltaTime,
            });
            context.SetDependency(childHandle);
            
        }

    }

}