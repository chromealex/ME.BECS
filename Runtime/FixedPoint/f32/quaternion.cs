using System;
using System.Runtime.CompilerServices;
using static ME.BECS.FixedPoint.math;

#if FIXED_POINT_F32
namespace ME.BECS {
    
    using ME.BECS.FixedPoint;

    public static class QuaternionExt {
        
        private static float3 FromQ2(quaternion quaternion) {

            var q1 = quaternion.value;
            var sqw = q1.w * q1.w;
            var sqx = q1.x * q1.x;
            var sqy = q1.y * q1.y;
            var sqz = q1.z * q1.z;
            var unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
            var test = q1.x * q1.w - q1.y * q1.z;
            float3 v;

            if (test > 0.4995f * unit) { // singularity at north pole
                v.y = 2f * math.atan2(q1.y, q1.x);
                v.x = math.PI / 2;
                v.z = 0;
                return QuaternionExt.NormalizeAngles(math.degrees(v));
            }

            if (test < -0.4995f * unit) { // singularity at south pole
                v.y = -2f * math.atan2(q1.y, q1.x);
                v.x = -math.PI / 2;
                v.z = 0;
                return QuaternionExt.NormalizeAngles(math.degrees(v));
            }

            var q = new quaternion(q1.w, q1.z, q1.x, q1.y).value;
            v.y = math.atan2(2f * q.x * q.w + 2f * q.y * q.z, 1 - 2f * (q.z * q.z + q.w * q.w)); // Yaw
            v.x = math.asin(2f * (q.x * q.z - q.w * q.y)); // Pitch
            v.z = math.atan2(2f * q.x * q.y + 2f * q.z * q.w, 1 - 2f * (q.y * q.y + q.z * q.z)); // Roll
            return QuaternionExt.NormalizeAngles(math.degrees(v));
        }

        private static float3 NormalizeAngles(float3 angles) {
            angles.x = QuaternionExt.NormalizeAngle(angles.x);
            angles.y = QuaternionExt.NormalizeAngle(angles.y);
            angles.z = QuaternionExt.NormalizeAngle(angles.z);
            return angles;
        }

        private static sfloat NormalizeAngle(sfloat angle) {
            while (angle > 360f) {
                angle -= 360f;
            }

            while (angle < 0f) {
                angle += 360f;
            }

            return angle;
        }

        public static float3 ToEuler(this quaternion quaternion) {

            return FromQ2(quaternion);
        }

    }

}

namespace ME.BECS.FixedPoint
{

    [Serializable]
    public partial struct quaternion : System.IEquatable<quaternion>, IFormattable
    {
        public float4 value;

        /// <summary>A quaternion representing the identity transform.</summary>
        public static quaternion identity => new quaternion(sfloat.Zero, sfloat.Zero, sfloat.Zero, sfloat.One);

        /// <summary>Constructs a quaternion from four float values.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public quaternion(sfloat x, sfloat y, sfloat z, sfloat w) { value.x = x; value.y = y; value.z = z; value.w = w; }

        /// <summary>Constructs a quaternion from float4 vector.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public quaternion(float4 value) { this.value = value; }

        /// <summary>Implicitly converts a float4 vector to a quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator quaternion(float4 v) { return new quaternion(v); }

        /// <summary>Constructs a unit quaternion from a float3x3 rotation matrix. The matrix must be orthonormal.</summary>
        public quaternion(float3x3 m) {
            var c0 = m.c0;
            var c1 = m.c1;
            var c2 = m.c2;
            if (c0.x + c1.y + c2.z > 0f) {
                this = quat(3, 1f + c0.x + c1.y + c2.z, c1.z - c2.y, c2.x - c0.z, c0.y - c1.x);
                return;
            }

            if (c0.x >= c1.y && c0.x >= c2.z) {
                this = quat(0, 1f + c0.x - c1.y - c2.z, c0.y + c1.x, c0.z + c2.x, c1.z - c2.y);
                return;
            }

            if (c1.y > c2.z) {
                this = quat(1, 1f - c0.x + c1.y - c2.z, c1.x + c0.y, c2.y + c1.z, c2.x - c0.z);
                return;
            }
            
            this = quat(2, 1f - c0.x - c1.y + c2.z, c2.x + c0.z, c2.y + c1.z, c0.y - c1.x);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static quaternion quat(int index, sfloat t, sfloat a, sfloat b, sfloat c) {
                var s = 0.5f / sqrt(t);
                return index switch {
                    3 => new(a * s, b * s, c * s, t * s),
                    2 => new(a * s, b * s, t * s, c * s),
                    1 => new(a * s, t * s, b * s, c * s),
                    _ => new(t * s, a * s, b * s, c * s)
                };
            }
        }

        /// <summary>Constructs a unit quaternion from an orthonormal float4x4 matrix.</summary>
        public quaternion(float4x4 m)
        {
            var c0 = m.c0;
            var c1 = m.c1;
            var c2 = m.c2;
            if (c0.x + c1.y + c2.z > 0f) {
                this = quat(3, 1f + c0.x + c1.y + c2.z, c1.z - c2.y, c2.x - c0.z, c0.y - c1.x);
                return;
            }

            if (c0.x >= c1.y && c0.x >= c2.z) {
                this = quat(0, 1f + c0.x - c1.y - c2.z, c0.y + c1.x, c0.z + c2.x, c1.z - c2.y);
                return;
            }

            if (c1.y > c2.z) {
                this = quat(1, 1f - c0.x + c1.y - c2.z, c1.x + c0.y, c2.y + c1.z, c2.x - c0.z);
                return;
            }
            
            this = quat(2, 1f - c0.x - c1.y + c2.z, c2.x + c0.z, c2.y + c1.z, c0.y - c1.x);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static quaternion quat(int index, sfloat t, sfloat a, sfloat b, sfloat c) {
                var s = 0.5f / sqrt(t);
                return index switch {
                    3 => new(a * s, b * s, c * s, t * s),
                    2 => new(a * s, b * s, t * s, c * s),
                    1 => new(a * s, t * s, b * s, c * s),
                    _ => new(t * s, a * s, b * s, c * s)
                };
            }
        }

        /// <summary>
        /// Returns a quaternion representing a rotation around a unit axis by an angle in radians.
        /// The rotation direction is clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion AxisAngle(float3 axis, sfloat angle)
        {
            sfloat sina, cosa;
            math.sincos((sfloat)0.5f * angle, out sina, out cosa);
            return quaternion(float4(axis * sina, cosa));
        }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the x-axis, then the y-axis and finally the z-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerXYZ(float3 xyz)
        {
            // return mul(rotateZ(xyz.z), mul(rotateY(xyz.y), rotateX(xyz.x)));
            float3 s, c;
            sincos((sfloat)0.5f * xyz, out s, out c);
            return quaternion(
                // s.x * c.y * c.z - s.y * s.z * c.x,
                // s.y * c.x * c.z + s.x * s.z * c.y,
                // s.z * c.x * c.y - s.x * s.y * c.z,
                // c.x * c.y * c.z + s.y * s.z * s.x
                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(-sfloat.One, sfloat.One, -sfloat.One, sfloat.One)
                );
        }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the x-axis, then the z-axis and finally the y-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerXZY(float3 xyz)
        {
            // return mul(rotateY(xyz.y), mul(rotateZ(xyz.z), rotateX(xyz.x)));
            float3 s, c;
            sincos((sfloat)0.5f * xyz, out s, out c);
            return quaternion(
                // s.x * c.y * c.z + s.y * s.z * c.x,
                // s.y * c.x * c.z + s.x * s.z * c.y,
                // s.z * c.x * c.y - s.x * s.y * c.z,
                // c.x * c.y * c.z - s.y * s.z * s.x
                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(sfloat.One, sfloat.One, -sfloat.One, -sfloat.One)
                );
        }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the y-axis, then the x-axis and finally the z-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerYXZ(float3 xyz)
        {
            // return mul(rotateZ(xyz.z), mul(rotateX(xyz.x), rotateY(xyz.y)));
            float3 s, c;
            sincos((sfloat)0.5f * xyz, out s, out c);
            return quaternion(
                // s.x * c.y * c.z - s.y * s.z * c.x,
                // s.y * c.x * c.z + s.x * s.z * c.y,
                // s.z * c.x * c.y + s.x * s.y * c.z,
                // c.x * c.y * c.z - s.y * s.z * s.x
                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(-sfloat.One, sfloat.One, sfloat.One, -sfloat.One)
                );
        }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the y-axis, then the z-axis and finally the x-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerYZX(float3 xyz)
        {
            // return mul(rotateX(xyz.x), mul(rotateZ(xyz.z), rotateY(xyz.y)));
            float3 s, c;
            sincos((sfloat)0.5f * xyz, out s, out c);
            return quaternion(
                // s.x * c.y * c.z - s.y * s.z * c.x,
                // s.y * c.x * c.z - s.x * s.z * c.y,
                // s.z * c.x * c.y + s.x * s.y * c.z,
                // c.x * c.y * c.z + s.y * s.z * s.x
                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(-sfloat.One, -sfloat.One, sfloat.One, sfloat.One)
                );
        }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the z-axis, then the x-axis and finally the y-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// This is the default order rotation order in Unity.
        /// </summary>
        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerZXY(float3 xyz)
        {
            // return mul(rotateY(xyz.y), mul(rotateX(xyz.x), rotateZ(xyz.z)));
            float3 s, c;
            sincos((sfloat)0.5f * xyz, out s, out c);
            return quaternion(
                // s.x * c.y * c.z + s.y * s.z * c.x,
                // s.y * c.x * c.z - s.x * s.z * c.y,
                // s.z * c.x * c.y - s.x * s.y * c.z,
                // c.x * c.y * c.z + s.y * s.z * s.x
                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(sfloat.One, -sfloat.One, -sfloat.One, sfloat.One)
                );
        }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the z-axis, then the y-axis and finally the x-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerZYX(float3 xyz)
        {
            // return mul(rotateX(xyz.x), mul(rotateY(xyz.y), rotateZ(xyz.z)));
            float3 s, c;
            sincos((sfloat)0.5f * xyz, out s, out c);
            return quaternion(
                // s.x * c.y * c.z + s.y * s.z * c.x,
                // s.y * c.x * c.z - s.x * s.z * c.y,
                // s.z * c.x * c.y + s.x * s.y * c.z,
                // c.x * c.y * c.z - s.y * s.x * s.z
                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(sfloat.One, -sfloat.One, sfloat.One, -sfloat.One)
                );
        }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the x-axis, then the y-axis and finally the z-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="x">The rotation angle around the x-axis in radians.</param>
        /// <param name="y">The rotation angle around the y-axis in radians.</param>
        /// <param name="z">The rotation angle around the z-axis in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerXYZ(sfloat x, sfloat y, sfloat z) { return EulerXYZ(float3(x, y, z)); }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the x-axis, then the z-axis and finally the y-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="x">The rotation angle around the x-axis in radians.</param>
        /// <param name="y">The rotation angle around the y-axis in radians.</param>
        /// <param name="z">The rotation angle around the z-axis in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerXZY(sfloat x, sfloat y, sfloat z) { return EulerXZY(float3(x, y, z)); }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the y-axis, then the x-axis and finally the z-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="x">The rotation angle around the x-axis in radians.</param>
        /// <param name="y">The rotation angle around the y-axis in radians.</param>
        /// <param name="z">The rotation angle around the z-axis in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerYXZ(sfloat x, sfloat y, sfloat z) { return EulerYXZ(float3(x, y, z)); }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the y-axis, then the z-axis and finally the x-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="x">The rotation angle around the x-axis in radians.</param>
        /// <param name="y">The rotation angle around the y-axis in radians.</param>
        /// <param name="z">The rotation angle around the z-axis in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerYZX(sfloat x, sfloat y, sfloat z) { return EulerYZX(float3(x, y, z)); }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the z-axis, then the x-axis and finally the y-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// This is the default order rotation order in Unity.
        /// </summary>
        /// <param name="x">The rotation angle around the x-axis in radians.</param>
        /// <param name="y">The rotation angle around the y-axis in radians.</param>
        /// <param name="z">The rotation angle around the z-axis in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerZXY(sfloat x, sfloat y, sfloat z) { return EulerZXY(float3(x, y, z)); }

        /// <summary>
        /// Returns a quaternion constructed by first performing a rotation around the z-axis, then the y-axis and finally the x-axis.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// </summary>
        /// <param name="x">The rotation angle around the x-axis in radians.</param>
        /// <param name="y">The rotation angle around the y-axis in radians.</param>
        /// <param name="z">The rotation angle around the z-axis in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion EulerZYX(sfloat x, sfloat y, sfloat z) { return EulerZYX(float3(x, y, z)); }

        /// <summary>
        /// Returns a quaternion constructed by first performing 3 rotations around the principal axes in a given order.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// When the rotation order is known at compile time, it is recommended for performance reasons to use specific
        /// Euler rotation constructors such as EulerZXY(...).
        /// </summary>
        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
        /// <param name="order">The order in which the rotations are applied.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion Euler(float3 xyz, RotationOrder order = RotationOrder.ZXY)
        {
            switch (order)
            {
                case RotationOrder.XYZ:
                    return EulerXYZ(xyz);
                case RotationOrder.XZY:
                    return EulerXZY(xyz);
                case RotationOrder.YXZ:
                    return EulerYXZ(xyz);
                case RotationOrder.YZX:
                    return EulerYZX(xyz);
                case RotationOrder.ZXY:
                    return EulerZXY(xyz);
                case RotationOrder.ZYX:
                    return EulerZYX(xyz);
                default:
                    return quaternion.identity;
            }
        }

        /// <summary>
        /// Returns a quaternion constructed by first performing 3 rotations around the principal axes in a given order.
        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
        /// When the rotation order is known at compile time, it is recommended for performance reasons to use specific
        /// Euler rotation constructors such as EulerZXY(...).
        /// </summary>
        /// <param name="x">The rotation angle around the x-axis in radians.</param>
        /// <param name="y">The rotation angle around the y-axis in radians.</param>
        /// <param name="z">The rotation angle around the z-axis in radians.</param>
        /// <param name="order">The order in which the rotations are applied.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion Euler(sfloat x, sfloat y, sfloat z, RotationOrder order = RotationOrder.Default)
        {
            return Euler(float3(x, y, z), order);
        }

        /// <summary>Returns a quaternion that rotates around the x-axis by a given number of radians.</summary>
        /// <param name="angle">The clockwise rotation angle when looking along the x-axis towards the origin in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion RotateX(sfloat angle)
        {
            sfloat sina, cosa;
            math.sincos((sfloat)0.5f * angle, out sina, out cosa);
            return quaternion(sina, sfloat.Zero, sfloat.Zero, cosa);
        }

        /// <summary>Returns a quaternion that rotates around the y-axis by a given number of radians.</summary>
        /// <param name="angle">The clockwise rotation angle when looking along the y-axis towards the origin in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion RotateY(sfloat angle)
        {
            sfloat sina, cosa;
            math.sincos((sfloat)0.5f * angle, out sina, out cosa);
            return quaternion(sfloat.Zero, sina, sfloat.Zero, cosa);
        }

        /// <summary>Returns a quaternion that rotates around the z-axis by a given number of radians.</summary>
        /// <param name="angle">The clockwise rotation angle when looking along the z-axis towards the origin in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion RotateZ(sfloat angle)
        {
            sfloat sina, cosa;
            math.sincos((sfloat)0.5f * angle, out sina, out cosa);
            return quaternion(sfloat.Zero, sfloat.Zero, sina, cosa);
        }

        /// <summary>
        /// Returns a quaternion view rotation given a unit length forward vector and a unit length up vector.
        /// The two input vectors are assumed to be unit length and not collinear.
        /// If these assumptions are not met use float3x3.LookRotationSafe instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion LookRotation(float3 forward, float3 up)
        {
            float3 t = normalize(cross(up, forward));
            return quaternion(float3x3(t, cross(forward, t), forward));
        }

        /// <summary>
        /// Returns a quaternion view rotation given a forward vector and an up vector.
        /// The two input vectors are not assumed to be unit length.
        /// If the magnitude of either of the vectors is so extreme that the calculation cannot be carried out reliably or the vectors are collinear,
        /// the identity will be returned instead.
        /// </summary>
        public static quaternion LookRotationSafe(float3 forward, float3 up)
        {
            sfloat forwardLengthSq = dot(forward, forward);
            sfloat upLengthSq = dot(up, up);

            forward *= rsqrt(forwardLengthSq);
            up *= rsqrt(upLengthSq);

            float3 t = cross(up, forward);
            sfloat tLengthSq = dot(t, t);
            t *= rsqrt(tLengthSq);

            sfloat mn = min(min(forwardLengthSq, upLengthSq), tLengthSq);
            sfloat mx = max(max(forwardLengthSq, upLengthSq), tLengthSq);

            bool accept = mn > FixMath.SMALL_VALUE && mx < FixMath.BIG_VALUE && isfinite(forwardLengthSq) && isfinite(upLengthSq) && isfinite(tLengthSq);
            return quaternion(select(float4(sfloat.Zero, sfloat.Zero, sfloat.Zero, sfloat.One), quaternion(float3x3(t, cross(forward, t),forward)).value, accept));
        }

        /// <summary>Returns true if the quaternion is equal to a given quaternion, false otherwise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(quaternion x) { return value.x == x.value.x && value.y == x.value.y && value.z == x.value.z && value.w == x.value.w; }

        /// <summary>Returns whether true if the quaternion is equal to a given quaternion, false otherwise.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object x) { return Equals((quaternion)x); }

        /// <summary>Returns a hash code for the quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() { return (int)math.hash(this); }

        /// <summary>Returns a string representation of the quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return string.Format("quaternion({0}f, {1}f, {2}f, {3}f)", value.x, value.y, value.z, value.w);
        }

        /// <summary>Returns a string representation of the quaternion using a specified format and culture-specific format information.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.Format("quaternion({0}f, {1}f, {2}f, {3}f)", value.x.ToString(format, formatProvider), value.y.ToString(format, formatProvider), value.z.ToString(format, formatProvider), value.w.ToString(format, formatProvider));
        }
    }

    public static partial class math
    {
        /// <summary>Returns a quaternion constructed from four float values.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion quaternion(sfloat x, sfloat y, sfloat z, sfloat w) { return new quaternion(x, y, z, w); }

        /// <summary>Returns a quaternion constructed from a float4 vector.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion quaternion(float4 value) { return new quaternion(value); }

        /// <summary>Returns a unit quaternion constructed from a float3x3 rotation matrix. The matrix must be orthonormal.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion quaternion(float3x3 m) { return new quaternion(m); }

        /// <summary>Returns a unit quaternion constructed from a float4x4 matrix. The matrix must be orthonormal.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion quaternion(float4x4 m) { return new quaternion(m); }

       /// <summary>Returns the conjugate of a quaternion value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion conjugate(quaternion q)
        {
            return quaternion(q.value * float4(-sfloat.One, -sfloat.One, -sfloat.One, sfloat.One));
        }

       /// <summary>Returns the inverse of a quaternion value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion inverse(quaternion q)
        {
            float4 x = q.value;
            return quaternion(rcp(dot(x, x)) * x * float4(-sfloat.One, -sfloat.One, -sfloat.One, sfloat.One));
        }

        /// <summary>Returns the dot product of two quaternions.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sfloat dot(quaternion a, quaternion b)
        {
            return dot(a.value, b.value);
        }

        /// <summary>Returns the length of a quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sfloat length(quaternion q)
        {
            return sqrt(dot(q.value, q.value));
        }

        /// <summary>Returns the squared length of a quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sfloat lengthsq(quaternion q)
        {
            return dot(q.value, q.value);
        }

        /// <summary>Returns a normalized version of a quaternion q by scaling it by 1 / length(q).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion normalize(quaternion q)
        {
            float4 x = q.value;
            return quaternion(rsqrt(dot(x, x)) * x);
        }

        /// <summary>
        /// Returns a safe normalized version of the q by scaling it by 1 / length(q).
        /// Returns the identity when 1 / length(q) does not produce a finite number.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion normalizesafe(quaternion q)
        {
            float4 x = q.value;
            sfloat len = math.dot(x, x);
            return quaternion(math.select(FixedPoint.quaternion.identity.value, x * math.rsqrt(len), len > FLT_MIN_NORMAL));
        }

        /// <summary>
        /// Returns a safe normalized version of the q by scaling it by 1 / length(q).
        /// Returns the given default value when 1 / length(q) does not produce a finite number.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion normalizesafe(quaternion q, quaternion defaultvalue)
        {
            float4 x = q.value;
            sfloat len = math.dot(x, x);
            return quaternion(math.select(defaultvalue.value, x * math.rsqrt(len), len > FLT_MIN_NORMAL));
        }

        /// <summary>Returns the natural exponent of a quaternion. Assumes w is zero.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion unitexp(quaternion q)
        {
            sfloat v_rcp_len = rsqrt(dot(q.value.xyz, q.value.xyz));
            sfloat v_len = rcp(v_rcp_len);
            sfloat sin_v_len, cos_v_len;
            sincos(v_len, out sin_v_len, out cos_v_len);
            return quaternion(float4(q.value.xyz * v_rcp_len * sin_v_len, cos_v_len));
        }

        /// <summary>Returns the natural exponent of a quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion exp(quaternion q)
        {
            sfloat v_rcp_len = rsqrt(dot(q.value.xyz, q.value.xyz));
            sfloat v_len = rcp(v_rcp_len);
            sfloat sin_v_len, cos_v_len;
            sincos(v_len, out sin_v_len, out cos_v_len);
            return quaternion(float4(q.value.xyz * v_rcp_len * sin_v_len, cos_v_len) * exp(q.value.w));
        }

        /// <summary>Returns the natural logarithm of a unit length quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion unitlog(quaternion q)
        {
            sfloat w = clamp(q.value.w, -sfloat.One, sfloat.One);
            sfloat s = acos(w) * rsqrt(sfloat.One - w*w);
            return quaternion(float4(q.value.xyz * s, sfloat.Zero));
        }

        /// <summary>Returns the natural logarithm of a quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion log(quaternion q)
        {
            sfloat v_len_sq = dot(q.value.xyz, q.value.xyz);
            sfloat q_len_sq = v_len_sq + q.value.w*q.value.w;

            sfloat s = acos(clamp(q.value.w * rsqrt(q_len_sq), -sfloat.One, sfloat.One)) * rsqrt(v_len_sq);
            return quaternion(float4(q.value.xyz * s, (sfloat)0.5f * log(q_len_sq)));
        }

        /// <summary>Returns the result of transforming the quaternion b by the quaternion a.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion mul(quaternion a, quaternion b)
        {
            return quaternion(a.value.wwww * b.value + (a.value.xyzx * b.value.wwwx + a.value.yzxy * b.value.zxyy) * float4(sfloat.One, sfloat.One, sfloat.One, -sfloat.One) - a.value.zxyz * b.value.yzxz);
        }

        /// <summary>Returns the result of transforming a vector by a quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 mul(quaternion q, float3 v)
        {
            float3 t = (sfloat)2.0f * cross(q.value.xyz, v);
            return v + q.value.w * t + cross(q.value.xyz, t);
        }

        /// <summary>Returns the result of rotating a vector by a unit quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 rotate(quaternion q, float3 v)
        {
            float3 t = (sfloat)2.0f * cross(q.value.xyz, v);
            return v + q.value.w * t + cross(q.value.xyz, t);
        }

        /// <summary>Returns the result of a normalized linear interpolation between two quaternions q1 and a2 using an interpolation parameter t.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion nlerp(quaternion q1, quaternion q2, sfloat t)
        {
            sfloat dt = dot(q1, q2);
            if(dt < sfloat.Zero)
            {
                q2.value = -q2.value;
            }

            return normalize(quaternion(lerp(q1.value, q2.value, t)));
        }

        /// <summary>Returns the result of a spherical interpolation between two quaternions q1 and a2 using an interpolation parameter t.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion slerp(quaternion q1, quaternion q2, sfloat t)
        {
            sfloat dt = dot(q1, q2);
            if (dt < sfloat.Zero)
            {
                dt = -dt;
                q2.value = -q2.value;
            }

            if (dt < FixMath.ALMOST_ONE)
            {
                sfloat angle = acos(dt);
                sfloat s = rsqrt(sfloat.One - dt * dt);    // 1.0f / sin(angle)
                sfloat w1 = sin(angle * (sfloat.One - t)) * s;
                sfloat w2 = sin(angle * t) * s;
                return quaternion(q1.value * w1 + q2.value * w2);
            }
            else
            {
                // if the angle is small, use linear interpolation
                return normalize(quaternion(lerp(q1.value, q2.value, t)));
            }
        }

        /// <summary>Returns a uint hash code of a quaternion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint hash(quaternion q)
        {
            return hash(q.value);
        }

        /// <summary>
        /// Returns a uint4 vector hash code of a quaternion.
        /// When multiple elements are to be hashes together, it can more efficient to calculate and combine wide hash
        /// that are only reduced to a narrow uint hash at the very end instead of at every step.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint4 hashwide(quaternion q)
        {
            return hashwide(q.value);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 forward(quaternion q) { return mul(q, float3(sfloat.Zero, sfloat.Zero, sfloat.One)); }  // for compatibility
    }
}
#endif