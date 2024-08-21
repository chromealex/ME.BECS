namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
    using ME.BECS.Players;

    public struct UnitAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<NavAgentComponent> navAgentDataPtr;
        public AspectDataPtr<NavAgentRuntimeComponent> navAgentRuntimeDataPtr;
        [QueryWith]
        public AspectDataPtr<UnitCommandGroupComponent> unitCommandGroupDataPtr;
        [QueryWith]
        public AspectDataPtr<UnitSelectionGroupComponent> unitSelectionGroupDataPtr;
        [QueryWith]
        public AspectDataPtr<UnitHealthComponent> healthDataPtr;
        [QueryWith]
        public AspectDataPtr<OwnerComponent> ownerDataPtr;

        public readonly bool isStatic {
            [INLINE(256)]
            get => this.ent.Has<IsUnitStaticComponent>();
            [INLINE(256)]
            set => this.ent.SetTag<IsUnitStaticComponent>(value);
        }

        public readonly ref float sightRangeSqr => ref this.component.sightRangeSqr;
        public readonly ref float height => ref this.componentRuntime.properties.height;
        public readonly ref readonly float readHeight => ref this.readComponentRuntime.properties.height;
        public readonly ref Ent owner => ref this.ownerDataPtr.Get(this.ent.id, this.ent.gen).ent;
        public readonly ref readonly Ent readOwner => ref this.ownerDataPtr.Read(this.ent.id, this.ent.gen).ent;
        public readonly ref float health => ref this.healthDataPtr.Get(this.ent.id, this.ent.gen).health;
        public readonly ref float healthMax => ref this.healthDataPtr.Get(this.ent.id, this.ent.gen).healthMax;
        public readonly ref readonly float readHealth => ref this.healthDataPtr.Read(this.ent.id, this.ent.gen).health;
        public readonly ref readonly float readHealthMax => ref this.healthDataPtr.Read(this.ent.id, this.ent.gen).healthMax;
        public readonly ref AgentType agentProperties => ref this.componentRuntime.properties;
        public readonly ref uint typeId => ref this.componentRuntime.properties.typeId;
        public readonly ref readonly uint readTypeId => ref this.readComponentRuntime.properties.typeId;
        public readonly ref float3 velocity => ref this.componentRuntime.velocity;
        public readonly ref float radius => ref this.componentRuntime.properties.radius;
        public readonly ref readonly float readRadius => ref this.readComponentRuntime.properties.radius;
        public readonly ref float speed => ref this.componentRuntime.speed;
        public readonly ref float maxSpeed => ref this.component.maxSpeed;
        public readonly ref float accelerationSpeed => ref this.component.accelerationSpeed;
        public readonly ref float decelerationSpeed => ref this.component.decelerationSpeed;
        public readonly ref float rotationSpeed => ref this.component.rotationSpeed;
        public readonly ref Ent unitCommandGroup => ref this.unitCommandGroupDataPtr.Get(this.ent.id, this.ent.gen).unitCommandGroup;
        public readonly ref Ent unitSelectionGroup => ref this.unitSelectionGroupDataPtr.Get(this.ent.id, this.ent.gen).unitSelectionGroup;
        public readonly ref readonly Ent readUnitSelectionGroup => ref this.unitSelectionGroupDataPtr.Read(this.ent.id, this.ent.gen).unitSelectionGroup;
        public readonly ref readonly Ent readUnitCommandGroup => ref this.unitCommandGroupDataPtr.Read(this.ent.id, this.ent.gen).unitCommandGroup;
        
        public readonly ref bool collideWithEnd => ref this.componentRuntime.collideWithEnd;
        public readonly float3 randomVector => this.componentRuntime.randomVector;
        public readonly ref NavAgentComponent component => ref this.navAgentDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref NavAgentRuntimeComponent componentRuntime => ref this.navAgentRuntimeDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly NavAgentRuntimeComponent readComponentRuntime => ref this.navAgentRuntimeDataPtr.Read(this.ent.id, this.ent.gen);

        public readonly bool RemoveFromCommandGroup() => UnitUtils.RemoveFromCommandGroup(in this);

        public readonly bool WillRemoveCommandGroup() => UnitUtils.WillRemoveCommandGroup(in this);

        public readonly bool RemoveFromSelectionGroup() => UnitUtils.RemoveFromSelectionGroup(in this);

        public readonly bool WillRemoveSelectionGroup() => UnitUtils.WillRemoveSelectionGroup(in this);

        public readonly bool IsPathFollow {
            get => this.ent.Has<PathFollowComponent>();
            set => this.ent.SetTag<PathFollowComponent>(value);
        }

        public readonly bool IsHold {
            get => this.ent.Has<UnitHoldComponent>();
            set => this.ent.SetTag<UnitHoldComponent>(value);
        }

        [INLINE(256)]
        public readonly void Hit(float damage, in Ent source, JobInfo jobInfo) {
            if (this.health > 0f) {
                JobUtils.Decrement(ref this.health, damage);
                this.ent.SetOneShot(new DamageTookComponent() {
                    sourceUnit = source,
                }, OneShotType.NextTick);
                var tr = this.ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
                ME.BECS.Effects.EffectUtils.CreateEffect(tr.position, tr.rotation, in this.ent.Read<UnitHealthComponent>().effectOnHit, jobInfo);
            }
        }

    }

}