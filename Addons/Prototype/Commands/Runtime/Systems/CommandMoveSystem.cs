namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    
    [BURST]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct CommandMoveSystem : IUpdate {

        [BURST]
        public struct Job : IJobForAspects<UnitCommandGroupAspect> {

            public BuildGraphSystem buildGraphSystem;
            
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

        public void OnUpdate(ref SystemContext context) {

            var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
            var handle = context.Query().AsUnsafe().With<CommandMove>().With<IsCommandGroupDirty>().Schedule<Job, UnitCommandGroupAspect>(new Job() {
                buildGraphSystem = buildGraphSystem,
            });
            context.SetDependency(handle);
            
        }

    }

}