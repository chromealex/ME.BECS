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
            
            public void Execute(in JobInfo jobInfo, ref TransformAspect tr, ref UnitAspect unit) {

                if (tr.IsCalculated == false) return;
                
                var fow = UnitUtils.GetTeam(in unit).Read<FogOfWarComponent>();
                FogOfWarUtils.Write(in this.props, in fow, in tr, in unit);

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Schedule<Job, TransformAspect, UnitAspect>(new Job() {
                props = context.world.GetSystem<CreateSystem>().heights.Read<FogOfWarStaticComponent>(),
            });
            context.SetDependency(dependsOn);

        }

    }

}