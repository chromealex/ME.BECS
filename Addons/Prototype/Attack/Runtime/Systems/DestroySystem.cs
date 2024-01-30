
namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Destroy system")]
    public struct DestroySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct DestroyJob : ME.BECS.Jobs.IJobAspect<ME.BECS.Units.UnitAspect> {

            public void Execute(ref ME.BECS.Units.UnitAspect unit) {
                if (unit.health <= 0f) {
                    ME.BECS.Units.Utils.DestroyUnit(unit);
                }
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = API.Query(in context).Schedule<DestroyJob, ME.BECS.Units.UnitAspect>();
            context.SetDependency(dependsOn);

        }

    }

}