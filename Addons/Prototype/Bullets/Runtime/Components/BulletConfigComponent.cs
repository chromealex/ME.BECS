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

    public struct BulletComponentGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.red;

    }
    
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct BulletConfigComponent : IConfigComponent {

        /// <summary>
        /// Damage value
        /// </summary>
        public uint damage;

        /// <summary>
        /// If hitRangeSqr > 0  -> use splash damage
        /// If hitRangeSqr <= 0 -> use single damage at point or for targetEnt
        /// </summary>
        [ValueSqr]
        public tfloat hitRangeSqr;

        public tfloat speed;

        /// <summary>
        /// If set - bullet will move towards target point if it moves
        /// </summary>
        public byte autoTarget;

    }

    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct BulletEffectOnDestroy : IConfigComponentStatic {

        public ME.BECS.Effects.EffectConfig effect;

    }

    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct FirePointComponent : IComponent {

        public ListAuto<Ent> points;
        public uint index;

    }

    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct BulletViewPoint : IConfigComponentStatic {

        public static BulletViewPoint Default = new BulletViewPoint() {
            rotation = quaternion.identity,
        };

        public float3 position;
        public quaternion rotation;

    }

    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct BulletViewPoints : IConfigComponentStatic {

        public MemArrayAuto<BulletViewPoint> points;

    }

}