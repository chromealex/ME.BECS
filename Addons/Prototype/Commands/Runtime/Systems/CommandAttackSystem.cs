namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    
    /// <summary>
    /// Coordinates command attack during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public partial struct CommandAttackSystem : IUpdate {

        /// <summary>
        /// Executes clean up work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct CleanUpJob : IJobForAspects<UnitAspect> {

            /// <summary>
            /// Processes clean up using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit) {

                if (unit.readUnitCommandGroup.IsAlive() == false || unit.readUnitCommandGroup.Has<CommandAttack>() == false) {
                    unit.ent.Remove<UnitAttackCommandComponent>();
                    unit.IsHold = false;
                }
                
            }

        }

        /// <summary>
        /// Executes move work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct MoveJob : IJobForAspects<UnitCommandGroupAspect> {

            /// <summary>
            /// Processes move using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitCommandGroupAspect commandGroup) {
                
                var attack = commandGroup.ent.Read<CommandAttack>();
                if (attack.target.IsAlive() == false) {
                    // Remove group
                    UnitUtils.DestroyCommandGroup(in commandGroup);
                    return;
                }

                for (uint i = 0u; i < commandGroup.readUnits.Count; ++i) {
                    var unit = commandGroup.readUnits[i];
                    if (unit.IsAlive() == false) continue;
                    unit.Set(new UnitAttackCommandComponent() {
                        target = attack.target,
                    });
                }
                
                commandGroup.ent.SetTag<IsCommandGroupDirty>(false);
                
            }

        }

        /// <summary>
        /// Updates command attack system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            context.Query().With<UnitAttackCommandComponent>().AsParallel().Schedule<CleanUpJob, UnitAspect>().AddDependency(ref context);
            context.Query().AsUnsafe().With<CommandAttack>().With<IsCommandGroupDirty>().Schedule<MoveJob, UnitCommandGroupAspect>().AddDependency(ref context);
            
        }

    }

}