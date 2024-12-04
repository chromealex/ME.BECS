
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Units;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Search Target system")]
    [RequiredDependencies(typeof(QuadTreeQuerySystem))]
    public struct SearchTargetSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public unsafe struct SearchTargetJob : ME.BECS.Jobs.IJobParallelForAspect<AttackAspect, QuadTreeQueryAspect, TransformAspect> {

            public World world;
            public SystemLink<FogOfWar.CreateSystem> fogOfWar;
            
            public void Execute(in JobInfo jobInfo, ref AttackAspect aspect, ref QuadTreeQueryAspect query, ref TransformAspect tr) {
                
                //var unit = aspect.ent.GetParent().GetAspect<UnitAspect>();
                //var player = unit.readOwner.GetAspect<Players.PlayerAspect>();
                UnitAspect nearestResult = default;
                for (uint i = 0u; i < query.results.results.Count; ++i) {
                    var ent = query.results.results[this.world.state, i];
                    if (ent.IsAlive() == false) continue;
                    var result = ent.GetAspect<UnitAspect>();
                    nearestResult = result;
                    break;
                    //if (this.fogOfWar.IsCreated == true && this.fogOfWar.Value.IsVisible(in player, in ent) == false) continue;
                    /*var distSq = math.lengthsq(tr.GetWorldMatrixPosition() - ent.GetAspect<TransformAspect>().GetWorldMatrixPosition());
                    var rangeSqr = aspect.readAttackRangeSqr;
                    if (distSq <= rangeSqr || math.sqrt(distSq) <= math.sqrt(rangeSqr) + result.readRadius) {
                        nearestResult = result;
                        break;
                    }*/
                }

                if (nearestResult.IsAlive() == true && nearestResult.readHealth > 0f) {
                    
                    aspect.SetTarget(nearestResult.ent);

                } else {
                    
                    aspect.SetTarget(default);
                    
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Schedule<SearchTargetJob, AttackAspect, QuadTreeQueryAspect, TransformAspect>(new SearchTargetJob() {
                world = context.world,
                fogOfWar = context.world.GetSystemLink<ME.BECS.FogOfWar.CreateSystem>(),
            });
            context.SetDependency(dependsOn);

        }

    }

}