
namespace ME.BECS.Units {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Destroy units with health <= 0")]
    public struct DestroySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct DestroyJob : IJobAspect<ME.BECS.Units.UnitAspect> {

            public void Execute(ref ME.BECS.Units.UnitAspect unit) {
                if (unit.health <= 0f) {
                    var tr = unit.ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
                    ME.BECS.Effects.EffectUtils.CreateEffect(tr.position, tr.rotation, in unit.ent.Read<UnitHealthComponent>().effectOnDestroy);
                    UnitUtils.DestroyUnit(unit);
                }
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Schedule<DestroyJob, ME.BECS.Units.UnitAspect>();
            context.SetDependency(dependsOn);

        }

    }

}