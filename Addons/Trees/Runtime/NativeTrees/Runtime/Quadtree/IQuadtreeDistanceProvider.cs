#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace NativeTrees
{
    public interface IQuadtreeDistanceProvider<T>
    {
        /// <summary>
        /// Return the (squared) distance to an object or it's AABB.
        /// </summary>
        /// <param name="point">The point to measure the distance from</param>
        /// <param name="obj">The object to measure the distance to</param>
        /// <param name="bounds">The bounds of the object</param>
        float DistanceSquared(float2 point, T obj, AABB2D bounds);
    }
}