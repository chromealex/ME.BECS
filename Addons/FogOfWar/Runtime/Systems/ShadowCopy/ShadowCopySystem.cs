
namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Players;
    using ME.BECS.Transforms;
    using ME.BECS.Views;

    /// <summary>
    /// Coordinates shadow copy during the ECS system lifecycle.
    /// </summary>
    [BURST]
    public partial struct ShadowCopySystem : IUpdate {

        /// <summary>
        /// Executes create work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct CreateJob : IJobForComponents<OwnerComponent, FogOfWarShadowCopyRequiredRuntimeComponent> {

            /// <summary>
            /// Players system used by <c>ShadowCopySystem.CreateJob</c>.
            /// </summary>
            public Players.PlayersSystem playersSystem;
            /// <summary>
            /// World used by the containing operation.
            /// </summary>
            public World world;

            /// <summary>
            /// Processes create using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref OwnerComponent owner, ref FogOfWarShadowCopyRequiredRuntimeComponent sc) {
                
                var origTr = ent.GetAspect<TransformAspect>();
                var pos = origTr.position;
                var rot = origTr.rotation;

                // Create shadow copies in special world
                var teams = this.playersSystem.GetTeams();
                for (uint i = 0u; i < teams.Length; ++i) {
                    if (sc.shadowCopy[i].IsAlive() == true) continue;
                    var team = teams[(int)i];
                    var copyEnt = ent.Clone(this.world.id, cloneHierarchy: true, in jobInfo);
                    FogOfWarUtils.ClearQuadTree(in copyEnt);
                    copyEnt.Remove<ParentComponent>();
                    sc.shadowCopy[i] = copyEnt;
                    copyEnt.EditorName = ent.EditorName;
                    var tr = copyEnt.GetOrCreateAspect<TransformAspect>();
                    tr.IsStaticHierarchy = true;
                    tr.position = pos;
                    tr.rotation = rot;
                    copyEnt.Set(new FogOfWarShadowCopyComponent() {
                        forTeam = team,
                        original = ent,
                    });
                    copyEnt.SetTag<IsViewRequested>(false);
                }

            }

        }

        /// <summary>
        /// Updates shadow copy system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var logicWorld = context.world.parent;

            // Collect all units which has not presented as shadow copy
            var dependsOn = API.Query(in logicWorld, context.dependsOn).AsReadonly().Schedule<CreateJob, OwnerComponent, FogOfWarShadowCopyRequiredRuntimeComponent>(new CreateJob() {
                playersSystem = logicWorld.GetSystem<PlayersSystem>(),
                world = context.world,
            });
            context.SetDependency(dependsOn);
            
        }

    }

}