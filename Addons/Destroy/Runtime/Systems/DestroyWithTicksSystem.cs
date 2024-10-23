using ME.BECS.Transforms;

namespace ME.BECS {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [UnityEngine.Tooltip("Update entities with ticks component (ent.Destroy(int ticks) API).")]
    [BURST(CompileSynchronously = true)]
    public struct DestroyWithTicksSystem : IUpdate {
        
        [BURST(CompileSynchronously = true)]
        public struct UpdateTicksJob : IJobParallelForComponents<DestroyWithTicks> {
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref DestroyWithTicks component) {
                --component.ticks;
                if (component.ticks <= 0f) {
                    ent.DestroyHierarchy();
                }
            }

        }

        public void OnUpdate(ref SystemContext context) {
            
            var childHandle = API.Query(in context).Schedule<UpdateTicksJob, DestroyWithTicks>();
            context.SetDependency(childHandle);
            
        }

    }

}