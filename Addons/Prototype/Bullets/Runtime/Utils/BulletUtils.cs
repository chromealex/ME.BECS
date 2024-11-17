namespace ME.BECS.Bullets {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Views;
    using ME.BECS.Transforms;
    using ME.BECS.Players;
    using Unity.Mathematics;
    
    public static class BulletUtils {

        [INLINE(256)]
        public static void RegisterFirePoint(in Ent root, in float3 position, in quaternion rotation, in JobInfo jobInfo) {

            var point = Ent.New(in jobInfo, editorName: "FirePoint");
            var tr = point.GetOrCreateAspect<TransformAspect>();
            point.SetParent(in root);
            tr.position = position;
            tr.rotation = rotation;

            root.Get<FirePointComponent>().point = point;

        }

        [INLINE(256)]
        public static Ent GetFirePoint(in Ent root) {

            return root.Read<FirePointComponent>().point;

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
        /// <param name="muzzleLifetime">Muzzle lifetime</param>
        /// <param name="jobInfo"></param>
        /// <returns></returns>
        [INLINE(256)]
        public static BulletAspect CreateBullet(in Ent sourceUnit, in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition, in Config config, in View muzzleView, float muzzleLifetime = 0.2f, in JobInfo jobInfo = default) {

            if (muzzleView.IsValid == true) {
                var muzzleEnt = Ent.New(in jobInfo, "MuzzlePoint");
                var tr = muzzleEnt.GetOrCreateAspect<TransformAspect>();
                tr.position = position;
                tr.rotation = rotation;
                muzzleEnt.InstantiateView(muzzleView);
                muzzleEnt.Destroy(muzzleLifetime);
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