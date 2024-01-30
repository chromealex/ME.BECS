namespace ME.BECS.Units {
    
    using Unity.Mathematics;

    public struct UnitComponentGroup { }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentComponent : IConfigComponent {

        public float maxSpeed;
        public float accelerationSpeed;
        public float deaccelerationSpeed;
        public float rotationSpeed;

    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentRuntimeComponent : IComponent {

        public AgentType properties;

        public float speed;
        public float3 collisionDirection;
        public float3 avoidanceVector;
        public float3 separationVector;
        public float3 alignmentVector;
        public float3 cohesionVector;
        public float3 desiredDirection;
        public float3 randomVector;
        public bool pathFollow;
        public bool collideWithEnd;
        public float3 velocity;

        public Ent attackSensor;

    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitHealthComponent : IConfigComponent {
 
        public float healthMax;
        public float health;

        public ME.BECS.Effects.EffectConfig effectOnHit;

    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitGroupComponent : IComponent {

        public Ent unitGroup;

    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct GroupComponent : IComponent {

        public ListAuto<Ent> units;
        public MemArrayAuto<Ent> targets;
        public float volume;

    }

}