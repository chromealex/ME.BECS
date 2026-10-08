using ME.BECS.Transforms;

namespace ME.BECS {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    /// <summary>
    /// Coordinates destroy with ticks during the ECS system lifecycle.
    /// </summary>
    [UnityEngine.Tooltip("Update entities with ticks component (ent.Destroy(ulong ticks) API).")]
    [BURST]
    public partial struct DestroyWithTicksSystem : IUpdate {
        
        /// <summary>
        /// Executes destroy with ticks system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForComponents<DestroyWithTicks> {
            
            /// <summary>
            /// Processes the job inputs for <c>DestroyWithTicksSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref DestroyWithTicks component) {
                if (component.ticks <= 0UL) {
                    ent.DestroyHierarchy();
                    return;
                }
                --component.ticks;
            }

        }

        /// <summary>
        /// Updates destroy with ticks system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {
            
            var childHandle = context.Query().AsParallel(4).Schedule<Job, DestroyWithTicks>();
            context.SetDependency(childHandle);
            
        }

    }

}