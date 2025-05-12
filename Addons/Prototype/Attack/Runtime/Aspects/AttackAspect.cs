#if FIXED_POINT
using tfloat = sfloat;
#else
using tfloat = System.Single;
#endif

namespace ME.BECS.Attack {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    public struct AttackAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<AttackComponent> attackDataPtr;
        [QueryWith]
        public AspectDataPtr<AttackRuntimeReloadComponent> attackRuntimeReloadDataPtr;
        public AspectDataPtr<AttackRuntimeFireComponent> attackRuntimeFireDataPtr;
        public AspectDataPtr<AttackTargetComponent> targetDataPtr;
        public AspectDataPtr<AttackTargetsComponent> targetsDataPtr;
        public AspectDataPtr<AttackVisualComponent> attackVisualDataPtr;

        public readonly ref AttackComponent component => ref this.attackDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly AttackComponent readComponent => ref this.attackDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref AttackVisualComponent componentVisual => ref this.attackVisualDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly AttackVisualComponent readComponentVisual => ref this.attackVisualDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref AttackRuntimeReloadComponent componentRuntimeReload => ref this.attackRuntimeReloadDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly AttackRuntimeReloadComponent readComponentRuntimeReload => ref this.attackRuntimeReloadDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref AttackRuntimeFireComponent componentRuntimeFire => ref this.attackRuntimeFireDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly AttackRuntimeFireComponent readComponentRuntimeFire => ref this.attackRuntimeFireDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref tfloat attackRangeSqr => ref this.component.sector.rangeSqr;
        public readonly ref readonly tfloat readAttackRangeSqr => ref this.readComponent.sector.rangeSqr;
        public readonly ref readonly tfloat readMinAttackRangeSqr => ref this.readComponent.sector.minRangeSqr;
        public readonly ref readonly tfloat readAttackSector => ref this.readComponent.sector.sector;
        public readonly ref readonly byte readIgnoreSelf => ref this.readComponent.ignoreSelf;
        
        public readonly Ent target => this.targetDataPtr.Read(this.ent.id, this.ent.gen).target;

        public readonly ListAuto<Ent> targets => this.targetsDataPtr.Read(this.ent.id, this.ent.gen).targets;

        public readonly uint CurrentTargetIndex => this.ent.Read<ME.BECS.Bullets.FirePointComponent>().index;

        public bool HasAnyTarget => this.target.IsAlive() == true || this.targets.Count > 0;
        public readonly uint Damage {
            get {
                if (this.ent.Has<ME.BECS.Bullets.DamageOverrideComponent>()) return this.ent.Read<ME.BECS.Bullets.DamageOverrideComponent>().damage;
                var config = this.readComponentVisual.bulletConfig.AsUnsafeConfig();
                if (config.IsValid() == true && config.TryRead(out ME.BECS.Bullets.BulletConfigComponent bulletConfigComponent)) {
                    return bulletConfigComponent.damage;
                }
                
                return 0u;
            }
        }
        
        [INLINE(256)]
        private readonly void CleanUpTarget() {
            
            this.ent.Remove<AttackTargetComponent>();
            
        }
        
        [INLINE(256)]
        private readonly void CleanUpTargets() {
            
            var targets = this.ent.Read<AttackTargetsComponent>().targets;
            if (targets.IsCreated == true) targets.Dispose();
            this.ent.Remove<AttackTargetsComponent>();
            
        }

        [INLINE(256)]
        public readonly void SetTarget(Ent ent) {
            this.CleanUpTargets();
            if (ent.IsAlive() == true) {
                if (this.ent.Read<AttackTargetComponent>().target != ent) {
                    this.CanFire = false;
                }
                this.ent.Set(new AttackTargetComponent() {
                    target = ent,
                });
            } else {
                this.ent.Remove<AttackTargetComponent>();
                this.CanFire = false;
            }
        }

        [INLINE(256)]
        public readonly void SetTargets(in ListAuto<Ent> list) {
            this.CleanUpTarget();
            if (list.IsCreated == true) {
                if (this.ent.Read<AttackTargetsComponent>().targets != list) {
                    this.CanFire = false;
                    var targets = this.ent.Read<AttackTargetsComponent>().targets;
                    if (targets.IsCreated == true) targets.Dispose();
                }
                this.ent.Set(new AttackTargetsComponent() {
                    targets = list,
                });
            } else {
                var targets = this.ent.Read<AttackTargetsComponent>().targets;
                if (targets.IsCreated == true) targets.Dispose();
                this.ent.Remove<AttackTargetsComponent>();
                this.CanFire = false;
            }
        }

        [INLINE(256)]
        public void SetTargetsAt(uint index, Ent target) {
            this.targetsDataPtr.Get(this.ent.id, this.ent.gen).targets[index] = target;
        }
        
        public readonly tfloat ReloadProgress => this.readComponentRuntimeReload.reloadTimer / this.readComponent.reloadTime;
        public readonly tfloat FireProgress => this.readComponentRuntimeFire.fireTimer / this.readComponent.fireTime;

        public readonly bool IsReloaded {
            [INLINE(256)]
            get => this.ent.Has<ReloadedComponent>();
            [INLINE(256)]
            set {
                if (value == true) {
                    this.ent.Set(new ReloadedComponent());
                } else {
                    this.componentRuntimeReload.reloadTimer = 0f;
                    this.ent.Remove<ReloadedComponent>();
                }
            }
        }
        
        public readonly bool CanFire {
            [INLINE(256)]
            get => this.ent.Has<CanFireComponent>();
            [INLINE(256)]
            set {
                if (value == true) {
                    this.ent.Set(new CanFireComponent());
                } else {
                    this.componentRuntimeFire.fireTimer = 0f;
                    this.ent.Remove<CanFireComponent>();
                    this.ent.SetTag<FireUsedComponent>(false);
                }
            }
        }

        [INLINE(256)]
        public readonly bool IsFireUsed() => this.ent.Has<FireUsedComponent>();
        
        [INLINE(256)]
        public readonly void UseFire() {
            ++this.componentRuntimeFire.fireRateCount;
            if (this.readComponentRuntimeFire.fireRateCount >= this.readComponent.rateCount) {
                this.componentRuntimeFire.fireRateCount = 0u;
                this.ent.SetTag<FireUsedComponent>(true);
            }
            this.ent.SetOneShot(new OnFireEvent(), OneShotType.NextTick);
        }

        [INLINE(256)]
        public readonly uint CalculateDPS() {
            return (uint)(this.Damage * math.max(1u, this.readComponent.rateCount) / this.readComponent.fireTime);
        }

        [INLINE(256)]
        public bool RateFire(tfloat dt) {
            if (this.readComponentRuntimeFire.fireRateCount < this.readComponent.rateCount) {
                this.componentRuntimeFire.fireRateTimer += dt;
                if (this.componentRuntimeFire.fireRateTimer >= this.readComponent.rateTime) {
                    this.componentRuntimeFire.fireRateTimer -= this.readComponent.rateTime;
                    return true;
                }
                return false;
            }
            return true;
        }

    }

}