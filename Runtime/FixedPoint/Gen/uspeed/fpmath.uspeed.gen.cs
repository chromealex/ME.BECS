namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using um = Unity.Mathematics;

    public static partial class fpmath {
        
        /// <summary>Returns the absolute value of a uspeed value.</summary>
        /// <param name="x">Input value.</param>
        /// <returns>The absolute value of the input.</returns>
        [INLINE(256)]
        public static uspeed abs(uspeed x) { return new uspeed(um::math.max(uspeed.zero.value, x.value)); }

        /// <summary>Returns the result of clamping the value valueToClamp into the interval (inclusive) [lowerBound, upperBound], where valueToClamp, lowerBound and upperBound are uspeed values.</summary>
        /// <param name="valueToClamp">Input value to be clamped.</param>
        /// <param name="lowerBound">Lower bound of the interval.</param>
        /// <param name="upperBound">Upper bound of the interval.</param>
        /// <returns>The clamping of the input valueToClamp into the interval (inclusive) [lowerBound, upperBound].</returns>
        [INLINE(256)]
        public static uspeed clamp(uspeed valueToClamp, uspeed lowerBound, uspeed upperBound) { return new uspeed(um::math.max(lowerBound.value, um::math.min(upperBound.value, valueToClamp.value))); }

        /// <summary>Returns the result of linearly interpolating from start to end using the interpolation parameter t.</summary>
        /// <remarks>
        /// If the interpolation parameter is not in the range [0, 1], then this function extrapolates.
        /// </remarks>
        /// <param name="start">The start point, corresponding to the interpolation parameter value of 0.</param>
        /// <param name="end">The end point, corresponding to the interpolation parameter value of 1.</param>
        /// <param name="t">The interpolation parameter. May be a value outside the interval [0, 1].</param>
        /// <returns>The interpolation from start to end.</returns>
        [INLINE(256)]
        public static uspeed lerp(uspeed start, uspeed end, ucvalue t) { return start + t * (end - start); }
        
        [INLINE(256)]
        public static uspeed sqrt(uspeed value) {
            if (value.value == 0u) {
                throw new System.Exception();
            }
            var f = (sfloat)value.value;
            f /= uspeed.PRECISION;
            f = libm.sqrtf(f);
            f *= uspeed.PRECISION;
            return new uspeed((uint)f);
        }

        

    }

}