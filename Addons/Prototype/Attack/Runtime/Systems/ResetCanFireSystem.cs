namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using ME.BECS.Transforms;

    /// <summary>
    /// Coordinates reset can fire during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [UnityEngine.Tooltip("Reset Can Fire system")]
    public partial struct ResetCanFireSystem : IUpdate {

        /// <summary>
        /// Executes reset can fire system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForAspects<AttackAspect, TransformAspect> {
            
            /// <summary>
            /// Processes the job inputs for <c>ResetCanFireSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect, ref TransformAspect tr) {

                if (tr.parent.Has<IsUnitStaticComponent>() == false) return;
                if (aspect.HasAnyTarget == false) aspect.CanFire = false;

            }

        }

        /// <summary>
        /// Updates reset can fire system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel().Without<CanFireWhileMovesTag>().Schedule<Job, AttackAspect, TransformAspect>();
            context.SetDependency(dependsOn);

        }

    }

}