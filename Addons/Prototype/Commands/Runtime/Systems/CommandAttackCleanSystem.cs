namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    
    [BURST]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public partial struct CommandAttackCleanSystem : IUpdate {

        [BURST]
        public partial struct RemoveJob : IJobForComponents<UnitAttackCommandComponent> {


            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAttackCommandComponent attackCommand) {
                
                if (attackCommand.target.IsAlive() == true) return;
                ent.Remove<UnitAttackCommandComponent>();
                var unitAspect = ent.GetAspect<UnitAspect>();
                unitAspect.RemoveFromCommandGroup();
                unitAspect.IsHold = false;
                
            }

        }

        public void OnUpdate(ref SystemContext context) {
            
            var handle = context.Query().AsParallel().Schedule<RemoveJob, UnitAttackCommandComponent>();
            context.SetDependency(handle);        
        }

    }

}