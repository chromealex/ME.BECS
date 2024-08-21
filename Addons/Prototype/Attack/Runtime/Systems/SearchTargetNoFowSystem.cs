
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Units;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Search Target system")]
    [RequiredDependencies(typeof(QuadTreeQuerySystem))]
    public struct SearchTargetNoFowSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public unsafe struct SearchTargetJob : IJobParallelForAspect<AttackAspect, QuadTreeQueryAspect, TransformAspect> {

            public World world;
            
            public void Execute(in JobInfo jobInfo, ref AttackAspect aspect, ref QuadTreeQueryAspect query, ref TransformAspect tr) {

                var unit = aspect.ent.GetParent().GetAspect<UnitAspect>();
                var player = unit.owner.GetAspect<Players.PlayerAspect>();
                var team = player.team;
                UnitAspect nearestResult = default;
                for (uint i = 0u; i < query.results.results.Count; ++i) {
                    var ent = query.results.results[this.world.state, i];
                    if (ent.IsAlive() == false) continue;
                    var result = ent.GetAspect<UnitAspect>();
                    if (UnitUtils.GetTeam(in result) == team) continue;

                    var dist = math.length(tr.GetWorldMatrixPosition() - ent.GetAspect<TransformAspect>().GetWorldMatrixPosition());
                    if (dist <= math.sqrt(aspect.attackRangeSqr) + result.readRadius) {
                        nearestResult = result;
                        break;
                    }
                    
                }

                if (nearestResult.IsAlive() == true && nearestResult.health > 0f) {
                    
                    aspect.SetTarget(nearestResult.ent);

                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Without<AttackTargetComponent>().Schedule<SearchTargetJob, AttackAspect, QuadTreeQueryAspect, TransformAspect>(new SearchTargetJob() {
                world = context.world,
            });
            context.SetDependency(dependsOn);

        }

    }

}