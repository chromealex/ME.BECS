
namespace ME.BECS.Units {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Effects;

    [BURST]
    [UnityEngine.Tooltip("Apply damage from DamageTookComponent")]
    public struct HitSystem : IUpdate {

        [BURST]
        public struct Job : IJobForComponents<DamageTookComponent> {

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

                if (damageComponent.sourceOwner.IsAlive() && damageComponent.sourceOwner.Has<PlayerDamageModifiers>()) {
                    ref var totals = ref damageComponent.sourceOwner.Get<PlayerDamageModifiers>();
                    var actual = System.Math.Min(unit.readHealth, damageComponent.damage);
                    switch (damageComponent.damageCategory) {
                        case 1: totals.heroShots += actual; break; case 2: totals.super += actual; break; case 3: totals.imprints += actual; break;
                        case 4: totals.sniper += actual; break; case 5: totals.tesla += actual; break; case 6: totals.tank += actual; break;
                        case 7: totals.flame += actual; break; case 8: totals.miner += actual; break; case 9: totals.rocket += actual; break;
                    }
                    if (unit.ent.Has<CombatDamageModifiers>() && unit.readOwner != damageComponent.sourceOwner) {
                        ref var victim = ref unit.ent.Get<CombatDamageModifiers>();
                        if (victim.playerUnit && unit.readHealth > 0) {
                            if (!victim.pvpStarted) { victim.pvpStarted = true; victim.pvpStartTime = totals.matchSeconds; }
                            if (damageComponent.damage >= unit.readHealth) {
                                if (!totals.kills.IsCreated) totals.kills = new ListAuto<PlayerDamageModifiers.Kill>(damageComponent.sourceOwner, 8);
                                totals.kills.Add(new PlayerDamageModifiers.Kill { ttk = totals.matchSeconds - victim.pvpStartTime, killerLevel = totals.level, victimLevel = victim.level });
                            }
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

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().Schedule<Job, DamageTookComponent>();
            context.SetDependency(dependsOn);

        }

    }

}