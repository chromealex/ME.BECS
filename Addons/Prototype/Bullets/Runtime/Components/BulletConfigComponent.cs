namespace ME.BECS.Bullets {
    
    using Unity.Mathematics;
    
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
        public float hitRangeSqr;

        public float speed;

        /// <summary>
        /// If set - bullet will move towards target point if it moves
        /// </summary>
        public byte autoTarget;

    }

    public struct BulletEffectOnDestroy : IConfigComponentStatic {

        public ME.BECS.Effects.EffectConfig effect;

    }

    public struct FirePointComponent : IComponent {

        public Ent point;

    }

    public struct BulletViewPoint : IConfigComponentStatic {

        public static BulletViewPoint Default = new BulletViewPoint() {
            rotation = quaternion.identity,
        };

        public float3 position;
        public quaternion rotation;

    }

}