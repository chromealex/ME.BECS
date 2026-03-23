namespace ME.BECS.Attack {
    
    using ME.BECS.Views;

    public class AttackMuzzleViewModule : IViewApplyState {

        public UnityEngine.GameObject muzzlePoint;
        public uint sensorIndex;

        public void ApplyState(in ViewData viewData) {

            EntRO ent = viewData;
            var unit = ent.GetAspect<ME.BECS.Units.UnitAspect>();
            var sensors = unit.readComponentRuntime.placements;
            if (this.sensorIndex >= sensors.Count) return;
            
            var sensor = sensors[this.sensorIndex];
            if (sensor.IsAlive() == false) return;
            
            var obj = sensor.Read<ME.BECS.Units.UnitPlacementComponent>().obj;
            if (obj.IsAlive() == false) return;
            var attack = obj.GetAspect<AttackAspect>();

            bool isFire = attack.FireProgress > 0;
            if (this.muzzlePoint.activeSelf != isFire) this.muzzlePoint.SetActive(isFire);
            
        }

    }

}