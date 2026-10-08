namespace ME.BECS.Attack {

    using Views;
    using ME.BECS.Units;
    
    /// <summary>
    /// Updates view presentation for animator attack view module.
    /// </summary>
    public class AnimatorAttackViewModule : IViewApplyState, IViewIgnoreTracker {

        /// <summary>
        /// Animator used to present entity state.
        /// </summary>
        public UnityEngine.Animator animator;
        /// <summary>
        /// Sensor index used to locate the associated entry.
        /// </summary>
        public uint sensorIndex;
        
        private static readonly int attackHash = UnityEngine.Animator.StringToHash("Attack");
        private static readonly int reloadHash = UnityEngine.Animator.StringToHash("Reload");
        private static readonly int hasTargetHash = UnityEngine.Animator.StringToHash("HasTarget");
        private static readonly int canAttackOnMoveHash = UnityEngine.Animator.StringToHash("CanAttackOnMove");

        /// <summary>
        /// Applies the current logic state to the presentation instance.
        /// </summary>
        public void ApplyState(in ViewData viewData) {

            EntRO ent = viewData;
            var unit = ent.GetAspect<UnitAspect>();
            var sensors = unit.readComponentRuntime.placements;
            if (this.sensorIndex >= sensors.Count) return;
            var sensor = sensors[this.sensorIndex];
            if (sensor.IsAlive() == false) return;
            var obj = sensor.Read<UnitPlacementComponent>().obj;
            if (obj.IsAlive() == false) return;
            var attack = obj.GetAspect<AttackAspect>();
            this.animator.SetFloat(attackHash, (float)attack.FireProgress);
            this.animator.SetFloat(reloadHash, (float)attack.ReloadProgress);
            this.animator.SetBool(hasTargetHash, attack.HasAnyTarget);
            this.animator.SetBool(canAttackOnMoveHash, attack.CanFireWhileMoves);
            
        }

    }

}