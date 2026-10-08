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
    
    using ME.BECS.Transforms;

    /// <summary>
    /// Defines command attack state and operations.
    /// </summary>
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct CommandAttack : ICommandComponent {

        /// <summary>
        /// Target position used by the associated spatial operation.
        /// </summary>
        public float3 TargetPosition => this.target.GetAspect<TransformAspect>().GetWorldMatrixPosition();

        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public Ent target;

    }

    /// <summary>
    /// Stores per-entity state for unit attack command.
    /// </summary>
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct UnitAttackCommandComponent : IComponent {

        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public Ent target;

    }

    /// <summary>
    /// Stores per-entity state for unit attack on move command.
    /// </summary>
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct UnitAttackOnMoveCommandComponent : IComponent {

        /// <summary>
        /// Destination or target of the associated operation.
        /// </summary>
        public Ent target;

    }

    /// <summary>
    /// Defines command move attack state and operations.
    /// </summary>
    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct CommandMoveAttack : ICommandComponent {

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