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

using UnityEngine;

namespace NativeTrees {

    /// <summary>
    /// Describes a ray in 3D space with a precomputed inverse direction, which benefits performance for raycast queries.
    /// </summary>
    public readonly struct PrecomputedRay {

        /// <summary>
        /// Origin position of the ray
        /// </summary>
        public readonly float3 origin;

        /// <summary>
        /// Direction of the ray
        /// </summary>
        public readonly float3 dir;

        /// <summary>
        /// One over the direction of the ray
        /// </summary>
        public readonly float3 invDir;

        public PrecomputedRay(Ray ray) {
            this.origin = (float3)ray.origin;
            this.dir = (float3)ray.direction;
            this.invDir = 1 / this.dir;
        }

        /// <summary>
        /// Create the pre-computed ray using the source for the direction, but replace it's origin with another position.
        /// </summary>
        public PrecomputedRay(PrecomputedRay source, float3 newOrigin) {
            this.dir = source.dir;
            this.invDir = source.invDir;
            this.origin = newOrigin;
        }

        public static explicit operator PrecomputedRay(Ray ray) {
            return new PrecomputedRay(ray);
        }

        public static explicit operator Ray(PrecomputedRay ray) {
            return new Ray((Vector3)ray.origin, (Vector3)ray.dir);
        }

    }

}