#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using static ME.BECS.FixedPoint.math;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

using System.Runtime.CompilerServices;
using UnityEngine;

namespace NativeTrees {

    /// <summary>
    /// 3D axis aligned bounding box with support for fast ray intersection checking.
    /// Optimized for burst compilation.
    /// </summary>
    /// <remarks>Differs from Unity's <see cref="Bounds"/> as this stores the min and max.
    /// Which is faster for overlap and ray intersection checking</remarks>
    public struct AABB {

        public float3 min;
        public float3 max;

        public float3 Center => .5f * (this.min + this.max);
        public float3 Size => this.max - this.min;

        public float3 Min => this.min;
        public float3 Max => this.max;


        /// <summary>
        /// Construct an AABB
        /// </summary>
        /// <param name="min">Bottom left</param>
        /// <param name="max">Top right</param>
        /// <remarks>Does not check wether max is greater than min for maximum performance.</remarks>
        public AABB(float3 min, float3 max) {
            this.min = min;
            this.max = max;
        }

        /// <summary>
        /// Returns wether this AABB overlaps with another AABB
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Overlaps(in AABB other) {
            return all(this.max >= other.min) &&
                   all(other.max >= this.min);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float3 point) {
            return all(point >= this.min) && all(point <= this.max);
        }

        /// <summary>
        /// Returns wether this AABB fully contains another
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(in AABB other) {
            return all(this.min <= other.min) &&
                   all(this.max >= other.max);
        }

        /// <summary>
        /// Returns the closest point on this AABB from a given point. If the point lies in this AABB, the point itself is returned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 ClosestPoint(float3 point) {
            return clamp(point, this.min, this.max);
        }

        /// <summary>
        /// Returns the closest point on this AABB from a given point. If the point lies in this AABB, the point itself is returned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 ClosestPoint(float2 point) {
            return clamp(point, this.min.xz, this.max.xz);
        }

        /// <summary>
        /// Returns the squared distance of a point to this AABB. If the point lies in the box, zero is returned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public tfloat DistanceSquared(float3 point) {
            return distancesq(point, this.ClosestPoint(point));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public tfloat DistanceSquared(float3 point, bool ignoreY) {
            if (ignoreY == true) {
                return distancesq(point.xz, this.ClosestPoint(point.xz));
            }

            return distancesq(point, this.ClosestPoint(point));
        }

        /// <summary>
        /// Returns if a ray intersects with this bounding box. If you need the test the same ray
        /// against a lot of AABB's, it's more efficient to precompute the inverse of the ray direction and call the PrecomputedRay overload.
        /// </summary>
        /// <remarks>This method does not handle the case where a component of the ray is on the edge of the box
        /// and may return a false positive in that case. See https://tavianator.com/2011/ray_box.html and https://tavianator.com/2015/ray_box_nan.html</remarks>
        /// <returns>Wether the ray intersects this bounding box</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntersectsRay(in Ray ray) {
            return this.IntersectsRay((PrecomputedRay)ray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntersectsRay(in PrecomputedRay ray) {
            return this.IntersectsRay(ray.origin, ray.invDir, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntersectsRay(in PrecomputedRay ray, out float3 point) {
            if (this.IntersectsRay(ray.origin, ray.invDir, out var tMin)) {
                point = ray.origin + ray.dir * tMin;
                return true;
            }

            point = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntersectsRay(in PrecomputedRay ray, out tfloat tMin) {
            return this.IntersectsRay(ray.origin, ray.invDir, out tMin);
        }

        /// <summary>
        /// Returns if a ray intersects with this bounding box.
        /// </summary>
        /// <remarks>This method does not handle the case where a component of the ray is on the edge of the box
        /// and may return a false positive in that case. See https://tavianator.com/2011/ray_box.html and https://tavianator.com/2015/ray_box_nan.html</remarks>
        /// <returns>Wether the ray intersects this bounding box</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntersectsRay(in float3 rayPos, in float3 rayInvDir, out tfloat tMin) {
            var t1 = (this.min - rayPos) * rayInvDir;
            var t2 = (this.max - rayPos) * rayInvDir;

            var tMin1 = min(t1, t2);
            var tMax1 = max(t1, t2);

            tMin = max(0, cmax(tMin1));
            var tMax = cmin(tMax1);

            return tMax >= tMin;
        }

        /// <summary>
        /// Returns wether max is greater or equal than min
        /// </summary>
        public bool IsValid => all(this.max >= this.min);

        public static explicit operator Bounds(AABB aabb) {
            return new Bounds(aabb.Center, aabb.Size);
        }

        public static implicit operator AABB(Bounds bounds) {
            return new AABB(bounds.min, bounds.max);
        }

    }

}