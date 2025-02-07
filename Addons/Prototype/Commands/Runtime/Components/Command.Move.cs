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
    
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct CommandMove : ICommandComponent {

        public float3 TargetPosition => this.targetPosition;

        public float3 targetPosition;

    }

}