#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS.Attack {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;
    using ME.BECS.Units;
    using ME.BECS.Bullets;
    using ME.BECS.Views;

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
            var unsafeConfig = config.AsUnsafeConfig();
            if (unsafeConfig.HasStatic<BulletViewPoint>() == true) {
                var point = unsafeConfig.ReadStatic<BulletViewPoint>();
                BulletUtils.RegisterFirePoint(in attackSensor, in point.position, in point.rotation, in jobInfo);    
            } else if (unsafeConfig.HasStatic<BulletViewPoints>() == true) {
                var points = unsafeConfig.ReadStatic<BulletViewPoints>().points;
                for (uint i = 0u; i < points.Length; ++i) {
                    var point = points[i];
                    BulletUtils.RegisterFirePoint(in attackSensor, in point.position, in point.rotation, in jobInfo);
                }
            }
            
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
        public static PositionToAttack GetPositionToAttack(in UnitAspect unit, in Ent target, tfloat nodeSize, out float3 position) {

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
            if (targetAttackSensor.IsAlive() == true) {
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
                var targetRadius = target.GetAspect<UnitAspect>().readRadius;
                var attackRangeSqr = attackSensor.sector.rangeSqr;
                position = targetTr.GetWorldMatrixPosition() - dirNormalized * (math.sqrt(attackRangeSqr) - offset + targetRadius);
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

        /// <summary>
        /// Create bullet entity with 200 ms muzzleLifetime
        /// </summary>
        /// <param name="attackAspect">Attack aspect</param>
        /// <param name="position">Initial bullet position</param>
        /// <param name="rotation">Initial bullet rotation</param>
        /// <param name="targetsMask">Targets tree mask</param>
        /// <param name="target">If set - use this to target bullet at runtime</param>
        /// <param name="targetPosition">If set</param>
        /// <param name="config">Bullet config</param>
        /// <param name="muzzleView">Muzzle view</param>
        /// <param name="jobInfo"></param>
        /// <returns></returns>
        [INLINE(256)]
        public static BulletAspect CreateBullet(in AttackAspect attackAspect, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition,
                                                in Config config, in ME.BECS.Views.View muzzleView, in JobInfo jobInfo = default) {
            return CreateBullet(in attackAspect, in position, in rotation, targetsMask, in target, in targetPosition, in config, in muzzleView, 200u, in jobInfo);
        }

        /// <summary>
        /// Create bullet entity
        /// </summary>
        /// <param name="attackAspect">Attack aspect</param>
        /// <param name="position">Initial bullet position</param>
        /// <param name="rotation">Initial bullet rotation</param>
        /// <param name="targetsMask">Targets tree mask</param>
        /// <param name="target">If set - use this to target bullet at runtime</param>
        /// <param name="targetPosition">If set</param>
        /// <param name="config">Bullet config</param>
        /// <param name="muzzleView">Muzzle view</param>
        /// <param name="muzzleLifetimeMs">Muzzle lifetime in ms</param>
        /// <param name="jobInfo"></param>
        /// <returns></returns>
        [INLINE(256)]
        public static BulletAspect CreateBullet(in AttackAspect attackAspect, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition, in Config config, in ME.BECS.Views.View muzzleView, uint muzzleLifetimeMs, in JobInfo jobInfo = default) {

            if (muzzleView.IsValid == true) {
                var muzzleEnt = Ent.New(in jobInfo, "MuzzlePoint");
                var tr = muzzleEnt.GetOrCreateAspect<TransformAspect>();
                tr.position = position;
                tr.rotation = rotation;
                muzzleEnt.InstantiateView(muzzleView);
                muzzleEnt.Destroy(muzzleLifetimeMs);
            }

            {
                var sourceUnit = attackAspect.ent.GetParent();
                var ent = Ent.New(in jobInfo, "Bullet");
                ME.BECS.Players.PlayerUtils.SetOwner(in ent, ME.BECS.Players.PlayerUtils.GetOwner(in sourceUnit));
                config.Apply(ent);
                var attack = ent.GetOrCreateAspect<QuadTreeQueryAspect>();
                attack.query.treeMask = targetsMask; // Search for targets in this tree
                attack.query.rangeSqr = math.max(1f, ent.Read<BulletConfigComponent>().hitRangeSqr);
                var tr = ent.GetOrCreateAspect<TransformAspect>();
                tr.position = position;
                tr.rotation = rotation;
                var bullet = ent.GetOrCreateAspect<BulletAspect>();
                bullet.component.targetEnt = target;
                bullet.component.targetWorldPos = target.IsAlive() == true ? ME.BECS.Units.UnitUtils.GetTargetBulletPosition(in sourceUnit, in target) : targetPosition;
                bullet.component.sourceUnit = sourceUnit;

                if (attackAspect.ent.Has<DamageOverrideComponent>()) {
                    bullet.damage = attackAspect.ent.Read<DamageOverrideComponent>().damage;
                }
                return bullet;
            }

        }
    }

}