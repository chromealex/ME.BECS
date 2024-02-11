using System.Runtime.CompilerServices;
using System.Diagnostics;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   A plane described by a normal and a negated distance from the origin. </summary>
    [DebuggerDisplay("{Normal}, {Distance}")]
    public struct Plane
    {
        private float4 m_NormalAndDistance;

        /// <summary>   Gets or sets the normal. </summary>
        ///
        /// <value> The normal. </value>
        public float3 Normal
        {
            get => m_NormalAndDistance.xyz;
            set => m_NormalAndDistance.xyz = value;
        }

        /// <summary>   Gets or sets the distance. Distance is negative distance from the origin. </summary>
        ///
        /// <value> Negative distance from the origin. </value>
        public float Distance
        {
            get => m_NormalAndDistance.w;
            set => m_NormalAndDistance.w = value;
        }

        /// <summary>
        /// Returns the distance from the point to the plane, positive if the point is on the side of the
        /// plane on which the plane normal points, zero if the point is on the plane, negative otherwise.
        /// </summary>
        ///
        /// <param name="point">    The point. </param>
        ///
        /// <returns>   Signed distance from the point to the plane. </returns>
        public float SignedDistanceToPoint(float3 point)
        {
            return Math.Dotxyz1(m_NormalAndDistance, point);
        }

        /// <summary>   Returns the closest point on the plane to the input point. </summary>
        ///
        /// <param name="point">    The point. </param>
        ///
        /// <returns>   The closest point. </returns>
        public float3 Projection(float3 point)
        {
            return point - Normal * SignedDistanceToPoint(point);
        }

        /// <summary>   Gets the flipped plane. Negates normal and distance. </summary>
        ///
        /// <value> The flipped plane. </value>
        public Plane Flipped => new Plane { m_NormalAndDistance = -m_NormalAndDistance };

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="normal">   The normal. </param>
        /// <param name="distance"> The distance. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Plane(float3 normal, float distance)
        {
            m_NormalAndDistance = new float4(normal, distance);
        }

        /// <summary>   Implicit cast that converts the given float4 to a Plane. </summary>
        ///
        /// <param name="plane">    The plane. </param>
        ///
        /// <returns>   The result of the operation. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Plane(float4 plane) => new Plane(plane.xyz, plane.w);

        /// <summary>   Implicit cast that converts the given Plane to a float4. </summary>
        ///
        /// <param name="plane">    The plane. </param>
        ///
        /// <returns>   The result of the operation. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator float4(Plane plane) => plane.m_NormalAndDistance;
    }

    /// <summary>   Helper functions. </summary>
    public static partial class Math
    {
        /// <summary>   Construct a Plane from direction. </summary>
        ///
        /// <param name="origin">       The origin. </param>
        /// <param name="direction">    The direction. </param>
        ///
        /// <returns>   A Plane. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Plane PlaneFromDirection(float3 origin, float3 direction)
        {
            float3 normal = math.normalize(direction);
            return new Plane(normal, -math.dot(normal, origin));
        }

        /// <summary>   Construct a Plane from two edges. </summary>
        ///
        /// <param name="origin">   The origin. </param>
        /// <param name="edgeA">    The edge a. </param>
        /// <param name="edgeB">    The edge b. </param>
        ///
        /// <returns>   A Plane. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Plane PlaneFromTwoEdges(float3 origin, float3 edgeA, float3 edgeB)
        {
            return PlaneFromDirection(origin, math.cross(edgeA, edgeB));
        }

        /// <summary>   Transform plane. </summary>
        ///
        /// <param name="transform">    The transform. </param>
        /// <param name="plane">        The plane. </param>
        ///
        /// <returns>   A Plane. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Plane TransformPlane(RigidTransform transform, Plane plane)
        {
            float3 normal = math.rotate(transform.rot, plane.Normal);
            return new Plane(normal, plane.Distance - math.dot(normal, transform.pos));
        }

        /// <summary>   Transform plane. </summary>
        ///
        /// <param name="transform">    The transform. </param>
        /// <param name="plane">        The plane. </param>
        ///
        /// <returns>   A Plane. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Plane TransformPlane(MTransform transform, Plane plane)
        {
            float3 normal = math.mul(transform.Rotation, plane.Normal);
            return new Plane(normal, plane.Distance - math.dot(normal, transform.Translation));
        }
    }
}
