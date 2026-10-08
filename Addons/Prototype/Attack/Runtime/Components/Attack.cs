#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Attack {

    /// <summary>
    /// Groups attack components for change tracking and queries.
    /// </summary>
    public struct AttackComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = new UnityEngine.Color(0.65f, 0.1f, 0f);

    }

    /// <summary>
    /// Defines configuration-backed entity data for attack.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackComponent : IConfigComponent, IConfigInitialize {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static AttackComponent Default => new AttackComponent() {
            sector = Sector.Default,
        };

        /// <summary>
        /// Sector bounds used by the spatial query.
        /// </summary>
        [UnityEngine.Header("General")]
        public Sector sector;
        /// <summary>
        /// Whether the query excludes the entity that initiated it.
        /// </summary>
        public bbool ignoreSelf;
        /// <summary>
        /// Reload time in the time units used by the containing API.
        /// </summary>
        public tfloat reloadTime;
        /// <summary>
        /// Animation duration.
        /// </summary>
        [Tooltip("Animation duration")]
        public tfloat fireTime;
        /// <summary>
        /// Fire time in animation between 0 and fireTime.
        /// </summary>
        [Tooltip("Fire time in animation between 0 and fireTime")]
        public tfloat attackTime;
        
        /// <summary>
        /// Rate time in the time units used by the containing API.
        /// </summary>
        [UnityEngine.Header("Fire Rate")]
        public tfloat rateTime;
        /// <summary>
        /// Rate count for the associated storage.
        /// </summary>
        public uint rateCount;

        /// <summary>
        /// Initializes attack component state from the supplied context.
        /// </summary>
        public void OnInitialize(in Ent ent) {
            ent.Set(new AttackRuntimeReloadComponent());
            ent.Set(new AttackRuntimeFireComponent());
        }

    }

    /// <summary>
    /// Defines configuration-backed entity data for attack bullet distribution.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackBulletDistributionComponent : IConfigComponent {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static AttackBulletDistributionComponent Default => new AttackBulletDistributionComponent() {
            sector = Sector.Default,
        };

        /// <summary>
        /// Defines the supported bullets spawn behaviour values.
        /// </summary>
        public enum BulletsSpawnBehaviour {
            /// <summary>
            /// Sector uniform distribution option for <c>AttackBulletDistributionComponent.BulletsSpawnBehaviour</c>.
            /// </summary>
            [UnityEngine.Tooltip("Uniform distribution in sector")]
            SectorUniformDistribution,
            /// <summary>
            /// Sector random distribution option for <c>AttackBulletDistributionComponent.BulletsSpawnBehaviour</c>.
            /// </summary>
            [UnityEngine.Tooltip("Random distribution in sector")]
            SectorRandomDistribution,
            /// <summary>
            /// Sector uniform random distribution option for <c>AttackBulletDistributionComponent.BulletsSpawnBehaviour</c>.
            /// </summary>
            [UnityEngine.Tooltip("Random distribution based on uniform distribution in sector")]
            SectorUniformRandomDistribution,
        }
        
        /// <summary>
        /// Bullets count at a time.
        /// </summary>
        [Tooltip("Bullets count at a time")]
        public uint bulletsCount;
        /// <summary>
        /// Sector bounds used by the spatial query.
        /// </summary>
        public Sector sector;
        /// <summary>
        /// Bullets spawn behaviour used by <c>AttackBulletDistributionComponent</c>.
        /// </summary>
        public BulletsSpawnBehaviour bulletsSpawnBehaviour;

    }

    /// <summary>
    /// Defines configuration-backed entity data for attack visual.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackVisualComponent : IConfigComponent {

        /// <summary>
        /// Bullet config used by <c>AttackVisualComponent</c>.
        /// </summary>
        public Config bulletConfig;
        /// <summary>
        /// Muzzle view used by <c>AttackVisualComponent</c>.
        /// </summary>
        public ME.BECS.Views.View muzzleView;

    }

    /// <summary>
    /// Defines configuration-backed entity data for attack targets count.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackTargetsCountComponent : IConfigComponent {

        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count;

    }

    /// <summary>
    /// Defines configuration-backed entity data for max hit count.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct MaxHitCountComponent : IConfigComponent {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public ushort value;

    }

    /// <summary>
    /// Stores per-entity state for attack runtime reload.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackRuntimeReloadComponent : IComponent {

        /// <summary>
        /// Reload timer in the time units used by the containing API.
        /// </summary>
        public tfloat reloadTimer;

    }

    /// <summary>
    /// Stores per-entity state for attack runtime fire.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackRuntimeFireComponent : IComponent {

        /// <summary>
        /// Fire timer in the time units used by the containing API.
        /// </summary>
        public tfloat fireTimer;
        /// <summary>
        /// Fire rate timer in the time units used by the containing API.
        /// </summary>
        public tfloat fireRateTimer;
        /// <summary>
        /// Fire rate count for the associated storage.
        /// </summary>
        public uint fireRateCount;
        /// <summary>
        /// Targets considered by the associated operation.
        /// </summary>
        public MemArrayAuto<float3> targets;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for attack sector.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackSectorComponent : IConfigComponent {

        /// <summary>
        /// Sector bounds used by the spatial query.
        /// </summary>
        public Sector sector;

    }

    /// <summary>
    /// Defines configuration-backed entity data for can fire while moves tag.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct CanFireWhileMovesTag : IConfigComponent {}

    /// <summary>
    /// Stores per-entity state for attack target.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackTargetComponent : IComponent {

        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public Ent target;

    }
    
    /// <summary>
    /// Stores per-entity state for attack targets.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackTargetsComponent : IComponent {

        /// <summary>
        /// Targets considered by the associated operation.
        /// </summary>
        public ListAuto<Ent> targets;

    }

    /// <summary>
    /// Stores per-entity state for reloaded.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct ReloadedComponent : IComponent {}
    /// <summary>
    /// Stores per-entity state for can fire.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct CanFireComponent : IComponent {}
    /// <summary>
    /// Stores per-entity state for fire used.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct FireUsedComponent : IComponent {}
    
    /// <summary>
    /// Stores per-entity state for on fire event.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct OnFireEvent : IComponent {}
    
    /// <summary>
    /// Defines configuration-backed entity data for rotate to attack while idle.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct RotateToAttackWhileIdleComponent : IConfigComponent {}

    /// <summary>
    /// Defines configuration-backed entity data for rotate attack sensor.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct RotateAttackSensorComponent : IConfigComponent {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static RotateAttackSensorComponent Default => new RotateAttackSensorComponent() { rotationSpeed = 1f, upNormal = math.up() };
        
        /// <summary>
        /// Degrees per second
        /// </summary>
        public tfloat rotationSpeed;
        /// <summary>
        /// Degrees per second 
        /// </summary>
        public tfloat persistentRotationSpeed;
        /// <summary>
        /// Should object return to local identity rotation or not
        /// </summary>
        public bbool returnToDefault;
        /// <summary>
        /// Local UP normal
        /// </summary>
        public float3 upNormal;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for attack filter.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackFilterComponent : IConfigComponent {

        /// <summary>
        /// Layers used by <c>AttackFilterComponent</c>.
        /// </summary>
        public ME.BECS.Units.LayerMask layers;

    }

    /// <summary>
    /// Defines immutable configuration data for attacker follow distance.
    /// </summary>
    [EditorComment("Unit will follow attacker")]
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackerFollowDistanceComponent : IConfigComponentStatic {

        /// <summary>
        /// Maximum value sqr.
        /// </summary>
        public tfloat maxValueSqr;

    }
    /// <summary>
    /// Stores per-entity state for comeback after attack.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    [EditorComment("The component contains position to comeback after trying to attack unit on damage took")]
    public struct ComebackAfterAttackComponent : IComponent {

        /// <summary>
        /// Return to position used by the associated spatial operation.
        /// </summary>
        public float3 returnToPosition;

    }
    
    /// <summary>
    /// Stores per-entity state for last target data.
    /// </summary>
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct LastTargetDataComponent : IComponent {

        /// <summary>
        /// Result produced by the associated operation.
        /// </summary>
        public AttackUtils.ReactionType result;
        /// <summary>
        /// Position in the coordinate space used by the containing API.
        /// </summary>
        public float3 position;
        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public Ent target;

    }

}