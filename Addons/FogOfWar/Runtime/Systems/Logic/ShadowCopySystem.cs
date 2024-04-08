
namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Views;
    using ME.BECS.Units;
    using ME.BECS.Players;

    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(PlayersSystem))]
    public struct ShadowCopySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct CreateJob : IJobParallelForAspect<UnitAspect> {

            public Players.PlayersSystem playersSystem;
            
            public void Execute(ref UnitAspect unit) {

                var tr = unit.ent.GetAspect<TransformAspect>();
                var owner = unit.readOwner.GetAspect<PlayerAspect>();
                // Create shadow copy for each player
                var teams = this.playersSystem.GetTeams();
                for (uint i = 0u; i < teams.Length; ++i) {
                    var team = teams[(int)i];
                    // We do not need shadow copy for the same team
                    if (team == owner.readTeam) continue;
                    var ent = Ent.New();
                    PlayerUtils.SetOwner(in ent, owner);
                    var shadowTr = ent.GetOrCreateAspect<TransformAspect>();
                    shadowTr.position = tr.position;
                    shadowTr.rotation = tr.rotation;
                    ent.Set(new FogOfWarShadowCopyComponent() {
                        forTeam = team,
                        original = unit.ent,
                    });
                    ent.InstantiateView(unit.ent.ReadStatic<UnitShadowCopyViewComponent>().view);
                }

                unit.ent.Set(new FogOfWarHasShadowCopyComponent());

            }

        }

        public void OnUpdate(ref SystemContext context) {

            // Collect all units which has not presented as shadow copy
            var dependsOn = context.Query().Without<FogOfWarHasShadowCopyComponent>().With<FogOfWarShadowCopyRequiredComponent>().ScheduleParallelFor<CreateJob, UnitAspect>(new CreateJob() {
                playersSystem = context.world.GetSystem<PlayersSystem>(),
            });
            context.SetDependency(dependsOn);
            
        }

    }

}