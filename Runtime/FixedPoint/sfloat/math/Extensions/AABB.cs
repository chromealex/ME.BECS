using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;

using System;

namespace ME.BECS.FixedPoint {

    [Serializable]
    public partial struct AABB {

        [UnityEngine.Serialization.FormerlySerializedAsAttribute("Center")] public float3 center;
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("Extents")] public float3 extents;
        public float3 size => this.extents * 2f;

        public float3 Size => this.extents * 2;
        public float3 Min => this.center - this.extents;
        public float3 Max => this.center + this.extents;
        public float3 min => this.center - this.extents;
        public float3 max => this.center + this.extents;

        public AABB(float3 center, float3 size) {
            this.center = center;
            this.extents = size * 0.5f;
        }

        /// <summary>Returns a string representation of the AABB.</summary>
        public override string ToString() {
            return $"AABB(Center:{this.center}, Extents:{this.extents}";
        }

        public void SetMinMax(float3 min, float3 max) {
            this.extents = (max - min) * 0.5f;
            this.center = min + this.extents;
        }

        public void Encapsulate(float3 point) {
            this.SetMinMax(math.min(this.Min, point), math.max(this.Max, point));
        }

        public bool Contains(float3 point) {
            if (point[0] < this.center[0] - this.extents[0]) {
                return false;
            }

            if (point[0] > this.center[0] + this.extents[0]) {
                return false;
            }

            if (point[1] < this.center[1] - this.extents[1]) {
                return false;
            }

            if (point[1] > this.center[1] + this.extents[1]) {
                return false;
            }

            if (point[2] < this.center[2] - this.extents[2]) {
                return false;
            }

            if (point[2] > this.center[2] + this.extents[2]) {
                return false;
            }

            return true;
        }

        public bool Intersects(in AABB bounds) => this.Contains(bounds);

        public bool Contains(AABB b) {
            return this.Contains(b.center + new float3(-b.extents.x, -b.extents.y, -b.extents.z))
                   && this.Contains(b.center + new float3(-b.extents.x, -b.extents.y, b.extents.z))
                   && this.Contains(b.center + new float3(-b.extents.x, b.extents.y, -b.extents.z))
                   && this.Contains(b.center + new float3(-b.extents.x, b.extents.y, b.extents.z))
                   && this.Contains(b.center + new float3(b.extents.x, -b.extents.y, -b.extents.z))
                   && this.Contains(b.center + new float3(b.extents.x, -b.extents.y, b.extents.z))
                   && this.Contains(b.center + new float3(b.extents.x, b.extents.y, -b.extents.z))
                   && this.Contains(b.center + new float3(b.extents.x, b.extents.y, b.extents.z));
        }

        private static float3 RotateExtents(float3 extents, float3 m0, float3 m1, float3 m2) {
            return math.abs(m0 * extents.x) + math.abs(m1 * extents.y) + math.abs(m2 * extents.z);
        }

        public static AABB Transform(float4x4 transform, AABB localBounds) {
            AABB transformed;
            transformed.extents = RotateExtents(localBounds.extents, transform.c0.xyz, transform.c1.xyz, transform.c2.xyz);
            transformed.center = math.transform(transform, localBounds.center);
            return transformed;
        }

        public sfloat DistanceSq(float3 point) {
            return math.lengthsq(math.max(math.abs(point - this.center), this.extents) - this.extents);
        }

    }

}