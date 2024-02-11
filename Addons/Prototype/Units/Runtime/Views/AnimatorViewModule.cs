namespace ME.BECS.Units {

    using Views;
    
    public class AnimatorModule : IViewApplyState {

        public UnityEngine.Animator animator;
        private static readonly int speedHash = UnityEngine.Animator.StringToHash("Speed");
        private float prevSpeed;

        public void ApplyState(in EntRO ent) {

            var componentProperties = ent.Read<NavAgentComponent>();
            var component = ent.Read<NavAgentRuntimeComponent>();
            var speedFactor = component.speed / componentProperties.maxSpeed;
            if (this.prevSpeed != speedFactor) {
                this.prevSpeed = speedFactor;
                this.animator.SetFloat(speedHash, speedFactor);
            }

        }

    }

}