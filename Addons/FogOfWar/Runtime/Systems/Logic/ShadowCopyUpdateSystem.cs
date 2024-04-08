
namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Views;
    using ME.BECS.Units;
    using ME.BECS.Players;

    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(PlayersSystem), typeof(CreateSystem), typeof(UpdateSystem), typeof(ShadowCopySystem))]
    public struct ShadowCopyUpdateSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct UpdateJob : IJobParallelForComponents<FogOfWarShadowCopyComponent> {

            public CreateSystem fow;
            
            public void Execute(in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy) {

                var shadowTr = ent.GetAspect<TransformAspect>();
                var isVisible = this.fow.IsVisible(in shadowCopy.forTeam, shadowTr.GetWorldMatrixPosition());
                // Update only visible objects
                if (isVisible == true) {
                    if (shadowCopy.original.IsAlive() == false) {
                        // if object is already destroyed - just destroy the shadow copy
                        ent.Destroy();
                    } else {
                        // if object is still alive - update properties
                        var tr = shadowCopy.original.GetAspect<TransformAspect>();
                        shadowTr.position = tr.position;
                        shadowTr.rotation = tr.rotation;
                        ent.Set(shadowCopy.original.Read<UnitHealthComponent>());
                    }
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            // Update all shadow copies
            var dependsOn = context.Query().ScheduleParallelFor<UpdateJob, FogOfWarShadowCopyComponent>(new UpdateJob() {
                fow = context.world.GetSystem<CreateSystem>(),
            });
            context.SetDependency(dependsOn);
            
        }

    }

}