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

    /// <summary>
    /// Groups destroy components for change tracking and queries.
    /// </summary>
    public struct DestroyComponentGroup {
        
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.black;
        
    }

    /// <summary>
    /// Defines configuration-backed entity data for destroy with lifetime config ms.
    /// </summary>
    [ComponentGroup(typeof(DestroyComponentGroup))]
    [EditorComment("Use this component to configure destroy lifetime after manual destroy call ent.DestroyWithLifetime()")]
    public struct DestroyWithLifetimeConfigMs : IConfigComponent {

        /// <summary>
        /// Remaining or configured lifetime in the units used by this API.
        /// </summary>
        public uint lifetime;

    }

    /// <summary>
    /// Defines configuration-backed entity data for destroy with lifetime ms.
    /// </summary>
    [ComponentGroup(typeof(DestroyComponentGroup))]
    public struct DestroyWithLifetimeMs : IConfigComponent {

        /// <summary>
        /// Remaining or configured lifetime in the units used by this API.
        /// </summary>
        public uint lifetime;

    }

    /// <summary>
    /// Defines configuration-backed entity data for destroy with lifetime.
    /// </summary>
    [ComponentGroup(typeof(DestroyComponentGroup))]
    public struct DestroyWithLifetime : IConfigComponent {

        /// <summary>
        /// Remaining or configured lifetime in the units used by this API.
        /// </summary>
        public tfloat lifetime;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for destroy with ticks.
    /// </summary>
    [ComponentGroup(typeof(DestroyComponentGroup))]
    public struct DestroyWithTicks : IConfigComponent {

        /// <summary>
        /// Ticks used by <c>DestroyWithTicks</c>.
        /// </summary>
        public ulong ticks;

    }

}