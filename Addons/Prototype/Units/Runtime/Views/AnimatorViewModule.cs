namespace ME.BECS.Units {

    using Views;
    using Unity.Mathematics;
    
    public class AnimatorModule : IViewApplyState {

        public UnityEngine.Animator animator;
        
        private static readonly int speedHash = UnityEngine.Animator.StringToHash("Speed");
        private float prevSpeed;

        public void ApplyState(in EntRO ent) {

            var componentProperties = ent.Read<NavAgentComponent>();
            var component = ent.Read<NavAgentRuntimeComponent>();
            var speedFactor = component.speed / componentProperties.maxSpeed;
            if (math.abs(this.prevSpeed - speedFactor) > math.EPSILON) {
                this.prevSpeed = speedFactor;
                this.animator.SetFloat(speedHash, speedFactor);
            }

        }

    }

}