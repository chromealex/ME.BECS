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
    /// Identifies the movement-agent configuration used for pathfinding.
    /// </summary>
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AgentType {

        /// <summary>
        /// Type id used to locate the associated entry.
        /// </summary>
        [UnityEngine.HideInInspector]
        public uint typeId;
        /// <summary>
        /// Radius used by the associated shape or query.
        /// </summary>
        public tfloat radius;
        /// <summary>
        /// Avoidance range used by <c>AgentType</c>.
        /// </summary>
        public tfloat avoidanceRange;
        /// <summary>
        /// Maximum slope.
        /// </summary>
        public tfloat maxSlope;
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public tfloat height;

    }

}