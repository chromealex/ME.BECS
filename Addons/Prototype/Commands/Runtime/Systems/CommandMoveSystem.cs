namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    
    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct CommandMoveSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForAspects<UnitCommandGroupAspect> {

            public BuildGraphSystem buildGraphSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitCommandGroupAspect commandGroup) {

                var move = commandGroup.ent.Read<CommandMove>();
                PathUtils.UpdateTarget(in this.buildGraphSystem, in commandGroup, in move.targetPosition, in jobInfo);
                
                for (uint i = 0u; i < commandGroup.readUnits.Count; ++i) {
                    var u = commandGroup.readUnits[i];
                    if (u.IsAlive() == false) continue;
                    var unit = u.GetAspect<UnitAspect>();
                    unit.IsHold = false;
                }
                
                commandGroup.ent.SetTag<IsCommandGroupDirty>(false);
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
            var handle = context.Query().With<CommandMove>().With<IsCommandGroupDirty>().Schedule<Job, UnitCommandGroupAspect>(new Job() {
                buildGraphSystem = buildGraphSystem,
            });
            context.SetDependency(handle);
            
        }

    }

}