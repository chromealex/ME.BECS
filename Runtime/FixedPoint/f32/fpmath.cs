#if FIXED_POINT_F32
namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System;
    using System.Text;

    public readonly struct FixConst : IEquatable<FixConst> {

        [INLINE(256)]
        public static explicit operator double(FixConst f) {
            return (double)(f.raw >> 32) + (uint)f.raw / (uint.MaxValue + 1.0);
        }

        [INLINE(256)]
        public static implicit operator FixConst(double value) {
            if (value < int.MinValue || value >= int.MaxValue + 1L) {
                throw new OverflowException();
            }

            var floor = System.Math.Floor(value);
            return new FixConst(((long)floor << 32) + (long)((value - floor) * (uint.MaxValue + 1.0) + 0.5));
        }

        [INLINE(256)]
        public static implicit operator sfloat(FixConst value) {
            return new sfloat((int)((value.Raw + (1 << (32 - sfloat.FRACTIONAL_BITS - 1))) >> (32 - sfloat.FRACTIONAL_BITS)));
        }

        [INLINE(256)]
        public static explicit operator int(FixConst value) {
            if (value.raw > 0) {
                return (int)(value.raw >> 32);
            } else {
                return (int)((value.raw + uint.MaxValue) >> 32);
            }
        }

        [INLINE(256)]
        public static implicit operator FixConst(int value) {
            return new FixConst((long)value << 32);
        }

        [INLINE(256)]
        public static bool operator ==(FixConst lhs, FixConst rhs) {
            return lhs.raw == rhs.raw;
        }

        [INLINE(256)]
        public static bool operator !=(FixConst lhs, FixConst rhs) {
            return lhs.raw != rhs.raw;
        }

        [INLINE(256)]
        public static bool operator >(FixConst lhs, FixConst rhs) {
            return lhs.raw > rhs.raw;
        }

        [INLINE(256)]
        public static bool operator >=(FixConst lhs, FixConst rhs) {
            return lhs.raw >= rhs.raw;
        }

        [INLINE(256)]
        public static bool operator <(FixConst lhs, FixConst rhs) {
            return lhs.raw < rhs.raw;
        }

        [INLINE(256)]
        public static bool operator <=(FixConst lhs, FixConst rhs) {
            return lhs.raw <= rhs.raw;
        }

        [INLINE(256)]
        public static FixConst operator +(FixConst value) {
            return value;
        }

        [INLINE(256)]
        public static FixConst operator -(FixConst value) {
            return new FixConst(-value.raw);
        }

        private readonly long raw;

        [INLINE(256)]
        public FixConst(long raw) {
            this.raw = raw;
        }

        public long Raw => this.raw;

        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is FixConst && (FixConst)obj == this;
        }

        [INLINE(256)]
        public override int GetHashCode() {
            return this.Raw.GetHashCode();
        }

        public override string ToString() {
            var sb = new StringBuilder();
            if (this.raw < 0) {
                sb.Append(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NegativeSign);
            }

            long abs = (int)this;
            abs = abs < 0 ? -abs : abs;
            sb.Append(abs.ToString());
            var fraction = (ulong)(this.raw & uint.MaxValue);
            if (fraction == 0) {
                return sb.ToString();
            }

            fraction = this.raw < 0 ? uint.MaxValue + 1L - fraction : fraction;
            fraction *= 1000000000L;
            fraction += (uint.MaxValue + 1L) >> 1;
            fraction >>= 32;

            sb.Append(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            sb.Append(fraction.ToString("D9").TrimEnd('0'));
            return sb.ToString();
        }

        public bool Equals(FixConst other) {
            return this.raw == other.raw;
        }

    }

    public class FixMathSinTable {

        public static readonly Unity.Burst.SharedStatic<Internal.Array<sfloat>> value = Unity.Burst.SharedStatic<Internal.Array<sfloat>>.GetOrCreate<FixMathSinTable>();

    }

    public class FixMathCordicTable {

        public static readonly Unity.Burst.SharedStatic<Internal.Array<sfloat>> value = Unity.Burst.SharedStatic<Internal.Array<sfloat>>.GetOrCreate<FixMathCordicTable>();

    }

    public class FixMathCordicGainTable {

        public static readonly Unity.Burst.SharedStatic<Internal.Array<sfloat>> value = Unity.Burst.SharedStatic<Internal.Array<sfloat>>.GetOrCreate<FixMathCordicGainTable>();

    }

    public class FixMathFactTable {

        public static readonly Unity.Burst.SharedStatic<Internal.Array<FixConst>> value = Unity.Burst.SharedStatic<Internal.Array<FixConst>>.GetOrCreate<FixMathFactTable>();

    }

    public class FixMathValues_ALMOST_ONE { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_ALMOST_ONE>(); }
    public class FixMathValues_SMALL_VALUE { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_SMALL_VALUE>(); }
    public class FixMathValues_BIG_VALUE { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_BIG_VALUE>(); }
    public class FixMathValues_DEG2RAD { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_DEG2RAD>(); }
    public class FixMathValues_RAD2DEG { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_RAD2DEG>(); }
    public class FixMathValues_PI { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_PI>(); }
    public class FixMathValues_E { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_E>(); }
    public class FixMathValues_LOG2E { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_LOG2E>(); }
    public class FixMathValues_LOG10E { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_LOG10E>(); }
    public class FixMathValues_LOG2_10 { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_LOG2_10>(); }
    public class FixMathValues_LN2 { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_LN2>(); }
    public class FixMathValues_LN10 { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_LN10>(); }
    public class FixMathValues_LOG10_2 { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_LOG10_2>(); }
    public class FixMathValues_PI_HALF { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_PI_HALF>(); }
    public class FixMathValues_PI_OVER_4 { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_PI_OVER_4>(); }
    public class FixMathValues_TWO_PI { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_TWO_PI>(); }
    public class FixMathValues_SQRT_2 { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_SQRT_2>(); }
    public class FixMathValues_PositiveInfinity { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_PositiveInfinity>(); }
    public class FixMathValues_NegativeInfinity { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_NegativeInfinity>(); }
    public class FixMathValues_NaN { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_NaN>(); }
    public class FixMathValues_FLT_MIN_NORMAL { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_FLT_MIN_NORMAL>(); }
    public class FixMathValues_EXP_2 { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_EXP_2>(); }
    public class FixMathValues_EXP_10 { public static readonly Unity.Burst.SharedStatic<sfloat> value = Unity.Burst.SharedStatic<sfloat>.GetOrCreate<FixMathValues_EXP_10>(); }
    
    public static partial class FixMath {

        public static ref sfloat ALMOST_ONE => ref FixMathValues_ALMOST_ONE.value.Data;

        public static ref sfloat SMALL_VALUE => ref FixMathValues_SMALL_VALUE.value.Data;
        public static ref sfloat BIG_VALUE => ref FixMathValues_BIG_VALUE.value.Data;

        public static ref sfloat DEG2RAD => ref FixMathValues_DEG2RAD.value.Data;
        public static ref sfloat RAD2DEG => ref FixMathValues_RAD2DEG.value.Data;

        public static ref sfloat PI => ref FixMathValues_PI.value.Data;
        public static ref sfloat E => ref FixMathValues_E.value.Data;
        public static ref sfloat LOG2E => ref FixMathValues_LOG2E.value.Data;
        public static ref sfloat LOG10E => ref FixMathValues_LOG10E.value.Data;
        public static ref sfloat LOG2_10 => ref FixMathValues_LOG2_10.value.Data;
        public static ref sfloat LN2 => ref FixMathValues_LN2.value.Data;
        public static ref sfloat LN10 => ref FixMathValues_LN10.value.Data;
        public static ref sfloat LOG10_2 => ref FixMathValues_LOG10_2.value.Data;

        public static ref sfloat PI_HALF => ref FixMathValues_PI_HALF.value.Data;
        public static ref sfloat PI_OVER_4 => ref FixMathValues_PI_OVER_4.value.Data;
        public static ref sfloat TWO_PI => ref FixMathValues_TWO_PI.value.Data;
        public static ref sfloat SQRT_2 => ref FixMathValues_SQRT_2.value.Data;

        public static ref sfloat PositiveInfinity => ref FixMathValues_PositiveInfinity.value.Data;
        public static ref sfloat NegativeInfinity => ref FixMathValues_NegativeInfinity.value.Data;
        public static ref sfloat NaN => ref FixMathValues_NaN.value.Data;

        public static ref sfloat FLT_MIN_NORMAL => ref FixMathValues_FLT_MIN_NORMAL.value.Data;

        public static ref sfloat EXP_2 => ref FixMathValues_EXP_2.value.Data;
        public static ref sfloat EXP_10 => ref FixMathValues_EXP_10.value.Data;

        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute]
        public static void Initialize() {
            if (QUARTER_SINE_RES_POWER >= sfloat.FRACTIONAL_BITS) {
                throw new System.Exception("_quarterSineResPower must be less than Fix.FractionalBits.");
            }

            if (FixMathTables.quarterSineConsts.Length != 90 * (1 << QUARTER_SINE_RES_POWER) + 1) {
                throw new System.Exception("_quarterSineConst.Length must be 90 * 2^(_quarterSineResPower) + 1.");
            }

            PI = piConst;
            E = eConst;
            LOG2E = log2EConst;
            LOG10E = (FixConst)0.43;
            LOG2_10 = log210Const;
            LN2 = ln2Const;
            LN10 = (FixConst)2.30;
            LOG10_2 = log102Const;
            PI_HALF = PI / 2;
            PI_OVER_4 = PI / 4;
            TWO_PI = PI * 2;
            SQRT_2 = Sqrt(2);

            EXP_2 = (FixConst)0.693147180559945309;
            EXP_10 = (FixConst)2.302585092994045684;

            DEG2RAD = (FixConst)Unity.Mathematics.math.TORADIANS_DBL;
            RAD2DEG = (FixConst)Unity.Mathematics.math.TODEGREES_DBL;

            SMALL_VALUE = new sfloat(int.MinValue + 1);
            BIG_VALUE = new sfloat(int.MaxValue - 1);
            ALMOST_ONE = (FixConst)0.9995;

            FLT_MIN_NORMAL = (FixConst)0.01;

            NaN = new sfloat(int.MinValue);
            PositiveInfinity = new sfloat(int.MaxValue);
            NegativeInfinity = new sfloat(int.MinValue + 1);

            var quarterSine = Array.ConvertAll(FixMathTables.quarterSineConsts, c => (sfloat)c);
            FixMathSinTable.value.Data.Resize((uint)quarterSine.Length);
            for (int i = 0; i < quarterSine.Length; ++i) FixMathSinTable.value.Data.Get(i) = quarterSine[i];
            var cordicAngles = Array.ConvertAll(FixMathTables.cordicAngleConsts, c => (sfloat)c);
            FixMathCordicTable.value.Data.Resize((uint)cordicAngles.Length);
            for (int i = 0; i < cordicAngles.Length; ++i) FixMathSinTable.value.Data.Get(i) = cordicAngles[i];
            var cordicGains = Array.ConvertAll(FixMathTables.cordicGainConsts, c => (sfloat)c);
            FixMathCordicGainTable.value.Data.Resize((uint)cordicGains.Length);
            for (int i = 0; i < cordicGains.Length; ++i) FixMathSinTable.value.Data.Get(i) = cordicGains[i];
            FixMathFactTable.value.Data.Resize((uint)FixMathTables.invFactConsts.Length);
            for (int i = 0; i < FixMathTables.invFactConsts.Length; ++i) FixMathSinTable.value.Data.Get(i) = FixMathTables.invFactConsts[i];
        }

        [INLINE(256)]
        public static sfloat Abs(sfloat value) {
            return value.RawValue < 0 ? new sfloat(-value.RawValue) : value;
        }

        [INLINE(256)]
        public static sfloat Sign(sfloat value) {
            if (value < 0) {
                return -1;
            } else if (value > 0) {
                return 1;
            } else {
                return 0;
            }
        }

        [INLINE(256)]
        public static sfloat Ceiling(sfloat value) {
            return new sfloat((value.RawValue + sfloat.FRACTION_MASK) & sfloat.INTEGER_MASK);
        }

        [INLINE(256)]
        public static sfloat Floor(sfloat value) {
            return new sfloat(value.RawValue & sfloat.INTEGER_MASK);
        }

        [INLINE(256)]
        public static sfloat Truncate(sfloat value) {
            if (value < 0) {
                return new sfloat((value.RawValue + sfloat.FRACTION_RANGE) & sfloat.INTEGER_MASK);
            } else {
                return new sfloat(value.RawValue & sfloat.INTEGER_MASK);
            }
        }

        [INLINE(256)]
        public static sfloat Round(sfloat value) {
            return new sfloat((value.RawValue + (sfloat.FRACTION_RANGE >> 1)) & ~sfloat.FRACTION_MASK);
        }

        [INLINE(256)]
        public static sfloat Min(sfloat v1, sfloat v2) {
            return v1 < v2 ? v1 : v2;
        }

        [INLINE(256)]
        public static sfloat Max(sfloat v1, sfloat v2) {
            return v1 > v2 ? v1 : v2;
        }

        [INLINE(256)]
        public static sfloat Sqrt(sfloat value) {
            if (value.RawValue < 0) {
                return NaN;
            }

            if (value.RawValue == 0) {
                return 0;
            }

            return new sfloat((int)(SqrtULong((ulong)value.RawValue << (sfloat.FRACTIONAL_BITS + 2)) + 1) >> 1);
        }

        [INLINE(256)]
        internal static uint SqrtULong(ulong n) {
            ulong x = 1L << ((31 + sfloat.FRACTIONAL_BITS + 2 + 1) / 2);
            while (true) {
                var y = (x + n / x) >> 1;
                if (y >= x) {
                    return (uint)x;
                }

                x = y;
            }
        }

        [INLINE(256)]
        public static sfloat Sin(sfloat radians) {
            var degrees = radians * FixMath.RAD2DEG;
            return CosRaw(degrees.RawValue - (90 << sfloat.FRACTIONAL_BITS));
            //return (sfloat)System.Math.Sin((double)radians);
        }

        [INLINE(256)]
        public static sfloat Cos(sfloat radians) {
            //return (sfloat)System.Math.Cos((float)degrees);
            var degrees = radians * FixMath.RAD2DEG;
            return CosRaw(degrees.RawValue);
        }

        [INLINE(256)]
        private static sfloat CosRaw(int raw) {
            raw = raw < 0 ? -raw : raw;
            var t = raw & ((1 << (sfloat.FRACTIONAL_BITS - QUARTER_SINE_RES_POWER)) - 1);
            raw = raw >> (sfloat.FRACTIONAL_BITS - QUARTER_SINE_RES_POWER);

            if (t == 0) {
                return CosRawLookup(raw);
            }

            var v1 = CosRawLookup(raw);
            var v2 = CosRawLookup(raw + 1);

            return new sfloat(
                (int)(
                         (
                             (long)v1.RawValue * ((1 << (sfloat.FRACTIONAL_BITS - QUARTER_SINE_RES_POWER)) - t)
                             + (long)v2.RawValue * t
                             + (1 << (sfloat.FRACTIONAL_BITS - QUARTER_SINE_RES_POWER - 1))
                         )
                         >> (sfloat.FRACTIONAL_BITS - QUARTER_SINE_RES_POWER)
                     )
            );
        }

        [INLINE(256)]
        private static sfloat CosRawLookup(int raw) {
            raw %= 360 * (1 << QUARTER_SINE_RES_POWER);

            if (raw < 90 * (1 << QUARTER_SINE_RES_POWER)) {
                return FixMathSinTable.value.Data.Get(90 * (1 << QUARTER_SINE_RES_POWER) - raw);
            } else if (raw < 180 * (1 << QUARTER_SINE_RES_POWER)) {
                raw -= 90 * (1 << QUARTER_SINE_RES_POWER);
                return -FixMathSinTable.value.Data.Get(raw);
            } else if (raw < 270 * (1 << QUARTER_SINE_RES_POWER)) {
                raw -= 180 * (1 << QUARTER_SINE_RES_POWER);
                return -FixMathSinTable.value.Data.Get(90 * (1 << QUARTER_SINE_RES_POWER) - raw);
            } else {
                raw -= 270 * (1 << QUARTER_SINE_RES_POWER);
                return FixMathSinTable.value.Data.Get(raw);
            }
        }

        [INLINE(256)]
        public static sfloat Tan(sfloat degrees) {
            return Sin(degrees) / Cos(degrees);
        }

        [INLINE(256)]
        public static sfloat Asin(sfloat value) {
            return Atan2(value, Sqrt((1 + value) * (1 - value)));
        }

        [INLINE(256)]
        public static sfloat Acos(sfloat value) {
            return Atan2(Sqrt((1 + value) * (1 - value)), value);
        }

        [INLINE(256)]
        public static sfloat Atan(sfloat value) {
            return Atan2(value, 1);
        }

        [INLINE(256)]
        public static sfloat Atan2(sfloat y, sfloat x) {
            y *= FixMath.RAD2DEG;
            x *= FixMath.RAD2DEG;
            if (x == 0 && y == 0) {
                throw new ArgumentOutOfRangeException("y and x cannot both be 0.");
            }

            sfloat angle = 0;
            sfloat xNew, yNew;

            if (x < 0) {
                if (y < 0) {
                    xNew = -y;
                    yNew = x;
                    angle = -90;
                } else if (y > 0) {
                    xNew = y;
                    yNew = -x;
                    angle = 90;
                } else {
                    xNew = x;
                    yNew = y;
                    angle = 180;
                }

                x = xNew;
                y = yNew;
            }

            for (var i = 0; i < sfloat.FRACTIONAL_BITS + 2; i++) {
                if (y > 0) {
                    xNew = x + (y >> i);
                    yNew = y - (x >> i);
                    angle += FixMathCordicTable.value.Data.Get(i);
                } else if (y < 0) {
                    xNew = x - (y >> i);
                    yNew = y + (x >> i);
                    angle -= FixMathCordicTable.value.Data.Get(i);
                } else {
                    break;
                }

                x = xNew;
                y = yNew;
            }

            return angle * FixMath.DEG2RAD;
        }

        [INLINE(256)]
        public static sfloat Exp(sfloat value) {
            return Pow(E, value);
        }

        [INLINE(256)]
        public static sfloat Exp2(sfloat value) {
            return Exp(value * EXP_2);
        }

        [INLINE(256)]
        public static sfloat Exp10(sfloat value) {
            return Exp(value * EXP_10);
        }

        [INLINE(256)]
        public static sfloat Pow(sfloat b, sfloat exp) {
            if (b == 1 || exp == 0) {
                return 1;
            }

            int intPow;
            sfloat intFactor;
            if ((exp.RawValue & sfloat.FRACTION_MASK) == 0) {
                intPow = (int)((exp.RawValue + (sfloat.FRACTION_RANGE >> 1)) >> sfloat.FRACTIONAL_BITS);
                sfloat t;
                int p;
                if (intPow < 0) {
                    t = 1 / b;
                    p = -intPow;
                } else {
                    t = b;
                    p = intPow;
                }

                intFactor = 1;
                while (p > 0) {
                    if ((p & 1) != 0) {
                        intFactor *= t;
                    }

                    t *= t;
                    p >>= 1;
                }

                return intFactor;
            }

            exp *= Log(b, 2);
            b = 2;
            intPow = (int)((exp.RawValue + (sfloat.FRACTION_RANGE >> 1)) >> sfloat.FRACTIONAL_BITS);
            intFactor = intPow < 0 ? sfloat.One >> -intPow : sfloat.One << intPow;

            var x = (
                        (exp.RawValue - (intPow << sfloat.FRACTIONAL_BITS)) * ln2Const.Raw
                        + (sfloat.FRACTION_RANGE >> 1)
                    ) >> sfloat.FRACTIONAL_BITS;
            if (x == 0) {
                return intFactor;
            }

            var fracFactor = x;
            var xa = x;
            for (var i = 2; i < FixMathFactTable.value.Data.Length; i++) {
                if (xa == 0) {
                    break;
                }

                xa *= x;
                xa += 1L << (32 - 1);
                xa >>= 32;
                var p = xa * FixMathFactTable.value.Data.Get(i).Raw;
                p += 1L << (32 - 1);
                p >>= 32;
                fracFactor += p;
            }

            return new sfloat((int)((((long)intFactor.RawValue * fracFactor + (1L << (32 - 1))) >> 32) + intFactor.RawValue));
        }

        [INLINE(256)]
        public static sfloat Log(sfloat value) {
            return Log2(value) * LN2;
        }

        [INLINE(256)]
        public static sfloat Log(sfloat value, sfloat b) {
            if (b == 2) {
                return Log2(value);
            } else if (b == E) {
                return Log(value);
            } else if (b == 10) {
                return Log10(value);
            } else {
                return Log2(value) / Log2(b);
            }
        }

        [INLINE(256)]
        public static sfloat Log10(sfloat value) {
            return Log2(value) * LOG10_2;
        }

        [INLINE(256)]
        public static sfloat Log2(sfloat value) {
            if (value <= 0) {
                return NaN;
            }

            var x = (uint)value.RawValue;
            var b = 1U << (sfloat.FRACTIONAL_BITS - 1);
            uint y = 0;

            while (x < 1U << sfloat.FRACTIONAL_BITS) {
                x <<= 1;
                y -= 1U << sfloat.FRACTIONAL_BITS;
            }

            while (x >= 2U << sfloat.FRACTIONAL_BITS) {
                x >>= 1;
                y += 1U << sfloat.FRACTIONAL_BITS;
            }

            ulong z = x;

            for (var i = 0; i < sfloat.FRACTIONAL_BITS; i++) {
                z = (z * z) >> sfloat.FRACTIONAL_BITS;
                if (z >= 2U << sfloat.FRACTIONAL_BITS) {
                    z >>= 1;
                    y += b;
                }

                b >>= 1;
            }

            return new sfloat((int)y);
        }

    }

    public static class FixMathTables {
        
        #region Sine Table
        public static readonly FixConst[] quarterSineConsts = {
            new(0), new(18740271), new(37480185), new(56219385),
            new(74957515), new(93694218), new(112429137), new(131161916),
            new(149892197), new(168619625), new(187343842), new(206064493),
            new(224781220), new(243493669), new(262201481), new(280904301),
            new(299601773), new(318293542), new(336979250), new(355658543),
            new(374331065), new(392996460), new(411654373), new(430304448),
            new(448946331), new(467579667), new(486204101), new(504819278),
            new(523424844), new(542020445), new(560605727), new(579180335),
            new(597743917), new(616296119), new(634836587), new(653364969),
            new(671880911), new(690384062), new(708874069), new(727350581),
            new(745813244), new(764261708), new(782695622), new(801114635),
            new(819518395), new(837906553), new(856278758), new(874634661),
            new(892973913), new(911296163), new(929601063), new(947888266),
            new(966157422), new(984408183), new(1002640203), new(1020853134),
            new(1039046630), new(1057220343), new(1075373929), new(1093507041),
            new(1111619334), new(1129710464), new(1147780085), new(1165827855),
            new(1183853429), new(1201856464), new(1219836617), new(1237793546),
            new(1255726910), new(1273636366), new(1291521575), new(1309382194),
            new(1327217885), new(1345028307), new(1362813122), new(1380571991),
            new(1398304576), new(1416010539), new(1433689544), new(1451341253),
            new(1468965330), new(1486561441), new(1504129249), new(1521668421),
            new(1539178623), new(1556659521), new(1574110783), new(1591532075),
            new(1608923068), new(1626283428), new(1643612827), new(1660910933),
            new(1678177418), new(1695411953), new(1712614210), new(1729783862),
            new(1746920580), new(1764024040), new(1781093915), new(1798129881),
            new(1815131613), new(1832098787), new(1849031081), new(1865928172),
            new(1882789739), new(1899615460), new(1916405015), new(1933158084),
            new(1949874349), new(1966553491), new(1983195193), new(1999799137),
            new(2016365009), new(2032892491), new(2049381270), new(2065831032),
            new(2082241464), new(2098612252), new(2114943086), new(2131233655),
            new(2147483648), new(2163692756), new(2179860670), new(2195987083),
            new(2212071688), new(2228114178), new(2244114248), new(2260071593),
            new(2275985909), new(2291856895), new(2307684246), new(2323467662),
            new(2339206844), new(2354901489), new(2370551301), new(2386155981),
            new(2401715233), new(2417228758), new(2432696264), new(2448117454),
            new(2463492036), new(2478819716), new(2494100203), new(2509333207),
            new(2524518436), new(2539655602), new(2554744416), new(2569784592),
            new(2584775843), new(2599717883), new(2614610429), new(2629453196),
            new(2644245902), new(2658988265), new(2673680006), new(2688320843),
            new(2702910498), new(2717448694), new(2731935154), new(2746369601),
            new(2760751762), new(2775081362), new(2789358128), new(2803581789),
            new(2817752074), new(2831868713), new(2845931437), new(2859939978),
            new(2873894071), new(2887793449), new(2901637847), new(2915427003),
            new(2929160652), new(2942838535), new(2956460391), new(2970025959),
            new(2983534983), new(2996987204), new(3010382368), new(3023720217),
            new(3037000500), new(3050222962), new(3063387353), new(3076493421),
            new(3089540917), new(3102529593), new(3115459201), new(3128329495),
            new(3141140230), new(3153891163), new(3166582050), new(3179212649),
            new(3191782722), new(3204292027), new(3216740327), new(3229127385),
            new(3241452965), new(3253716833), new(3265918754), new(3278058497),
            new(3290135830), new(3302150525), new(3314102350), new(3325991081),
            new(3337816489), new(3349578350), new(3361276439), new(3372910535),
            new(3384480416), new(3395985861), new(3407426651), new(3418802568),
            new(3430113397), new(3441358921), new(3452538927), new(3463653201),
            new(3474701533), new(3485683711), new(3496599527), new(3507448772),
            new(3518231241), new(3528946727), new(3539595028), new(3550175940),
            new(3560689261), new(3571134792), new(3581512334), new(3591821689),
            new(3602062661), new(3612235055), new(3622338677), new(3632373336),
            new(3642338838), new(3652234996), new(3662061621), new(3671818526),
            new(3681505524), new(3691122431), new(3700669065), new(3710145244),
            new(3719550787), new(3728885515), new(3738149250), new(3747341816),
            new(3756463039), new(3765512743), new(3774490758), new(3783396912),
            new(3792231035), new(3800992960), new(3809682520), new(3818299548),
            new(3826843882), new(3835315358), new(3843713815), new(3852039094),
            new(3860291035), new(3868469481), new(3876574278), new(3884605270),
            new(3892562305), new(3900445232), new(3908253899), new(3915988159),
            new(3923647864), new(3931232868), new(3938743028), new(3946178199),
            new(3953538241), new(3960823014), new(3968032378), new(3975166196),
            new(3982224333), new(3989206654), new(3996113026), new(4002943318),
            new(4009697400), new(4016375143), new(4022976420), new(4029501105),
            new(4035949075), new(4042320205), new(4048614376), new(4054831467),
            new(4060971360), new(4067033938), new(4073019085), new(4078926688),
            new(4084756634), new(4090508812), new(4096183113), new(4101779428),
            new(4107297652), new(4112737678), new(4118099404), new(4123382727),
            new(4128587547), new(4133713764), new(4138761282), new(4143730003),
            new(4148619834), new(4153430681), new(4158162453), new(4162815059),
            new(4167388412), new(4171882423), new(4176297008), new(4180632082),
            new(4184887562), new(4189063369), new(4193159422), new(4197175643),
            new(4201111956), new(4204968286), new(4208744559), new(4212440704),
            new(4216056650), new(4219592328), new(4223047672), new(4226422614),
            new(4229717092), new(4232931042), new(4236064403), new(4239117116),
            new(4242089121), new(4244980364), new(4247790788), new(4250520341),
            new(4253168970), new(4255736624), new(4258223255), new(4260628816),
            new(4262953261), new(4265196545), new(4267358626), new(4269439463),
            new(4271439016), new(4273357246), new(4275194119), new(4276949597),
            new(4278623649), new(4280216242), new(4281727345), new(4283156931),
            new(4284504972), new(4285771441), new(4286956316), new(4288059574),
            new(4289081193), new(4290021154), new(4290879439), new(4291656032),
            new(4292350918), new(4292964084), new(4293495518), new(4293945210),
            new(4294313152), new(4294599336), new(4294803757), new(4294926411),
            new(4294967296),
        };
        #endregion

        #region CORDIC Tables
        public static readonly FixConst[] cordicAngleConsts = {
            new(193273528320), new(114096026022), new(60285206653), new(30601712202),
            new(15360239180), new(7687607525), new(3844741810), new(1922488225),
            new(961258780), new(480631223), new(240315841), new(120157949),
            new(60078978), new(30039490), new(15019745), new(7509872),
            new(3754936), new(1877468), new(938734), new(469367),
            new(234684), new(117342), new(58671), new(29335),
        };

        public static readonly FixConst[] cordicGainConsts = {
            new(3037000500), new(2716375826), new(2635271635), new(2614921743),
            new(2609829388), new(2608555990), new(2608237621), new(2608158028),
            new(2608138129), new(2608133154), new(2608131911), new(2608131600),
            new(2608131522), new(2608131503), new(2608131498), new(2608131497),
            new(2608131496), new(2608131496), new(2608131496), new(2608131496),
            new(2608131496), new(2608131496), new(2608131496), new(2608131496),
        };
        #endregion

        #region Inverse Factorial Table
        public static readonly FixConst[] invFactConsts = {
            new(4294967296),
            new(4294967296),
            new(2147483648),
            new(715827883),
            new(178956971),
            new(35791394),
            new(5965232),
            new(852176),
            new(106522),
            new(11836),
            new(1184),
            new(108),
            new(9),
            new(1),
        };
        #endregion

    }

    public static partial class FixMath {

        private static FixConst piConst => new(13493037705);
        private static FixConst eConst => new(11674931555);
        private static FixConst log2EConst => new(6196328019);
        private static FixConst log210Const => new(14267572527);
        private static FixConst ln2Const => new(2977044472);
        private static FixConst log102Const => new(1292913986);

        private const int QUARTER_SINE_RES_POWER = 2;

    }

}
#endif