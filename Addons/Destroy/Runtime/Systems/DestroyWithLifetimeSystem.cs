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
    
    /// <summary>
    /// Coordinates destroy with lifetime during the ECS system lifecycle.
    /// </summary>
    [UnityEngine.Tooltip("Update entities with lifetime component (ent.Destroy(float lifetime) API).")]
    [BURST]
    public partial struct DestroyWithLifetimeSystem : IUpdate {

        /// <summary>
        /// Executes lifetime work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct LifetimeJob : IJobForComponents<DestroyWithLifetime> {

            /// <summary>
            /// Elapsed simulation time supplied to this update.
            /// </summary>
            public tfloat deltaTime;

            /// <summary>
            /// Processes lifetime using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref DestroyWithLifetime component) {
                component.lifetime -= this.deltaTime;
                if (component.lifetime <= 0f) {
                    ent.DestroyHierarchy();
                }
            }

        }

        /// <summary>
        /// Executes lifetime ms work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct LifetimeMsJob : IJobForComponents<DestroyWithLifetimeMs> {

            /// <summary>
            /// Elapsed simulation time in milliseconds.
            /// </summary>
            public uint deltaTimeMs;
            
            /// <summary>
            /// Processes lifetime ms using the supplied job inputs.
            /// </summary>
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

        /// <summary>
        /// Updates destroy with lifetime system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {
            
            var childHandle = context.Query().AsParallel(4).Schedule<LifetimeJob, DestroyWithLifetime>(new LifetimeJob() {
                deltaTime = context.deltaTime,
            });
            var childHandleMs = context.Query().AsParallel(4).Schedule<LifetimeMsJob, DestroyWithLifetimeMs>(new LifetimeMsJob() {
                deltaTimeMs = context.deltaTimeMs,
            });
            context.SetDependency(Unity.Jobs.JobHandle.CombineDependencies(childHandleMs, childHandle));
            
        }

    }

}