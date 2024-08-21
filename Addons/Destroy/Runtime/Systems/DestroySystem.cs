using ME.BECS.Transforms;

namespace ME.BECS {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [UnityEngine.Tooltip("Update entities with lifetime component (ent.Destroy(lifetime) API).")]
    [BURST(CompileSynchronously = true)]
    public struct DestroyWithLifetimeSystem : IUpdate {
        
        [BURST(CompileSynchronously = true)]
        public struct UpdateLifetimeJob : IJobParallelForComponents<DestroyWithLifetime> {

            public float deltaTime;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref DestroyWithLifetime component) {
                component.lifetime -= this.deltaTime;
                if (component.lifetime <= 0f) {
                    ent.DestroyHierarchy();
                }
            }

        }

        public void OnUpdate(ref SystemContext context) {
            
            var childHandle = API.Query(in context).Schedule<UpdateLifetimeJob, DestroyWithLifetime>(new UpdateLifetimeJob() {
                deltaTime = context.deltaTime,
            });
            context.SetDependency(childHandle);
            
        }

    }

}