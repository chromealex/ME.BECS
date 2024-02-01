namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    public static class Math {

        [INLINE(256)]
        public static float3 MoveTowards(float3 current, float3 target, float maxDistanceDelta) {
            var delta = target - current;
            var sqDist = math.dot(delta, delta);
            if (sqDist <= maxDistanceDelta * maxDistanceDelta) return target;
            var dist = math.sqrt(sqDist);
            return current + delta / dist * maxDistanceDelta;
        }

        /// <summary>
        /// Converts quaternion representation to euler
        /// </summary>
        [INLINE(256)]
        public static float3 ToEuler(this quaternion quaternion) {
            float4 q = quaternion.value;
            double3 res;
 
            double sinr_cosp = +2.0 * (q.w * q.x + q.y * q.z);
            double cosr_cosp = +1.0 - 2.0 * (q.x * q.x + q.y * q.y);
            res.x = math.atan2(sinr_cosp, cosr_cosp);
 
            double sinp = +2.0 * (q.w * q.y - q.z * q.x);
            if (math.abs(sinp) >= 1) {
                res.y = math.PI / 2 * math.sign(sinp);
            } else {
                res.y = math.asin(sinp);
            }
 
            double siny_cosp = +2.0 * (q.w * q.z + q.x * q.y);
            double cosy_cosp = +1.0 - 2.0 * (q.y * q.y + q.z * q.z);
            res.z = math.atan2(siny_cosp, cosy_cosp);
 
            return (float3) res;
        }

        [INLINE(256)]
        public static bool IsInPolygon(in float3 position, in float3 p1, in float3 p2, in float3 p3, in float3 p4) {
            static float3 Point(in float3 p1, in float3 p2, in float3 p3, in float3 p4, int i) {
                switch (i) {
                    case 0: return p1;
                    case 1: return p2;
                    case 2: return p3;
                    case 3: return p4;
                }
                return p1;
            }
            bool result = false;
            int j = 3;
            for (int i = 0; i < 4; ++i) {

                var point1 = Point(p1, p2, p3, p4, i);
                var point2 = Point(p1, p2, p3, p4, j);
                if (point1.z < position.z && point2.z >= position.z || 
                    point2.z < position.z && point1.z >= position.z)
                {
                    if (point1.x + (position.z - point1.z) /
                        (point2.z - point1.z) *
                        (point2.x - point1.x) < position.x)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

    }

}