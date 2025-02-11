#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    [System.Serializable]
    public struct Sector {

        public static Sector Default => new Sector() {
            rangeSqr = (tfloat)(0f),
            sector = (tfloat)(360f),
        };

        public tfloat rangeSqr;
        public tfloat minRangeSqr;
        [UnityEngine.RangeAttribute(0f, 360f)]
        public tfloat sector;

        [INLINE(256)]
        public static Sector Lerp(in Sector a, in Sector b, tfloat t) {
            var result = new Sector {
                rangeSqr = math.lerp(a.rangeSqr, b.rangeSqr, t),
                minRangeSqr = math.lerp(a.minRangeSqr, b.minRangeSqr, t),
                sector = math.lerp(a.sector, b.sector, t),
            };
            return result;
        }

        [INLINE(256)]
        public bool IsValid() {
            return this.sector > 0 && this.sector < 360;
        }

    }

}