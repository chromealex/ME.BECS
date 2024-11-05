namespace ME.BECS.Bullets {
    
    using Unity.Mathematics;
    
    public struct BulletConfigComponent : IConfigComponent {

        /// <summary>
        /// Damage value
        /// </summary>
        public float damage;

        /// <summary>
        /// If hitRange > 0 - use splash damage
        /// If hitRange <= 0 - use single damage at point or for targetEnt
        /// </summary>
        public float hitRange;

        public float speed;

        /// <summary>
        /// If set - bullet will move towards target point if it moves
        /// </summary>
        public bool autoTarget;

        public ME.BECS.Effects.EffectConfig effectOnDestroy;

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