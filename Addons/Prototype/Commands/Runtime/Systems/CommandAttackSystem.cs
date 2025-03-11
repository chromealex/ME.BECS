namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    
    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct CommandAttackSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct CleanUpJob : IJobForAspects<UnitAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit) {

                if (unit.readUnitCommandGroup.IsAlive() == false || unit.readUnitCommandGroup.Has<CommandAttack>() == false) {
                    unit.ent.Remove<UnitAttackCommandComponent>();
                    unit.IsHold = false;
                }
                
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct MoveJob : IJobForAspects<UnitCommandGroupAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitCommandGroupAspect commandGroup) {
                
                var attack = commandGroup.ent.Read<CommandAttack>();
                if (attack.target.IsAlive() == false) {
                    // Remove group
                    UnitUtils.DestroyCommandGroup(in commandGroup);
                    return;
                }

                for (uint i = 0u; i < commandGroup.readUnits.Count; ++i) {
                    var unit = commandGroup.readUnits[i];
                    unit.Set(new UnitAttackCommandComponent() {
                        target = attack.target,
                    });
                }
                
                commandGroup.ent.SetTag<IsCommandGroupDirty>(false);
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var handle = context.Query().With<UnitAttackCommandComponent>().AsParallel().Schedule<CleanUpJob, UnitAspect>();
            handle = context.Apply(handle);
            handle = context.Query(handle).With<CommandAttack>().With<IsCommandGroupDirty>().Schedule<MoveJob, UnitCommandGroupAspect>();
            context.SetDependency(handle);
            
        }

    }

}