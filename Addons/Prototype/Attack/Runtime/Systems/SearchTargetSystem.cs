
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Search Target system")]
    public struct SearchTargetSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public unsafe struct SearchTargetJob : ME.BECS.Jobs.IJobParallelForAspect<AttackAspect> {

            public World world;

            public void Execute(ref AttackAspect aspect) {
                
                var query = aspect.ent.GetAspect<QuadTreeQueryAspect>();
                var team = ME.BECS.Units.Utils.GetTeam(aspect.ent.GetParent());
                Units.UnitAspect nearestResult = default;
                for (uint i = 0u; i < query.results.results.Count; ++i) {
                    var ent = query.results.results[this.world.state, i];
                    if (ent.IsAlive() == false) continue;
                    var result = ent.GetAspect<ME.BECS.Units.UnitAspect>();
                    if (ME.BECS.Units.Utils.GetTeam(in result) == team) continue;
                    nearestResult = result;
                }

                if (nearestResult.IsAlive() == true && nearestResult.health > 0f) {
                    
                    aspect.SetTarget(nearestResult.ent);

                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = API.Query(in context).Without<AttackTargetComponent>().ScheduleParallelFor<SearchTargetJob, AttackAspect>(new SearchTargetJob() {
                world = context.world,
            });
            context.SetDependency(dependsOn);

        }

    }

}