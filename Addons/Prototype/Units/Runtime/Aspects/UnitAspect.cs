namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    public struct UnitAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<NavAgentComponent> navAgentDataPtr;
        public AspectDataPtr<NavAgentRuntimeComponent> navAgentRuntimeDataPtr;
        [QueryWith]
        public AspectDataPtr<UnitGroupComponent> unitGroupDataPtr;
        [QueryWith]
        public AspectDataPtr<UnitHealthComponent> healthDataPtr;
        [QueryWith]
        public AspectDataPtr<ME.BECS.Players.OwnerComponent> ownerDataPtr;

        public readonly ref Ent owner => ref this.ownerDataPtr.Get(this.ent.id, this.ent.gen).ent;
        public readonly ref float health => ref this.healthDataPtr.Get(this.ent.id, this.ent.gen).health;
        public readonly ref float healthMax => ref this.healthDataPtr.Get(this.ent.id, this.ent.gen).healthMax;
        public readonly ref AgentType agentProperties => ref this.componentRuntime.properties;
        public readonly ref uint typeId => ref this.componentRuntime.properties.typeId;
        public readonly ref float3 velocity => ref this.componentRuntime.velocity;
        public readonly ref float radius => ref this.componentRuntime.properties.radius;
        public readonly ref float speed => ref this.componentRuntime.speed;
        public readonly ref float maxSpeed => ref this.component.maxSpeed;
        public readonly ref float accelerationSpeed => ref this.component.accelerationSpeed;
        public readonly ref float deaccelerationSpeed => ref this.component.deaccelerationSpeed;
        public readonly ref float rotationSpeed => ref this.component.rotationSpeed;
        public readonly ref Ent unitGroup => ref this.unitGroupDataPtr.Get(this.ent.id, this.ent.gen).unitGroup;
        public readonly ref bool pathFollow => ref this.componentRuntime.pathFollow;
        public readonly ref bool collideWithEnd => ref this.componentRuntime.collideWithEnd;
        public readonly float3 randomVector => this.componentRuntime.randomVector;
        public readonly ref NavAgentComponent component => ref this.navAgentDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref NavAgentRuntimeComponent componentRuntime => ref this.navAgentRuntimeDataPtr.Get(this.ent.id, this.ent.gen);

        public readonly bool RemoveFromGroup() => Utils.RemoveFromGroup(in this);

        public readonly bool WillRemoveGroup() => Utils.WillRemoveGroup(in this);

        [INLINE(256)]
        public void Hit(float damage) {
            if (this.health > 0f) {
                //JobUtils.Decrement(ref this.health, damage);
                this.health -= damage;
                var tr = this.ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
                ME.BECS.Effects.EffectUtils.CreateEffect(tr.position, tr.rotation, in this.ent.Read<UnitHealthComponent>().effectOnHit);
            }
        }

    }

}