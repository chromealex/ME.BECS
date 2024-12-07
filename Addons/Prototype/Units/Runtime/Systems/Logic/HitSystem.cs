
namespace ME.BECS.Units {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Effects;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Apply damage from DamageTookComponent")]
    public struct HitSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForComponents<DamageTookComponent> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref DamageTookComponent damageComponent) {
                if (damageComponent.damage == 0u) return;
                if (damageComponent.target.IsAlive() == false) return;
                var unit = damageComponent.target.GetAspect<UnitAspect>();
                var newHealth = (int)unit.readHealth - (int)damageComponent.damage;
                if (newHealth <= 0) {
                    unit.health = 0u;
                } else {
                    unit.health = (uint)newHealth;
                }
                // Use damage
                damageComponent.damage = 0u;
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Schedule<Job, DamageTookComponent>();
            context.SetDependency(dependsOn);

        }

    }

}