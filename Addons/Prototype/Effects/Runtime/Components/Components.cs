#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS.Effects {
    
    /// <summary>
    /// Groups effect components for change tracking and queries.
    /// </summary>
    public struct EffectComponentGroup {
        
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.black;
        
    }
    
    /// <summary>
    /// Stores per-entity state for effect.
    /// </summary>
    [ComponentGroup(typeof(EffectComponentGroup))]
    public struct EffectComponent : IComponent {

    }

    /// <summary>
    /// Use this in component with EntityConfig
    /// </summary>
    [System.Serializable]
    public struct EffectConfig {

        /// <summary>
        /// Configuration supplying values for this instance.
        /// </summary>
        public Config config;
        /// <summary>
        /// Remaining or configured lifetime in the units used by this API.
        /// </summary>
        public tfloat lifetime;

    }

}