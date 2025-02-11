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

    public struct CommandComponentsGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.yellow;

    }

    public interface ICommandComponent : IComponent {

        public float3 TargetPosition { get; }

    }

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct BuildInProgress : IComponent {

        public Ent building;

    }

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct BuildingInProgress : IComponent {

        public LockSpinner lockSpinner;
        public tfloat value;
        public tfloat timeToBuild;
        public ListAuto<Ent> builders;
        
    }

}