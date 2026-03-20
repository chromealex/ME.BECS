using tfloat = sfloat;
using ME.BECS.FixedPoint;
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
using System;

namespace ME.BECS.FixedPoint {

    [Serializable]
    public partial struct FPlane {

        internal const int size = 16;
        private float3 normalData;
        private tfloat distanceData;

        /// <summary>
        ///   <para>Normal vector of the plane.</para>
        /// </summary>
        public float3 normal {
            [INLINE(256)]
            get => this.normalData;
            [INLINE(256)]
            set => this.normalData = value;
        }

        /// <summary>
        ///   <para>The distance measured from the Plane to the origin, along the Plane's normal.</para>
        /// </summary>
        public tfloat distance {
            [INLINE(256)]
            get => this.distanceData;
            [INLINE(256)]
            set => this.distanceData = value;
        }

        /// <summary>
        ///   <para>Creates a plane.</para>
        /// </summary>
        /// <param name="inNormal"></param>
        /// <param name="inPoint"></param>
        [INLINE(256)]
        public FPlane(float3 inNormal, float3 inPoint) {
            this.normalData = math.normalize(inNormal);
            this.distanceData = -math.dot(this.normalData, inPoint);
        }

        /// <summary>
        ///   <para>Creates a plane.</para>
        /// </summary>
        /// <param name="inNormal"></param>
        /// <param name="d"></param>
        [INLINE(256)]
        public FPlane(float3 inNormal, tfloat d) {
            this.normalData = math.normalize(inNormal);
            this.distanceData = d;
        }

        /// <summary>
        ///   <para>Creates a plane.</para>
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        [INLINE(256)]
        public FPlane(float3 a, float3 b, float3 c) {
            this.normalData = math.normalize(math.cross(b - a, c - a));
            this.distanceData = -math.dot(this.normalData, a);
        }

        /// <summary>
        ///   <para>Sets a plane using a point that lies within it along with a normal to orient it.</para>
        /// </summary>
        /// <param name="inNormal">The plane's normal vector.</param>
        /// <param name="inPoint">A point that lies on the plane.</param>
        [INLINE(256)]
        public void SetNormalAndPosition(float3 inNormal, float3 inPoint) {
            this.normalData = math.normalize(inNormal);
            this.distanceData = -math.dot(this.normalData, inPoint);
        }

        /// <summary>
        ///   <para>Sets a plane using three points that lie within it.  The points go around clockwise as you look down on the top surface of the plane.</para>
        /// </summary>
        /// <param name="a">First point in clockwise order.</param>
        /// <param name="b">Second point in clockwise order.</param>
        /// <param name="c">Third point in clockwise order.</param>
        [INLINE(256)]
        public void Set3Points(float3 a, float3 b, float3 c) {
            this.normalData = math.normalize(math.cross(b - a, c - a));
            this.distanceData = -math.dot(this.normalData, a);
        }

        /// <summary>
        ///   <para>Makes the plane face in the opposite direction.</para>
        /// </summary>
        [INLINE(256)]
        public void Flip() {
            this.normalData = -this.normalData;
            this.distanceData = -this.distanceData;
        }

        /// <summary>
        ///   <para>Returns a copy of the plane that faces in the opposite direction.</para>
        /// </summary>
        public FPlane flipped {
            [INLINE(256)]
            get => new(-this.normalData, -this.distanceData);
        }

        /// <summary>
        ///   <para>Moves the plane in space by the translation vector.</para>
        /// </summary>
        /// <param name="translation">The offset in space to move the plane with.</param>
        [INLINE(256)]
        public void Translate(float3 translation) {
            this.distanceData += math.dot(this.normalData, translation);
        }

        /// <summary>
        ///   <para>Returns a copy of the given plane that is moved in space by the given translation.</para>
        /// </summary>
        /// <param name="fPlane">The plane to move in space.</param>
        /// <param name="translation">The offset in space to move the plane with.</param>
        /// <returns>
        ///   <para>The translated plane.</para>
        /// </returns>
        [INLINE(256)]
        public static FPlane Translate(FPlane fPlane, float3 translation) {
            return new FPlane(fPlane.normalData, fPlane.distanceData += math.dot(fPlane.normalData, translation));
        }

        /// <summary>
        ///   <para>For a given point returns the closest point on the plane.</para>
        /// </summary>
        /// <param name="point">The point to project onto the plane.</param>
        /// <returns>
        ///   <para>A point on the plane that is closest to point.</para>
        /// </returns>
        [INLINE(256)]
        public float3 ClosestPointOnPlane(float3 point) {
            var num = math.dot(this.normalData, point) + this.distanceData;
            return point - this.normalData * num;
        }

        /// <summary>
        ///   <para>Returns a signed distance from plane to point.</para>
        /// </summary>
        /// <param name="point"></param>
        [INLINE(256)]
        public tfloat GetDistanceToPoint(float3 point) {
            return math.dot(this.normalData, point) + this.distanceData;
        }

        /// <summary>
        ///   <para>Is a point on the positive side of the plane?</para>
        /// </summary>
        /// <param name="point"></param>
        [INLINE(256)]
        public bool GetSide(float3 point) {
            return (double)math.dot(this.normalData, point) + (double)this.distanceData > 0.0;
        }

        /// <summary>
        ///   <para>Are two points on the same side of the plane?</para>
        /// </summary>
        /// <param name="inPt0"></param>
        /// <param name="inPt1"></param>
        [INLINE(256)]
        public bool SameSide(float3 inPt0, float3 inPt1) {
            var distanceToPoint1 = this.GetDistanceToPoint(inPt0);
            var distanceToPoint2 = this.GetDistanceToPoint(inPt1);
            return ((double)distanceToPoint1 > 0.0 && (double)distanceToPoint2 > 0.0) || ((double)distanceToPoint1 <= 0.0 && (double)distanceToPoint2 <= 0.0);
        }

        [INLINE(256)]
        public bool Raycast(Ray ray, out tfloat enter) {
            var a = math.dot(ray.direction, this.normalData);
            var num = -math.dot(ray.origin, this.normalData) - this.distanceData;
            if (a <= 0.0f) {
                enter = 0.0f;
                return false;
            }

            enter = num / a;
            return (double)enter > 0.0;
        }

        [INLINE(256)]
        public static explicit operator FPlane(UnityEngine.Plane plane) {
            return new FPlane((float3)plane.normal, (tfloat)plane.distance);
        }

    }

}