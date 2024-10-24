namespace ME.BECS.Units {
    
    using Unity.Mathematics;
    using System.Runtime.InteropServices;

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

    [EditorComment("Current unit agent values")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentRuntimeComponent : IComponent {

        public AgentType properties;

        public float speed;
        public int collideWithEnd;
        
        public float3 collisionDirection;
        public float3 avoidanceVector;
        public float3 separationVector;
        public float3 alignmentVector;
        public float3 cohesionVector;
        public float3 desiredDirection;
        public float3 randomVector;
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

    [EditorComment("Current unit health")]
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

    [EditorComment("Contains units list and chain targets")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct CommandGroupComponent : IComponent {

        public LockSpinner lockIndex;
        public ListAuto<Ent> units;
        public MemArrayAuto<Ent> targets;
        public Ent nextChainTarget;
        public Ent prevChainTarget;
        public uint volume;

    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct IsCommandGroupDirty : IComponent {}
    
    [EditorComment("Added as one-shot component on damage took")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct DamageTookComponent : IComponent {

        public Ent source;
        public Ent target;
        public float damage;

    }
    
    [EditorComment("Is unit on hold?")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitHoldComponent : IComponent {}
    
    [EditorComment("Contains units list")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct SelectionGroupComponent : IComponent {

        public ListAuto<Ent> units;
        
    }

}