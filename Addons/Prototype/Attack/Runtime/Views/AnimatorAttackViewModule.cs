namespace ME.BECS.Attack {

    using Views;
    
    public class AnimatorAttackViewModule : IViewApplyState {

        public UnityEngine.Animator animator;
        
        private static readonly int attackHash = UnityEngine.Animator.StringToHash("Attack");

        public void ApplyState(in EntRO ent) {

            var sensor = ent.GetAspect<ME.BECS.Units.UnitAspect>().readComponentRuntime.attackSensor;
            if (sensor.IsAlive() == false) return;
            var attack = sensor.Read<AttackRuntimeFireComponent>();
            this.animator.SetFloat(attackHash, (float)attack.fireTimer);
            
        }

    }

}