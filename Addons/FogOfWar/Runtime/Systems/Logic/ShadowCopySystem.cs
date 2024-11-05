
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
        public struct CreateJob : IJobComponents<OwnerComponent> {

            public Players.PlayersSystem playersSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref OwnerComponent ownerComponent) {

                var tr = ent.GetAspect<TransformAspect>();
                var owner = ownerComponent.ent.GetAspect<PlayerAspect>();
                // Create shadow copy for each player
                var teams = this.playersSystem.GetTeams();
                for (uint i = 0u; i < teams.Length; ++i) {
                    var team = teams[(int)i];
                    // We do not need shadow copy for the same team
                    if (team == owner.readTeam) continue;
                    var copyEnt = Ent.New(in jobInfo);
                    PlayerUtils.SetOwner(in copyEnt, owner);
                    var shadowTr = copyEnt.GetOrCreateAspect<TransformAspect>();
                    shadowTr.position = tr.position;
                    shadowTr.rotation = tr.rotation;
                    copyEnt.Set(new FogOfWarShadowCopyComponent() {
                        forTeam = team,
                        original = ent,
                    });
                    copyEnt.InstantiateView(ent.ReadStatic<UnitShadowCopyViewComponent>().view);
                }

                ent.Set(new FogOfWarHasShadowCopyComponent());

            }

        }

        public void OnUpdate(ref SystemContext context) {

            // Collect all units which has not presented as shadow copy
            var dependsOn = context.Query().Without<FogOfWarHasShadowCopyComponent>().With<FogOfWarShadowCopyRequiredComponent>().Schedule<CreateJob, OwnerComponent>(new CreateJob() {
                playersSystem = context.world.GetSystem<PlayersSystem>(),
            });
            context.SetDependency(dependsOn);
            
        }

    }

}