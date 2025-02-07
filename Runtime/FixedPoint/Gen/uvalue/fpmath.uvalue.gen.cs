namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using um = Unity.Mathematics;

    public static partial class fpmath {
        
        /// <summary>Returns the absolute value of a uvalue value.</summary>
        /// <param name="x">Input value.</param>
        /// <returns>The absolute value of the input.</returns>
        [INLINE(256)]
        public static uvalue abs(uvalue x) { return new uvalue(um::math.max(uvalue.zero.value, x.value)); }

        /// <summary>Returns the result of clamping the value valueToClamp into the interval (inclusive) [lowerBound, upperBound], where valueToClamp, lowerBound and upperBound are uvalue values.</summary>
        /// <param name="valueToClamp">Input value to be clamped.</param>
        /// <param name="lowerBound">Lower bound of the interval.</param>
        /// <param name="upperBound">Upper bound of the interval.</param>
        /// <returns>The clamping of the input valueToClamp into the interval (inclusive) [lowerBound, upperBound].</returns>
        [INLINE(256)]
        public static uvalue clamp(uvalue valueToClamp, uvalue lowerBound, uvalue upperBound) { return new uvalue(um::math.max(lowerBound.value, um::math.min(upperBound.value, valueToClamp.value))); }

        /// <summary>Returns the result of linearly interpolating from start to end using the interpolation parameter t.</summary>
        /// <remarks>
        /// If the interpolation parameter is not in the range [0, 1], then this function extrapolates.
        /// </remarks>
        /// <param name="start">The start point, corresponding to the interpolation parameter value of 0.</param>
        /// <param name="end">The end point, corresponding to the interpolation parameter value of 1.</param>
        /// <param name="t">The interpolation parameter. May be a value outside the interval [0, 1].</param>
        /// <returns>The interpolation from start to end.</returns>
        [INLINE(256)]
        public static uvalue lerp(uvalue start, uvalue end, ucvalue t) { return start + t * (end - start); }
        
        [INLINE(256)]
        public static uvalue sqrt(uvalue value) {
            if (value.value == 0u) {
                throw new System.Exception();
            }
            var f = (sfloat)value.value;
            f /= uvalue.PRECISION;
            f = libm.sqrtf(f);
            f *= uvalue.PRECISION;
            return new uvalue((uint)f);
        }

        

    }

}