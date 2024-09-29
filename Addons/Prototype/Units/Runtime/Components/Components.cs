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

    [ComponentGroup(typeof(UnitComponentGroup))]
    [StructLayout(LayoutKind.Explicit)]
    public struct NavAgentRuntimeComponent : IComponent {

        [FieldOffset(0)]
        public AgentType properties;

        [FieldOffset(20)]
        public float speed;
        [FieldOffset(24)]
        public float3 collisionDirection;
        [FieldOffset(24 + 12)]
        public float3 avoidanceVector;
        [FieldOffset(24 + 12 + 12)]
        public float3 separationVector;
        [FieldOffset(24 + 12 + 12 + 12)]
        public float3 alignmentVector;
        [FieldOffset(24 + 12 + 12 + 12 + 12)]
        public float3 cohesionVector;
        [FieldOffset(24 + 12 + 12 + 12 + 12 + 12)]
        public float3 desiredDirection;
        [FieldOffset(24 + 12 + 12 + 12 + 12 + 12 + 12)]
        public float3 randomVector;
        [FieldOffset(24 + 12 + 12 + 12 + 12 + 12 + 12 + 12)]
        public float3 velocity;
        [FieldOffset(24 + 12 + 12 + 12 + 12 + 12 + 12 + 12 + 12)]
        public int collideWithEnd;

        [FieldOffset(24 + 12 + 12 + 12 + 12 + 12 + 12 + 12 + 12 + 4)]
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