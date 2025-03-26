#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.Bullets {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Views;
    using ME.BECS.Transforms;
    using ME.BECS.Players;
    
    public static class BulletUtils {

        [INLINE(256)]
        public static Ent RegisterFirePoint(in Ent root, in float3 position, in quaternion rotation, in JobInfo jobInfo) {

            var point = Ent.New(in jobInfo, editorName: "FirePoint");
            var tr = point.GetOrCreateAspect<TransformAspect>();
            point.SetParent(in root);
            tr.position = position;
            tr.rotation = rotation;

            ref var firePoints = ref root.Get<FirePointComponent>();
            if (firePoints.points.IsCreated == false) firePoints.points = new ListAuto<Ent>(in point, 1u);
            firePoints.points.Add(point);
            return point;

        }

        [INLINE(256)]
        public static Ent GetNextFirePoint(in Ent root) {
            
            ref var point = ref root.Get<FirePointComponent>();
            var points = point.points;
            if (point.index >= points.Count) {
                point.index = 0u;
            }
            if (point.index >= points.Count) return default;
            return points[point.index++];

        }

        [INLINE(256)]
        public static ListAuto<Ent> GetFirePoints(in Ent root) {

            return root.Read<FirePointComponent>().points;

        }

        /// <summary>
        /// Create bullet entity with 200 ms muzzleLifetime
        /// </summary>
        /// <param name="sourceUnit">Source unit</param>
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
        public static BulletAspect CreateBullet(in Ent sourceUnit, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition,
                                                in Config config, in View muzzleView, in JobInfo jobInfo = default) {
            return CreateBullet(in sourceUnit, in position, in rotation, targetsMask, in target, in targetPosition, in config, in muzzleView, 200u, in jobInfo);
        }

        /// <summary>
        /// Create bullet entity
        /// </summary>
        /// <param name="sourceUnit">Source unit</param>
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
        public static BulletAspect CreateBullet(in Ent sourceUnit, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition, in Config config, in View muzzleView, uint muzzleLifetimeMs, in JobInfo jobInfo = default) {

            if (muzzleView.IsValid == true) {
                var muzzleEnt = Ent.New(in jobInfo, "MuzzlePoint");
                var tr = muzzleEnt.GetOrCreateAspect<TransformAspect>();
                tr.position = position;
                tr.rotation = rotation;
                muzzleEnt.InstantiateView(muzzleView);
                muzzleEnt.Destroy(muzzleLifetimeMs);
            }

            {
                var ent = Ent.New(in jobInfo, "Bullet");
                PlayerUtils.SetOwner(in ent, PlayerUtils.GetOwner(in sourceUnit));
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
                return bullet;
            }

        }

    }

}