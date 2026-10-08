namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    
    /// <summary>
    /// Coordinates command move during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public partial struct CommandMoveSystem : IUpdate {

        /// <summary>
        /// Executes command move system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForAspects<UnitCommandGroupAspect> {

            /// <summary>
            /// Graph-building system used by this operation.
            /// </summary>
            public BuildGraphSystem buildGraphSystem;
            
            /// <summary>
            /// Processes the job inputs for <c>CommandMoveSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitCommandGroupAspect commandGroup) {

                var move = commandGroup.ent.Read<CommandMove>();
                var target = Path.Target.Create(move.targetPosition);
                if (move.targets.IsCreated == true) Path.Target.Create(move.targets);
                PathUtils.UpdateTarget(in this.buildGraphSystem, in commandGroup, in target, in jobInfo);
                
                for (uint i = 0u; i < commandGroup.readUnits.Count; ++i) {
                    var u = commandGroup.readUnits[i];
                    if (u.IsAlive() == false) continue;
                    var unit = u.GetAspect<UnitAspect>();
                    unit.IsHold = false;
                }
                
                commandGroup.ent.SetTag<IsCommandGroupDirty>(false);
                
            }

        }

        /// <summary>
        /// Updates command move system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
            var handle = context.Query().AsUnsafe().With<CommandMove>().With<IsCommandGroupDirty>().Schedule<Job, UnitCommandGroupAspect>(new Job() {
                buildGraphSystem = buildGraphSystem,
            });
            context.SetDependency(handle);
            
        }

    }

}