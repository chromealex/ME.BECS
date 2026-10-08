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
    /// Defines command build state and operations.
    /// </summary>
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct CommandBuild : ICommandComponent {

        /// <summary>
        /// Gets target position; this implementation returns <c>this.snappedPosition</c>.
        /// </summary>
        public float3 TargetPosition => this.snappedPosition;

        /// <summary>
        /// Snapped position used by the associated spatial operation.
        /// </summary>
        public float3 snappedPosition;
        /// <summary>
        /// Orientation in the coordinate space used by the containing API.
        /// </summary>
        public quaternion rotation;
        /// <summary>
        /// Size of the represented value in the units used by this API.
        /// </summary>
        public uint2 size;
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public tfloat height;
        /// <summary>
        /// Building type id used to locate the associated entry.
        /// </summary>
        public uint buildingTypeId;
        /// <summary>
        /// Time to build used by <c>CommandBuild</c>.
        /// </summary>
        public tfloat timeToBuild;
        /// <summary>
        /// Owner associated with this entry.
        /// </summary>
        public Ent owner;
        /// <summary>
        /// Building used by <c>CommandBuild</c>.
        /// </summary>
        public Ent building;

    }

}