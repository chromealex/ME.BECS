#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.Bullets {

    /// <summary>
    /// Groups bullet components for change tracking and queries.
    /// </summary>
    public struct BulletComponentGroup {
        
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.red;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for is bullet custom fly.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct IsBulletCustomFlyComponent : IConfigComponent {}
    
    /// <summary>
    /// Defines configuration-backed entity data for bullet config.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct BulletConfigComponent : IConfigComponent {

        /// <summary>
        /// Damage value (min)
        /// </summary>
        [Tooltip("Useful with hitRangeSqr only, used for fade damage range calculations.")]
        public uint damageMin;
        /// <summary>
        /// Damage value (max)
        /// </summary>
        public uint damage;

        /// <summary>
        /// If hitRangeSqr > 0  -> use splash damage
        /// If hitRangeSqr <= 0 -> use single damage at point or for targetEnt
        /// </summary>
        [ValueSqr]
        public tfloat hitRangeSqr;

        /// <summary>
        /// Movement or transition rate in the units used by this API.
        /// </summary>
        public tfloat speed;

        /// <summary>
        /// If set - bullet will move towards target point if it moves
        /// </summary>
        public bbool autoTarget;

    }

    /// <summary>
    /// Defines immutable configuration data for bullet effect on destroy.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct BulletEffectOnDestroy : IConfigComponentStatic {

        /// <summary>
        /// Effect configuration or instance used by this operation.
        /// </summary>
        public ME.BECS.Effects.EffectConfig effect;

    }

    /// <summary>
    /// Stores per-entity state for fire point.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct FirePointComponent : IComponent {

        /// <summary>
        /// Points used by the associated geometry or query.
        /// </summary>
        public ListAuto<Ent> points;
        /// <summary>
        /// Index of this entry within its containing storage.
        /// </summary>
        public uint index;

    }

    /// <summary>
    /// Defines immutable configuration data for bullet view point.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct BulletViewPoint : IConfigComponentStatic {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static BulletViewPoint Default = new BulletViewPoint() {
            rotation = quaternion.identity,
        };

        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public uint id;
        /// <summary>
        /// Position in the coordinate space used by the containing API.
        /// </summary>
        public float3 position;
        /// <summary>
        /// Orientation in the coordinate space used by the containing API.
        /// </summary>
        public quaternion rotation;

    }

    /// <summary>
    /// Defines immutable configuration data for bullet view points.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct BulletViewPoints : IConfigComponentStatic {

        /// <summary>
        /// Points used by the associated geometry or query.
        /// </summary>
        public MemArrayAuto<BulletViewPoint> points;

    }

}