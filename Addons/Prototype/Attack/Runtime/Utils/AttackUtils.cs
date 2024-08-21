namespace ME.BECS.Attack {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;
    using Unity.Mathematics;
    using ME.BECS.Units;
    using ME.BECS.Bullets;
    using ME.BECS.Attack;

    public static class AttackUtils {

        [INLINE(256)]
        public static Ent CreateAttackSensor(in UnitAspect unit, int targetsMask, Config config, JobInfo jobInfo) {
            
            var attackSensor = Ent.New(jobInfo);
            config.Apply(attackSensor);
            attackSensor.Set<QuadTreeQueryAspect>();
            attackSensor.Set<AttackAspect>();
            attackSensor.Set<TransformAspect>();
            var trSensor = attackSensor.GetAspect<TransformAspect>();
            trSensor.localPosition = float3.zero;
            trSensor.localRotation = quaternion.identity;
            var attackAspect = attackSensor.GetAspect<AttackAspect>();
            var attackQueryAspect = attackSensor.GetAspect<QuadTreeQueryAspect>();
            attackQueryAspect.query.treeMask = targetsMask;
            attackQueryAspect.query.range = math.sqrt(math.max(attackAspect.attackRangeSqr, unit.sightRangeSqr));
            attackQueryAspect.query.nearestCount = 1;
            var point = config.AsUnsafeConfig().ReadStatic<BulletViewPoint>();
            BulletUtils.RegisterFirePoint(attackSensor, point.position, point.rotation, jobInfo);
            return attackSensor;

        }

        [INLINE(256)]
        public static bool GetPositionToAttack(in UnitAspect unit, in Ent target, out float3 position) {

            position = default;
            var unitTr = unit.ent.GetAspect<TransformAspect>();
            var targetTr = target.GetAspect<TransformAspect>();
            var attackSensor = unit.componentRuntime.attackSensor.Read<AttackComponent>();
            var sightRangeSqr = unit.sightRangeSqr;
            var dir = targetTr.GetWorldMatrixPosition() - unitTr.GetWorldMatrixPosition();
            var distSq = math.lengthsq(dir);
            // if our unit is in range [attackRange, sightRange] - find target point
            if (distSq > 0f && distSq <= sightRangeSqr && distSq > attackSensor.attackRangeSqr) {
                var offset = unit.radius;
                // find point on line
                var dirNormalized = math.normalize(dir);
                var attackRangeSqr = attackSensor.attackRangeSqr;
                position = targetTr.GetWorldMatrixPosition() - dirNormalized * (math.sqrt(attackRangeSqr) - offset);
                return true;
            }

            return false;

        }

    }

}