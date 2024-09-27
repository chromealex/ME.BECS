namespace ME.BECS.Units {
    
    using Unity.Mathematics;

    public struct UnitComponentGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.black;
        
    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentComponent : IConfigComponent {

        public float maxSpeed;
        public float accelerationSpeed;
        public float decelerationSpeed;
        public float rotationSpeed;
        public float sightRangeSqr;

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
        public bool collideWithEnd;
        public float3 velocity;

        public Ent attackSensor;

    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct IsUnitStaticComponent : IComponent {}

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitQuadSizeComponent : IConfigComponent {

        public uint2 size;
        public float height;

    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct TimeToBuildComponent : IConfigComponent {

        public float value;

    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct PathFollowComponent : IComponent {}

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitHealthComponent : IConfigComponent {
 
        public float healthMax;
        public float health;

        public ME.BECS.Effects.EffectConfig effectOnHit;
        public ME.BECS.Effects.EffectConfig effectOnDestroy;

    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitCommandGroupComponent : IComponent {

        public Ent unitCommandGroup;
        
    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitSelectionGroupComponent : IComponent {

        public Ent unitSelectionGroup;
        
    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct CommandGroupComponent : IComponent {

        public ListAuto<Ent> units;
        public MemArrayAuto<Ent> targets;
        public Ent nextChainTarget;
        public Ent prevChainTarget;
        public uint volume;
        public LockSpinner lockIndex;

    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct IsCommandGroupDirty : IComponent {}
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct DamageTookComponent : IComponent {

        public Ent sourceUnit;

    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitHoldComponent : IComponent {}

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct SelectionGroupComponent : IComponent {

        public ListAuto<Ent> units;
        
    }

}