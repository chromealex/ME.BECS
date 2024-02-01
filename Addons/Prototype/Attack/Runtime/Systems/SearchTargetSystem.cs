
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Search Target system")]
    [RequiredDependencies(typeof(FogOfWar.CreateSystem))]
    public struct SearchTargetSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public unsafe struct SearchTargetJob : ME.BECS.Jobs.IJobParallelForAspect<AttackAspect, TransformAspect> {

            public World world;
            public FogOfWar.CreateSystem fogOfWar;
            
            public void Execute(ref AttackAspect aspect, ref TransformAspect tr) {
                
                var query = aspect.ent.GetAspect<QuadTreeQueryAspect>();
                var unit = aspect.ent.GetParent().GetAspect<ME.BECS.Units.UnitAspect>();
                var player = unit.owner.GetAspect<Players.PlayerAspect>();
                var team = player.team;
                Units.UnitAspect nearestResult = default;
                for (uint i = 0u; i < query.results.results.Count; ++i) {
                    var ent = query.results.results[this.world.state, i];
                    if (ent.IsAlive() == false) continue;
                    var result = ent.GetAspect<ME.BECS.Units.UnitAspect>();
                    if (ME.BECS.Units.UnitUtils.GetTeam(in result) == team) continue;
                    if (this.fogOfWar.IsVisible(in player, in result) == false) continue;
                    
                    var dist = math.lengthsq(tr.GetWorldMatrixPosition() - ent.GetAspect<TransformAspect>().GetWorldMatrixPosition());
                    if (dist <= aspect.attackRangeSqr) {
                        nearestResult = result;
                    }
                    
                }

                if (nearestResult.IsAlive() == true && nearestResult.health > 0f) {
                    
                    aspect.SetTarget(nearestResult.ent);

                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Without<AttackTargetComponent>().ScheduleParallelFor<SearchTargetJob, AttackAspect, TransformAspect>(new SearchTargetJob() {
                world = context.world,
                fogOfWar = context.world.GetSystem<ME.BECS.FogOfWar.CreateSystem>(),
            });
            context.SetDependency(dependsOn);

        }

    }

}