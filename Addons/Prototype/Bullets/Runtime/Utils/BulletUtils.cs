namespace ME.BECS.Bullets {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Views;
    using ME.BECS.Transforms;
    using ME.BECS.Players;
    using Unity.Mathematics;
    
    public static class BulletUtils {

        [INLINE(256)]
        public static void RegisterFirePoint(in Ent root, in float3 position, in quaternion rotation) {

            var point = Ent.New();
            var tr = point.GetOrCreateAspect<TransformAspect>();
            point.SetParent(root);
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
        /// <param name="position">Initial bullet position</param>
        /// <param name="rotation">Initial bullet rotation</param>
        /// <param name="targetsMask">Targets tree mask</param>
        /// <param name="target">If set - use this to target bullet at runtime</param>
        /// <param name="targetPosition">If set</param>
        /// <param name="config">Bullet config</param>
        /// <param name="view">Bullet view</param>
        /// <param name="muzzleView">Muzzle view</param>
        /// <param name="muzzleLifetime">Muzzle lifetime</param>
        /// <returns></returns>
        [INLINE(256)]
        public static BulletAspect CreateBullet(in float3 position, in quaternion rotation, int targetsMask, in Ent target, in float3 targetPosition, in Config config, in View view, in View muzzleView, float muzzleLifetime = 0.2f) {

            {
                var muzzleEnt = Ent.New();
                var tr = muzzleEnt.GetOrCreateAspect<TransformAspect>();
                tr.position = position;
                tr.rotation = rotation;
                muzzleEnt.InstantiateView(muzzleView);
                muzzleEnt.Destroy(muzzleLifetime);
            }

            {
                var ent = Ent.New();
                config.Apply(ent);
                ent.InstantiateView(view);
                var attack = ent.GetOrCreateAspect<QuadTreeQueryAspect>();
                attack.query.treeMask = targetsMask; // Search for targets in this tree
                attack.query.range = math.max(1f, ent.Read<BulletConfigComponent>().hitRange);
                var tr = ent.GetOrCreateAspect<TransformAspect>();
                tr.position = position;
                tr.rotation = rotation;
                var bullet = ent.GetOrCreateAspect<BulletAspect>();
                bullet.component.targetEnt = target;
                bullet.component.targetWorldPos = target.IsAlive() == true ? target.GetAspect<TransformAspect>().position : targetPosition;
                return bullet;
            }

        }

    }

}