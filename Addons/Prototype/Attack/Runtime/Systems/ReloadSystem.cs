
using UnityEngine;
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    /// <summary>
    /// Coordinates reload during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [UnityEngine.Tooltip("Reload system")]
    public partial struct ReloadSystem : IUpdate {

        /// <summary>
        /// Executes reload work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct ReloadJob : IJobForAspects<AttackAspect> {

            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            public tfloat dt;
            
            /// <summary>
            /// Processes reload using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect) {

                aspect.componentRuntimeReload.reloadTimer += this.dt;
                if (aspect.readComponentRuntimeReload.reloadTimer >= aspect.readComponent.reloadTime) {

                    aspect.IsReloaded = true;

                }

                if (aspect.componentRuntimeFire.fireTimer > 0 && aspect.componentRuntimeFire.fireTimer >= aspect.readComponent.attackTime) {
                    // Finish fire timer
                    aspect.componentRuntimeFire.fireTimer += this.dt;
                    if (aspect.readComponentRuntimeFire.fireTimer >= aspect.readComponent.fireTime) {
                        aspect.CanFire = false;
                    }
                }

            }

        }

        /// <summary>
        /// Updates reload system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel().Without<ReloadedComponent>().Schedule<ReloadJob, AttackAspect>(new ReloadJob() {
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}