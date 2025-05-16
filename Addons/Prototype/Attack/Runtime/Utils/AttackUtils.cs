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
        public static float3 GetNearestPoint(in TransformAspect targetTr, in float3 fromPos) {

            var center = targetTr.GetWorldMatrixPosition();
            if (targetTr.ent.TryRead(out UnitQuadSizeComponent quadSizeComponent) == true) {
                var dir = math.normalizesafe(fromPos - center);
                dir *= new float3(quadSizeComponent.size.x, 0f, quadSizeComponent.size.y);
                var rot = targetTr.GetWorldMatrixRotation();
                dir = math.mul(rot, dir);
                if (Math.IntersectsRect(new float2(0f, 0f), new float2(dir.x, dir.z), new Rect(-(new float2(quadSizeComponent.size)) * 0.5f, new float2(quadSizeComponent.size)), out var point) == true) {
                    center += math.mul(math.inverse(rot), new float3(point.x, 0f, point.y));
                }
            } else {
                var targetRadius = targetTr.ent.GetAspect<UnitAspect>().readRadius;
                var dir = math.normalizesafe(fromPos - center);
                center += dir * (targetRadius);
            }
            
            UnityEngine.Debug.DrawLine((UnityEngine.Vector3)center, (UnityEngine.Vector3)(center + new float3(0f, 10f, 0f)), UnityEngine.Color.cyan, 3f);
            
            return center;
            
        }

        [INLINE(256)]
        public static PositionToAttack GetPositionToAttack(in UnitCommandGroupAspect group, in Ent target, tfloat nodeSize, out float3 position) {

            position = default;
            if (group.readUnits.Count == 0u) return PositionToAttack.None;

            var targetPos = target.GetAspect<TransformAspect>().position;
            var d = tfloat.MaxValue;
            var result = PositionToAttack.RotateToTarget;
            foreach (var unit in group.readUnits) {
                var res = GetPositionToAttack(unit.GetAspect<UnitAspect>(), in target, nodeSize, out var pos, default);
                var dist = math.distancesq(targetPos, pos);
                if (dist < d) {
                    position = pos;
                    d = dist;
                }
                if (res == PositionToAttack.MoveToPoint) {
                    result = PositionToAttack.MoveToPoint;
                }
            }

            return result;
            
        }

        [INLINE(256)]
        public static PositionToAttack GetPositionToAttack(in UnitAspect unit, in Ent target, tfloat nodeSize, out float3 position) {
            return GetPositionToAttack(in unit, in target, nodeSize, out position, default);
        }

        [INLINE(256)]
        public static PositionToAttack GetPositionToAttack(in UnitAspect unit, in Ent target, tfloat nodeSize, out float3 position, in SystemLink<ME.BECS.FogOfWar.CreateSystem> fogOfWarSystem) {

            position = default;
            var owner = unit.readOwner.GetAspect<ME.BECS.Players.PlayerAspect>();
            var unitTr = unit.ent.GetAspect<TransformAspect>();
            var targetTr = target.GetAspect<TransformAspect>();
            var fromPos = unitTr.GetWorldMatrixPosition();
            var targetNearestPoint = GetNearestPoint(in targetTr, in fromPos);
            var attackSensor = unit.readComponentRuntime.attackSensor.Read<AttackComponent>();
            var offset = nodeSize;
            var sightRange = math.sqrt(unit.readSightRangeSqr) + offset * 0.5f;
            var sightRangeSqr = sightRange * sightRange;
            var dir = targetNearestPoint - fromPos;
            var dirNormalized = math.normalizesafe(dir);
            var distSq = math.lengthsq(dir);
            var minRangeSq = attackSensor.sector.minRangeSqr;

            var targetAttackSensor = target.GetAspect<UnitAspect>().readComponentRuntime.attackSensor;
            if (targetAttackSensor.IsAlive() == true) {
                var targetAttack = targetAttackSensor.Read<AttackComponent>();
                // if unit can't attack target and he is in target's attack range
                if (CanAttack(in unit, in target) == false && math.lengthsq(dir) < targetAttack.sector.rangeSqr) {
                    var targetAttackRange = math.sqrt(targetAttack.sector.rangeSqr);
                    position = unitTr.GetWorldMatrixPosition() - dirNormalized * (targetAttackRange + offset);
                    return PositionToAttack.MoveToPoint;
                }
            }
            
            if (distSq <= minRangeSq) {
                //if target is too close - get out from target
                position = unitTr.GetWorldMatrixPosition() - dirNormalized * (math.sqrt(minRangeSq) + offset);
                return PositionToAttack.MoveToPoint;
            }
            // if our unit is in range [attackRange, sightRange] - find target point
            if (distSq > 0f && distSq <= sightRangeSqr && distSq > attackSensor.sector.rangeSqr) {
                // find point on the line
                var attackRangeSqr = attackSensor.sector.rangeSqr;
                position = targetNearestPoint - dirNormalized * (math.sqrt(attackRangeSqr) - offset);
                return PositionToAttack.MoveToPoint;
            } else if (distSq > 0f && ((fogOfWarSystem.IsCreated == false && distSq <= sightRangeSqr) || (fogOfWarSystem.IsCreated == true && fogOfWarSystem.Value.IsVisible(in owner, targetNearestPoint) == true)) && distSq <= attackSensor.sector.rangeSqr) {
                // we are in attack range already - try to look at attacker
                return PositionToAttack.RotateToTarget;
            }

            position = targetNearestPoint - dirNormalized * offset * 2f;

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
                if (bullet.readConfig.autoTarget == 1) bullet.component.targetEnt = target;
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