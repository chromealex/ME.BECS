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

namespace ME.BECS.Commands {
    
    /// <summary>
    /// Defines command move state and operations.
    /// </summary>
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct CommandMove : ICommandComponent {

        /// <summary>
        /// Gets target position; this implementation returns <c>this.targetPosition</c>.
        /// </summary>
        public float3 TargetPosition => this.targetPosition;

        /// <summary>
        /// Target position used by the associated spatial operation.
        /// </summary>
        public float3 targetPosition;
        /// <summary>
        /// Targets considered by the associated operation.
        /// </summary>
        public ListAuto<float3> targets;

    }

}