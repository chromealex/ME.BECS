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
        public quaternion rotation;

        public float3 Center => .5f * (this.min + this.max);
        public float3 Size => this.max - this.min;

        public float3 Min => this.min;
        public float3 Max => this.max;

        /// <summary>
        /// Construct an AABB
        /// </summary>
        /// <param name="min">Bottom left</param>
        /// <param name="max">Top right</param>
        /// <param name="rotation"></param>
        /// <remarks>Does not check wether max is greater than min for maximum performance.</remarks>
        public AABB(float3 min, float3 max, quaternion rotation) {
            this.min = min;
            this.max = max;
            this.rotation = rotation;
        }

        /// <summary>
        /// Returns wether this AABB overlaps with another AABB
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Overlaps(in AABB other) {
            if (math.all(this.rotation.value == new float4(0f, 0f, 0f, 1f)) == true &&
                math.all(other.rotation.value == new float4(0f, 0f, 0f, 1f)) == true) {
                return all(this.max >= other.min) &&
                       all(other.max >= this.min);
            }
            
            float3 aCenter = (this.min + this.max) * 0.5f;
            float3 aExtents = (this.max - this.min) * 0.5f;
            float3 bCenter = (other.min + other.max) * 0.5f;
            float3 bExtents = (other.max - other.min) * 0.5f;

            float3x3 aAxes = new float3x3(
                math.mul(this.rotation, new float3(1, 0, 0)),
                math.mul(this.rotation, new float3(0, 1, 0)),
                math.mul(this.rotation, new float3(0, 0, 1))
            );

            float3x3 bAxes = new float3x3(
                math.mul(other.rotation, new float3(1, 0, 0)),
                math.mul(other.rotation, new float3(0, 1, 0)),
                math.mul(other.rotation, new float3(0, 0, 1))
            );

            float3x3 R = math.mul(math.transpose(aAxes), bAxes);
            float3x3 AbsR = new float3x3(
                math.abs(R.c0) + new float3(1e-6f),
                math.abs(R.c1) + new float3(1e-6f),
                math.abs(R.c2) + new float3(1e-6f)
            );

            float3 t = math.mul(math.transpose(aAxes), (bCenter - aCenter));

            for (int i = 0; i < 3; i++) {
                var ra = aExtents[i];
                var rb = bExtents.x * AbsR[i][0] + bExtents.y * AbsR[i][1] + bExtents.z * AbsR[i][2];
                if (math.abs(t[i]) > ra + rb) return false;
            }

            for (int i = 0; i < 3; i++) {
                var ra = aExtents.x * AbsR[0][i] + aExtents.y * AbsR[1][i] + aExtents.z * AbsR[2][i];
                var rb = bExtents[i];
                if (math.abs(t.x * R[0][i] + t.y * R[1][i] + t.z * R[2][i]) > ra + rb) return false;
            }

            for (int i = 0; i < 3; ++i) {
                for (int j = 0; j < 3; ++j) {
                    var ra = aExtents[(i + 1) % 3] * AbsR[(i + 2) % 3][j] + aExtents[(i + 2) % 3] * AbsR[(i + 1) % 3][j];
                    var rb = bExtents[(j + 1) % 3] * AbsR[i][(j + 2) % 3] + bExtents[(j + 2) % 3] * AbsR[i][(j + 1) % 3];
                    var tVal = math.abs(
                        t[(i + 2) % 3] * R[(i + 1) % 3][j] -
                        t[(i + 1) % 3] * R[(i + 2) % 3][j]
                    );
                    if (tVal > ra + rb) return false;
                }
            }

            return true;
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
        public bool IntersectsRay(in PrecomputedRay ray) {
            return this.IntersectsRay(ray.origin, in ray.dir, ray.radius, ray.invDir, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntersectsRay(in PrecomputedRay ray, out float3 point) {
            if (this.IntersectsRay(ray.origin, in ray.dir, ray.radius, ray.invDir, out var tMin)) {
                point = ray.origin + ray.dir * tMin;
                return true;
            }

            point = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntersectsRay(in PrecomputedRay ray, out tfloat tMin) {
            return this.IntersectsRay(ray.origin, in ray.dir, ray.radius, ray.invDir, out tMin);
        }

        /// <summary>
        /// Returns if a ray intersects with this bounding box.
        /// </summary>
        /// <remarks>This method does not handle the case where a component of the ray is on the edge of the box
        /// and may return a false positive in that case. See https://tavianator.com/2011/ray_box.html and https://tavianator.com/2015/ray_box_nan.html</remarks>
        /// <returns>Wether the ray intersects this bounding box</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntersectsRay(float3 rayPos, in float3 rayDir, float2 radius, float3 rayInvDir, out tfloat tMin) {

            float3 localMin = this.min - new float3(radius.x, radius.y, radius.x);
            float3 localMax = this.max + new float3(radius.x, radius.y, radius.x);
            if (math.all(this.rotation.value == new float4(0f, 0f, 0f, 1f)) == false) {
                float3 center = (this.min + this.max) * 0.5f;
                float3 extents = (this.max - this.min) * 0.5f;
                quaternion invRot = inverse(this.rotation);
                float3 localOrigin = mul(invRot, rayPos - center);
                float3 localDir = mul(invRot, rayDir);
                localMin = -extents - new float3(radius.x, radius.y, radius.x);
                localMax = extents + new float3(radius.x, radius.y, radius.x);
                rayInvDir = new float3(
                    math.abs(localDir.x) < 1e-8f ? float.PositiveInfinity : 1f / localDir.x,
                    math.abs(localDir.y) < 1e-8f ? float.PositiveInfinity : 1f / localDir.y,
                    math.abs(localDir.z) < 1e-8f ? float.PositiveInfinity : 1f / localDir.z
                );
                rayPos = localOrigin;
            }
            
            var t1 = (localMin - rayPos) * rayInvDir;
            var t2 = (localMax - rayPos) * rayInvDir;
            var tMin1 = math.min(t1, t2);
            var tMax1 = math.max(t1, t2);
            tMin = math.max(0, cmax(tMin1));
            var tMax = cmin(tMax1);
            return tMax >= tMin;
            
        }

        /// <summary>
        /// Returns wether max is greater or equal than min
        /// </summary>
        public bool IsValid => all(this.max >= this.min);

    }

}