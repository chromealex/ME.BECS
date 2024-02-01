namespace ME.BECS.Bullets {
    
    using Unity.Mathematics;

    public struct BulletRuntimeComponent : IComponent {

        /// <summary>
        /// if targetEnt is set - use it,
        /// otherwise use targetWorldPos
        /// </summary>
        public Ent targetEnt;
        public float3 targetWorldPos;
        /// <summary>
        /// Unit source
        /// </summary>
        public Ent sourceUnit;

    }
    
}