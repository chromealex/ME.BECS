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
    public struct CommandBuild : ICommandComponent {

        public float3 TargetPosition => this.snappedPosition;

        public float3 snappedPosition;
        public quaternion rotation;
        public uint2 size;
        public tfloat height;
        public uint buildingTypeId;
        public tfloat timeToBuild;
        public Ent owner;
        public Ent building;

    }

}