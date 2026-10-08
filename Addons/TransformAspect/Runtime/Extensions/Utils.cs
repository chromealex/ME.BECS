#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Transforms {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides helper operations for matrix.
    /// </summary>
    [IgnoreProfiler]
    public static class MatrixUtils {

        /// <summary>
        /// Creates the value from to rotation.
        /// </summary>
        [INLINE(256)]
        public static quaternion FromToRotation(float3 from, float3 to) {
            return quaternion.AxisAngle(angle: math.acos(math.clamp(math.dot(from, math.normalizesafe(to)), -1f, 1f)), axis: math.normalizesafe(math.cross(from, to)));
        }

        /// <summary>
        /// Creates the value from to rotation safe.
        /// </summary>
        [INLINE(256)]
        public static quaternion FromToRotationSafe(float3 from, float3 to) {
            return quaternion.AxisAngle(angle: math.acos(math.clamp(math.dot(math.normalizesafe(from), math.normalizesafe(to)), -1f, 1f)), axis: math.normalizesafe(math.cross(from, to)));
        }

        /// <summary>
        /// Returns position.
        /// </summary>
        [INLINE(256)]
        public static float3 GetPosition(in float4x4 matrix) {
            return matrix.c3.xyz;
        }
 
        /// <summary>
        /// Returns rotation.
        /// </summary>
        [INLINE(256)]
        public static quaternion GetRotation(in float4x4 matrix) {
            return quaternion.LookRotationSafe(matrix.c2.xyz, matrix.c1.xyz);
        }
        
        /// <summary>
        /// Returns scale.
        /// </summary>
        [INLINE(256)]
        public static float3 GetScale(in float4x4 matrix) {
            return new float3(
                math.length(matrix.c0),
                math.length(matrix.c1),
                math.length(matrix.c2)
            );
        }

    }

}