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

namespace ME.BECS.Units {

    using Views;
    
    /// <summary>
    /// Updates view presentation for animator module.
    /// </summary>
    public class AnimatorModule : IViewApplyState {

        /// <summary>
        /// Animator used to present entity state.
        /// </summary>
        public UnityEngine.Animator animator;
        
        private static readonly int speedHash = UnityEngine.Animator.StringToHash("Speed");
        private tfloat prevSpeed;

        /// <summary>
        /// Applies the current logic state to the presentation instance.
        /// </summary>
        public void ApplyState(in ViewData viewData) {

            EntRO ent = viewData;
            var componentProperties = ent.Read<NavAgentComponent>();
            var component = ent.Read<NavAgentRuntimeSpeedComponent>();
            var speedFactor = component.speed / componentProperties.maxSpeed;
            if (math.abs(this.prevSpeed - speedFactor) > math.EPSILON) {
                this.prevSpeed = speedFactor;
                this.animator.SetFloat(speedHash, (float)speedFactor);
            }

        }

    }

}