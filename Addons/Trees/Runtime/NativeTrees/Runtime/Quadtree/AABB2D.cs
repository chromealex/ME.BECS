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
    /// 2D axis aligned bounding box with support for fast ray intersection checking.
    /// </summary>
    public readonly struct AABB2D {

        public readonly float2 min;
        public readonly float2 max;

        public float2 Center => 0.5f * (this.min + this.max);
        public float2 Size => this.max - this.min;
        public bool IsValid => all(this.max >= this.min);

        /// <summary>
        /// Construct an AABB
        /// </summary>
        /// <param name="min">Bottom left</param>
        /// <param name="max">Top right</param>
        /// <remarks>Does not check wether max is greater than min for maximum performance.</remarks>
        public AABB2D(float2 min, float2 max) {
            this.min = min;
            this.max = max;
        }

        /// <summary>
        /// Returns wether this AABB overlaps with another AABB
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Overlaps(in AABB2D other) {
            return all(this.max >= other.min) &&
                   all(other.max >= this.min);
        }

        /// <summary>
        /// Returns wether this AABB fully contains another
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(in AABB2D other) {
            return all(this.min <= other.min) &&
                   all(this.max >= other.max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(float2 point) {
            return all(point >= this.min) && all(point <= this.max);
        }

        /// <summary>
        /// Returns the closest point on this AABB from a given point. If the point lies in this AABB, the point itself is returned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float2 ClosestPoint(float2 point) {
            return clamp(point, this.min, this.max);
        }

        /// <summary>
        /// Returns the squared distance of a point to this AABB. If the point lies in the box, zero is returned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly tfloat DistanceSquared(float2 point) {
            return distancesq(point, this.ClosestPoint(point));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ContainsPoint(in float2 point) {
            return all(point >= this.min) && all(point <= this.max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IntersectsRay(in Ray2D ray) {
            return this.IntersectsRay((PrecomputedRay2D)ray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IntersectsRay(in PrecomputedRay2D ray) {
            return this.IntersectsRay(ray.origin, ray.invDir, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IntersectsRay(in PrecomputedRay2D ray, out float2 point) {
            if (this.IntersectsRay(ray.origin, ray.invDir, out var t)) {
                point = ray.origin + ray.dir * t;
                return true;
            }

            point = default;
            return false;
        }


        /// <summary>
        /// Checks if this AABB intersects with a ray.
        /// Fast implementation:
        /// https://tavianator.com/2011/ray_box.html
        /// </summary>
        /// <param name="rayPos">Ray origin position</param>
        /// <param name="rayInvDir">One over the ray's direction</param>
        /// <returns>If the ray intersected this AABB</returns>
        /// <remarks>This method does not handle the case where a component of the ray is on the edge of the box.
        /// And may return a false positive in that case. Checking is ommited for performance and the fact that the intersecter
        /// generally implements a further check.
        /// See https://tavianator.com/2011/ray_box.html and https://tavianator.com/2015/ray_box_nan.html</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IntersectsRay(in float2 rayPos, in float2 rayInvDir, out tfloat tMin) {
            var t1 = (this.min - rayPos) * rayInvDir;
            var t2 = (this.max - rayPos) * rayInvDir;

            var tMin1 = min(t1, t2);
            var tMax1 = max(t1, t2);

            tMin = max(0, cmax(tMin1));
            var tMax = cmin(tMax1);

            return tMax >= tMin;
        }

    }

}