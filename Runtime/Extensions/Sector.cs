namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public struct Sector {

        public static Sector Default => new Sector() {
            rangeSqr = 0f,
            sector = 360f,
        };

        public float rangeSqr;
        public float minRangeSqr;
        [UnityEngine.RangeAttribute(0f, 360f)]
        public float sector;

        [INLINE(256)]
        public static Sector Lerp(in Sector a, in Sector b, float t) {
            var result = new Sector();
            result.rangeSqr = math.lerp(a.rangeSqr, b.rangeSqr, t);
            result.minRangeSqr = math.lerp(a.minRangeSqr, b.minRangeSqr, t);
            result.sector = math.lerp(a.sector, b.sector, t);
            return result;
        }

        [INLINE(256)]
        public bool IsValid() {
            return this.sector > 0f && this.sector < 360f;
        }

    }

}