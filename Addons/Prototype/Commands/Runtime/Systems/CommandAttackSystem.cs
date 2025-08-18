namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    
    [BURST]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct CommandAttackSystem : IUpdate {

        [BURST]
        public struct CleanUpJob : IJobForAspects<UnitAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit) {

                if (unit.readUnitCommandGroup.IsAlive() == false || unit.readUnitCommandGroup.Has<CommandAttack>() == false) {
                    unit.ent.Remove<UnitAttackCommandComponent>();
                    unit.IsHold = false;
                }
                
            }

        }

        [BURST]
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
                    if (unit.IsAlive() == false) continue;
                    unit.Set(new UnitAttackCommandComponent() {
                        target = attack.target,
                    });
                }
                
                commandGroup.ent.SetTag<IsCommandGroupDirty>(false);
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            context.Query().With<UnitAttackCommandComponent>().AsParallel().Schedule<CleanUpJob, UnitAspect>().AddDependency(ref context);
            context.Query().With<CommandAttack>().With<IsCommandGroupDirty>().Schedule<MoveJob, UnitCommandGroupAspect>().AddDependency(ref context);
            
        }

    }

}