
using ME.BECS.Transforms;
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Units {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using ME.BECS.Players;

    /// <summary>
    /// Provides typed access to the entity components used for health.
    /// </summary>
    public partial struct HealthAspect : IAspect {

        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for owner.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<OwnerComponent> ownerDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for health.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<UnitHealthComponent> healthDataPtr;

        /// <summary>
        /// Owner associated with this entry.
        /// </summary>
        public readonly ref Ent owner => ref this.ownerDataPtr.GetOrThrow(this.ent.id, this.ent.gen).ent;
        /// <summary>
        /// Read-only access to owner.
        /// </summary>
        public readonly ref readonly Ent readOwner => ref this.ownerDataPtr.Read(this.ent.id, this.ent.gen).ent;
        /// <summary>
        /// Current health value stored for the unit.
        /// </summary>
        public readonly ref uint health => ref this.healthDataPtr.GetOrThrow(this.ent.id, this.ent.gen).health;
        /// <summary>
        /// Maximum health value stored for the unit.
        /// </summary>
        public readonly ref uint healthMax => ref this.healthDataPtr.GetOrThrow(this.ent.id, this.ent.gen).healthMax;
        /// <summary>
        /// Read-only access to health.
        /// </summary>
        public readonly ref readonly uint readHealth => ref this.healthDataPtr.Read(this.ent.id, this.ent.gen).health;
        /// <summary>
        /// Read-only access to health max.
        /// </summary>
        public readonly ref readonly uint readHealthMax => ref this.healthDataPtr.Read(this.ent.id, this.ent.gen).healthMax;
        
        /// <summary>
        /// Applies damage with its owner, source and damage-type information.
        /// </summary>
        [INLINE(256)]
        [NotThreadSafe]
        public readonly void Hit(in Ent hitOwner, uint damage, in Ent source, in JobInfo jobInfo, byte damageTypeId = 0) {
            if (damage == 0u) return;
            if (this.readHealth > 0u) {
                var ent = Ent.New<UnitHitEntityType>(in jobInfo);
                ent.Set(new DamageTookComponent() {
                    sourceOwner = hitOwner,
                    source = source,
                    target = this.ent,
                    damage = damage,
                    damageTotal = damage,
                    damageTypeId = damageTypeId,
                });
                ent.Destroy(1UL);
                this.ent.SetOneShot(new DamageTookEvent() {
                    source = source,
                });
                this.ent.Set(new LastDamageSource() {
                    source = source,
                    owner = hitOwner,
                });
                var tr = this.ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
                ME.BECS.Effects.EffectUtils.CreateEffect(in jobInfo, tr.position, tr.rotation, this.ent.ReadStatic<UnitEffectOnHitComponent>().effect);
            }
        }

    }
    
    /// <summary>
    /// Provides typed access to the entity components used for unit.
    /// </summary>
    public partial struct UnitAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for nav agent.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<NavAgentComponent> navAgentDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for nav agent runtime.
        /// </summary>
        public AspectDataPtr<NavAgentRuntimeComponent> navAgentRuntimeDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for nav agent runtime rvo.
        /// </summary>
        public AspectDataPtr<NavAgentRVOPathRuntimeComponent> navAgentRuntimeRvoDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for nav agent runtime speed.
        /// </summary>
        public AspectDataPtr<NavAgentRuntimeSpeedComponent> navAgentRuntimeSpeedDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for unit command group.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<UnitCommandGroupComponent> unitCommandGroupDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for unit selection group.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<UnitSelectionGroupComponent> unitSelectionGroupDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for health.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<UnitHealthComponent> healthDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for owner.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<OwnerComponent> ownerDataPtr;

        /// <summary>
        /// Indicates is static.
        /// </summary>
        public readonly bool IsStatic {
            [INLINE(256)]
            get => this.ent.Has<IsUnitStaticComponent>();
            [INLINE(256)]
            set => this.ent.SetTag<IsUnitStaticComponent>(value);
        }

        /// <summary>
        /// Indicates is path follow.
        /// </summary>
        public readonly bool IsPathFollow {
            [INLINE(256)]
            get => this.ent.Has<PathFollowComponent>();
            [INLINE(256)]
            set => this.ent.SetTag<PathFollowComponent>(value);
        }

        /// <summary>
        /// Indicates is hold.
        /// </summary>
        public readonly bool IsHold {
            [INLINE(256)]
            get => this.ent.Has<UnitHoldComponent>();
            [INLINE(256)]
            set => this.ent.SetTag<UnitHoldComponent>(value);
        }

        /// <summary>
        /// Indicates is dead.
        /// </summary>
        public readonly bool IsDead {
            [INLINE(256)]
            get => this.ent.Has<UnitIsDeadTag>();
            [INLINE(256)]
            set => this.ent.SetTag<UnitIsDeadTag>(value);
        }

        /// <summary>
        /// Indicates is collide with end.
        /// </summary>
        public readonly bool IsCollideWithEnd {
            [INLINE(256)]
            get => this.ent.Has<UnitIsCollideWithEnd>();
            [INLINE(256)]
            set => this.ent.SetTag<UnitIsCollideWithEnd>(value);
        }
        
        /// <summary>
        /// Minimum sight range sqr.
        /// </summary>
        public readonly ref tfloat minSightRangeSqr => ref this.component.sightRange.minRangeSqr;
        /// <summary>
        /// Squared sight range used by the associated calculation.
        /// </summary>
        public readonly ref tfloat sightRangeSqr => ref this.component.sightRange.rangeSqr;
        /// <summary>
        /// Sector bounds used by the spatial query.
        /// </summary>
        public readonly ref tfloat sector => ref this.component.sightRange.sector;
        /// <summary>
        /// Read-only access to min sight range sqr.
        /// </summary>
        public readonly ref readonly tfloat readMinSightRangeSqr => ref this.readComponent.sightRange.minRangeSqr;
        /// <summary>
        /// Read-only access to sight range sqr.
        /// </summary>
        public readonly ref readonly tfloat readSightRangeSqr => ref this.readComponent.sightRange.rangeSqr;
        /// <summary>
        /// Read-only access to sector.
        /// </summary>
        public readonly ref readonly tfloat readSector => ref this.readComponent.sightRange.sector;
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public readonly ref tfloat height => ref this.componentRuntime.properties.height;
        /// <summary>
        /// Read-only access to height.
        /// </summary>
        public readonly ref readonly tfloat readHeight => ref this.readComponentRuntime.properties.height;
        /// <summary>
        /// Owner associated with this entry.
        /// </summary>
        public readonly ref Ent owner => ref this.ownerDataPtr.GetOrThrow(this.ent.id, this.ent.gen).ent;
        /// <summary>
        /// Read-only access to owner.
        /// </summary>
        public readonly ref readonly Ent readOwner => ref this.ownerDataPtr.Read(this.ent.id, this.ent.gen).ent;
        /// <summary>
        /// Current health value stored for the unit.
        /// </summary>
        public readonly ref uint health => ref this.healthDataPtr.GetOrThrow(this.ent.id, this.ent.gen).health;
        /// <summary>
        /// Maximum health value stored for the unit.
        /// </summary>
        public readonly ref uint healthMax => ref this.healthDataPtr.GetOrThrow(this.ent.id, this.ent.gen).healthMax;
        /// <summary>
        /// Read-only access to health.
        /// </summary>
        public readonly ref readonly uint readHealth => ref this.healthDataPtr.Read(this.ent.id, this.ent.gen).health;
        /// <summary>
        /// Read-only access to health max.
        /// </summary>
        public readonly ref readonly uint readHealthMax => ref this.healthDataPtr.Read(this.ent.id, this.ent.gen).healthMax;
        /// <summary>
        /// Agent properties used by <c>UnitAspect</c>.
        /// </summary>
        public readonly ref AgentType agentProperties => ref this.componentRuntime.properties;
        /// <summary>
        /// Read-only access to agent properties.
        /// </summary>
        public readonly ref readonly AgentType readAgentProperties => ref this.readComponentRuntime.properties;
        /// <summary>
        /// Type id used to locate the associated entry.
        /// </summary>
        public readonly ref uint typeId => ref this.componentRuntime.properties.typeId;
        /// <summary>
        /// Read-only access to type ID.
        /// </summary>
        public readonly ref readonly uint readTypeId => ref this.readComponentRuntime.properties.typeId;
        /// <summary>
        /// Velocity used by <c>UnitAspect</c>.
        /// </summary>
        public readonly ref float3 velocity => ref this.componentRuntimeRvo.velocity;
        /// <summary>
        /// Read-only access to velocity.
        /// </summary>
        public readonly ref readonly float3 readVelocity => ref this.readComponentRuntimeRvo.velocity;
        /// <summary>
        /// Radius used by the associated shape or query.
        /// </summary>
        public readonly ref tfloat radius => ref this.componentRuntime.properties.radius;
        /// <summary>
        /// Read-only access to radius.
        /// </summary>
        public readonly ref readonly tfloat readRadius => ref this.readComponentRuntime.properties.radius;
        /// <summary>
        /// Movement or transition rate in the units used by this API.
        /// </summary>
        public readonly ref tfloat speed => ref this.componentRuntimeSpeed.speed;
        /// <summary>
        /// Read-only access to speed.
        /// </summary>
        public readonly ref readonly tfloat readSpeed => ref this.readComponentRuntimeSpeed.speed;
        /// <summary>
        /// Maximum speed.
        /// </summary>
        public readonly ref tfloat maxSpeed => ref this.component.maxSpeed;
        /// <summary>
        /// Read-only access to max speed.
        /// </summary>
        public readonly ref readonly tfloat readMaxSpeed => ref this.readComponent.maxSpeed;
        /// <summary>
        /// Acceleration speed controlling the associated calculation.
        /// </summary>
        public readonly ref tfloat accelerationSpeed => ref this.component.accelerationSpeed;
        /// <summary>
        /// Deceleration speed controlling the associated calculation.
        /// </summary>
        public readonly ref tfloat decelerationSpeed => ref this.component.decelerationSpeed;
        /// <summary>
        /// Read-only access to acceleration speed.
        /// </summary>
        public readonly ref readonly tfloat readAccelerationSpeed => ref this.readComponent.accelerationSpeed;
        /// <summary>
        /// Read-only access to deceleration speed.
        /// </summary>
        public readonly ref readonly tfloat readDecelerationSpeed => ref this.readComponent.decelerationSpeed;
        /// <summary>
        /// Rotation speed controlling the associated calculation.
        /// </summary>
        public readonly ref tfloat rotationSpeed => ref this.component.rotationSpeed;
        /// <summary>
        /// Read-only access to rotation speed.
        /// </summary>
        public readonly ref readonly tfloat readRotationSpeed => ref this.readComponent.rotationSpeed;
        /// <summary>
        /// Unit command group used by <c>UnitAspect</c>.
        /// </summary>
        public readonly ref Ent unitCommandGroup => ref this.unitCommandGroupDataPtr.GetOrThrow(this.ent.id, this.ent.gen).unitCommandGroup;
        /// <summary>
        /// Unit selection group used by <c>UnitAspect</c>.
        /// </summary>
        public readonly ref Ent unitSelectionGroup => ref this.unitSelectionGroupDataPtr.GetOrThrow(this.ent.id, this.ent.gen).unitSelectionGroup;
        /// <summary>
        /// Read-only access to unit selection group.
        /// </summary>
        public readonly ref readonly Ent readUnitSelectionGroup => ref this.unitSelectionGroupDataPtr.Read(this.ent.id, this.ent.gen).unitSelectionGroup;
        /// <summary>
        /// Read-only access to unit command group.
        /// </summary>
        public readonly ref readonly Ent readUnitCommandGroup => ref this.unitCommandGroupDataPtr.Read(this.ent.id, this.ent.gen).unitCommandGroup;
        
        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        public readonly ref NavAgentComponent component => ref this.navAgentDataPtr.GetOrThrow(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component.
        /// </summary>
        public readonly ref readonly NavAgentComponent readComponent => ref this.navAgentDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Component runtime in the time units used by the containing API.
        /// </summary>
        public readonly ref NavAgentRuntimeComponent componentRuntime => ref this.navAgentRuntimeDataPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component runtime.
        /// </summary>
        public readonly ref readonly NavAgentRuntimeComponent readComponentRuntime => ref this.navAgentRuntimeDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Component runtime rvo used by <c>UnitAspect</c>.
        /// </summary>
        public readonly ref NavAgentRVOPathRuntimeComponent componentRuntimeRvo => ref this.navAgentRuntimeRvoDataPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component runtime rvo.
        /// </summary>
        public readonly ref readonly NavAgentRVOPathRuntimeComponent readComponentRuntimeRvo => ref this.navAgentRuntimeRvoDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Component runtime speed controlling the associated calculation.
        /// </summary>
        public readonly ref NavAgentRuntimeSpeedComponent componentRuntimeSpeed => ref this.navAgentRuntimeSpeedDataPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component runtime speed.
        /// </summary>
        public readonly ref readonly NavAgentRuntimeSpeedComponent readComponentRuntimeSpeed => ref this.navAgentRuntimeSpeedDataPtr.Read(this.ent.id, this.ent.gen);

        /// <summary>
        /// Removes from command group.
        /// </summary>
        public readonly bool RemoveFromCommandGroup() => UnitUtils.RemoveFromCommandGroup(in this);

        /// <summary>
        /// Handles the transition before the command group is removed.
        /// </summary>
        public readonly bool WillRemoveCommandGroup() => UnitUtils.WillRemoveCommandGroup(in this);

        /// <summary>
        /// Removes from selection group.
        /// </summary>
        public readonly bool RemoveFromSelectionGroup() => UnitUtils.RemoveFromSelectionGroup(in this);

        /// <summary>
        /// Handles the transition before the selection group is removed.
        /// </summary>
        public readonly bool WillRemoveSelectionGroup() => UnitUtils.WillRemoveSelectionGroup(in this);

        /// <summary>
        /// Tests whether the context has selection group.
        /// </summary>
        [INLINE(256)]
        public readonly bool HasSelectionGroup() {
            return this.readUnitSelectionGroup.IsAlive();
        }

        /// <summary>
        /// Tests whether the context has command group.
        /// </summary>
        [INLINE(256)]
        public readonly bool HasCommandGroup() {
            return this.readUnitCommandGroup.IsAlive();
        }

        /// <summary>
        /// Gets placements count; this implementation returns <c>this.readComponentRuntime.placements.Count</c>.
        /// </summary>
        public readonly uint PlacementsCount => this.readComponentRuntime.placements.Count;

        /// <summary>
        /// Validates placements.
        /// </summary>
        [INLINE(256)]
        public readonly void ValidatePlacements(in JobInfo jobInfo) {
            if (this.readComponentRuntime.placements.IsCreated == false) {
                this.InitPlacements(in jobInfo);
            }
            if (this.readComponentRuntime.placementsRoot.IsAlive() == false) {
                this.SetPlacementRoot(this.CreatePlacementsRoot());
            }
        }

        /// <summary>
        /// Creates placements root.
        /// </summary>
        [INLINE(256)]
        public readonly Ent CreatePlacementsRoot() {
            var placements = Ent.New<PlacementsEntityType>(JobInfo.Create(this.ent.worldId), "Placements");
            PlayerUtils.SetOwner(placements, this.readOwner.GetAspect<PlayerAspect>());
            var tr = placements.Set<TransformAspect>();
            // tr.IsStaticLocal = true;
            placements.SetParent(this.ent);
            return placements;
        }

        /// <summary>
        /// Initializes placements.
        /// </summary>
        [INLINE(256)]
        public readonly void InitPlacements(in JobInfo jobInfo) {
            var placementsDataComponent = this.ent.ReadStatic<UnitPlacementsDataComponent>();
            if (placementsDataComponent.placements.IsCreated == true) {
                if (this.readComponentRuntime.placements.IsCreated == false) this.componentRuntime.placements = new ListAuto<Ent>(this.ent, placementsDataComponent.placements.Length);
                for (uint i = 0u; i < placementsDataComponent.placements.Length; ++i) {
                    var placement = placementsDataComponent.placements[i];
                    this.componentRuntime.placements.Add(this.CreatePlacement(in jobInfo, placement.id, placement.placementType, placement.localPosition, placement.localRotation));
                }
            } else {
                this.componentRuntime.placements = new ListAuto<Ent>(this.ent, 2u);
            }
        }

        [INLINE(256)]
        private readonly Ent CreatePlacement(in JobInfo jobInfo, uint id, uint placementType, float3 localPosition, quaternion localRotation) {
            var ent = Ent.New<PlacementEntityType>(in jobInfo, "Placement");
            var tr = ent.Set<TransformAspect>();
            if (this.readComponentRuntime.placementsRoot.IsAlive() == true) {
                ent.SetParent(this.readComponentRuntime.placementsRoot);
            } else {
                ent.SetParent(this.ent);
            }
            PlayerUtils.SetOwner(ent, this.readOwner.GetAspect<PlayerAspect>());
            tr.localPosition = localPosition;
            tr.localRotation = localRotation;
            tr.IsStaticLocal = true;
            ent.Set(new UnitPlacementComponent() {
                placementType = placementType,
                id = id,
            });
            return ent;
        }

        /// <summary>
        /// Sets placement root.
        /// </summary>
        [INLINE(256)]
        public readonly void SetPlacementRoot(Ent ent) {
            this.componentRuntime.placementsRoot = ent;
        }

        /// <summary>
        /// Reads placement.
        /// </summary>
        [INLINE(256)]
        public readonly Ent ReadPlacement(uint id) {
            if (this.readComponentRuntime.placements.IsCreated == true) {
                for (uint i = 0u; i < this.readComponentRuntime.placements.Count; ++i) {
                    var placement = this.readComponentRuntime.placements[i];
                    if (placement.Read<UnitPlacementComponent>().id == id) {
                        return placement;
                    }
                }
            }
            return default;
        }

        /// <summary>
        /// Returns placement.
        /// </summary>
        [INLINE(256)]
        public readonly Ent GetPlacement(in JobInfo jobInfo, uint id) {
            if (this.readComponentRuntime.placements.IsCreated == true) {
                for (uint i = 0u; i < this.readComponentRuntime.placements.Count; ++i) {
                    var placement = this.readComponentRuntime.placements[i];
                    if (placement.Read<UnitPlacementComponent>().id == id) {
                        return placement;
                    }
                }
            }
            return this.CreatePlacement(in jobInfo, id, 0, float3.zero, quaternion.identity);
        }
        
        /// <summary>
        /// Sets to placement.
        /// </summary>
        [INLINE(256)]
        public readonly Ent SetToPlacement(in JobInfo jobInfo, in Ent obj, uint id) {
            this.ValidatePlacements(in jobInfo);
            var placement = this.GetPlacement(in jobInfo, id);
            obj.SetParent(placement);
            placement.Get<UnitPlacementComponent>().obj = obj;
            return placement;
        }

    }

}
