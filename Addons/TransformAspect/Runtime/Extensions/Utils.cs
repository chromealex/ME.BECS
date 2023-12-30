namespace ME.BECS.TransformAspect {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;

    public static class MatrixUtils {

        [INLINE(256)]
        public static float3 GetPosition(in float4x4 matrix) {
            return matrix.c3.xyz;
        }
 
        [INLINE(256)]
        public static quaternion GetRotation(in float4x4 matrix) {
            float3 forward = matrix.c2.xyz;
            float3 upwards = matrix.c1.xyz;
            if (forward.x * forward.y * forward.z == 0f && upwards.x * upwards.y * upwards.z == 0f) return quaternion.identity;
            return quaternion.LookRotation(forward, upwards);
        }
        
        [INLINE(256)]
        public static float3 GetScale(in float4x4 matrix) {
            float3 scale;
            scale.x = math.length(matrix.c0);
            scale.y = math.length(matrix.c1);
            scale.z = math.length(matrix.c2);
            return scale;
        }

    }

}