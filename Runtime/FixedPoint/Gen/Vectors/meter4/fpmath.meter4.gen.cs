namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using um = Unity.Mathematics;

    public static partial class fpmath {
        
        /// <summary>Returns the absolute value of a meter4 value.</summary>
        /// <param name="x">Input value.</param>
        /// <returns>The absolute value of the input.</returns>
        [INLINE(256)]
        public static meter4 abs(meter4 x) { return new meter4(um::math.max(0, x.x.value), um::math.max(0, x.y.value), um::math.max(0, x.z.value), um::math.max(0, x.w.value)); }

        /// <summary>Returns the result of clamping the value valueToClamp into the interval (inclusive) [lowerBound, upperBound], where valueToClamp, lowerBound and upperBound are meter4 values.</summary>
        /// <param name="valueToClamp">Input value to be clamped.</param>
        /// <param name="lowerBound">Lower bound of the interval.</param>
        /// <param name="upperBound">Upper bound of the interval.</param>
        /// <returns>The clamping of the input valueToClamp into the interval (inclusive) [lowerBound, upperBound].</returns>
        [INLINE(256)]
        public static meter4 clamp(meter4 valueToClamp, meter4 lowerBound, meter4 upperBound) { return new meter4(um::math.max(lowerBound.x.value, um::math.min(upperBound.x.value, valueToClamp.x.value)),
                                                                                                                       um::math.max(lowerBound.y.value, um::math.min(upperBound.y.value, valueToClamp.y.value)),
                                                                                                                       um::math.max(lowerBound.z.value, um::math.min(upperBound.z.value, valueToClamp.z.value)),
                                                                                                                       um::math.max(lowerBound.w.value, um::math.min(upperBound.w.value, valueToClamp.w.value))); }

        /// <summary>Returns the result of linearly interpolating from start to end using the interpolation parameter t.</summary>
        /// <remarks>
        /// If the interpolation parameter is not in the range [0, 1], then this function extrapolates.
        /// </remarks>
        /// <param name="start">The start point, corresponding to the interpolation parameter value of 0.</param>
        /// <param name="end">The end point, corresponding to the interpolation parameter value of 1.</param>
        /// <param name="t">The interpolation parameter. May be a value outside the interval [0, 1].</param>
        /// <returns>The interpolation from start to end.</returns>
        [INLINE(256)]
        public static meter4 lerp(meter4 start, meter4 end, ucvalue t) { return start + t * (end - start); }
        
        [INLINE(256)]
        public static meter4 sqrt(meter4 value) {
            return new meter4(fpmath.sqrt(value.x).value, fpmath.sqrt(value.y).value, fpmath.sqrt(value.z).value, fpmath.sqrt(value.w).value);
        }

    }

}