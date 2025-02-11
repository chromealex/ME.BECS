namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public partial struct umeter {

        [INLINE(256)]
        public static umeter operator *(ucvalue value1, umeter value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        [INLINE(256)]
        public static umeter operator *(umeter value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static umeter operator *(umeter value1, usec value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static umeter operator /(umeter value1, usec value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

    }

    public partial struct meter {

        [INLINE(256)]
        public static meter operator *(ucvalue value1, meter value2) {
            value2.value *= (int)value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        [INLINE(256)]
        public static meter operator *(meter value1, ucvalue value2) {
            value1.value *= (int)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static meter operator *(meter value1, usec value2) {
            value1.value *= (int)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static meter operator /(meter value1, usec value2) {
            value1.value /= (int)value2.value;
            value1.value *= PRECISION;
            return value1;
        }

    }

    public partial struct umeter2 {

        [INLINE(256)]
        public static umeter2 operator *(ucvalue value1, umeter2 value2) {
            value2.x *= value1;
            value2.y *= value1;
            return value2;
        }

        [INLINE(256)]
        public static umeter2 operator *(umeter2 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            return value1;
        }

    }

    public partial struct umeter3 {

        [INLINE(256)]
        public static umeter3 operator *(ucvalue value1, umeter3 value2) {
            value2.x *= value1;
            value2.y *= value1;
            value2.z *= value1;
            return value2;
        }

        [INLINE(256)]
        public static umeter3 operator *(umeter3 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            return value1;
        }

    }

    public partial struct umeter4 {

        [INLINE(256)]
        public static umeter4 operator *(ucvalue value1, umeter4 value2) {
            value2.x *= value1;
            value2.y *= value1;
            value2.z *= value1;
            value2.w *= value1;
            return value2;
        }

        [INLINE(256)]
        public static umeter4 operator *(umeter4 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            value1.w *= value2;
            return value1;
        }

    }

    public partial struct meter2 {

        [INLINE(256)]
        public static meter2 operator *(ucvalue value1, meter2 value2) {
            value2.x *= value1;
            value2.y *= value1;
            return value2;
        }

        [INLINE(256)]
        public static meter2 operator *(meter2 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            return value1;
        }

    }

    public partial struct meter3 {

        [INLINE(256)]
        public static meter3 operator *(ucvalue value1, meter3 value2) {
            value2.x *= value1;
            value2.y *= value1;
            value2.z *= value1;
            return value2;
        }

        [INLINE(256)]
        public static meter3 operator *(meter3 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            return value1;
        }

        [INLINE(256)]
        public static implicit operator FixedPoint.float3(meter3 value) {
            var val = new FixedPoint.float3((sfloat)value.x.value, (sfloat)value.y.value, (sfloat)value.z.value);
            val /= (sfloat)meter.PRECISION;
            return new FixedPoint.float3(val);
        }

        [INLINE(256)]
        public static implicit operator meter3(FixedPoint.float3 value) {
            return meter3.FromSFloat(value);
        }

    }

    public partial struct meter4 {

        [INLINE(256)]
        public static meter4 operator *(ucvalue value1, meter4 value2) {
            value2.x *= value1;
            value2.y *= value1;
            value2.z *= value1;
            value2.w *= value1;
            return value2;
        }

        [INLINE(256)]
        public static meter4 operator *(meter4 value1, ucvalue value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            value1.w *= value2;
            return value1;
        }

    }

    public partial struct uangle {

        [INLINE(256)]
        public static uangle operator *(ucvalue value1, uangle value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        [INLINE(256)]
        public static uangle operator *(uangle value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static uangle operator *(uangle value1, usec value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static uangle operator /(uangle value1, usec value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

    }

    public partial struct usec {

        [INLINE(256)]
        public static implicit operator ucvalue(usec value) {
            return new ucvalue(fpmath.clamp(value, usec.zero, usec.oneSecond).value / PRECISION * ucvalue.PRECISION);
        }

        [INLINE(256)]
        public static usec operator *(ucvalue value1, usec value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        [INLINE(256)]
        public static usec operator *(usec value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

    }

    public partial struct uspeed {

        [INLINE(256)]
        public static uspeed operator *(ucvalue value1, uspeed value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        [INLINE(256)]
        public static uspeed operator *(uspeed value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static uspeed operator *(uspeed value1, usec value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static uspeed operator /(uspeed value1, usec value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static usec operator *(usec value1, uspeed value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

    }

    public partial struct uvalue {

        [INLINE(256)]
        public static uvalue operator *(ucvalue value1, uvalue value2) {
            value2.value *= value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        [INLINE(256)]
        public static uvalue operator *(uvalue value1, ucvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static uvalue operator *(uvalue value1, usec value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static uvalue operator /(uvalue value1, usec value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static usec operator *(usec value1, uvalue value2) {
            value1.value *= value2.value;
            value1.value /= PRECISION;
            return value1;
        }

    }

    public partial struct svalue {

        [INLINE(256)]
        public static svalue operator *(ucvalue value1, svalue value2) {
            value2.value *= (int)value1.value;
            value2.value /= PRECISION;
            return value2;
        }

        [INLINE(256)]
        public static svalue operator *(svalue value1, ucvalue value2) {
            value1.value *= (int)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static svalue operator *(svalue value1, usec value2) {
            value1.value *= (int)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static svalue operator /(svalue value1, usec value2) {
            value1.value /= (int)value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static usec operator *(usec value1, svalue value2) {
            value1.value *= (uint)value2.value;
            value1.value /= PRECISION;
            return value1;
        }

    }

}