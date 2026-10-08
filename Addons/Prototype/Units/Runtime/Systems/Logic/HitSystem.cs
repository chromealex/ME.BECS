
namespace ME.BECS.Units {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Effects;

    /// <summary>
    /// Coordinates hit during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [UnityEngine.Tooltip("Apply damage from DamageTookComponent")]
    public partial struct HitSystem : IUpdate {

        /// <summary>
        /// Executes hit system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForComponents<DamageTookComponent> {

            /// <summary>
            /// Processes the job inputs for <c>HitSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref DamageTookComponent damageComponent) {
                if (damageComponent.damage == 0u) return;
                if (damageComponent.target.IsAlive() == false) return;
                var unit = damageComponent.target.GetAspect<HealthAspect>();
                if (unit.ent.TryRead(out UnitInvincibility unitInvincibility) == true) {
                    if (unitInvincibility.behaviour == UnitInvincibility.InvincibleBehaviour.NoDamage) {
                        return;
                    } else if (unitInvincibility.behaviour == UnitInvincibility.InvincibleBehaviour.IgnoreLastHit) {
                        if (damageComponent.damage >= unit.readHealth) {
                            damageComponent.damage = unit.readHealth - 1u;
                        }
                    }
                }

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

        /// <summary>
        /// Updates hit system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Schedule<Job, DamageTookComponent>();
            context.SetDependency(dependsOn);

        }

    }

}