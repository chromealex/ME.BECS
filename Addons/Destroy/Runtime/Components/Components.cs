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

namespace ME.BECS {

    public struct DestroyComponentGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.black;
        
    }

    [ComponentGroup(typeof(DestroyComponentGroup))]
    public struct DestroyWithLifetimeMs : IConfigComponent {

        public uint lifetime;

    }

    [ComponentGroup(typeof(DestroyComponentGroup))]
    public struct DestroyWithLifetime : IConfigComponent {

        public tfloat lifetime;

    }
    
    [ComponentGroup(typeof(DestroyComponentGroup))]
    public struct DestroyWithTicks : IConfigComponent {

        public ulong ticks;

    }

}