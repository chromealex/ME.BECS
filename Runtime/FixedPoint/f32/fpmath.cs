
#if FIXED_POINT_F32
namespace ME.BECS {
    
    using System;
    using System.Text;
    
    public struct FixConst {
		public static explicit operator double (FixConst f) {
			return (double)(f.raw >> 32) + ((uint)(f.raw) / (uint.MaxValue + 1.0));
		}

		public static implicit operator FixConst (double value) {
			if (value < int.MinValue || value >= int.MaxValue + 1L)
				throw new OverflowException();

			var floor = System.Math.Floor(value);
			return new FixConst(((long)floor << 32) + (long)((value - floor) * (uint.MaxValue + 1.0) + 0.5));
		}

		public static implicit operator sfloat (FixConst value) {
			return new sfloat((int)((value.Raw + (1 << (32 - sfloat.FRACTIONAL_BITS - 1))) >> (32 - sfloat.FRACTIONAL_BITS)));
		}

		public static explicit operator int (FixConst value) {
			if (value.raw > 0)
				return (int)(value.raw >> 32);
			else
				return (int)((value.raw + uint.MaxValue) >> 32);
		}

		public static implicit operator FixConst (int value) {
			return new FixConst((long)value << 32);
		}

		public static bool operator == (FixConst lhs, FixConst rhs) {
			return lhs.raw == rhs.raw;
		}

		public static bool operator != (FixConst lhs, FixConst rhs) {
			return lhs.raw != rhs.raw;
		}

		public static bool operator > (FixConst lhs, FixConst rhs) {
			return lhs.raw > rhs.raw;
		}

		public static bool operator >= (FixConst lhs, FixConst rhs) {
			return lhs.raw >= rhs.raw;
		}

		public static bool operator < (FixConst lhs, FixConst rhs) {
			return lhs.raw < rhs.raw;
		}

		public static bool operator <= (FixConst lhs, FixConst rhs) {
			return lhs.raw <= rhs.raw;
		}

		public static FixConst operator + (FixConst value) {
			return value;
		}

		public static FixConst operator - (FixConst value) {
			return new FixConst(-value.raw);
		}

		long raw;

		public FixConst (long raw) {
			this.raw = raw;
		}

		public long Raw { get { return this.raw; } }

		public override bool Equals (object obj) {
			return (obj is FixConst && ((FixConst)obj) == this);
		}

		public override int GetHashCode () {
			return Raw.GetHashCode();
		}

		public override string ToString () {
			var sb = new StringBuilder();
			if (this.raw < 0)
				sb.Append(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NegativeSign);
			long abs = (int)this;
			abs = abs < 0 ? -abs : abs;
			sb.Append(abs.ToString());
			ulong fraction = (ulong)(this.raw & uint.MaxValue);
			if (fraction == 0)
				return sb.ToString();

			fraction = this.raw < 0 ? (uint.MaxValue + 1L) - fraction : fraction;
			fraction *= 1000000000L;
			fraction += (uint.MaxValue + 1L) >> 1;
			fraction >>= 32;

			sb.Append(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
			sb.Append(fraction.ToString("D9").TrimEnd('0'));
			return sb.ToString();
		}
	}
    
    public static partial class FixMath {

        public static readonly sfloat ALMOST_ONE;

        public static readonly sfloat SMALL_VALUE;
        public static readonly sfloat BIG_VALUE;

        public static readonly sfloat DEG2RAD;
        public static readonly sfloat RAD2DEG;

        public static readonly sfloat PI;
		public static readonly sfloat E;
        public static readonly sfloat LOG2E;
        public static readonly sfloat LOG10E;
        public static readonly sfloat LOG2_10;
        public static readonly sfloat LN2;
        public static readonly sfloat LN10;
        public static readonly sfloat LOG10_2;
		static sfloat[] quarterSine;
		static sfloat[] cordicAngles;
		static sfloat[] cordicGains;
        
        public static readonly sfloat PI_HALF;
        public static readonly sfloat PI_OVER_4;
        public static readonly sfloat TWO_PI;
        public static readonly sfloat SQRT_2;

        public static readonly sfloat PositiveInfinity;
        public static readonly sfloat NegativeInfinity;
        public static readonly sfloat NaN;

        public static readonly sfloat FLT_MIN_NORMAL;
        
        public static readonly sfloat EXP_2;
        public static readonly sfloat EXP_10;

		static FixMath () {
			if (QUARTER_SINE_RES_POWER >= sfloat.FRACTIONAL_BITS)
				throw new System.Exception("_quarterSineResPower must be less than Fix.FractionalBits.");
			if (quarterSineConsts.Length !=  90 * (1 << QUARTER_SINE_RES_POWER) + 1)
				throw new System.Exception("_quarterSineConst.Length must be 90 * 2^(_quarterSineResPower) + 1."); 

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

			quarterSine = Array.ConvertAll(quarterSineConsts, c => (sfloat)c);
			cordicAngles = Array.ConvertAll(cordicAngleConsts, c => (sfloat)c);
			cordicGains = Array.ConvertAll(cordicGainConsts, c => (sfloat)c);
		}

		public static sfloat Abs (sfloat value) {
			return value.RawValue < 0 ? new sfloat(-value.RawValue) : value;
		}

		public static sfloat Sign (sfloat value) {
			if (value < 0)
				return -1;
			else if (value > 0)
				return 1;
			else
				return 0;
		}

		public static sfloat Ceiling (sfloat value) {
			return new sfloat((value.RawValue + sfloat.FRACTION_MASK) & sfloat.INTEGER_MASK);
		}

		public static sfloat Floor (sfloat value) {
			return new sfloat(value.RawValue & sfloat.INTEGER_MASK);
		}

		public static sfloat Truncate (sfloat value) {
			if (value < 0)
				return new sfloat((value.RawValue + sfloat.FRACTION_RANGE) & sfloat.INTEGER_MASK);
			else
				return new sfloat(value.RawValue & sfloat.INTEGER_MASK);
		}

		public static sfloat Round (sfloat value) {
			return new sfloat((value.RawValue + (sfloat.FRACTION_RANGE >> 1)) & ~sfloat.FRACTION_MASK);
		}

		public static sfloat Min (sfloat v1, sfloat v2) {
			return v1 < v2 ? v1 : v2;
		}

		public static sfloat Max (sfloat v1, sfloat v2) {
			return v1 > v2 ? v1 : v2;
		}

		public static sfloat Sqrt (sfloat value) {
            if (value.RawValue < 0) return NaN;
			if (value.RawValue == 0)
				return 0;

			return new sfloat((int)(SqrtULong((ulong)value.RawValue << (sfloat.FRACTIONAL_BITS + 2)) + 1) >> 1);
		}

		internal static uint SqrtULong (ulong n) {
			ulong x = 1L << ((31 + (sfloat.FRACTIONAL_BITS + 2) + 1) / 2);
			while (true) {
				ulong y = (x + n / x) >> 1;
				if (y >= x)
					return (uint)x;
				x = y;
			}
		}

		public static sfloat Sin (sfloat degrees) {
            return (sfloat)System.Math.Sin((float)degrees);
			return CosRaw(degrees.RawValue - (90 << sfloat.FRACTIONAL_BITS));
		}

		public static sfloat Cos (sfloat degrees) {
            return (sfloat)System.Math.Cos((float)degrees);
			return CosRaw(degrees.RawValue);
		}

		static sfloat CosRaw (int raw) {
			raw = raw < 0 ? -raw : raw;
			int t = raw & ((1 << (sfloat.FRACTIONAL_BITS - QUARTER_SINE_RES_POWER)) - 1);
			raw = (raw >> (sfloat.FRACTIONAL_BITS - QUARTER_SINE_RES_POWER));

			if (t == 0)
				return CosRawLookup(raw);

			sfloat v1 = CosRawLookup(raw);
			sfloat v2 = CosRawLookup(raw + 1);

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

		static sfloat CosRawLookup (int raw) {
			raw %= 360 * (1 << QUARTER_SINE_RES_POWER);

			if (raw < 90 * (1 << QUARTER_SINE_RES_POWER)) {
				return quarterSine[90 * (1 << QUARTER_SINE_RES_POWER) - raw];
			} else if (raw < 180 * (1 << QUARTER_SINE_RES_POWER)) {
				raw -= 90 * (1 << QUARTER_SINE_RES_POWER);
				return -quarterSine[raw];
			} else if (raw < 270 * (1 << QUARTER_SINE_RES_POWER)) {
				raw -= 180 * (1 << QUARTER_SINE_RES_POWER);
				return -quarterSine[90 * (1 << QUARTER_SINE_RES_POWER) - raw];
			} else {
				raw -= 270 * (1 << QUARTER_SINE_RES_POWER);
				return quarterSine[raw];
			}
		}

		public static sfloat Tan (sfloat degrees) {
			return Sin(degrees) / Cos(degrees);
		}

		public static sfloat Asin (sfloat value) {
			return Atan2(value, Sqrt((1 + value) * (1 - value)));
		}

		public static sfloat Acos (sfloat value) {
			return Atan2(Sqrt((1 + value) * (1 - value)), value);
		}

		public static sfloat Atan (sfloat value) {
			return Atan2(value, 1);
		}

		public static sfloat Atan2 (sfloat y, sfloat x) {
            return (sfloat)System.Math.Atan2((float)y, (float)x);
			if (x == 0 && y == 0)
				throw new ArgumentOutOfRangeException("y and x cannot both be 0.");

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

			for (int i = 0; i < sfloat.FRACTIONAL_BITS + 2; i++) {
				if (y > 0) {
					xNew = x + (y >> i);
					yNew = y - (x >> i);
					angle += cordicAngles[i];
				} else if (y < 0) {
					xNew = x - (y >> i);
					yNew = y + (x >> i);
					angle -= cordicAngles[i];
				} else
					break;

				x = xNew;
				y = yNew;
			}

			return angle;
		}

		public static sfloat Exp (sfloat value) {
			return Pow(E, value);
		}

        public static sfloat Exp2(sfloat value) {
            return Exp(value * FixMath.EXP_2);
        }

        public static sfloat Exp10(sfloat value) {
            return Exp(value * FixMath.EXP_10);
        }

		public static sfloat Pow (sfloat b, sfloat exp) {
			if (b == 1 || exp == 0)
				return 1;

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
					if ((p & 1) != 0)
						intFactor *= t;
					t *= t;
					p >>= 1;
				}

				return intFactor;
			}

			exp *= Log(b, 2);
			b = 2;
			intPow = (int)((exp.RawValue + (sfloat.FRACTION_RANGE >> 1)) >> sfloat.FRACTIONAL_BITS);
			intFactor = intPow < 0 ? sfloat.One >> -intPow : sfloat.One << intPow;

			long x = (
				((exp.RawValue - (intPow << sfloat.FRACTIONAL_BITS)) * ln2Const.Raw)
				+ (sfloat.FRACTION_RANGE >> 1)
				) >> sfloat.FRACTIONAL_BITS;
			if (x == 0)
				return intFactor;

			long fracFactor = x;
			long xa = x;
			for (int i = 2; i < invFactConsts.Length; i++) {
				if (xa == 0)
					break;
				xa *= x;
				xa += (1L << (32 - 1));
				xa >>= 32;
				long p = xa * invFactConsts[i].Raw;
				p += (1L << (32 - 1));
				p >>= 32;
				fracFactor += p;
			}

			return new sfloat((int)((((long)intFactor.RawValue * fracFactor + (1L << (32 - 1))) >> 32) + intFactor.RawValue));
		}

		public static sfloat Log (sfloat value) {
			return Log2(value) * LN2;
		}

		public static sfloat Log (sfloat value, sfloat b) {
			if (b == 2)
				return Log2(value);
			else if (b == E)
				return Log(value);
			else if (b == 10)
				return Log10(value);
			else
				return Log2(value) / Log2(b);
		}

		public static sfloat Log10 (sfloat value) {
			return Log2(value) * LOG10_2;
		}

		public static sfloat Log2 (sfloat value) {
            if (value <= 0) return NaN;

			uint x = (uint)value.RawValue;
			uint b = 1U << (sfloat.FRACTIONAL_BITS - 1);
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

			for (int i = 0; i < sfloat.FRACTIONAL_BITS; i++) {
				z = z * z >> sfloat.FRACTIONAL_BITS;
				if (z >= 2U << sfloat.FRACTIONAL_BITS) {
					z >>= 1;
					y += b;
				}
				b >>= 1;
			}

			return new sfloat((int)y);
		}

    }
    
    public static partial class FixMath {
		static FixConst piConst = new FixConst(13493037705);
		static FixConst eConst = new FixConst(11674931555);
		static FixConst log2EConst = new FixConst(6196328019);
		static FixConst log210Const = new FixConst(14267572527);
		static FixConst ln2Const = new FixConst(2977044472);
		static FixConst log102Const = new FixConst(1292913986);

		const int QUARTER_SINE_RES_POWER = 2;
		#region Sine Table
		static FixConst[] quarterSineConsts = {
			new FixConst(0), new FixConst(18740271), new FixConst(37480185), new FixConst(56219385), 
			new FixConst(74957515), new FixConst(93694218), new FixConst(112429137), new FixConst(131161916), 
			new FixConst(149892197), new FixConst(168619625), new FixConst(187343842), new FixConst(206064493), 
			new FixConst(224781220), new FixConst(243493669), new FixConst(262201481), new FixConst(280904301), 
			new FixConst(299601773), new FixConst(318293542), new FixConst(336979250), new FixConst(355658543), 
			new FixConst(374331065), new FixConst(392996460), new FixConst(411654373), new FixConst(430304448), 
			new FixConst(448946331), new FixConst(467579667), new FixConst(486204101), new FixConst(504819278), 
			new FixConst(523424844), new FixConst(542020445), new FixConst(560605727), new FixConst(579180335), 
			new FixConst(597743917), new FixConst(616296119), new FixConst(634836587), new FixConst(653364969), 
			new FixConst(671880911), new FixConst(690384062), new FixConst(708874069), new FixConst(727350581), 
			new FixConst(745813244), new FixConst(764261708), new FixConst(782695622), new FixConst(801114635), 
			new FixConst(819518395), new FixConst(837906553), new FixConst(856278758), new FixConst(874634661), 
			new FixConst(892973913), new FixConst(911296163), new FixConst(929601063), new FixConst(947888266), 
			new FixConst(966157422), new FixConst(984408183), new FixConst(1002640203), new FixConst(1020853134), 
			new FixConst(1039046630), new FixConst(1057220343), new FixConst(1075373929), new FixConst(1093507041), 
			new FixConst(1111619334), new FixConst(1129710464), new FixConst(1147780085), new FixConst(1165827855), 
			new FixConst(1183853429), new FixConst(1201856464), new FixConst(1219836617), new FixConst(1237793546), 
			new FixConst(1255726910), new FixConst(1273636366), new FixConst(1291521575), new FixConst(1309382194), 
			new FixConst(1327217885), new FixConst(1345028307), new FixConst(1362813122), new FixConst(1380571991), 
			new FixConst(1398304576), new FixConst(1416010539), new FixConst(1433689544), new FixConst(1451341253), 
			new FixConst(1468965330), new FixConst(1486561441), new FixConst(1504129249), new FixConst(1521668421), 
			new FixConst(1539178623), new FixConst(1556659521), new FixConst(1574110783), new FixConst(1591532075), 
			new FixConst(1608923068), new FixConst(1626283428), new FixConst(1643612827), new FixConst(1660910933), 
			new FixConst(1678177418), new FixConst(1695411953), new FixConst(1712614210), new FixConst(1729783862), 
			new FixConst(1746920580), new FixConst(1764024040), new FixConst(1781093915), new FixConst(1798129881), 
			new FixConst(1815131613), new FixConst(1832098787), new FixConst(1849031081), new FixConst(1865928172), 
			new FixConst(1882789739), new FixConst(1899615460), new FixConst(1916405015), new FixConst(1933158084), 
			new FixConst(1949874349), new FixConst(1966553491), new FixConst(1983195193), new FixConst(1999799137), 
			new FixConst(2016365009), new FixConst(2032892491), new FixConst(2049381270), new FixConst(2065831032), 
			new FixConst(2082241464), new FixConst(2098612252), new FixConst(2114943086), new FixConst(2131233655), 
			new FixConst(2147483648), new FixConst(2163692756), new FixConst(2179860670), new FixConst(2195987083), 
			new FixConst(2212071688), new FixConst(2228114178), new FixConst(2244114248), new FixConst(2260071593), 
			new FixConst(2275985909), new FixConst(2291856895), new FixConst(2307684246), new FixConst(2323467662), 
			new FixConst(2339206844), new FixConst(2354901489), new FixConst(2370551301), new FixConst(2386155981), 
			new FixConst(2401715233), new FixConst(2417228758), new FixConst(2432696264), new FixConst(2448117454), 
			new FixConst(2463492036), new FixConst(2478819716), new FixConst(2494100203), new FixConst(2509333207), 
			new FixConst(2524518436), new FixConst(2539655602), new FixConst(2554744416), new FixConst(2569784592), 
			new FixConst(2584775843), new FixConst(2599717883), new FixConst(2614610429), new FixConst(2629453196), 
			new FixConst(2644245902), new FixConst(2658988265), new FixConst(2673680006), new FixConst(2688320843), 
			new FixConst(2702910498), new FixConst(2717448694), new FixConst(2731935154), new FixConst(2746369601), 
			new FixConst(2760751762), new FixConst(2775081362), new FixConst(2789358128), new FixConst(2803581789), 
			new FixConst(2817752074), new FixConst(2831868713), new FixConst(2845931437), new FixConst(2859939978), 
			new FixConst(2873894071), new FixConst(2887793449), new FixConst(2901637847), new FixConst(2915427003), 
			new FixConst(2929160652), new FixConst(2942838535), new FixConst(2956460391), new FixConst(2970025959), 
			new FixConst(2983534983), new FixConst(2996987204), new FixConst(3010382368), new FixConst(3023720217), 
			new FixConst(3037000500), new FixConst(3050222962), new FixConst(3063387353), new FixConst(3076493421), 
			new FixConst(3089540917), new FixConst(3102529593), new FixConst(3115459201), new FixConst(3128329495), 
			new FixConst(3141140230), new FixConst(3153891163), new FixConst(3166582050), new FixConst(3179212649), 
			new FixConst(3191782722), new FixConst(3204292027), new FixConst(3216740327), new FixConst(3229127385), 
			new FixConst(3241452965), new FixConst(3253716833), new FixConst(3265918754), new FixConst(3278058497), 
			new FixConst(3290135830), new FixConst(3302150525), new FixConst(3314102350), new FixConst(3325991081), 
			new FixConst(3337816489), new FixConst(3349578350), new FixConst(3361276439), new FixConst(3372910535), 
			new FixConst(3384480416), new FixConst(3395985861), new FixConst(3407426651), new FixConst(3418802568), 
			new FixConst(3430113397), new FixConst(3441358921), new FixConst(3452538927), new FixConst(3463653201), 
			new FixConst(3474701533), new FixConst(3485683711), new FixConst(3496599527), new FixConst(3507448772), 
			new FixConst(3518231241), new FixConst(3528946727), new FixConst(3539595028), new FixConst(3550175940), 
			new FixConst(3560689261), new FixConst(3571134792), new FixConst(3581512334), new FixConst(3591821689), 
			new FixConst(3602062661), new FixConst(3612235055), new FixConst(3622338677), new FixConst(3632373336), 
			new FixConst(3642338838), new FixConst(3652234996), new FixConst(3662061621), new FixConst(3671818526), 
			new FixConst(3681505524), new FixConst(3691122431), new FixConst(3700669065), new FixConst(3710145244), 
			new FixConst(3719550787), new FixConst(3728885515), new FixConst(3738149250), new FixConst(3747341816), 
			new FixConst(3756463039), new FixConst(3765512743), new FixConst(3774490758), new FixConst(3783396912), 
			new FixConst(3792231035), new FixConst(3800992960), new FixConst(3809682520), new FixConst(3818299548), 
			new FixConst(3826843882), new FixConst(3835315358), new FixConst(3843713815), new FixConst(3852039094), 
			new FixConst(3860291035), new FixConst(3868469481), new FixConst(3876574278), new FixConst(3884605270), 
			new FixConst(3892562305), new FixConst(3900445232), new FixConst(3908253899), new FixConst(3915988159), 
			new FixConst(3923647864), new FixConst(3931232868), new FixConst(3938743028), new FixConst(3946178199), 
			new FixConst(3953538241), new FixConst(3960823014), new FixConst(3968032378), new FixConst(3975166196), 
			new FixConst(3982224333), new FixConst(3989206654), new FixConst(3996113026), new FixConst(4002943318), 
			new FixConst(4009697400), new FixConst(4016375143), new FixConst(4022976420), new FixConst(4029501105), 
			new FixConst(4035949075), new FixConst(4042320205), new FixConst(4048614376), new FixConst(4054831467), 
			new FixConst(4060971360), new FixConst(4067033938), new FixConst(4073019085), new FixConst(4078926688), 
			new FixConst(4084756634), new FixConst(4090508812), new FixConst(4096183113), new FixConst(4101779428), 
			new FixConst(4107297652), new FixConst(4112737678), new FixConst(4118099404), new FixConst(4123382727), 
			new FixConst(4128587547), new FixConst(4133713764), new FixConst(4138761282), new FixConst(4143730003), 
			new FixConst(4148619834), new FixConst(4153430681), new FixConst(4158162453), new FixConst(4162815059), 
			new FixConst(4167388412), new FixConst(4171882423), new FixConst(4176297008), new FixConst(4180632082), 
			new FixConst(4184887562), new FixConst(4189063369), new FixConst(4193159422), new FixConst(4197175643), 
			new FixConst(4201111956), new FixConst(4204968286), new FixConst(4208744559), new FixConst(4212440704), 
			new FixConst(4216056650), new FixConst(4219592328), new FixConst(4223047672), new FixConst(4226422614), 
			new FixConst(4229717092), new FixConst(4232931042), new FixConst(4236064403), new FixConst(4239117116), 
			new FixConst(4242089121), new FixConst(4244980364), new FixConst(4247790788), new FixConst(4250520341), 
			new FixConst(4253168970), new FixConst(4255736624), new FixConst(4258223255), new FixConst(4260628816), 
			new FixConst(4262953261), new FixConst(4265196545), new FixConst(4267358626), new FixConst(4269439463), 
			new FixConst(4271439016), new FixConst(4273357246), new FixConst(4275194119), new FixConst(4276949597), 
			new FixConst(4278623649), new FixConst(4280216242), new FixConst(4281727345), new FixConst(4283156931), 
			new FixConst(4284504972), new FixConst(4285771441), new FixConst(4286956316), new FixConst(4288059574), 
			new FixConst(4289081193), new FixConst(4290021154), new FixConst(4290879439), new FixConst(4291656032), 
			new FixConst(4292350918), new FixConst(4292964084), new FixConst(4293495518), new FixConst(4293945210), 
			new FixConst(4294313152), new FixConst(4294599336), new FixConst(4294803757), new FixConst(4294926411), 
			new FixConst(4294967296),
		};
		#endregion

		#region CORDIC Tables
		static FixConst[] cordicAngleConsts = {
			new FixConst(193273528320), new FixConst(114096026022), new FixConst(60285206653), new FixConst(30601712202), 
			new FixConst(15360239180), new FixConst(7687607525), new FixConst(3844741810), new FixConst(1922488225), 
			new FixConst(961258780), new FixConst(480631223), new FixConst(240315841), new FixConst(120157949), 
			new FixConst(60078978), new FixConst(30039490), new FixConst(15019745), new FixConst(7509872), 
			new FixConst(3754936), new FixConst(1877468), new FixConst(938734), new FixConst(469367), 
			new FixConst(234684), new FixConst(117342), new FixConst(58671), new FixConst(29335), 
		};

		static FixConst[] cordicGainConsts = {
			new FixConst(3037000500), new FixConst(2716375826), new FixConst(2635271635), new FixConst(2614921743), 
			new FixConst(2609829388), new FixConst(2608555990), new FixConst(2608237621), new FixConst(2608158028), 
			new FixConst(2608138129), new FixConst(2608133154), new FixConst(2608131911), new FixConst(2608131600), 
			new FixConst(2608131522), new FixConst(2608131503), new FixConst(2608131498), new FixConst(2608131497), 
			new FixConst(2608131496), new FixConst(2608131496), new FixConst(2608131496), new FixConst(2608131496), 
			new FixConst(2608131496), new FixConst(2608131496), new FixConst(2608131496), new FixConst(2608131496), 
		};
		#endregion

		#region Inverse Factorial Table
		static FixConst[] invFactConsts = {
			new FixConst(4294967296),
			new FixConst(4294967296),
			new FixConst(2147483648),
			new FixConst(715827883),
			new FixConst(178956971),
			new FixConst(35791394),
			new FixConst(5965232),
			new FixConst(852176),
			new FixConst(106522),
			new FixConst(11836),
			new FixConst(1184),
			new FixConst(108),
			new FixConst(9),
			new FixConst(1),
		};
		#endregion
	}
    
}
#endif