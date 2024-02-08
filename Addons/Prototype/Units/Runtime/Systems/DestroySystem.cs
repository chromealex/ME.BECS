
namespace ME.BECS.Units {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Effects;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Destroy units with health <= 0")]
    public struct DestroySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct DestroyJob : IJobAspect<UnitAspect> {

            public void Execute(ref UnitAspect unit) {
                if (unit.health <= 0f) {
                    var tr = unit.ent.GetAspect<TransformAspect>();
                    EffectUtils.CreateEffect(tr.position, tr.rotation, in unit.ent.Read<UnitHealthComponent>().effectOnDestroy);
                    UnitUtils.DestroyUnit(unit);
                }
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Schedule<DestroyJob, UnitAspect>();
            context.SetDependency(dependsOn);

        }

    }

}