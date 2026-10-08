namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Stores an unsigned fixed-point distance value.
    /// </summary>
    public partial struct umeter {

        /// <summary>
        /// Creates the value from s float.
        /// </summary>
        [INLINE(256)]
        public static umeter FromSFloat(sfloat value) {
            var ms = new umeter((uint)(value * PRECISION));
            return ms;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static umeter operator *(ucvalue value1, umeter value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static umeter operator *(umeter value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static umeter operator *(umeter value1, usec value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Divides the left operand by the right operand.
        /// </summary>
        [INLINE(256)]
        public static umeter operator /(umeter value1, usec value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

    }

    /// <summary>
    /// Stores a signed fixed-point distance value.
    /// </summary>
    public partial struct meter {

        /// <summary>
        /// Creates the value from s float.
        /// </summary>
        [INLINE(256)]
        public static meter FromSFloat(sfloat value) {
            var ms = new meter((int)(value * PRECISION));
            return ms;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static meter operator *(ucvalue value1, meter value2) {
            value2.value *= (int)value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static meter operator *(meter value1, ucvalue value2) {
            value1.value *= (int)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static meter operator *(meter value1, usec value2) {
            value1.value *= (int)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Divides the left operand by the right operand.
        /// </summary>
        [INLINE(256)]
        public static meter operator /(meter value1, usec value2) {
            value1.value /= (int)value2.value;
            value1.value *= PRECISION;
            return value1;
        }

    }

    /// <summary>
    /// Stores a 2-component fixed-point distance vector.
    /// </summary>
    public partial struct umeter2 {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static umeter2 operator *(ucvalue value1, umeter2 value2) {
            value2.x *= value1;
            value2.y *= value1;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static umeter2 operator *(umeter2 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            return value1;
        }

    }

    /// <summary>
    /// Stores a 3-component fixed-point distance vector.
    /// </summary>
    public partial struct umeter3 {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static umeter3 operator *(ucvalue value1, umeter3 value2) {
            value2.x *= value1;
            value2.y *= value1;
            value2.z *= value1;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static umeter3 operator *(umeter3 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            return value1;
        }

    }

    /// <summary>
    /// Stores a 4-component fixed-point distance vector.
    /// </summary>
    public partial struct umeter4 {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static umeter4 operator *(ucvalue value1, umeter4 value2) {
            value2.x *= value1;
            value2.y *= value1;
            value2.z *= value1;
            value2.w *= value1;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static umeter4 operator *(umeter4 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            value1.w *= value2;
            return value1;
        }

    }

    /// <summary>
    /// Stores a 2-component fixed-point distance vector.
    /// </summary>
    public partial struct meter2 {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static meter2 operator *(ucvalue value1, meter2 value2) {
            value2.x *= value1;
            value2.y *= value1;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static meter2 operator *(meter2 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            return value1;
        }

    }

    /// <summary>
    /// Stores a 3-component fixed-point distance vector.
    /// </summary>
    public partial struct meter3 {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static meter3 operator *(ucvalue value1, meter3 value2) {
            value2.x *= value1;
            value2.y *= value1;
            value2.z *= value1;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static meter3 operator *(meter3 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            return value1;
        }

        /// <summary>
        /// Converts the supplied value to <c>FixedPoint.float3</c>.
        /// </summary>
        [INLINE(256)]
        public static implicit operator FixedPoint.float3(meter3 value) {
            var val = new FixedPoint.float3((sfloat)value.x.value, (sfloat)value.y.value, (sfloat)value.z.value);
            val /= (sfloat)meter.PRECISION;
            return new FixedPoint.float3(val);
        }

        /// <summary>
        /// Converts the supplied value to <c>meter3</c>.
        /// </summary>
        [INLINE(256)]
        public static implicit operator meter3(FixedPoint.float3 value) {
            return meter3.FromSFloat(value);
        }

    }

    /// <summary>
    /// Stores a 4-component fixed-point distance vector.
    /// </summary>
    public partial struct meter4 {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static meter4 operator *(ucvalue value1, meter4 value2) {
            value2.x *= value1;
            value2.y *= value1;
            value2.z *= value1;
            value2.w *= value1;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static meter4 operator *(meter4 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            value1.w *= value2;
            return value1;
        }

    }

    /// <summary>
    /// Stores an unsigned fixed-point angle value.
    /// </summary>
    public partial struct uangle {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uangle operator *(ucvalue value1, uangle value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uangle operator *(uangle value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uangle operator *(uangle value1, usec value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Divides the left operand by the right operand.
        /// </summary>
        [INLINE(256)]
        public static uangle operator /(uangle value1, usec value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

    }

    /// <summary>
    /// Stores an unsigned fixed-point time value.
    /// </summary>
    public partial struct usec {

        /// <summary>
        /// Converts the supplied value to <c>ucvalue</c>.
        /// </summary>
        [INLINE(256)]
        public static implicit operator ucvalue(usec value) {
            return new ucvalue(fpmath.clamp(value, usec.zero, usec.oneSecond).value / PRECISION * ucvalue.PRECISION);
        }

        /// <summary>
        /// Subtracts the operands or negates a single operand.
        /// </summary>
        [INLINE(256)]
        public static usec operator -(usec value1, uint value2) {
            return value1 - new usec(value2);
        }

        /// <summary>
        /// Adds the operands.
        /// </summary>
        [INLINE(256)]
        public static usec operator +(usec value1, uint value2) {
            return value1 + new usec(value2);
        }

        /// <summary>
        /// Tests whether the left operand is less than the right operand.
        /// </summary>
        [INLINE(256)]
        public static bool operator <(usec value1, uint value2) {
            return value1.value < value2;
        }

        /// <summary>
        /// Tests whether the left operand is greater than the right operand.
        /// </summary>
        [INLINE(256)]
        public static bool operator >(usec value1, uint value2) {
            return value1.value > value2;
        }

        /// <summary>
        /// Tests whether the left operand is less than or equal to the right operand.
        /// </summary>
        [INLINE(256)]
        public static bool operator <=(usec value1, uint value2) {
            return value1.value <= value2;
        }

        /// <summary>
        /// Tests whether the left operand is greater than or equal to the right operand.
        /// </summary>
        [INLINE(256)]
        public static bool operator >=(usec value1, uint value2) {
            return value1.value >= value2;
        }

        /// <summary>
        /// Tests equality of the operands.
        /// </summary>
        [INLINE(256)]
        public static bool operator ==(usec value1, uint value2) {
            return value1.value == value2;
        }

        /// <summary>
        /// Tests whether the operands differ.
        /// </summary>
        [INLINE(256)]
        public static bool operator !=(usec value1, uint value2) {
            return !(value1 == value2);
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static usec operator *(ucvalue value1, usec value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static usec operator *(usec value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

    }

    /// <summary>
    /// Stores an unsigned fixed-point speed value.
    /// </summary>
    public partial struct uspeed {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uspeed operator *(uint value1, uspeed value2) {
            return new uspeed(value1 * value2.value);
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uspeed operator *(ucvalue value1, uspeed value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uspeed operator *(uspeed value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uspeed operator *(uspeed value1, usec value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Divides the left operand by the right operand.
        /// </summary>
        [INLINE(256)]
        public static uspeed operator /(uspeed value1, usec value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static usec operator *(usec value1, uspeed value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

    }

    /// <summary>
    /// Stores an unsigned fixed-point scalar value.
    /// </summary>
    public partial struct uvalue {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uvalue operator *(ucvalue value1, uvalue value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uvalue operator *(uvalue value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static uvalue operator *(uvalue value1, usec value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Divides the left operand by the right operand.
        /// </summary>
        [INLINE(256)]
        public static uvalue operator /(uvalue value1, usec value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static usec operator *(usec value1, uvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

    }

    /// <summary>
    /// Stores a signed fixed-point scalar value.
    /// </summary>
    public partial struct svalue {

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static svalue operator *(ucvalue value1, svalue value2) {
            value2.value *= (int)value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static svalue operator *(svalue value1, ucvalue value2) {
            value1.value *= (int)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static svalue operator *(svalue value1, usec value2) {
            value1.value *= (int)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        /// <summary>
        /// Divides the left operand by the right operand.
        /// </summary>
        [INLINE(256)]
        public static svalue operator /(svalue value1, usec value2) {
            value1.value /= (int)value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        /// <summary>
        /// Multiplies the operands.
        /// </summary>
        [INLINE(256)]
        public static usec operator *(usec value1, svalue value2) {
            value1.value *= (uint)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

    }

}