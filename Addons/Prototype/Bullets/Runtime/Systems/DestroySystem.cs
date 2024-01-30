
namespace ME.BECS.Bullets {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Bullets destroy system")]
    public struct DestroySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct DestroyJob : ME.BECS.Jobs.IJobAspect<BulletAspect, QuadTreeQueryAspect, TransformAspect> {

            public void Execute(ref BulletAspect bullet, ref QuadTreeQueryAspect query, ref TransformAspect tr) {

                if (bullet.config.hitRange > 0f) {

                    // use splash
                    for (uint i = 0u; i < query.results.results.Count; ++i) {
                        var unit = query.results.results[i];
                        if (unit.IsAlive() == false) continue;
                        var targetUnit = unit.GetAspect<ME.BECS.Units.UnitAspect>();
                        targetUnit.Hit(bullet.config.damage);
                    }

                } else if (bullet.component.targetEnt.IsAlive() == true) {
                    
                    // hit only target unit if its alive and set
                    var targetUnit = bullet.component.targetEnt.GetAspect<ME.BECS.Units.UnitAspect>();
                    targetUnit.Hit(bullet.config.damage);
                    
                } else if (bullet.component.targetEnt == Ent.Null) {

                    // hit first target in range because targetEnt was not set
                    if (query.results.results.Count > 0u) {
                        var unit = query.results.results[0];
                        if (unit.IsAlive() == true) {
                            var targetUnit = unit.GetAspect<ME.BECS.Units.UnitAspect>();
                            targetUnit.Hit(bullet.config.damage);
                        }
                    }

                }

                ME.BECS.Effects.EffectUtils.CreateEffect(tr.position, tr.rotation, in bullet.config.effectOnDestroy);
                bullet.ent.DestroyHierarchy();

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = API.Query(in context).With<TargetReachedComponent>().Schedule<DestroyJob, BulletAspect, QuadTreeQueryAspect, TransformAspect>();
            context.SetDependency(dependsOn);

        }

    }

}