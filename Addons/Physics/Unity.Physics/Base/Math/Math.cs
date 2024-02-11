using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Unity.Physics
{
    /// <summary>   Common math helper functions. </summary>
    [DebuggerStepThrough]
    public static partial class Math
    {
        /// <summary>   Constants. </summary>
        [DebuggerStepThrough]
        public static class Constants
        {
            internal static float4 One4F => new float4(1);
            internal static float4 Min4F => new float4(float.MinValue);
            internal static float4 Max4F => new float4(float.MaxValue);
            internal static float3 Min3F => new float3(float.MinValue);
            internal static float3 Max3F => new float3(float.MaxValue);
            internal static float3 MaxDisplacement3F => new float3(float.MaxValue * 0.5f);

            /// <summary>
            /// Smallest float such that 1.0 + eps != 1.0 Different from float.Epsilon which is
            /// the smallest value greater than zero.
            /// </summary>
            public const float Eps = 1.192092896e-07F;

            /// <summary>
            /// This constant is identical to the one in the Unity Mathf library, to ensure
            /// identical behaviour.
            /// </summary>
            public const float UnityEpsilonNormalSqrt = 1e-15F;

            /// <summary>
            /// This constant is identical to the one in the Unity Mathf library, to ensure identical
            /// behaviour.
            /// </summary>
            public const float UnityEpsilon = 0.00001F;

            /// <summary>   Tau. </summary>
            public const float Tau = 2.0f * math.PI;
            /// <summary>   1.0f / Tau. </summary>
            public const float OneOverTau = 1.0f / Tau;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int NextMultipleOf16(int input) => ((input + 15) >> 4) << 4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong NextMultipleOf16(ulong input) => ((input + 15) >> 4) << 4;

        /// Note that alignment must be a power of two for this to work.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int NextMultipleOf(int input, int alignment) => (input + (alignment - 1)) & (~(alignment - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong NextMultipleOf(ulong input, ulong alignment) => (input + (alignment - 1)) & (~(alignment - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfMinComponent(float2 v) => v.x < v.y ? 0 : 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfMinComponent(float3 v) => v.x < v.y ? ((v.x < v.z) ? 0 : 2) : ((v.y < v.z) ? 1 : 2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfMinComponent(float4 v) => math.cmax(math.select(new int4(0, 1, 2, 3), new int4(-1), math.cmin(v) < v));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfMaxComponent(float2 v) => v.x > v.y ? 0 : 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfMaxComponent(float3 v) => v.x > v.y ? ((v.x > v.z) ? 0 : 2) : ((v.y > v.z) ? 1 : 2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfMaxComponent(float4 v) => math.cmax(math.select(new int4(0, 1, 2, 3), new int4(-1), math.cmax(v) > v));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float HorizontalMul(float3 v) => v.x * v.y * v.z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float HorizontalMul(float4 v) => (v.x * v.y) * (v.z * v.w);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Dotxyz1(float4 lhs, float3 rhs) => math.dot(lhs, new float4(rhs, 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double Dotxyz1(double4 lhs, double3 rhs) => math.dot(lhs, new double4(rhs, 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Det(float3 a, float3 b, float3 c) => math.dot(math.cross(a, b), c); // TODO: use math.determinant()?

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float RSqrtSafe(float v) => math.select(math.rsqrt(v), 0.0f, math.abs(v) < 1e-10f);

        /// <summary>   Clamps the vector to to maximum length. </summary>
        ///
        /// <param name="maxLength">    The maximum length. </param>
        /// <param name="vector">       [in,out] The vector to be clamped. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClampToMaxLength(float maxLength, ref float3 vector)
        {
            float lengthSq = math.lengthsq(vector);
            bool maxExceeded = lengthSq > maxLength * maxLength;
            if (maxExceeded)
            {
                float invLen = math.rsqrt(lengthSq);
                float3 rescaledVector = maxLength * invLen * vector;
                vector = rescaledVector;
            }
        }

        /// <summary>   Normalize and return the lenght of a vector. </summary>
        ///
        /// <param name="v">    A float3 to normalize. </param>
        /// <param name="n">    [out] A normalized float3. </param>
        ///
        /// <returns>   Length of v. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float NormalizeWithLength(float3 v, out float3 n)
        {
            float lengthSq = math.lengthsq(v);
            float invLength = math.rsqrt(lengthSq);
            n = v * invLength;
            return lengthSq * invLength;
        }

        /// <summary>   Check if 'v' is normalized. </summary>
        ///
        /// <param name="v">    A float3 to check if normalized. </param>
        ///
        /// <returns>   True if normalized, false if not. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(float3 v)
        {
            float lenZero = math.lengthsq(v) - 1.0f;
            float absLenZero = math.abs(lenZero);
            return absLenZero < Constants.UnityEpsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsApproximatelyEqual(float a, float b, float epsilon = Constants.UnityEpsilon)
        {
            return math.abs(a - b) < epsilon;
        }

        /// <summary>   Return two normals perpendicular to the input vector. </summary>
        ///
        /// <param name="v">    Input vector. </param>
        /// <param name="p">    [out] Normal 1. </param>
        /// <param name="q">    [out] Normal 2. </param>
        public static void CalculatePerpendicularNormalized(float3 v, out float3 p, out float3 q)
        {
            float3 vSquared = v * v;
            float3 lengthsSquared = vSquared + vSquared.xxx; // y = ||j x v||^2, z = ||k x v||^2
            float3 invLengths = math.rsqrt(lengthsSquared);

            // select first direction, j x v or k x v, whichever has greater magnitude
            float3 dir0 = new float3(-v.y, v.x, 0.0f);
            float3 dir1 = new float3(-v.z, 0.0f, v.x);
            bool cmp = (lengthsSquared.y > lengthsSquared.z);
            float3 dir = math.select(dir1, dir0, cmp);

            // normalize and get the other direction
            float invLength = math.select(invLengths.z, invLengths.y, cmp);
            p = dir * invLength;
            float3 cross = math.cross(v, dir);
            q = cross * invLength;
        }

        /// <summary>   Calculate the eigenvectors and eigenvalues of a symmetric 3x3 matrix. </summary>
        ///
        /// <param name="a">            A float3x3 to process. </param>
        /// <param name="eigenVectors"> [out] The eigen vectors. </param>
        /// <param name="eigenValues">  [out] The eigen values. </param>
        internal static void DiagonalizeSymmetricApproximation(float3x3 a, out float3x3 eigenVectors, out float3 eigenValues)
        {
            float GetMatrixElement(float3x3 m, int row, int col)
            {
                switch (col)
                {
                    case 0: return m.c0[row];
                    case 1: return m.c1[row];
                    case 2: return m.c2[row];
                    default: UnityEngine.Assertions.Assert.IsTrue(false); return 0.0f;
                }
            }

            void SetMatrixElement(ref float3x3 m, int row, int col, float x)
            {
                switch (col)
                {
                    case 0: m.c0[row] = x; break;
                    case 1: m.c1[row] = x; break;
                    case 2: m.c2[row] = x; break;
                    default: UnityEngine.Assertions.Assert.IsTrue(false); break;
                }
            }

            eigenVectors = float3x3.identity;
            float epsSq = 1e-14f * (math.lengthsq(a.c0) + math.lengthsq(a.c1) + math.lengthsq(a.c2));
            const int maxIterations = 10;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                // Find the row (p) and column (q) of the off-diagonal entry with greater magnitude
                int p = 0, q = 1;
                {
                    float maxEntry = math.abs(a.c1[0]);
                    float mag02 = math.abs(a.c2[0]);
                    float mag12 = math.abs(a.c2[1]);
                    if (mag02 > maxEntry)
                    {
                        maxEntry = mag02;
                        p = 0;
                        q = 2;
                    }
                    if (mag12 > maxEntry)
                    {
                        maxEntry = mag12;
                        p = 1;
                        q = 2;
                    }

                    // Terminate if it's small enough
                    if (maxEntry * maxEntry < epsSq)
                    {
                        break;
                    }
                }

                // Calculate jacobia rotation
                float3x3 j = float3x3.identity;
                {
                    float apq = GetMatrixElement(a, p, q);
                    float tau = (GetMatrixElement(a, q, q) - GetMatrixElement(a, p, p)) / (2.0f * apq);
                    float t = math.sqrt(1.0f + tau * tau);
                    if (tau > 0.0f)
                    {
                        t = 1.0f / (tau + t);
                    }
                    else
                    {
                        t = 1.0f / (tau - t);
                    }
                    float c = math.rsqrt(1.0f + t * t);
                    float s = t * c;

                    SetMatrixElement(ref j, p, p, c);
                    SetMatrixElement(ref j, q, q, c);
                    SetMatrixElement(ref j, p, q, s);
                    SetMatrixElement(ref j, q, p, -s);
                }

                // Rotate a
                a = math.mul(math.transpose(j), math.mul(a, j));
                eigenVectors = math.mul(eigenVectors, j);
            }
            eigenValues = new float3(a.c0.x, a.c1.y, a.c2.z);
        }

        /// <summary>
        /// Returns the twist angle of the swing-twist decomposition of q about i, j, or k corresponding
        /// to index = 0, 1, or 2 respectively. Full calculation for readability:
        ///      float invLength = RSqrtSafe(dot * dot + w * w);
        ///      float sinHalfAngle = dot * invLength;
        ///      float cosHalfAngle = w * invLength;
        /// Observe: invLength cancels in the tan^-1(sin / cos) calc, so avoid unnecessary calculations.
        /// </summary>
        ///
        /// <param name="q">                A quaternion to process. </param>
        /// <param name="twistAxisIndex">   Zero-based index of the twist axis. </param>
        ///
        /// <returns>   The calculated twist angle. </returns>
        public static float CalculateTwistAngle(quaternion q, int twistAxisIndex)
        {
            // q = swing * twist, twist = normalize(twistAxis * twistAxis dot q.xyz, q.w)
            float dot = q.value[twistAxisIndex];
            float w = q.value.w;
            float halfAngle = math.atan2(dot, w);
            return halfAngle + halfAngle;
        }

        /// <summary>   Returns a quaternion q with q * from = to. </summary>
        ///
        /// <param name="from"> From rotation. </param>
        /// <param name="to">   To rotation. </param>
        ///
        /// <returns>   A quaternion such that q * from = to. </returns>
        internal static quaternion FromToRotation(float3 from, float3 to)
        {
            Assert.IsTrue(math.abs(math.lengthsq(from) - 1.0f) < 1e-4f);
            Assert.IsTrue(math.abs(math.lengthsq(to) - 1.0f) < 1e-4f);
            float3 cross = math.cross(from, to);
            CalculatePerpendicularNormalized(from, out float3 safeAxis, out float3 unused); // for when angle ~= 180
            float dot = math.dot(from, to);
            float3 squares = new float3(0.5f - new float2(dot, -dot) * 0.5f, math.lengthsq(cross));
            float3 inverses = math.select(math.rsqrt(squares), 0.0f, squares < 1e-10f);
            float2 sinCosHalfAngle = squares.xy * inverses.xy;
            float3 axis = math.select(cross * inverses.z, safeAxis, squares.z < 1e-10f);
            return new quaternion(new float4(axis * sinCosHalfAngle.x, sinCosHalfAngle.y));
        }

        // Note: taken from Unity.Animation/Core/MathExtensions.cs, which will be moved to Unity.Mathematics at some point
        //       after that, this should be removed and the Mathematics version should be used
        #region toEuler
        static float3 toEuler(quaternion q, math.RotationOrder order = math.RotationOrder.Default)
        {
            const float epsilon = 1e-6f;

            //prepare the data
            var qv = q.value;
            var d1 = qv * qv.wwww * new float4(2.0f); //xw, yw, zw, ww
            var d2 = qv * qv.yzxw * new float4(2.0f); //xy, yz, zx, ww
            var d3 = qv * qv;
            var euler = new float3(0.0f);

            const float CUTOFF = (1.0f - 2.0f * epsilon) * (1.0f - 2.0f * epsilon);

            switch (order)
            {
                case math.RotationOrder.ZYX:
                {
                    var y1 = d2.z + d1.y;
                    if (y1 * y1 < CUTOFF)
                    {
                        var x1 = -d2.x + d1.z;
                        var x2 = d3.x + d3.w - d3.y - d3.z;
                        var z1 = -d2.y + d1.x;
                        var z2 = d3.z + d3.w - d3.y - d3.x;
                        euler = new float3(math.atan2(x1, x2), math.asin(y1), math.atan2(z1, z2));
                    }
                    else //zxz
                    {
                        y1 = math.clamp(y1, -1.0f, 1.0f);
                        var abcd = new float4(d2.z, d1.y, d2.y, d1.x);
                        var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); //2(ad+bc)
                        var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                        euler = new float3(math.atan2(x1, x2), math.asin(y1), 0.0f);
                    }

                    break;
                }

                case math.RotationOrder.ZXY:
                {
                    var y1 = d2.y - d1.x;
                    if (y1 * y1 < CUTOFF)
                    {
                        var x1 = d2.x + d1.z;
                        var x2 = d3.y + d3.w - d3.x - d3.z;
                        var z1 = d2.z + d1.y;
                        var z2 = d3.z + d3.w - d3.x - d3.y;
                        euler = new float3(math.atan2(x1, x2), -math.asin(y1), math.atan2(z1, z2));
                    }
                    else //zxz
                    {
                        y1 = math.clamp(y1, -1.0f, 1.0f);
                        var abcd = new float4(d2.z, d1.y, d2.y, d1.x);
                        var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); //2(ad+bc)
                        var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                        euler = new float3(math.atan2(x1, x2), -math.asin(y1), 0.0f);
                    }

                    break;
                }

                case math.RotationOrder.YXZ:
                {
                    var y1 = d2.y + d1.x;
                    if (y1 * y1 < CUTOFF)
                    {
                        var x1 = -d2.z + d1.y;
                        var x2 = d3.z + d3.w - d3.x - d3.y;
                        var z1 = -d2.x + d1.z;
                        var z2 = d3.y + d3.w - d3.z - d3.x;
                        euler = new float3(math.atan2(x1, x2), math.asin(y1), math.atan2(z1, z2));
                    }
                    else //yzy
                    {
                        y1 = math.clamp(y1, -1.0f, 1.0f);
                        var abcd = new float4(d2.x, d1.z, d2.y, d1.x);
                        var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); //2(ad+bc)
                        var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                        euler = new float3(math.atan2(x1, x2), math.asin(y1), 0.0f);
                    }

                    break;
                }

                case math.RotationOrder.YZX:
                {
                    var y1 = d2.x - d1.z;
                    if (y1 * y1 < CUTOFF)
                    {
                        var x1 = d2.z + d1.y;
                        var x2 = d3.x + d3.w - d3.z - d3.y;
                        var z1 = d2.y + d1.x;
                        var z2 = d3.y + d3.w - d3.x - d3.z;
                        euler = new float3(math.atan2(x1, x2), -math.asin(y1), math.atan2(z1, z2));
                    }
                    else //yxy
                    {
                        y1 = math.clamp(y1, -1.0f, 1.0f);
                        var abcd = new float4(d2.x, d1.z, d2.y, d1.x);
                        var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); //2(ad+bc)
                        var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                        euler = new float3(math.atan2(x1, x2), -math.asin(y1), 0.0f);
                    }

                    break;
                }

                case math.RotationOrder.XZY:
                {
                    var y1 = d2.x + d1.z;
                    if (y1 * y1 < CUTOFF)
                    {
                        var x1 = -d2.y + d1.x;
                        var x2 = d3.y + d3.w - d3.z - d3.x;
                        var z1 = -d2.z + d1.y;
                        var z2 = d3.x + d3.w - d3.y - d3.z;
                        euler = new float3(math.atan2(x1, x2), math.asin(y1), math.atan2(z1, z2));
                    }
                    else //xyx
                    {
                        y1 = math.clamp(y1, -1.0f, 1.0f);
                        var abcd = new float4(d2.x, d1.z, d2.z, d1.y);
                        var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); //2(ad+bc)
                        var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                        euler = new float3(math.atan2(x1, x2), math.asin(y1), 0.0f);
                    }

                    break;
                }

                case math.RotationOrder.XYZ:
                {
                    var y1 = d2.z - d1.y;
                    if (y1 * y1 < CUTOFF)
                    {
                        var x1 = d2.y + d1.x;
                        var x2 = d3.z + d3.w - d3.y - d3.x;
                        var z1 = d2.x + d1.z;
                        var z2 = d3.x + d3.w - d3.y - d3.z;
                        euler = new float3(math.atan2(x1, x2), -math.asin(y1), math.atan2(z1, z2));
                    }
                    else   //xzx
                    {
                        y1 = math.clamp(y1, -1.0f, 1.0f);
                        var abcd = new float4(d2.z, d1.y, d2.x, d1.z);
                        var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); //2(ad+bc)
                        var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                        euler = new float3(math.atan2(x1, x2), -math.asin(y1), 0.0f);
                    }

                    break;
                }
            }

            return eulerReorderBack(euler, order);
        }

        static float3 eulerReorderBack(float3 euler, math.RotationOrder order)
        {
            switch (order)
            {
                case math.RotationOrder.XZY:
                    return euler.xzy;
                case math.RotationOrder.YZX:
                    return euler.zxy;
                case math.RotationOrder.YXZ:
                    return euler.yxz;
                case math.RotationOrder.ZXY:
                    return euler.yzx;
                case math.RotationOrder.ZYX:
                    return euler.zyx;
                case math.RotationOrder.XYZ:
                default:
                    return euler;
            }
        }

        #endregion

        /// <summary>
        /// Convert a quaternion orientation to Euler angles. Use this method to calculate angular
        /// velocity needed to achieve a target orientation.
        /// </summary>
        ///
        /// <param name="q">        An orientation. </param>
        /// <param name="order">    (Optional) The rotation order. </param>
        ///
        /// <returns>   The given quaternion converted Euler angles (float3). </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ToEulerAngles(this quaternion q, math.RotationOrder order = math.RotationOrder.XYZ)
        {
            return toEuler(q, order);
        }

        /// <summary>   Checks if the matrix has non-uniform scale. </summary>
        /// <param name="m">    The matrix. </param>
        /// <param name="eps">  Epsilon value used in the non-uniform scale determination. </param>
        /// <returns>   True if the matrix has non-uniform scale. </returns>
        public static bool HasNonUniformScale(this float4x4 m, float eps = 1e-5f)
        {
            var s = new float3(math.lengthsq(m.c0.xyz), math.lengthsq(m.c1.xyz), math.lengthsq(m.c2.xyz));
            return math.abs(math.cmin(s) - math.cmax(s)) > eps;
        }

        /// <summary> Checks if the matrix has non-identity scale. </summary>
        /// <param name="m">    The matrix. </param>
        /// <param name="eps">  Epsilon value used in the non-identity scale determination. </param>
        /// <returns>  True if the matrix has non-identity scale. </returns>
        public static bool HasNonIdentityScale(this float4x4 m, float eps = 1e-5f)
        {
            return math.lengthsq(m.DecomposeScale() - new float3(1f)) > eps;
        }

        /// <summary>   Checks if the matrix has shear. </summary>
        /// <param name="m">    The matrix. </param>
        /// <returns>   True if the matrix has shear. </returns>
        public static bool HasShear(this float4x4 m)
        {
            // scale each axis by abs of its max component in order to work with very large/small scales
            var rs0 = m.c0.xyz / math.max(math.cmax(math.abs(m.c0.xyz)), float.Epsilon);
            var rs1 = m.c1.xyz / math.max(math.cmax(math.abs(m.c1.xyz)), float.Epsilon);
            var rs2 = m.c2.xyz / math.max(math.cmax(math.abs(m.c2.xyz)), float.Epsilon);
            // verify all axes are orthogonal
            const float k_Zero = 1e-6f;
            return
                math.abs(math.dot(rs0, rs1)) > k_Zero ||
                math.abs(math.dot(rs0, rs2)) > k_Zero ||
                math.abs(math.dot(rs1, rs2)) > k_Zero;
        }

        /// <summary>
        /// Physics internally represents all rigid bodies in world space. If a static body is in a
        /// hierarchy, its local-to-world matrix must be decomposed when building the physics world. This
        /// method returns a world-space RigidTransform that would be decomposed for such a rigid body.
        /// </summary>
        ///
        /// <param name="localToWorld"> The local to world. </param>
        ///
        /// <returns>   A world-space RigidTransform as used by physics. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RigidTransform DecomposeRigidBodyTransform(in float4x4 localToWorld) =>
            new RigidTransform(DecomposeRigidBodyOrientation(localToWorld), localToWorld.c3.xyz);

        /// <summary>
        /// Physics internally represents all rigid bodies in world space. If a static body is in a
        /// hierarchy, its local-to-world matrix must be decomposed when building the physics world. This
        /// method returns a world-space orientation that would be decomposed for such a rigid body.
        /// </summary>
        ///
        /// <param name="localToWorld"> The local to world. </param>
        ///
        /// <returns>   A world-space orientation as used by physics. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion DecomposeRigidBodyOrientation(in float4x4 localToWorld) =>
            quaternion.LookRotationSafe(localToWorld.c2.xyz, localToWorld.c1.xyz);

        /// <summary>
        /// Obtain 3-dimensional scale vector of the provided 4x4 transformation matrix, the components
        /// of which represent the lengths of the three orthonormal basis vectors forming the 3x3 rotational sub-matrix,
        /// respectively.
        /// </summary>
        /// <param name="matrix">The 4x4 transformation matrix.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 DecomposeScale(this float4x4 matrix) =>
            new float3(math.length(matrix.c0.xyz), math.length(matrix.c1.xyz), math.length(matrix.c2.xyz));
    }
}
