namespace ME.BECS.Attack {

    public class AttackMuzzleViewModule : ME.BECS.Views.IViewApplyState {

        public UnityEngine.GameObject muzzlePoint;

        public void ApplyState(in EntRO ent) {
            
            var unit = ent.GetAspect<ME.BECS.Units.UnitAspect>();
            var sensor = unit.readComponentRuntime.attackSensor;
            if (sensor.IsAlive() == false) return;
            var attack = sensor.GetAspect<AttackAspect>();

            bool isFire = attack.FireProgress > 0;
            if (this.muzzlePoint.activeSelf != isFire) this.muzzlePoint.SetActive(isFire);
            
        }

    }

}