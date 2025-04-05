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

    [ComponentGroup(typeof(BulletComponentGroup))]
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
    
    [ComponentGroup(typeof(BulletComponentGroup))]
    public struct DamageOverrideComponent : IComponent {

        public uint damage;

    }
    
}