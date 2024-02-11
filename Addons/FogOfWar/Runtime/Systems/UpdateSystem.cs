namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using ME.BECS.Players;
    using Transforms;

    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(CreateSystem))]
    public struct UpdateSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<TransformAspect, UnitAspect> {

            public FogOfWarStaticComponent props;
            
            public void Execute(ref TransformAspect tr, ref UnitAspect unit) {
                
                var fow = UnitUtils.GetTeam(in unit).Read<FogOfWarComponent>();
                FogOfWarUtils.Write(in this.props, in fow, in tr, in unit);

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().ScheduleParallelFor<Job, TransformAspect, UnitAspect>(new Job() {
                props = context.world.GetSystem<CreateSystem>().heights.Read<FogOfWarStaticComponent>(),
            });
            context.SetDependency(dependsOn);

        }

    }

}