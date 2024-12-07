using ME.BECS.Transforms;

namespace ME.BECS {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [UnityEngine.Tooltip("Update entities with ticks component (ent.Destroy(ulong ticks) API).")]
    [BURST(CompileSynchronously = true)]
    public struct DestroyWithTicksSystem : IUpdate {
        
        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForComponents<DestroyWithTicks> {
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref DestroyWithTicks component) {
                if (component.ticks <= 0UL) {
                    ent.DestroyHierarchy();
                    return;
                }
                --component.ticks;
            }

        }

        public void OnUpdate(ref SystemContext context) {
            
            var childHandle = context.Query().AsParallel().Schedule<Job, DestroyWithTicks>();
            context.SetDependency(childHandle);
            
        }

    }

}