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
    /// Defines command components group data used by entity processing.
    /// </summary>
    public struct CommandComponentsGroup {
        
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.yellow;

    }

    /// <summary>
    /// Stores per-entity state for i command.
    /// </summary>
    public interface ICommandComponent : IComponent {

        /// <summary>
        /// Target position used by the associated spatial operation.
        /// </summary>
        public float3 TargetPosition { get; }

    }

    /// <summary>
    /// Stores per-entity state for build in progress.
    /// </summary>
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct BuildInProgress : IComponent {

        /// <summary>
        /// Building used by <c>BuildInProgress</c>.
        /// </summary>
        public Ent building;

    }

    /// <summary>
    /// Stores per-entity state for building in progress.
    /// </summary>
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct BuildingInProgress : IComponent {

        /// <summary>
        /// Spin lock protecting concurrent access to this state.
        /// </summary>
        public LockSpinner lockSpinner;
        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public tfloat value;
        /// <summary>
        /// Time to build used by <c>BuildingInProgress</c>.
        /// </summary>
        public tfloat timeToBuild;
        /// <summary>
        /// Builders used by <c>BuildingInProgress</c>.
        /// </summary>
        public ListAuto<Ent> builders;
        
    }
    
    /// <summary>
    /// Stores per-entity state for received command from user event.
    /// </summary>
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct ReceivedCommandFromUserEvent : IComponent {}

}