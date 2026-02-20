#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
using Ray2D = ME.BECS.FixedPoint.Ray2D;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
using Ray2D = UnityEngine.Ray2D;
#endif

using UnityEngine;

namespace NativeTrees {

    /// <summary>
    /// Describes a ray in 3D space with a precomputed inverse direction, which benefits performance for raycast queries.
    /// </summary>
    public readonly struct PrecomputedRay2D {

        /// <summary>
        /// Origin position of the ray
        /// </summary>
        public readonly float2 origin;

        /// <summary>
        /// Direction of the ray
        /// </summary>
        public readonly float2 dir;

        /// <summary>
        /// One over the direction of the ray
        /// </summary>
        public readonly float2 invDir;

        public PrecomputedRay2D(Ray2D ray) {
            this.origin = ray.origin;
            this.dir = ray.direction;
            this.invDir = 1f / this.dir;
        }

        /// <summary>
        /// Create the pre-computed ray using the source for the direction, but replace it's origin with another position.
        /// </summary>
        public PrecomputedRay2D(PrecomputedRay2D source, float2 newOrigin) {
            this.dir = source.dir;
            this.invDir = source.invDir;
            this.origin = newOrigin;
        }

        public static explicit operator PrecomputedRay2D(Ray2D ray) {
            return new PrecomputedRay2D(ray);
        }

        public static explicit operator Ray2D(PrecomputedRay2D ray) {
            return new Ray2D(ray.origin, ray.dir);
        }

    }

}