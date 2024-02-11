using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>
    /// A target in the space of a rigid body that will align with a corresponding target in the
    /// space of the other body to which it is constrained.
    /// </summary>
    public struct BodyFrame : IEquatable<BodyFrame>
    {
        /// <summary>
        /// The bind pose anchor or target position of the joint in the space of its rigid body.
        /// </summary>
        public float3 Position;

        /// <summary>
        /// The bind pose orientation of the joint's x-axis in the space of its rigid body.
        /// </summary>
        public float3 Axis;

        /// <summary>
        /// The bind pose orientation of the joint's y-axis in the space of its rigid body.
        /// </summary>
        public float3 PerpendicularAxis;

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="transform">    The transform. </param>
        public BodyFrame(RigidTransform transform)
        {
            Position = transform.pos;
            var rotation = new float3x3(transform.rot);
            Axis = rotation.c0;
            PerpendicularAxis = rotation.c1;
        }

        static readonly float3 k_DefaultAxis = new float3(1f, 0f, 0f);
        static readonly float3 k_DefaultPerpendicular = new float3(0f, 1f, 0f);

        /// <summary>   The identity <see cref="BodyFrame"/>. </summary>
        public static readonly BodyFrame Identity =
            new BodyFrame { Axis = k_DefaultAxis, PerpendicularAxis = k_DefaultPerpendicular };

        /// <summary>   Converts this object to a rigid transform. </summary>
        ///
        /// <returns>   A RigidTransform. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RigidTransform AsRigidTransform() => new RigidTransform(ValidateAxes(), Position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Math.MTransform AsMTransform() => new Math.MTransform(ValidateAxes(), Position);

        // slower than math.orthonormalize(), this method replicates UnityEngine.Vector3.OrthoNormalize()
        // it is more robust if input Axis is not pre-normalized or frame is degenerate
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3x3 OrthoNormalize(float3 u, float3 v)
        {
            var mag = math.length(u);
            u = math.select(k_DefaultAxis, u / mag, mag > Math.Constants.UnityEpsilon);

            v -= math.dot(u, v) * u;
            mag = math.length(v);
            v = math.select(OrthoNormalVectorFast(u), v / mag, mag > Math.Constants.UnityEpsilon);

            return new float3x3(u, v, math.cross(u, v));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3 OrthoNormalVectorFast(float3 n)
        {
            const float kRcpSqrt2 = 0.7071067811865475244008443621048490f;
            var usePlaneYZ = math.abs(n.z) > kRcpSqrt2;
            var a = math.select(math.dot(n.xy, n.xy), math.dot(n.yz, n.yz), usePlaneYZ);
            var k = math.rcp(math.sqrt(a));
            return math.select(new float3(-n.y * k, n.x * k, 0f), new float3(0f, -n.z * k, n.y * k), usePlaneYZ);
        }

        internal float3x3 ValidateAxes()
        {
            // TODO: math.orthonormalize() does not guarantee an ortho-normalized result when Axis is non-normalized
            var sqrMag = math.lengthsq(Axis);
            const float kEpsilon = Math.Constants.UnityEpsilon;
            return sqrMag >= 1f - kEpsilon && sqrMag <= 1f + kEpsilon
                ? math.orthonormalize(new float3x3(Axis, PerpendicularAxis, default))
                : OrthoNormalize(Axis, PerpendicularAxis);
        }

        /// <summary>   Tests if this BodyFrame is considered equal to another. </summary>
        ///
        /// <param name="other">    The body frame to compare to this object. </param>
        ///
        /// <returns>   True if the objects are considered equal, false if they are not. </returns>
        public bool Equals(BodyFrame other) =>
            Position.Equals(other.Position)
            && Axis.Equals(other.Axis)
            && PerpendicularAxis.Equals(other.PerpendicularAxis);

        /// <summary>   Tests if this object is considered equal to another. </summary>
        ///
        /// <param name="obj">  The object to compare to this object. </param>
        ///
        /// <returns>   True if the objects are considered equal, false if they are not. </returns>
        public override bool Equals(object obj) => obj is BodyFrame other && Equals(other);

        /// <summary>   Calculates a hash code for this object. </summary>
        ///
        /// <returns>   A hash code for this object. </returns>
        public override int GetHashCode() => unchecked((int)math.hash(new float3x3(Position, Axis, PerpendicularAxis)));

        /// <summary>   Convert this object into a string representation. </summary>
        ///
        /// <returns>   A string that represents this object. </returns>
        public override string ToString() =>
            $"BodyFrame {{ Axis = {Axis}, PerpendicularAxis = {PerpendicularAxis}, Position = {Position} }}";

        /// <summary>   Implicit cast that converts the given RigidTransform to a BodyFrame. </summary>
        ///
        /// <param name="transform">    The transform. </param>
        ///
        /// <returns>   The result of the operation. </returns>
        public static implicit operator BodyFrame(RigidTransform transform) => new BodyFrame(transform);
    }
}
