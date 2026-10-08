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
    /// Stores per-entity state for bullet runtime.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct BulletRuntimeComponent : IComponent {

        /// <summary>
        /// Prev world pos used by the associated spatial operation.
        /// </summary>
        public float3 prevWorldPos;
        /// <summary>
        /// if targetEnt is set - use it,
        /// otherwise use targetWorldPos
        /// </summary>
        public Ent targetEnt;
        /// <summary>
        /// Target world pos used by the associated spatial operation.
        /// </summary>
        public float3 targetWorldPos;
        /// <summary>
        /// Unit source
        /// </summary>
        public Ent sourceUnit;
        /// <summary>
        /// Source world pos used by the associated spatial operation.
        /// </summary>
        public float3 sourceWorldPos;
        
    }
    
    /// <summary>
    /// Stores per-entity state for damage override.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct DamageOverrideComponent : IComponent {

        /// <summary>
        /// Damage value used by the combat calculation.
        /// </summary>
        public uint damage;

    }
    
    /// <summary>
    /// Stores per-entity state for damage min override.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct DamageMinOverrideComponent : IComponent {

        /// <summary>
        /// Damage value used by the combat calculation.
        /// </summary>
        public uint damage;

    }
    
    /// <summary>
    /// Stores per-entity state for damage multiplier.
    /// </summary>
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct DamageMultiplierComponent : IComponent {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static DamageMultiplierComponent Default => new DamageMultiplierComponent() { factor = 1f };
        
        /// <summary>
        /// Factor used by <c>DamageMultiplierComponent</c>.
        /// </summary>
        public tfloat factor;

    }
    
}