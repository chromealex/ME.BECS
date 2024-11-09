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

        [BURST(CompileSynchronously = true)]
        public struct RevealJob : IJobParallelForComponents<FogOfWarRevealerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarRevealerComponent revealer) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                var fow = UnitUtils.GetTeam(in ent).Read<FogOfWarComponent>();
                FogOfWarUtils.Write(in this.props, in fow, in tr, revealer.height, revealer.range);

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var fowStaticData = context.world.GetSystem<CreateSystem>().heights.Read<FogOfWarStaticComponent>();
            var dependsOn = context.Query().Schedule<Job, TransformAspect, UnitAspect>(new Job() {
                props = fowStaticData,
            });
            dependsOn = context.Query(dependsOn).WithAspect<TransformAspect>().With<OwnerComponent>().Schedule<RevealJob, FogOfWarRevealerComponent>(new RevealJob() {
                props = fowStaticData,
            });
            context.SetDependency(dependsOn);

        }

    }

}