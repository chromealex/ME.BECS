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

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct CommandAttack : ICommandComponent {

        public float3 TargetPosition => this.target.GetAspect<TransformAspect>().GetWorldMatrixPosition();

        public Ent target;

    }

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct UnitAttackCommandComponent : IComponent {

        public Ent target;

    }

}