namespace ME.BECS.Attack {

    public class AttackMuzzleViewModule : ME.BECS.Views.IViewApplyState {

        public UnityEngine.GameObject muzzlePoint;
        public uint sensorIndex;

        public void ApplyState(in EntRO ent) {
            
            var unit = ent.GetAspect<ME.BECS.Units.UnitAspect>();
            var sensors = unit.readComponentRuntime.attackSensors;
            if (this.sensorIndex >= sensors.Count) return;
            
            var sensor = sensors[this.sensorIndex];
            if (sensor.IsAlive() == false) return;
            var attack = sensor.GetAspect<AttackAspect>();

            bool isFire = attack.FireProgress > 0;
            if (this.muzzlePoint.activeSelf != isFire) this.muzzlePoint.SetActive(isFire);
            
        }

    }

}