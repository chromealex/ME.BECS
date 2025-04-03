namespace ME.BECS.Attack {

    using Views;
    
    public class AnimatorAttackViewModule : IViewApplyState {

        public UnityEngine.Animator animator;
        
        private static readonly int attackHash = UnityEngine.Animator.StringToHash("Attack");
        private static readonly int reloadHash = UnityEngine.Animator.StringToHash("Reload");
        private static readonly int hasTargetHash = UnityEngine.Animator.StringToHash("HasTarget");

        public void ApplyState(in EntRO ent) {

            var sensor = ent.GetAspect<ME.BECS.Units.UnitAspect>().readComponentRuntime.attackSensor;
            if (sensor.IsAlive() == false) return;
            var attack = sensor.GetAspect<AttackAspect>();
            this.animator.SetFloat(attackHash, (float)attack.FireProgress);
            this.animator.SetFloat(reloadHash, (float)attack.ReloadProgress);
            this.animator.SetBool(hasTargetHash, attack.HasAnyTarget);
            
        }

    }

}