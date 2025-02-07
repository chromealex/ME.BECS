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
    
    public struct EffectComponentGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.black;
        
    }
    
    [ComponentGroup(typeof(EffectComponentGroup))]
    public struct EffectComponent : IComponent {

    }

    /// <summary>
    /// Use this in component with EntityConfig
    /// </summary>
    [System.Serializable]
    public struct EffectConfig {

        public Config config;
        public tfloat lifetime;

    }

}