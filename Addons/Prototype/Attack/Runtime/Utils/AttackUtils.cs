namespace ME.BECS.Attack {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;
    using Unity.Mathematics;
    using ME.BECS.Units;
    using ME.BECS.Bullets;
    using ME.BECS.Attack;

    public static class AttackUtils {

        public enum PositionToAttack {

            None,
            RotateToTarget,
            MoveToPoint,

        }
        
        [INLINE(256)]
        public static Ent CreateAttackSensor(int targetsMask, Config config, in JobInfo jobInfo) {
            
            var attackSensor = Ent.New(in jobInfo, editorName: "AttackSensor");
            config.Apply(in attackSensor);
            attackSensor.Set<QuadTreeQueryAspect>();
            attackSensor.Set<AttackAspect>();
            attackSensor.Set<TransformAspect>();
            var trSensor = attackSensor.GetAspect<TransformAspect>();
            trSensor.localPosition = float3.zero;
            trSensor.localRotation = quaternion.identity;
            var attackAspect = attackSensor.GetAspect<AttackAspect>();
            var attackQueryAspect = attackSensor.GetAspect<QuadTreeQueryAspect>();
            attackQueryAspect.query.treeMask = targetsMask;
            attackQueryAspect.query.rangeSqr = attackAspect.readAttackRangeSqr;
            attackQueryAspect.query.minRangeSqr = attackAspect.readMinAttackRangeSqr;
            attackQueryAspect.query.sector = attackAspect.readAttackSector;
            attackQueryAspect.query.ignoreSelf = attackAspect.readIgnoreSelf;
            attackQueryAspect.query.nearestCount = 1;
            var point = config.AsUnsafeConfig().ReadStatic<BulletViewPoint>();
            BulletUtils.RegisterFirePoint(attackSensor, point.position, point.rotation, jobInfo);
            return attackSensor;

        }

        [INLINE(256)]
        public static PositionToAttack GetPositionToAttack(in UnitAspect unit, in Ent target, float nodeSize, out float3 position) {

            position = default;
            var unitTr = unit.ent.GetAspect<TransformAspect>();
            var targetTr = target.GetAspect<TransformAspect>();
            var attackSensor = unit.readComponentRuntime.attackSensor.Read<AttackComponent>();
            var offset = unit.readRadius + nodeSize;
            var sightRange = math.sqrt(unit.readSightRangeSqr) + offset * 0.5f;
            var dir = targetTr.GetWorldMatrixPosition() - unitTr.GetWorldMatrixPosition();
            var dirNormalized = math.normalizesafe(dir);
            var distSq = math.lengthsq(dir);
            var minRange = attackSensor.sector.minRangeSqr;
            if (distSq <= minRange) {
                // get out from target
                position = unitTr.GetWorldMatrixPosition() - dirNormalized * (minRange + offset);
                return PositionToAttack.MoveToPoint;
            }
            // if our unit is in range [attackRange, sightRange] - find target point
            if (distSq > 0f && distSq <= (sightRange * sightRange) && distSq > attackSensor.sector.rangeSqr) {
                // find point on the line
                var attackRangeSqr = attackSensor.sector.rangeSqr;
                position = targetTr.GetWorldMatrixPosition() - dirNormalized * (math.sqrt(attackRangeSqr) - offset);
                return PositionToAttack.MoveToPoint;
            } else if (distSq > 0f && distSq <= (sightRange * sightRange) && distSq <= attackSensor.sector.rangeSqr) {
                // we are in attack range already - try to look at attacker
                return PositionToAttack.RotateToTarget;
            }
            
            position = unitTr.GetWorldMatrixPosition() - dirNormalized * offset * 2f;

            return PositionToAttack.MoveToPoint;

        }

    }

}