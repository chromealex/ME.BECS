#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Defines a sector-shaped spatial region.
    /// </summary>
    [System.Serializable]
    public struct Sector {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static Sector Default => new Sector() {
            rangeSqr = (tfloat)(1f),
            sector = (tfloat)(360f),
        };

        /// <summary>
        /// Squared range used for distance comparisons without a square root.
        /// </summary>
        public tfloat rangeSqr;
        /// <summary>
        /// Minimum range sqr.
        /// </summary>
        public tfloat minRangeSqr;
        /// <summary>
        /// Sector bounds used by the spatial query.
        /// </summary>
        [UnityEngine.RangeAttribute(0f, 360f)]
        public tfloat sector;

        /// <summary>
        /// Interpolates between the supplied endpoints using the given interpolation factor.
        /// </summary>
        [INLINE(256)]
        public static Sector Lerp(in Sector a, in Sector b, tfloat t) {
            var result = new Sector {
                rangeSqr = math.lerp(a.rangeSqr, b.rangeSqr, t),
                minRangeSqr = math.lerp(a.minRangeSqr, b.minRangeSqr, t),
                sector = math.lerp(a.sector, b.sector, t),
            };
            return result;
        }

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsValid() {
            return this.sector > 0 && this.sector < 360;
        }

    }

}