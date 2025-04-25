#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Players;

    public struct UnitAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<NavAgentComponent> navAgentDataPtr;
        public AspectDataPtr<NavAgentRuntimeComponent> navAgentRuntimeDataPtr;
        public AspectDataPtr<NavAgentRuntimeSpeedComponent> navAgentRuntimeSpeedDataPtr;
        [QueryWith]
        public AspectDataPtr<UnitCommandGroupComponent> unitCommandGroupDataPtr;
        [QueryWith]
        public AspectDataPtr<UnitSelectionGroupComponent> unitSelectionGroupDataPtr;
        [QueryWith]
        public AspectDataPtr<UnitHealthComponent> healthDataPtr;
        [QueryWith]
        public AspectDataPtr<OwnerComponent> ownerDataPtr;

        public readonly bool IsStatic {
            [INLINE(256)]
            get => this.ent.Has<IsUnitStaticComponent>();
            [INLINE(256)]
            set => this.ent.SetTag<IsUnitStaticComponent>(value);
        }

        public readonly bool IsPathFollow {
            [INLINE(256)]
            get => this.ent.Has<PathFollowComponent>();
            [INLINE(256)]
            set => this.ent.SetTag<PathFollowComponent>(value);
        }

        public readonly bool IsHold {
            [INLINE(256)]
            get => this.ent.Has<UnitHoldComponent>();
            [INLINE(256)]
            set => this.ent.SetTag<UnitHoldComponent>(value);
        }

        public readonly bool IsDead {
            [INLINE(256)]
            get => this.ent.Has<UnitIsDeadTag>();
            [INLINE(256)]
            set => this.ent.SetTag<UnitIsDeadTag>(value);
        }

        public readonly ref tfloat minSightRangeSqr => ref this.component.sightRange.minRangeSqr;
        public readonly ref tfloat sightRangeSqr => ref this.component.sightRange.rangeSqr;
        public readonly ref tfloat sector => ref this.component.sightRange.sector;
        public readonly ref readonly tfloat readMinSightRangeSqr => ref this.readComponent.sightRange.minRangeSqr;
        public readonly ref readonly tfloat readSightRangeSqr => ref this.readComponent.sightRange.rangeSqr;
        public readonly ref readonly tfloat readSector => ref this.readComponent.sightRange.sector;
        public readonly ref tfloat height => ref this.componentRuntime.properties.height;
        public readonly ref readonly tfloat readHeight => ref this.readComponentRuntime.properties.height;
        public readonly ref Ent owner => ref this.ownerDataPtr.Get(this.ent.id, this.ent.gen).ent;
        public readonly ref readonly Ent readOwner => ref this.ownerDataPtr.Read(this.ent.id, this.ent.gen).ent;
        public readonly ref uint health => ref this.healthDataPtr.Get(this.ent.id, this.ent.gen).health;
        public readonly ref uint healthMax => ref this.healthDataPtr.Get(this.ent.id, this.ent.gen).healthMax;
        public readonly ref readonly uint readHealth => ref this.healthDataPtr.Read(this.ent.id, this.ent.gen).health;
        public readonly ref readonly uint readHealthMax => ref this.healthDataPtr.Read(this.ent.id, this.ent.gen).healthMax;
        public readonly ref AgentType agentProperties => ref this.componentRuntime.properties;
        public readonly ref readonly AgentType readAgentProperties => ref this.readComponentRuntime.properties;
        public readonly ref uint typeId => ref this.componentRuntime.properties.typeId;
        public readonly ref readonly uint readTypeId => ref this.readComponentRuntime.properties.typeId;
        public readonly ref float3 velocity => ref this.componentRuntime.velocity;
        public readonly ref readonly float3 readVelocity => ref this.readComponentRuntime.velocity;
        public readonly ref tfloat radius => ref this.componentRuntime.properties.radius;
        public readonly ref readonly tfloat readRadius => ref this.readComponentRuntime.properties.radius;
        public readonly ref tfloat speed => ref this.componentRuntimeSpeed.speed;
        public readonly ref readonly tfloat readSpeed => ref this.readComponentRuntimeSpeed.speed;
        public readonly ref tfloat maxSpeed => ref this.component.maxSpeed;
        public readonly ref readonly tfloat readMaxSpeed => ref this.readComponent.maxSpeed;
        public readonly ref tfloat accelerationSpeed => ref this.component.accelerationSpeed;
        public readonly ref tfloat decelerationSpeed => ref this.component.decelerationSpeed;
        public readonly ref readonly tfloat readAccelerationSpeed => ref this.readComponent.accelerationSpeed;
        public readonly ref readonly tfloat readDecelerationSpeed => ref this.readComponent.decelerationSpeed;
        public readonly ref tfloat rotationSpeed => ref this.component.rotationSpeed;
        public readonly ref readonly tfloat readRotationSpeed => ref this.readComponent.rotationSpeed;
        public readonly ref Ent unitCommandGroup => ref this.unitCommandGroupDataPtr.Get(this.ent.id, this.ent.gen).unitCommandGroup;
        public readonly ref Ent unitSelectionGroup => ref this.unitSelectionGroupDataPtr.Get(this.ent.id, this.ent.gen).unitSelectionGroup;
        public readonly ref readonly Ent readUnitSelectionGroup => ref this.unitSelectionGroupDataPtr.Read(this.ent.id, this.ent.gen).unitSelectionGroup;
        public readonly ref readonly Ent readUnitCommandGroup => ref this.unitCommandGroupDataPtr.Read(this.ent.id, this.ent.gen).unitCommandGroup;
        
        public readonly ref int collideWithEnd => ref this.componentRuntime.collideWithEnd;
        public readonly float3 randomVector => this.componentRuntime.randomVector;
        public readonly ref NavAgentComponent component => ref this.navAgentDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly NavAgentComponent readComponent => ref this.navAgentDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref NavAgentRuntimeComponent componentRuntime => ref this.navAgentRuntimeDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly NavAgentRuntimeComponent readComponentRuntime => ref this.navAgentRuntimeDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref NavAgentRuntimeSpeedComponent componentRuntimeSpeed => ref this.navAgentRuntimeSpeedDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly NavAgentRuntimeSpeedComponent readComponentRuntimeSpeed => ref this.navAgentRuntimeSpeedDataPtr.Read(this.ent.id, this.ent.gen);

        public readonly bool RemoveFromCommandGroup() => UnitUtils.RemoveFromCommandGroup(in this);

        public readonly bool WillRemoveCommandGroup() => UnitUtils.WillRemoveCommandGroup(in this);

        public readonly bool RemoveFromSelectionGroup() => UnitUtils.RemoveFromSelectionGroup(in this);

        public readonly bool WillRemoveSelectionGroup() => UnitUtils.WillRemoveSelectionGroup(in this);

        [INLINE(256)]
        public readonly bool HasSelectionGroup() {
            return this.readUnitSelectionGroup.IsAlive();
        }

        [INLINE(256)]
        public readonly bool HasCommandGroup() {
            return this.readUnitCommandGroup.IsAlive();
        }

        [INLINE(256)]
        [NotThreadSafe]
        public readonly void Hit(uint damage, in Ent source, in JobInfo jobInfo) {
            if (this.readHealth > 0u) {
                var ent = Ent.New(in jobInfo);
                ent.Set(new DamageTookComponent() {
                    source = source,
                    target = this.ent,
                    damage = damage,
                });
                ent.Destroy(1UL);
                this.ent.SetOneShot(new DamageTookEvent() {
                    source = source,
                });
                var tr = this.ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
                ME.BECS.Effects.EffectUtils.CreateEffect(tr.position, tr.rotation, this.ent.ReadStatic<UnitEffectOnHitComponent>().effect, in jobInfo);
            }
        }

    }

}