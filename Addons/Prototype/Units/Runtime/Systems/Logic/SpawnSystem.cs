
namespace ME.BECS.Units {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Effects;

    /// <summary>
    /// Coordinates spawn during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [UnityEngine.Tooltip("Unit spawn effect system")]
    public partial struct SpawnSystem : IUpdate {

        /// <summary>
        /// Executes spawn system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForAspects<TransformAspect, UnitAspect> {

            /// <summary>
            /// Processes the job inputs for <c>SpawnSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect tr, ref UnitAspect unit) {
                EffectUtils.CreateEffect(in jobInfo, tr.position, tr.rotation, unit.ent.ReadStatic<UnitEffectOnSpawnComponent>().effect, unit.readOwner.GetAspect<ME.BECS.Players.PlayerAspect>());
            }

        }

        /// <summary>
        /// Updates spawn system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            context.Query().With<UnitJustSpawnedEvent>().Schedule<Job, TransformAspect, UnitAspect>().AddDependency(ref context);

        }

    }

}