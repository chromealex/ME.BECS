namespace ME.BECS.Attack {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;
    using Unity.Mathematics;

    public static class AttackUtils {

        [INLINE(256)]
        public static Ent CreateAttackSensor(int targetsMask, Config config) {
            
            var attackSensor = Ent.New();
            config.Apply(attackSensor);
            attackSensor.Set<QuadTreeQueryAspect>();
            attackSensor.Set<ME.BECS.Attack.AttackAspect>();
            attackSensor.Set<ME.BECS.Transforms.TransformAspect>();
            var trSensor = attackSensor.GetAspect<ME.BECS.Transforms.TransformAspect>();
            trSensor.localPosition = float3.zero;
            trSensor.localRotation = quaternion.identity;
            var attackAspect = attackSensor.GetAspect<ME.BECS.Attack.AttackAspect>();
            var attackQueryAspect = attackSensor.GetAspect<QuadTreeQueryAspect>();
            attackQueryAspect.query.treeMask = targetsMask;
            attackQueryAspect.query.range = math.sqrt(math.max(attackAspect.attackRangeSqr, attackAspect.sightRangeSqr));
            attackQueryAspect.query.nearestCount = 1;
            var point = config.AsUnsafeConfig().ReadStatic<ME.BECS.Bullets.BulletViewPoint>();
            ME.BECS.Bullets.BulletUtils.RegisterFirePoint(attackSensor, point.position, point.rotation);
            return attackSensor;

        }

    }

}