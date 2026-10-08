namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    
    /// <summary>
    /// Coordinates command attack clean during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public partial struct CommandAttackCleanSystem : IUpdate {

        /// <summary>
        /// Executes remove work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct RemoveJob : IJobForComponents<UnitAttackCommandComponent> {


            /// <summary>
            /// Processes remove using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAttackCommandComponent attackCommand) {
                
                if (attackCommand.target.IsAlive() == true) return;
                ent.Remove<UnitAttackCommandComponent>();
                var unitAspect = ent.GetAspect<UnitAspect>();
                unitAspect.RemoveFromCommandGroup();
                unitAspect.IsHold = false;
                
            }

        }

        /// <summary>
        /// Updates command attack clean system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {
            
            var handle = context.Query().AsParallel().Schedule<RemoveJob, UnitAttackCommandComponent>();
            context.SetDependency(handle);        
        }

    }

}