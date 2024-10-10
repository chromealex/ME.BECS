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
    [StructLayout(LayoutKind.Explicit, Size = 136)]
    public struct NavAgentRuntimeComponent : IComponent {

        [FieldOffset(0)]
        public AgentType properties;

        [FieldOffset(AgentType.SIZE)]
        public float speed;
        [FieldOffset(AgentType.SIZE + 4)]
        public int collideWithEnd;
        
        [FieldOffset(AgentType.SIZE + 4 + sizeof(float) + sizeof(int))]
        public float3 collisionDirection;
        [FieldOffset(AgentType.SIZE + 4 + sizeof(float) + sizeof(int) + 12)]
        public float3 avoidanceVector;
        [FieldOffset(AgentType.SIZE + 4 + sizeof(float) + sizeof(int) + 12 * 2)]
        public float3 separationVector;
        [FieldOffset(AgentType.SIZE + 4 + sizeof(float) + sizeof(int) + 12 * 3)]
        public float3 alignmentVector;
        [FieldOffset(AgentType.SIZE + 4 + sizeof(float) + sizeof(int) + 12 * 4)]
        public float3 cohesionVector;
        [FieldOffset(AgentType.SIZE + 4 + sizeof(float) + sizeof(int) + 12 * 5)]
        public float3 desiredDirection;
        [FieldOffset(AgentType.SIZE + 4 + sizeof(float) + sizeof(int) + 12 * 6)]
        public float3 randomVector;
        [FieldOffset(AgentType.SIZE + 4 + sizeof(float) + sizeof(int) + 12 * 7)]
        public float3 velocity;

        [FieldOffset(AgentType.SIZE + 4 + sizeof(float) + sizeof(int) + 12 * 8)]
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

        public Ent sourceUnit;

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