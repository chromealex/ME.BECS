#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.Units {
    
    using System.Runtime.InteropServices;

    public struct UnitComponentGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.black;
        
    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentComponent : IConfigComponent {
        
        public static NavAgentComponent Default = new NavAgentComponent() {
            sightRange = Sector.Default,
        };

        public tfloat maxSpeed;
        public tfloat accelerationSpeed;
        public tfloat decelerationSpeed;
        public tfloat rotationSpeed;
        public Sector sightRange;

    }

    [EditorComment("Current unit agent values")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentRuntimeComponent : IComponent {

        public AgentType properties;

        public int collideWithEnd;
        
        public float3 collisionDirection;
        public float3 separationVector;
        public float3 alignmentVector;
        public float3 avoidanceVector;
        public float3 cohesionVector;
        public float3 desiredDirection;
        public float3 randomVector;
        public float3 velocity;

        public Ent attackSensor;

    }

    [EditorComment("Current unit agent speed values")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentRuntimeSpeedComponent : IComponent {

        public tfloat speed;

    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct IsUnitStaticComponent : IConfigComponent {}

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitQuadSizeComponent : IConfigComponent {

        public uint2 size;
        public tfloat height;

    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct TimeToBuildComponent : IConfigComponent {

        public tfloat value;

    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct PathFollowComponent : IComponent {}

    [EditorComment("Current unit health")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitHealthComponent : IConfigComponent {
 
        public uint healthMax;
        public uint health;

    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitEffectOnHitComponent : IConfigComponentStatic {
 
        public ME.BECS.Effects.EffectConfig effect;

    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitEffectOnDestroyComponent : IConfigComponentStatic {
 
        public ME.BECS.Effects.EffectConfig effect;

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
    
    [EditorComment("Added as new entity one shot")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct DamageTookComponent : IComponent {

        public Ent source;
        public Ent target;
        public uint damage;

    }
    
    [EditorComment("Added as one-shot component on target unit")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct DamageTookEvent : IComponent {

        public Ent source;

    }
    
    [EditorComment("Is unit on hold?")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitHoldComponent : IComponent {}
    
    [EditorComment("Contains units list")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct SelectionGroupComponent : IComponent {

        public ListAuto<Ent> units;
        
    }

    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitLookAtComponent : IComponent {

        public float3 target;

    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitBelongsToComponent : IConfigComponent {

        public Layer layer;
        
    }
    
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitIsDeadTag : IComponent { }

}