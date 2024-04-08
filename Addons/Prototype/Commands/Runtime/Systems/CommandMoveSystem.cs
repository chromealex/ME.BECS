namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct CommandMoveSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<UnitCommandGroupAspect> {

            public BuildGraphSystem buildGraphSystem;
            
            public void Execute(ref UnitCommandGroupAspect commandGroup) {

                var move = commandGroup.ent.Read<CommandMove>();
                PathUtils.UpdateTarget(in this.buildGraphSystem, in commandGroup, in move.targetPosition);
                
                commandGroup.ent.SetTag<IsCommandGroupDirty>(false);
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
            var handle = context.Query().With<CommandMove>().With<IsCommandGroupDirty>().ScheduleParallelFor<Job, UnitCommandGroupAspect>(new Job() {
                buildGraphSystem = buildGraphSystem,
            });
            context.SetDependency(handle);
            
        }

    }

}