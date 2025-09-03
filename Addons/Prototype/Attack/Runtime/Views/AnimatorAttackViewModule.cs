namespace ME.BECS.Attack {

    using Views;
    using ME.BECS.Units;
    
    public class AnimatorAttackViewModule : IViewInitialize, IViewApplyState, IViewIgnoreTracker {

        public UnityEngine.Animator animator;
        public string attackState;
        public int attackStateLayer;
        public int attackStateHash;
        
        private static readonly int attackHash = UnityEngine.Animator.StringToHash("Attack");
        private static readonly int reloadHash = UnityEngine.Animator.StringToHash("Reload");
        private static readonly int hasTargetHash = UnityEngine.Animator.StringToHash("HasTarget");
        private static readonly int canAttackOnMoveHash = UnityEngine.Animator.StringToHash("CanAttackOnMove");

        public void OnInitialize(in EntRO ent) {
            this.attackStateHash = UnityEngine.Animator.StringToHash(this.attackState);
        }

        public void ApplyState(in EntRO ent) {

            var unit = ent.GetAspect<UnitAspect>();
            var sensor = unit.readComponentRuntime.attackSensor;
            if (sensor.IsAlive() == false) return;
            var attack = sensor.GetAspect<AttackAspect>();
            this.animator.SetFloat(attackHash, (float)attack.FireProgress);
            this.animator.SetFloat(reloadHash, (float)attack.ReloadProgress);
            this.animator.SetBool(hasTargetHash, attack.HasAnyTarget);
            this.animator.SetBool(canAttackOnMoveHash, attack.CanFireWhileMoves);

            if (this.attackStateHash != 0 && attack.FireProgress > 0f) {
                this.animator.Play(this.attackStateHash, this.attackStateLayer, (float)attack.FireProgress);
            }
            
        }

    }

}