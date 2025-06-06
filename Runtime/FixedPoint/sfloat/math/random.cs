using System;
using System.Runtime.CompilerServices;
using static ME.BECS.FixedPoint.math;
using System.Diagnostics;
using tfloat = sfloat;

namespace ME.BECS.FixedPoint
{
    /// <summary>
    /// Random Number Generator based on xorshift.
    /// Designed for minimal state (32bits) to be easily embeddable into components.
    /// Core functionality is integer multiplication free to improve vectorization
    /// on less capable SIMD instruction sets.
    /// </summary>
    [Serializable]
    public partial struct Random
    {
        public uint state;

        /// <summary>
        /// Constructs a Random instance with a given seed value. The seed must be non-zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Random(uint seed)
        {
            state = seed;
            CheckInitState();
            NextState();
        }

        /// <summary>
        /// Constructs a <see cref="Random"/> instance with an index that gets hashed.  The index must not be uint.MaxValue.
        /// </summary>
        /// <remarks>
        /// Use this function when you expect to create several Random instances in a loop.
        /// </remarks>
        /// <example>
        /// <code>
        /// for (uint i = 0; i &lt; 4096; ++i)
        /// {
        ///     Random rand = Random.CreateFromIndex(i);
        ///
        ///     // Random numbers drawn from loop iteration j will be very different
        ///     // from every other loop iteration k.
        ///     rand.NextUInt();
        /// }
        /// </code>
        /// </example>
        /// <param name="index">An index that will be hashed for Random creation.  Must not be uint.MaxValue.</param>
        /// <returns><see cref="Random"/> created from an index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Random CreateFromIndex(uint index)
        {
            CheckIndexForHash(index);

            // Wang hash will hash 61 to zero but we want uint.MaxValue to hash to zero.  To make this happen
            // we must offset by 62.
            return new Random(WangHash(index + 62u));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint WangHash(uint n)
        {
            // https://gist.github.com/badboy/6267743#hash-function-construction-principles
            // Wang hash: this has the property that none of the outputs will
            // collide with each other, which is important for the purposes of
            // seeding a random number generator.  This was verified empirically
            // by checking all 2^32 uints.
            n = (n ^ 61u) ^ (n >> 16);
            n *= 9u;
            n = n ^ (n >> 4);
            n *= 0x27d4eb2du;
            n = n ^ (n >> 15);

            return n;
        }

        /// <summary>
        /// Initialized the state of the Random instance with a given seed value. The seed must be non-zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InitState(uint seed = 0x6E624EB7u)
        {
            state = seed;
            NextState();
        }

        /// <summary>Returns a uniformly random bool value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBool()
        {
            return (NextState() & 1) == 1;
        }

        /// <summary>Returns a uniformly random bool2 value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool2 NextBool2()
        {
            uint v = NextState();
            return (uint2(v) & uint2(1, 2)) == 0;
        }

        /// <summary>Returns a uniformly random bool3 value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool3 NextBool3()
        {
            uint v = NextState();
            return (uint3(v) & uint3(1, 2, 4)) == 0;
        }

        /// <summary>Returns a uniformly random bool4 value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool4 NextBool4()
        {
            uint v = NextState();
            return (uint4(v) & uint4(1, 2, 4, 8)) == 0;
        }


        /// <summary>Returns a uniformly random int value in the interval [-2147483647, 2147483647].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt()
        {
            return (int)NextState() ^ -2147483648;
        }

        /// <summary>Returns a uniformly random int2 value with all components in the interval [-2147483647, 2147483647].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 NextInt2()
        {
            return int2((int)NextState(), (int)NextState()) ^ -2147483648;
        }

        /// <summary>Returns a uniformly random int3 value with all components in the interval [-2147483647, 2147483647].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 NextInt3()
        {
            return int3((int)NextState(), (int)NextState(), (int)NextState()) ^ -2147483648;
        }

        /// <summary>Returns a uniformly random int4 value with all components in the interval [-2147483647, 2147483647].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int4 NextInt4()
        {
            return int4((int)NextState(), (int)NextState(), (int)NextState(), (int)NextState()) ^ -2147483648;
        }

        /// <summary>Returns a uniformly random int value in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt(int max)
        {
            CheckNextIntMax(max);
            return (int)((NextState() * (ulong)max) >> 32);
        }

        /// <summary>Returns a uniformly random int2 value with all components in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 NextInt2(int2 max)
        {
            CheckNextIntMax(max.x);
            CheckNextIntMax(max.y);
            return int2((int)(NextState() * (ulong)max.x >> 32),
                        (int)(NextState() * (ulong)max.y >> 32));
        }

        /// <summary>Returns a uniformly random int3 value with all components in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 NextInt3(int3 max)
        {
            CheckNextIntMax(max.x);
            CheckNextIntMax(max.y);
            CheckNextIntMax(max.z);
            return int3((int)(NextState() * (ulong)max.x >> 32),
                        (int)(NextState() * (ulong)max.y >> 32),
                        (int)(NextState() * (ulong)max.z >> 32));
        }

        /// <summary>Returns a uniformly random int4 value with all components in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int4 NextInt4(int4 max)
        {
            CheckNextIntMax(max.x);
            CheckNextIntMax(max.y);
            CheckNextIntMax(max.z);
            CheckNextIntMax(max.w);
            return int4((int)(NextState() * (ulong)max.x >> 32),
                        (int)(NextState() * (ulong)max.y >> 32),
                        (int)(NextState() * (ulong)max.z >> 32),
                        (int)(NextState() * (ulong)max.w >> 32));
        }

        /// <summary>Returns a uniformly random int value in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt(int min, int max)
        {
            CheckNextIntMinMax(min, max);
            uint range = (uint)(max - min);
            return (int)(NextState() * (ulong)range >> 32) + min;
        }

        /// <summary>Returns a uniformly random int2 value with all components in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 NextInt2(int2 min, int2 max)
        {
            CheckNextIntMinMax(min.x, max.x);
            CheckNextIntMinMax(min.y, max.y);
            uint2 range = (uint2)(max - min);
            return int2((int)(NextState() * (ulong)range.x >> 32),
                        (int)(NextState() * (ulong)range.y >> 32)) + min;
        }

        /// <summary>Returns a uniformly random int3 value with all components in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 NextInt3(int3 min, int3 max)
        {
            CheckNextIntMinMax(min.x, max.x);
            CheckNextIntMinMax(min.y, max.y);
            CheckNextIntMinMax(min.z, max.z);
            uint3 range = (uint3)(max - min);
            return int3((int)(NextState() * (ulong)range.x >> 32),
                        (int)(NextState() * (ulong)range.y >> 32),
                        (int)(NextState() * (ulong)range.z >> 32)) + min;
        }

        /// <summary>Returns a uniformly random int4 value with all components in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int4 NextInt4(int4 min, int4 max)
        {
            CheckNextIntMinMax(min.x, max.x);
            CheckNextIntMinMax(min.y, max.y);
            CheckNextIntMinMax(min.z, max.z);
            CheckNextIntMinMax(min.w, max.w);
            uint4 range = (uint4)(max - min);
            return int4((int)(NextState() * (ulong)range.x >> 32),
                        (int)(NextState() * (ulong)range.y >> 32),
                        (int)(NextState() * (ulong)range.z >> 32),
                        (int)(NextState() * (ulong)range.w >> 32)) + min;
        }

        /// <summary>Returns a uniformly random uint value in the interval [0, 4294967294].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt()
        {
            return NextState() - 1u;
        }

        /// <summary>Returns a uniformly random uint2 value with all components in the interval [0, 4294967294].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint2 NextUInt2()
        {
            return uint2(NextState(), NextState()) - 1u;
        }

        /// <summary>Returns a uniformly random uint3 value with all components in the interval [0, 4294967294].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint3 NextUInt3()
        {
            return uint3(NextState(), NextState(), NextState()) - 1u;
        }

        /// <summary>Returns a uniformly random uint4 value with all components in the interval [0, 4294967294].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint4 NextUInt4()
        {
            return uint4(NextState(), NextState(), NextState(), NextState()) - 1u;
        }


        /// <summary>Returns a uniformly random uint value in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt(uint max)
        {
            return (uint)((NextState() * (ulong)max) >> 32);
        }

        /// <summary>Returns a uniformly random uint2 value with all components in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint2 NextUInt2(uint2 max)
        {
            return uint2(   (uint)(NextState() * (ulong)max.x >> 32),
                            (uint)(NextState() * (ulong)max.y >> 32));
        }

        /// <summary>Returns a uniformly random uint3 value with all components in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint3 NextUInt3(uint3 max)
        {
            return uint3(   (uint)(NextState() * (ulong)max.x >> 32),
                            (uint)(NextState() * (ulong)max.y >> 32),
                            (uint)(NextState() * (ulong)max.z >> 32));
        }

        /// <summary>Returns a uniformly random uint4 value with all components in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint4 NextUInt4(uint4 max)
        {
            return uint4(   (uint)(NextState() * (ulong)max.x >> 32),
                            (uint)(NextState() * (ulong)max.y >> 32),
                            (uint)(NextState() * (ulong)max.z >> 32),
                            (uint)(NextState() * (ulong)max.w >> 32));
        }

        /// <summary>Returns a uniformly random uint value in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt(uint min, uint max)
        {
            CheckNextUIntMinMax(min, max);
            uint range = max - min;
            return (uint)(NextState() * (ulong)range >> 32) + min;
        }

        /// <summary>Returns a uniformly random uint2 value with all components in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint2 NextUInt2(uint2 min, uint2 max)
        {
            CheckNextUIntMinMax(min.x, max.x);
            CheckNextUIntMinMax(min.y, max.y);
            uint2 range = max - min;
            return uint2((uint)(NextState() * (ulong)range.x >> 32),
                         (uint)(NextState() * (ulong)range.y >> 32)) + min;
        }

        /// <summary>Returns a uniformly random uint3 value with all components in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint3 NextUInt3(uint3 min, uint3 max)
        {
            CheckNextUIntMinMax(min.x, max.x);
            CheckNextUIntMinMax(min.y, max.y);
            CheckNextUIntMinMax(min.z, max.z);
            uint3 range = max - min;
            return uint3((uint)(NextState() * (ulong)range.x >> 32),
                         (uint)(NextState() * (ulong)range.y >> 32),
                         (uint)(NextState() * (ulong)range.z >> 32)) + min;
        }

        /// <summary>Returns a uniformly random uint4 value with all components in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint4 NextUInt4(uint4 min, uint4 max)
        {
            CheckNextUIntMinMax(min.x, max.x);
            CheckNextUIntMinMax(min.y, max.y);
            CheckNextUIntMinMax(min.z, max.z);
            CheckNextUIntMinMax(min.w, max.w);
            uint4 range = (uint4)(max - min);
            return uint4((uint)(NextState() * (ulong)range.x >> 32),
                         (uint)(NextState() * (ulong)range.y >> 32),
                         (uint)(NextState() * (ulong)range.z >> 32),
                         (uint)(NextState() * (ulong)range.w >> 32)) + min;
        }

        /// <summary>Returns a uniformly random float value in the interval [0, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sfloat NextFloat()
        {
            return asfloat(0x3f800000 | (NextState() >> 9)) - 1.0f;
        }

        /// <summary>Returns a uniformly random float2 value with all components in the interval [0, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 NextFloat2()
        {
            return asfloat(0x3f800000 | (uint2(NextState(), NextState()) >> 9)) - 1.0f;
        }

        /// <summary>Returns a uniformly random float3 value with all components in the interval [0, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 NextFloat3()
        {
            return asfloat(0x3f800000 | (uint3(NextState(), NextState(), NextState()) >> 9)) - 1.0f;
        }

        /// <summary>Returns a uniformly random float4 value with all components in the interval [0, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float4 NextFloat4()
        {
            return asfloat(0x3f800000 | (uint4(NextState(), NextState(), NextState(), NextState()) >> 9)) - 1.0f;
        }


        /// <summary>Returns a uniformly random float value in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sfloat NextFloat(tfloat max) { return NextFloat() * max; }

        /// <summary>Returns a uniformly random float2 value with all components in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 NextFloat2(float2 max) { return NextFloat2() * max; }

        /// <summary>Returns a uniformly random float3 value with all components in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 NextFloat3(float3 max) { return NextFloat3() * max; }

        /// <summary>Returns a uniformly random float4 value with all components in the interval [0, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float4 NextFloat4(float4 max) { return NextFloat4() * max; }


        /// <summary>Returns a uniformly random float value in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sfloat NextFloat(tfloat min, tfloat max) { return NextFloat() * (max - min) + min; }

        /// <summary>Returns a uniformly random float2 value with all components in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 NextFloat2(float2 min, float2 max) { return NextFloat2() * (max - min) + min; }

        /// <summary>Returns a uniformly random float3 value with all components in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 NextFloat3(float3 min, float3 max) { return NextFloat3() * (max - min) + min; }

        /// <summary>Returns a uniformly random float4 value with all components in the interval [min, max).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float4 NextFloat4(float4 min, float4 max) { return NextFloat4() * (max - min) + min; }

        
        /// <summary>Returns a unit length float2 vector representing a uniformly random 2D direction.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 NextFloat2Direction()
        {
            sfloat angle = NextFloat() * PI * 2.0f;
            sfloat s, c;
            sincos(angle, out s, out c);
            return float2(c, s);
        }

        /// <summary>Returns a unit length float3 vector representing a uniformly random 3D direction.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 NextFloat3Direction()
        {
            float2 rnd = NextFloat2();
            sfloat z = rnd.x * 2.0f - 1.0f;
            sfloat r = sqrt(max(1.0f - z * z, 0.0f));
            sfloat angle = rnd.y * PI * 2.0f;
            sfloat s, c;
            sincos(angle, out s, out c);
            return float3(c*r, s*r, z);
        }

        /// <summary>Returns a unit length quaternion representing a uniformly 3D rotation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public quaternion NextQuaternionRotation()
        {
            float3 rnd = NextFloat3(float3(2.0f * PI, 2.0f * PI, 1.0f));
            sfloat u1 = rnd.z;
            float2 theta_rho = rnd.xy;

            sfloat i = sqrt(1.0f - u1);
            sfloat j = sqrt(u1);

            float2 sin_theta_rho;
            float2 cos_theta_rho;
            sincos(theta_rho, out sin_theta_rho, out cos_theta_rho);

            quaternion q = quaternion(i * sin_theta_rho.x, i * cos_theta_rho.x, j * sin_theta_rho.y, j * cos_theta_rho.y);
            return quaternion(select(q.value, -q.value, q.value.w < 0.0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NextState()
        {
            CheckState();
            uint t = state;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return t;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckInitState()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (state == 0)
                throw new System.ArgumentException("Seed must be non-zero");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckIndexForHash(uint index)
        {
            if (index == uint.MaxValue)
                throw new System.ArgumentException("Index must not be uint.MaxValue");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckState()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if(state == 0)
                throw new System.ArgumentException("Invalid state 0. Random object has not been properly initialized");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckNextIntMax(int max)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (max < 0)
                throw new System.ArgumentException("max must be positive");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckNextIntMinMax(int min, int max)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (min > max)
                throw new System.ArgumentException("min must be less than or equal to max");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckNextUIntMinMax(uint min, uint max)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (min > max)
                throw new System.ArgumentException("min must be less than or equal to max");
#endif
        }

    }
}
