
namespace ME.BECS.Units {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Effects;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Destroy units with health <= 0")]
    public struct DestroySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct DestroyJob : IJobForAspects<UnitAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit) {
                if (unit.readHealth <= 0u) {
                    var tr = unit.ent.GetAspect<TransformAspect>();
                    EffectUtils.CreateEffect(in jobInfo, tr.position, tr.rotation, unit.ent.ReadStatic<UnitEffectOnDestroyComponent>().effect);
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