#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Attack {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using ME.BECS.Transforms;

    /// <summary>
    /// Provides typed access to the entity components used for attack.
    /// </summary>
    public partial struct AttackAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for attack.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<AttackComponent> attackDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for attack runtime reload.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<AttackRuntimeReloadComponent> attackRuntimeReloadDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for attack runtime fire.
        /// </summary>
        public AspectDataPtr<AttackRuntimeFireComponent> attackRuntimeFireDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for target.
        /// </summary>
        public AspectDataPtr<AttackTargetComponent> targetDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for targets.
        /// </summary>
        public AspectDataPtr<AttackTargetsComponent> targetsDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for attack visual.
        /// </summary>
        public AspectDataPtr<AttackVisualComponent> attackVisualDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for attack bullet distribution.
        /// </summary>
        public AspectDataPtr<AttackBulletDistributionComponent> attackBulletDistributionPtr;

        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        public readonly ref AttackComponent component => ref this.attackDataPtr.GetOrThrow(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component.
        /// </summary>
        public readonly ref readonly AttackComponent readComponent => ref this.attackDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Component visual used by <c>AttackAspect</c>.
        /// </summary>
        public readonly ref AttackVisualComponent componentVisual => ref this.attackVisualDataPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component visual.
        /// </summary>
        public readonly ref readonly AttackVisualComponent readComponentVisual => ref this.attackVisualDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Component runtime reload used by <c>AttackAspect</c>.
        /// </summary>
        public readonly ref AttackRuntimeReloadComponent componentRuntimeReload => ref this.attackRuntimeReloadDataPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component runtime reload.
        /// </summary>
        public readonly ref readonly AttackRuntimeReloadComponent readComponentRuntimeReload => ref this.attackRuntimeReloadDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Component runtime fire used by <c>AttackAspect</c>.
        /// </summary>
        public readonly ref AttackRuntimeFireComponent componentRuntimeFire => ref this.attackRuntimeFireDataPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component runtime fire.
        /// </summary>
        public readonly ref readonly AttackRuntimeFireComponent readComponentRuntimeFire => ref this.attackRuntimeFireDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to bullet distribution.
        /// </summary>
        public readonly ref readonly AttackBulletDistributionComponent readBulletDistribution => ref this.attackBulletDistributionPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Squared attack range used by the associated calculation.
        /// </summary>
        public readonly ref tfloat attackRangeSqr => ref this.component.sector.rangeSqr;
        /// <summary>
        /// Read-only access to attack range sqr.
        /// </summary>
        public readonly ref readonly tfloat readAttackRangeSqr => ref this.readComponent.sector.rangeSqr;
        /// <summary>
        /// Read-only access to min attack range sqr.
        /// </summary>
        public readonly ref readonly tfloat readMinAttackRangeSqr => ref this.readComponent.sector.minRangeSqr;
        /// <summary>
        /// Read-only access to attack sector.
        /// </summary>
        public readonly ref readonly tfloat readAttackSector => ref this.readComponent.sector.sector;
        /// <summary>
        /// Read-only access to bullet distribution sector.
        /// </summary>
        public readonly ref readonly tfloat readBulletDistributionSector => ref this.readBulletDistribution.sector.sector;
        /// <summary>
        /// Read-only access to ignore self.
        /// </summary>
        public readonly ref readonly bbool readIgnoreSelf => ref this.readComponent.ignoreSelf;
        
        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public readonly Ent target => this.targetDataPtr.Read(this.ent.id, this.ent.gen).target;

        /// <summary>
        /// Targets considered by the associated operation.
        /// </summary>
        public readonly ListAuto<Ent> targets => this.targetsDataPtr.Read(this.ent.id, this.ent.gen).targets;

        /// <summary>
        /// Current target index used to locate the associated entry.
        /// </summary>
        public readonly uint CurrentTargetIndex => this.ent.Read<ME.BECS.Bullets.FirePointComponent>().index;

        /// <summary>
        /// Indicates has any target.
        /// </summary>
        public bool HasAnyTarget {
            get {
                if (this.target.IsAlive() == true) return true;
                for (uint i = 0u; i < this.targets.Count; ++i) {
                    if (this.targets[i].IsAlive() == true) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Damage value used by the combat calculation.
        /// </summary>
        public readonly uint Damage {
            get {
                if (this.ent.Has<ME.BECS.Bullets.DamageOverrideComponent>() == true) return this.ent.Read<ME.BECS.Bullets.DamageOverrideComponent>().damage;
                var config = this.readComponentVisual.bulletConfig.AsUnsafeConfig();
                if (config.IsValid() == true && config.TryRead(out ME.BECS.Bullets.BulletConfigComponent bulletConfigComponent) == true) {
                    if (this.ent.TryRead(out ME.BECS.Bullets.DamageMultiplierComponent multiplierComponent) == true) {
                        return (uint)math.floor(bulletConfigComponent.damage * multiplierComponent.factor);
                    }
                    return bulletConfigComponent.damage;
                }
                
                return 0u;
            }
        }

        /// <summary>
        /// Returns first target.
        /// </summary>
        [INLINE(256)]
        public Ent GetFirstTarget() {
            if (this.target.IsAlive() == true) return this.target;
            for (uint i = 0u; i < this.targets.Count; ++i) {
                if (this.targets[i].IsAlive() == true) return this.targets[i];
            }
            return default;
        }

        /// <summary>
        /// Clears target.
        /// </summary>
        [INLINE(256)]
        public readonly void CleanUpTarget() {
            
            this.ent.Remove<AttackTargetComponent>();
            
        }
        
        /// <summary>
        /// Clears targets.
        /// </summary>
        [INLINE(256)]
        public readonly void CleanUpTargets() {
            
            var targets = this.ent.Read<AttackTargetsComponent>().targets;
            if (targets.IsCreated == true) targets.Dispose();
            this.ent.Remove<AttackTargetsComponent>();
            
        }

        /// <summary>
        /// Sets target.
        /// </summary>
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

        /// <summary>
        /// Sets targets.
        /// </summary>
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

        /// <summary>
        /// Sets targets at.
        /// </summary>
        [INLINE(256)]
        public void SetTargetsAt(uint index, Ent target) {
            this.targetsDataPtr.Get(this.ent.id, this.ent.gen).targets[index] = target;
        }
        
        /// <summary>
        /// Indicates can fire while moves.
        /// </summary>
        public readonly bool CanFireWhileMoves => this.ent.Has<CanFireWhileMovesTag>();
        
        /// <summary>
        /// Reload progress used by <c>AttackAspect</c>.
        /// </summary>
        public readonly tfloat ReloadProgress => this.readComponentRuntimeReload.reloadTimer / this.readComponent.reloadTime;
        /// <summary>
        /// Fire progress used by <c>AttackAspect</c>.
        /// </summary>
        public readonly tfloat FireProgress => this.readComponentRuntimeFire.fireTimer / this.readComponent.fireTime;

        /// <summary>
        /// Indicates is reloaded.
        /// </summary>
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
        
        /// <summary>
        /// Indicates can fire.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is fire used.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsFireUsed() => this.ent.Has<FireUsedComponent>();
        
        /// <summary>
        /// Uses fire.
        /// </summary>
        [INLINE(256)]
        public readonly void UseFire() {
            ++this.componentRuntimeFire.fireRateCount;
            if (this.readComponentRuntimeFire.fireRateCount >= this.readComponent.rateCount) {
                this.componentRuntimeFire.fireRateCount = 0u;
                this.ent.SetTag<FireUsedComponent>(true);
                this.IsReloaded = false;
            }
            this.ent.SetOneShot(new OnFireEvent(), OneShotType.NextTick);
        }

        /// <summary>
        /// Calculates dps.
        /// </summary>
        [INLINE(256)]
        public readonly uint CalculateDPS() {
            return (uint)(this.Damage * math.max(1u, this.readComponent.rateCount) / this.readComponent.fireTime);
        }

        /// <summary>
        /// Evaluates fire.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is any target in sector.
        /// </summary>
        [INLINE(256)]
        public bool IsAnyTargetInSector() {

            var sector = this.readComponent.sector;
            if (this.ent.TryRead(out AttackSectorComponent sectorComponent) == true) {
                sector = sectorComponent.sector;
            }

            var tr = this.ent.GetAspect<TransformAspect>();
            if (this.ent.TryRead(out AttackTargetsComponent attackTargetsComponent) == true) {

                for (uint i = 0u; i < attackTargetsComponent.targets.Count; ++i) {
                    var target = attackTargetsComponent.targets[i];
                    if (target.IsAlive() == false) continue;
                    var targetAspect = target.GetAspect<TransformAspect>();
                    if (IsTargetInSector(in tr, in targetAspect, in sector) == true) return true;
                }

            } else if (this.target.IsAlive() == true) {

                if (this.target.IsAlive() == false) return false;
                var target = this.target.GetAspect<TransformAspect>();
                if (IsTargetInSector(in tr, in target, in sector) == true) return true;
                
            }

            return false;

        }

        [INLINE(256)]
        private static bool IsTargetInSector(in TransformAspect tr, in TransformAspect target, in Sector sector) {
            var mathSector = new MathSector(tr.position, tr.rotation, sector.sector);
            return mathSector.IsValid(target.position);
        }

    }

}