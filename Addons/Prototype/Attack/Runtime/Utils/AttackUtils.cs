namespace ME.BECS.Attack {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;
    using Unity.Mathematics;
    using ME.BECS.Units;
    using ME.BECS.Bullets;

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
        public static Ent CreateAttackSensor<T>(int targetsMask, Config config, in JobInfo jobInfo, in T subFilter) where T : unmanaged, IComponent {

            var sensor = CreateAttackSensor(targetsMask, config, in jobInfo);
            sensor.Set(new QuadTreeQueryHasCustomFilterTag());
            sensor.Set(subFilter);
            return sensor;

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

            var unitAttackMask = unit.readComponentRuntime.attackSensor.Read<AttackFilterComponent>().layers;
            var targetAttackLayer = target.Read<UnitBelongsToComponent>().layer;

            var targetAttackSensor = target.GetAspect<UnitAspect>().readComponentRuntime.attackSensor;
            if (targetAttackSensor.IsAlive()) {
                var targetAttack = targetAttackSensor.Read<AttackComponent>();
                // if unit can't attack target and he is in target's attack range
                if (unitAttackMask.Contains(targetAttackLayer) == false && math.lengthsq(dir) < targetAttack.sector.rangeSqr) {
                    var targetAttackRange = math.sqrt(targetAttack.sector.rangeSqr);
                    position = unitTr.GetWorldMatrixPosition() - dirNormalized * (targetAttackRange + offset);
                    return PositionToAttack.MoveToPoint;
                }
            }
            
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

            position = targetTr.GetWorldMatrixPosition() - dirNormalized * offset * 2f;

            return PositionToAttack.MoveToPoint;

        }

        [INLINE(256)]
        public static bool CanAttack(in UnitAspect unit, in Ent target) {
            
            var unitAttackMask = unit.readComponentRuntime.attackSensor.Read<AttackFilterComponent>().layers;
            var targetAttackLayer = target.Read<UnitBelongsToComponent>().layer;
            return unitAttackMask.Contains(targetAttackLayer);
        }

    }

}