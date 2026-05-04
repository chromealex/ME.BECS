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
    using Unity.Collections.LowLevel.Unsafe;

    public static class AttackUtils {

        public enum ReactionType {

            None,
            RotateToTarget,
            MoveToTarget,
            RunAway,

        }

        [INLINE(256)]
        public static Ent CreateAttackSensor(int targetsMask, Config config, in JobInfo jobInfo) {

            var attackSensor = Ent.New<AttackSensorEntityType>(in jobInfo, editorName: "AttackSensor");
            config.Apply(in attackSensor);
            attackSensor.Set<QuadTreeQueryAspect>();
            attackSensor.Set<AttackAspect>();
            var trSensor = attackSensor.Set<TransformAspect>();
            trSensor.localPosition = float3.zero;
            trSensor.localRotation = quaternion.identity;
            trSensor.IsStaticLocal = true;
            var attackAspect = attackSensor.GetAspect<AttackAspect>();
            var attackQueryAspect = attackSensor.GetAspect<QuadTreeQueryAspect>();
            attackQueryAspect.query.treeMask = targetsMask;
            attackQueryAspect.query.rangeSqr = attackAspect.readAttackRangeSqr;
            attackQueryAspect.query.minRangeSqr = attackAspect.readMinAttackRangeSqr;
            attackQueryAspect.query.sector = attackAspect.readAttackSector;
            attackQueryAspect.query.ignoreSelf = attackAspect.readIgnoreSelf;
            attackQueryAspect.query.ignoreSorting = true;
            attackQueryAspect.query.nearestCount = 1;
            attackQueryAspect.query.updatePerTick = 2;
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
        public static Ent CreateAttackSensorSpatial(int targetsMask, Config config, in JobInfo jobInfo) {

            var attackSensor = Ent.New<AttackSensorEntityType>(in jobInfo, editorName: "AttackSensor");
            config.Apply(in attackSensor);
            attackSensor.Set<SpatialQueryAspect>();
            attackSensor.Set<AttackAspect>();
            var trSensor = attackSensor.Set<TransformAspect>();
            trSensor.localPosition = float3.zero;
            trSensor.localRotation = quaternion.identity;
            trSensor.IsStaticLocal = true;
            var attackAspect = attackSensor.GetAspect<AttackAspect>();
            var attackQueryAspect = attackSensor.GetAspect<SpatialQueryAspect>();
            attackQueryAspect.query.treeMask = targetsMask;
            attackQueryAspect.query.rangeSqr = attackAspect.readAttackRangeSqr;
            attackQueryAspect.query.minRangeSqr = attackAspect.readMinAttackRangeSqr;
            attackQueryAspect.query.sector = attackAspect.readAttackSector;
            attackQueryAspect.query.ignoreSelf = attackAspect.readIgnoreSelf;
            attackQueryAspect.query.ignoreSorting = true;
            attackQueryAspect.query.nearestCount = 1;
            attackQueryAspect.query.updatePerTick = 2;
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
        public static bool CanAttack(in UnitAspect unit, in Ent target) {

            var attackSensors = unit.readComponentRuntime.placements;
            for (uint i = 0u; i < attackSensors.Count; ++i) {
                var obj = attackSensors[i].Read<UnitPlacementComponent>().obj;
                if (obj.IsAlive() == false) continue;
                var unitAttackMask = obj.Read<AttackFilterComponent>().layers;
                var targetAttackLayer = target.Read<UnitBelongsToComponent>().layer;
                if (unitAttackMask.Contains(targetAttackLayer) == true) return true;
            }

            return false;

        }

        [INLINE(256)]
        public static bool CanAttack(in AttackAspect attackSensor, in Ent target) {
            
            var unitAttackMask = attackSensor.ent.Read<AttackFilterComponent>().layers;
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
        public static BulletAspect CreateBullet3D(in AttackAspect attackAspect, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition,
                                                in Config config, in ME.BECS.Views.View muzzleView, in JobInfo jobInfo = default) {
            return CreateBullet3D(in attackAspect, in position, in rotation, targetsMask, in target, in targetPosition, in config, in muzzleView, 200u, in jobInfo);
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
        public static BulletAspect CreateBullet3D(in AttackAspect attackAspect, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition, in Config config, in ME.BECS.Views.View muzzleView, uint muzzleLifetimeMs, in JobInfo jobInfo = default) {

            var bullet = CreateBullet_INTERNAL(in attackAspect, in position, in rotation, targetsMask, in target, in targetPosition, in config, in muzzleView, 200u, in jobInfo);
            var attack = bullet.ent.GetOrCreateAspect<OctreeQueryAspect>();
            attack.query.ignoreSorting = true;
            attack.query.treeMask = targetsMask; // Search for targets in this tree
            attack.query.rangeSqr = math.max(0.01f, bullet.ent.Read<BulletConfigComponent>().hitRangeSqr);
            if (bullet.readConfig.autoTarget == true) {
                // Set nearest count to 1
                attack.query.nearestCount = 1;
            } else {
                attack.query.nearestCount = 0;
            }
            return bullet;

        }

        [INLINE(256)]
        private static BulletAspect CreateBullet_INTERNAL(in AttackAspect attackAspect, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition, in Config config, in ME.BECS.Views.View muzzleView, uint muzzleLifetimeMs, in JobInfo jobInfo = default) {
            
            if (muzzleView.IsValid == true) {
                var muzzleEnt = Ent.New<AttackMuzzlePointEntityType>(in jobInfo, "MuzzlePoint");
                var tr = muzzleEnt.GetOrCreateAspect<TransformAspect>();
                tr.IsStaticLocal = true;
                tr.position = position;
                tr.rotation = rotation;
                muzzleEnt.InstantiateView(muzzleView);
                muzzleEnt.Destroy(muzzleLifetimeMs);
            }

            {
                var placement = attackAspect.ent.ReadParent();
                var placements = placement.ReadParent();
                var sourceUnit = placements.ReadParent();
                var ent = Ent.New<BulletEntityType>(in jobInfo, "Bullet");
                ME.BECS.Players.PlayerUtils.SetOwner(in ent, ME.BECS.Players.PlayerUtils.GetOwner(in sourceUnit));
                config.Apply(ent);
                var tr = ent.GetOrCreateAspect<TransformAspect>();
                tr.position = position;
                tr.rotation = rotation;
                var bullet = ent.GetOrCreateAspect<BulletAspect>();
                if (bullet.readConfig.autoTarget == true) bullet.component.targetEnt = target;
                bullet.component.targetWorldPos = target.IsAlive() == true ? ME.BECS.Units.UnitUtils.GetTargetBulletPosition(in sourceUnit, in target) : targetPosition;
                bullet.component.sourceWorldPos = position;
                bullet.component.sourceUnit = sourceUnit;

                if (attackAspect.ent.Has<DamageMinOverrideComponent>() == true) {
                    bullet.damageMin = attackAspect.ent.Read<DamageMinOverrideComponent>().damage;
                }
                if (attackAspect.ent.Has<DamageOverrideComponent>() == true) {
                    bullet.damage = attackAspect.ent.Read<DamageOverrideComponent>().damage;
                } else if (attackAspect.ent.Has<DamageMultiplierComponent>() == true) {
                    ref var dmg = ref ent.Get<BulletConfigComponent>();
                    var factor = attackAspect.ent.Read<DamageMultiplierComponent>().factor;
                    dmg.damage = (uint)math.floor(dmg.damage * factor);
                    dmg.damageMin = (uint)math.floor(dmg.damageMin * factor);
                }
                return bullet;
            }

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

            var bullet = CreateBullet_INTERNAL(in attackAspect, in position, in rotation, targetsMask, in target, in targetPosition, in config, in muzzleView, 200u, in jobInfo);
            var attack = bullet.ent.GetOrCreateAspect<QuadTreeQueryAspect>();
            attack.query.ignoreSorting = true;
            attack.query.treeMask = targetsMask; // Search for targets in this tree
            attack.query.rangeSqr = math.max(1f, bullet.ent.Read<BulletConfigComponent>().hitRangeSqr);
            if (bullet.readConfig.autoTarget == true) {
                // Set nearest count to 1
                attack.query.nearestCount = 1;
            } else {
                attack.query.nearestCount = 0;
            }
            return bullet;

        }
        
        [INLINE(256)]
        public static BulletAspect CreateBulletSpatial(in AttackAspect attackAspect, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition,
                                                       in Config config, in ME.BECS.Views.View muzzleView, in JobInfo jobInfo = default) {
            return CreateBulletSpatial(in attackAspect, in position, in rotation, targetsMask, in target, in targetPosition, in config, in muzzleView, 200u, in jobInfo);
        }
        
        [INLINE(256)]
        public static BulletAspect CreateBulletSpatial(in AttackAspect attackAspect, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition, in Config config, in ME.BECS.Views.View muzzleView, uint muzzleLifetimeMs, in JobInfo jobInfo = default) {

            var bullet = CreateBullet_INTERNAL(in attackAspect, in position, in rotation, targetsMask, in target, in targetPosition, in config, in muzzleView, 200u, in jobInfo);
            var attack = bullet.ent.GetOrCreateAspect<SpatialQueryAspect>();
            attack.query.ignoreSorting = true;
            attack.query.treeMask = targetsMask; // Search for targets in this tree
            attack.query.rangeSqr = math.max(1f, bullet.ent.Read<BulletConfigComponent>().hitRangeSqr);
            if (bullet.readConfig.autoTarget == true) {
                // Set nearest count to 1
                attack.query.nearestCount = 1;
            } else {
                attack.query.nearestCount = 0;
            }
            return bullet;

        }

        [INLINE(256)]
        public static bool SetTargetChanged(in UnitCommandGroupAspect group, ReactionType result, in float3 position, in Ent target) {
            ref var data = ref group.ent.Get<LastTargetDataComponent>();
            if (data.result != result ||
                math.any(data.position != position) == true ||
                data.target != target) {
                data.result = result;
                data.position = position;
                data.target = target;
                return true;
            }
            return false;
        }

        [INLINE(256)]
        public static Ent GetUnitByAttackAspect(AttackAspect aspect) {
            var placement = aspect.ent.ReadParent();
            var placements = placement.ReadParent();
            var unit = placements.ReadParent();
            return unit;
        }

        public readonly struct BulletData {

            public readonly float3 direction;

            public BulletData(in float3 direction) {
                this.direction = direction;
            }

        }
        
        [INLINE(256)]
        public static UnsafeList<BulletData> Distribute(in Ent ent, in float3 sourcePosition, in float3 targetPosition) {
            var results = new UnsafeList<BulletData>(1, Constants.ALLOCATOR_TEMP);
            if (ent.TryRead(out AttackBulletDistributionComponent attackBulletTargetsComponent) == true && attackBulletTargetsComponent.bulletsCount > 1u) {
                var dir = math.normalizesafe(targetPosition - sourcePosition);
                var sector = attackBulletTargetsComponent.sector;
                var startAngle = -sector.sector * 0.5f;
                var step = sector.sector / (attackBulletTargetsComponent.bulletsCount - 1u);
                if (attackBulletTargetsComponent.bulletsSpawnBehaviour == AttackBulletDistributionComponent.BulletsSpawnBehaviour.SectorUniformDistribution) {
                    var range = math.sqrt(sector.rangeSqr);
                    for (uint i = 0u; i < attackBulletTargetsComponent.bulletsCount; ++i) {
                        var currentAngle = math.radians(startAngle + step * i);
                        quaternion rot = quaternion.AxisAngle(math.up(), currentAngle);
                        var d = math.mul(rot, dir);
                        results.Add(new BulletData(math.normalizesafe(d * range)));
                    }
                } else if (attackBulletTargetsComponent.bulletsSpawnBehaviour == AttackBulletDistributionComponent.BulletsSpawnBehaviour.SectorRandomDistribution) {
                    for (uint i = 0u; i < attackBulletTargetsComponent.bulletsCount; ++i) {
                        var angleDeg = ent.GetRandomValue(startAngle, startAngle + sector.sector);
                        var angle = math.radians(angleDeg);
                        var rot = quaternion.AxisAngle(math.up(), angle);
                        var d = math.mul(rot, dir);
                        results.Add(new BulletData(d));
                    }
                } else if (attackBulletTargetsComponent.bulletsSpawnBehaviour == AttackBulletDistributionComponent.BulletsSpawnBehaviour.SectorUniformRandomDistribution) {
                    for (uint i = 0u; i < attackBulletTargetsComponent.bulletsCount; ++i) {
                        var baseAngle = startAngle + step * i;
                        var jitter = ent.GetRandomValue(-step * 0.5f, step * 0.5f);
                        var angle = math.radians(baseAngle + jitter);
                        var rot = quaternion.AxisAngle(math.up(), angle);
                        var d = math.mul(rot, dir);
                        results.Add(new BulletData(d));
                    }
                }
            } else {
                results.Add(new BulletData(math.normalizesafe(targetPosition - sourcePosition)));
            }
            return results;
        }

    }

}