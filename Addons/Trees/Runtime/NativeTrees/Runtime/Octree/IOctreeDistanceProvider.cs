#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace NativeTrees {
    
    public interface IOctreeDistanceProvider<T> {

        /// <summary>
        /// Return the (squared) distance to an object or it's AABB.
        /// </summary>
        /// <param name="point">The point to measure the distance from</param>
        /// <param name="obj">The object to measure the distance to</param>
        /// <param name="bounds">The bounds of the object</param>
        tfloat DistanceSquared(float3 point, T obj, AABB bounds);

    }

}