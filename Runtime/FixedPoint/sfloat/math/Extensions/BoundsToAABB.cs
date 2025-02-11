using UnityEngine;

namespace ME.BECS.FixedPoint
{
    public static class AABBExtensions
    {
        public static AABB ToAABB(this Bounds bounds)
        {
            return new AABB { center = (float3)bounds.center, extents = (float3)bounds.extents};
        }

        public static Bounds ToBounds(this AABB aabb)
        {
            return new Bounds { center = (Vector3)aabb.center, extents = (Vector3)aabb.extents};
        }
    }
}
