#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {

    /// <summary>
    /// Groups pathfinding components for change tracking and queries.
    /// </summary>
    public struct PathfindingComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.green;

    }

    /// <summary>
    /// Stores per-entity state for agent.
    /// </summary>
    [ComponentGroup(typeof(PathfindingComponentGroup))]
    public struct AgentComponent : IComponent {

        /// <summary>
        /// Filter restricting the entries considered by this operation.
        /// </summary>
        public Filter filter;

    }
    
    /// <summary>
    /// Stores per-entity state for target.
    /// </summary>
    [ComponentGroup(typeof(PathfindingComponentGroup))]
    public struct TargetComponent : IComponent {

        /// <summary>
        /// Creates <c>TargetComponent</c> using the supplied creation arguments.
        /// </summary>
        public static TargetComponent Create(in Ent targetInfo, in Ent graphEnt) => new TargetComponent() {
            target = targetInfo,
            graphEnt = graphEnt,
        };

        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public Ent target;
        /// <summary>
        /// Graph ent used by <c>TargetComponent</c>.
        /// </summary>
        public Ent graphEnt;

    }

    /// <summary>
    /// Stores per-entity state for target info.
    /// </summary>
    [ComponentGroup(typeof(PathfindingComponentGroup))]
    public struct TargetInfoComponent : IComponent {
        
        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public Path.Target target;
        /// <summary>
        /// Volume used by the associated bounds calculation.
        /// </summary>
        public uint volume;

    }

    /// <summary>
    /// Stores per-entity state for target path.
    /// </summary>
    [ComponentGroup(typeof(PathfindingComponentGroup))]
    public struct TargetPathComponent : IComponent {

        /// <summary>
        /// Path selected or processed by this operation.
        /// </summary>
        public Path path;
        /// <summary>
        /// Chunks to update used by <c>TargetPathComponent</c>.
        /// </summary>
        public MemArrayAuto<byte> chunksToUpdate;

    }

}