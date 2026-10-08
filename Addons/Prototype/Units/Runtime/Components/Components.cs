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

    /// <summary>
    /// Groups unit components for change tracking and queries.
    /// </summary>
    public struct UnitComponentGroup {
        
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.black;
        
    }

    /// <summary>
    /// Stores per-entity state for unit placement.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitPlacementComponent : IComponent {

        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public uint id;
        /// <summary>
        /// Placement type used by <c>UnitPlacementComponent</c>.
        /// </summary>
        public uint placementType;
        /// <summary>
        /// Object represented by this entry.
        /// </summary>
        public Ent obj;

    }

    /// <summary>
    /// Defines immutable configuration data for unit placements data.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitPlacementsDataComponent : IConfigComponentStatic {

        /// <summary>
        /// Defines placement state and operations for <c>UnitPlacementsDataComponent</c>.
        /// </summary>
        [System.Serializable]
        public struct Placement {

            /// <summary>
            /// Identifier used to address this entry within its containing registry.
            /// </summary>
            public uint id;
            /// <summary>
            /// Placement type used by <c>UnitPlacementsDataComponent.Placement</c>.
            /// </summary>
            public uint placementType;
            /// <summary>
            /// Local position used by the associated spatial operation.
            /// </summary>
            public float3 localPosition;
            /// <summary>
            /// Local rotation used by <c>UnitPlacementsDataComponent.Placement</c>.
            /// </summary>
            public quaternion localRotation;

        }
        
        /// <summary>
        /// Placements used by <c>UnitPlacementsDataComponent</c>.
        /// </summary>
        public MemArrayAuto<Placement> placements;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for nav agent.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentComponent : IConfigComponent {
        
        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static NavAgentComponent Default = new NavAgentComponent() {
            sightRange = Sector.Default,
        };

        /// <summary>
        /// Maximum speed.
        /// </summary>
        public tfloat maxSpeed;
        /// <summary>
        /// Acceleration speed controlling the associated calculation.
        /// </summary>
        public tfloat accelerationSpeed;
        /// <summary>
        /// Deceleration speed controlling the associated calculation.
        /// </summary>
        public tfloat decelerationSpeed;
        /// <summary>
        /// Rotation speed controlling the associated calculation.
        /// </summary>
        public tfloat rotationSpeed;
        /// <summary>
        /// Sight range used by <c>NavAgentComponent</c>.
        /// </summary>
        public Sector sightRange;

    }

    /// <summary>
    /// Stores per-entity state for nav agent rvo path runtime.
    /// </summary>
    [EditorComment("Current unit agent RVO values")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentRVOPathRuntimeComponent : IComponent {

        /// <summary>
        /// Collision direction used by <c>NavAgentRVOPathRuntimeComponent</c>.
        /// </summary>
        public float3 collisionDirection;
        /// <summary>
        /// Alignment vector used by <c>NavAgentRVOPathRuntimeComponent</c>.
        /// </summary>
        public float3 alignmentVector;
        /// <summary>
        /// Velocity used by <c>NavAgentRVOPathRuntimeComponent</c>.
        /// </summary>
        public float3 velocity;
        /// <summary>
        /// Path direction used by <c>NavAgentRVOPathRuntimeComponent</c>.
        /// </summary>
        public float3 pathDirection;

    }

    /// <summary>
    /// Stores per-entity state for nav agent runtime.
    /// </summary>
    [EditorComment("Current unit agent values")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentRuntimeComponent : IComponent {

        /// <summary>
        /// Configuration values used by this operation.
        /// </summary>
        public AgentType properties;

        /// <summary>
        /// Desired direction used by <c>NavAgentRuntimeComponent</c>.
        /// </summary>
        public float3 desiredDirection;

        /// <summary>
        /// Placements root used by <c>NavAgentRuntimeComponent</c>.
        /// </summary>
        public Ent placementsRoot;
        /// <summary>
        /// Placements used by <c>NavAgentRuntimeComponent</c>.
        /// </summary>
        public ListAuto<Ent> placements;
        /// <summary>
        /// Whether collide with end behavior or state is selected.
        /// </summary>
        public bbool collideWithEnd;

    }

    /// <summary>
    /// Stores per-entity state for nav agent runtime speed.
    /// </summary>
    [EditorComment("Current unit agent speed values")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct NavAgentRuntimeSpeedComponent : IComponent {

        /// <summary>
        /// Movement or transition rate in the units used by this API.
        /// </summary>
        public tfloat speed;

    }

    /// <summary>
    /// Defines configuration-backed entity data for is unit.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct IsUnitStaticComponent : IConfigComponent {}

    /// <summary>
    /// Defines configuration-backed entity data for unit quad size.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitQuadSizeComponent : IConfigComponent {

        /// <summary>
        /// Size of the represented value in the units used by this API.
        /// </summary>
        public uint2 size;
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public tfloat height;

    }

    /// <summary>
    /// Defines configuration-backed entity data for time to build.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct TimeToBuildComponent : IConfigComponent {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public tfloat value;

    }
    
    /// <summary>
    /// Stores per-entity state for path follow.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct PathFollowComponent : IComponent {}

    /// <summary>
    /// Defines configuration-backed entity data for unit health.
    /// </summary>
    [EditorComment("Current unit health")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitHealthComponent : IConfigComponent {
 
        /// <summary>
        /// Maximum health value stored for the unit.
        /// </summary>
        public uint healthMax;
        /// <summary>
        /// Current health value stored for the unit.
        /// </summary>
        public uint health;

    }

    /// <summary>
    /// Stores per-entity state for unit just spawned event.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitJustSpawnedEvent : IComponent { }

    /// <summary>
    /// Defines immutable configuration data for unit effect on hit.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitEffectOnHitComponent : IConfigComponentStatic {
 
        /// <summary>
        /// Effect configuration or instance used by this operation.
        /// </summary>
        public ME.BECS.Effects.EffectConfig effect;

    }

    /// <summary>
    /// Defines immutable configuration data for unit effect on destroy.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitEffectOnDestroyComponent : IConfigComponentStatic {
 
        /// <summary>
        /// Effect configuration or instance used by this operation.
        /// </summary>
        public ME.BECS.Effects.EffectConfig effect;

    }

    /// <summary>
    /// Defines immutable configuration data for unit effect on spawn.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitEffectOnSpawnComponent : IConfigComponentStatic {
 
        /// <summary>
        /// Effect configuration or instance used by this operation.
        /// </summary>
        public ME.BECS.Effects.EffectConfig effect;

    }

    /// <summary>
    /// Stores per-entity state for unit command group.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitCommandGroupComponent : IComponent {

        /// <summary>
        /// Unit command group used by <c>UnitCommandGroupComponent</c>.
        /// </summary>
        public Ent unitCommandGroup;
        
    }

    /// <summary>
    /// Stores per-entity state for unit selection group.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitSelectionGroupComponent : IComponent {

        /// <summary>
        /// Unit selection group used by <c>UnitSelectionGroupComponent</c>.
        /// </summary>
        public Ent unitSelectionGroup;
        
    }

    /// <summary>
    /// Stores per-entity state for command group.
    /// </summary>
    [EditorComment("Contains units list and chain targets")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct CommandGroupComponent : IComponent {

        /// <summary>
        /// Index of the synchronization lock used for this entry.
        /// </summary>
        public LockSpinner lockIndex;
        /// <summary>
        /// Units included in the associated collection or operation.
        /// </summary>
        public ListAuto<Ent> units;
        /// <summary>
        /// Targets considered by the associated operation.
        /// </summary>
        public MemArrayAuto<Ent> targets;
        /// <summary>
        /// Next chain target used by <c>CommandGroupComponent</c>.
        /// </summary>
        public Ent nextChainTarget;
        /// <summary>
        /// Prev chain target used by <c>CommandGroupComponent</c>.
        /// </summary>
        public Ent prevChainTarget;
        /// <summary>
        /// Volume used by the associated bounds calculation.
        /// </summary>
        public uint volume;

    }
    
    /// <summary>
    /// Stores per-entity state for is command group dirty.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct IsCommandGroupDirty : IComponent {}
    
    /// <summary>
    /// Stores per-entity state for damage took.
    /// </summary>
    [EditorComment("Added as new entity one shot")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct DamageTookComponent : IComponent {

        /// <summary>
        /// Source owner used by <c>DamageTookComponent</c>.
        /// </summary>
        public Ent sourceOwner;
        /// <summary>
        /// Source data or instance used by this operation.
        /// </summary>
        public Ent source;
        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public Ent target;
        /// <summary>
        /// Damage value used by the combat calculation.
        /// </summary>
        public uint damage;
        /// <summary>
        /// Damage total used by <c>DamageTookComponent</c>.
        /// </summary>
        public uint damageTotal;
        /// <summary>
        /// Damage type id used to locate the associated entry.
        /// </summary>
        public byte damageTypeId;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for unit invincibility.
    /// </summary>
    [EditorComment("Is unit invincible or not")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitInvincibility : IConfigComponent {

        /// <summary>
        /// Defines the supported invincible behaviour values.
        /// </summary>
        public enum InvincibleBehaviour {
            /// <summary>
            /// No damage option for <c>UnitInvincibility.InvincibleBehaviour</c>.
            /// </summary>
            NoDamage = 0,
            /// <summary>
            /// Ignore last hit option for <c>UnitInvincibility.InvincibleBehaviour</c>.
            /// </summary>
            IgnoreLastHit = 1,
        }
        
        /// <summary>
        /// Behaviour used by <c>UnitInvincibility</c>.
        /// </summary>
        public InvincibleBehaviour behaviour;

    }

    /// <summary>
    /// Stores per-entity state for damage took event.
    /// </summary>
    [EditorComment("Added as one-shot component on target unit")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct DamageTookEvent : IComponent {

        /// <summary>
        /// Source data or instance used by this operation.
        /// </summary>
        public Ent source; // Unit ent

    }

    /// <summary>
    /// Stores per-entity state for last damage source.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct LastDamageSource : IComponent {

        /// <summary>
        /// Source data or instance used by this operation.
        /// </summary>
        public Ent source;
        /// <summary>
        /// Owner associated with this entry.
        /// </summary>
        public Ent owner;

    }
    
    /// <summary>
    /// Stores per-entity state for unit hold.
    /// </summary>
    [EditorComment("Is unit on hold?")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitHoldComponent : IComponent {}
    
    /// <summary>
    /// Stores per-entity state for selection group.
    /// </summary>
    [EditorComment("Contains units list")]
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct SelectionGroupComponent : IComponent {

        /// <summary>
        /// Units included in the associated collection or operation.
        /// </summary>
        public ListAuto<Ent> units;
        
    }

    /// <summary>
    /// Stores per-entity state for unit look at.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitLookAtComponent : IComponent {

        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public float3 target;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for unit belongs to.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitBelongsToComponent : IConfigComponent {

        /// <summary>
        /// Layer used by <c>UnitBelongsToComponent</c>.
        /// </summary>
        public Layer layer;
        
    }
    
    /// <summary>
    /// Stores per-entity state for unit is dead tag.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitIsDeadTag : IComponent { }
    
    /// <summary>
    /// Stores per-entity state for unit is collide with end.
    /// </summary>
    [ComponentGroup(typeof(UnitComponentGroup))]
    public struct UnitIsCollideWithEnd : IComponent { }

}
