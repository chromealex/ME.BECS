using System;
using System.Text;

#if FIXED_POINT_F32
[System.Serializable]
public struct sfloat : IComparable<sfloat> {
	internal const int FRACTIONAL_BITS = 10;

	internal const int INTEGER_BITS = sizeof(int) * 8 - FRACTIONAL_BITS;
	internal const int FRACTION_MASK = (int)(uint.MaxValue >> INTEGER_BITS);
	internal const int INTEGER_MASK = (int)(-1 & ~FRACTION_MASK);
	internal const int FRACTION_RANGE = FRACTION_MASK + 1;
	internal const int MIN_INTEGER = int.MinValue >> FRACTIONAL_BITS;
	internal const int MAX_INTEGER = int.MaxValue >> FRACTIONAL_BITS;

	public static readonly sfloat Zero = new sfloat(0);
	public static readonly sfloat One = new sfloat(FRACTION_RANGE);
	public static readonly sfloat MinValue = new sfloat(int.MinValue);
	public static readonly sfloat MaxValue = new sfloat(int.MaxValue);
	public static readonly sfloat Epsilon = new sfloat(1);

    public static sfloat PositiveInfinity => ME.BECS.FixMath.PositiveInfinity;
    public static sfloat NegativeInfinity => ME.BECS.FixMath.NegativeInfinity;
    
	static sfloat () {
		if (FRACTIONAL_BITS < 8)
			throw new Exception("Fix must have at least 8 fractional bits.");
		if (INTEGER_BITS < 10)
			throw new Exception("Fix must have at least 10 integer bits.");
		if (FRACTIONAL_BITS % 2 == 1)
			throw new Exception("Fix must have an even number of fractional and integer bits.");
	}

	public static int FractionalBits { get { return FRACTIONAL_BITS; } }
	public static int IntegerBits { get { return INTEGER_BITS; } }
	public static int FractionMask { get { return FRACTION_MASK; } }
	public static int IntegerMask { get { return INTEGER_MASK; } }
	public static int FractionRange { get { return FRACTION_RANGE; } }
	public static int MinInteger { get { return MIN_INTEGER; } }
	public static int MaxInteger { get { return MAX_INTEGER; } }

	public static sfloat Mix (int integer, int numerator, int denominator) {
		if (numerator < 0 || denominator < 0)
			throw new ArgumentException("Ratio must be positive.");

		int fraction = ((int)((long)FRACTION_RANGE * numerator / denominator) & FRACTION_MASK);
		fraction = integer < 0 ? -fraction : fraction;

		return new sfloat((integer << FRACTIONAL_BITS) + fraction);
	}

	public static sfloat Ratio (int numerator, int denominator) {
		return new sfloat((int)((((long)numerator << (FRACTIONAL_BITS + 1)) / (long)denominator + 1) >> 1));
	}

	public static explicit operator double (sfloat value) {
		return (double)(value.rawValue >> FRACTIONAL_BITS) + (value.rawValue & FRACTION_MASK) / (double)FRACTION_RANGE;
	}

	public static explicit operator float (sfloat value) {
		return (float)(double)value;
	}

    public static implicit operator sfloat (float value) {
        return (ME.BECS.FixConst)value;
    }

	public static explicit operator int (sfloat value) {
		if (value.rawValue > 0)
			return value.rawValue >> FRACTIONAL_BITS;
		else
			return (value.rawValue + FRACTION_MASK) >> FRACTIONAL_BITS;
	}

	public static implicit operator sfloat (int value) {
		return new sfloat(value << FRACTIONAL_BITS);
	}

	public static bool operator == (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return false;
		return lhs.rawValue == rhs.rawValue;
	}

	public static bool operator != (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return false;
		return lhs.rawValue != rhs.rawValue;
	}

	public static bool operator > (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return false;
		return lhs.rawValue > rhs.rawValue;
	}

	public static bool operator >= (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return false;
		return lhs.rawValue >= rhs.rawValue;
	}

	public static bool operator < (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return false;
		return lhs.rawValue < rhs.rawValue;
	}

	public static bool operator <= (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return false;
		return lhs.rawValue <= rhs.rawValue;
	}

	public static sfloat operator + (sfloat value) {
        if (value.IsNaN() == true) return ME.BECS.FixMath.NaN;
		return value;
	}

	public static sfloat operator - (sfloat value) {
        if (value.IsNaN() == true) return ME.BECS.FixMath.NaN;
		return new sfloat(-value.rawValue);
	}

	public static sfloat operator + (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return ME.BECS.FixMath.NaN;
		return new sfloat(lhs.rawValue + rhs.rawValue);
	}

	public static sfloat operator - (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return ME.BECS.FixMath.NaN;
		return new sfloat(lhs.rawValue - rhs.rawValue);
	}

	public static sfloat operator * (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return ME.BECS.FixMath.NaN;
		return new sfloat((int)(((long)lhs.rawValue * (long)rhs.rawValue + (FRACTION_RANGE >> 1)) >> FRACTIONAL_BITS));
	}

	public static sfloat operator / (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return ME.BECS.FixMath.NaN;
        if (rhs.rawValue == 0) return ME.BECS.FixMath.NaN;
		return new sfloat((int)((((long)lhs.rawValue << (FRACTIONAL_BITS + 1)) / (long)rhs.rawValue + 1) >> 1));
	}

	public static sfloat operator % (sfloat lhs, sfloat rhs) {
        if (lhs.IsNaN() == true || rhs.IsNaN() == true) return ME.BECS.FixMath.NaN;
		return new sfloat(lhs.RawValue % rhs.RawValue);
	}

	public static sfloat operator << (sfloat lhs, int rhs) {
        if (lhs.IsNaN() == true) return ME.BECS.FixMath.NaN;
		return new sfloat(lhs.RawValue << rhs);
	}

	public static sfloat operator >> (sfloat lhs, int rhs) {
        if (lhs.IsNaN() == true) return ME.BECS.FixMath.NaN;
		return new sfloat(lhs.RawValue >> rhs);
	}

	public int rawValue;

	public sfloat (int rawValue) {
		this.rawValue = rawValue;
	}

	public int RawValue { get { return this.rawValue; } }

	public override bool Equals (object obj) {
		return (obj is sfloat && ((sfloat)obj) == this);
	}

	public override int GetHashCode () {
		return this.RawValue.GetHashCode();
	}

	public override string ToString () {
		var sb = new StringBuilder();
		if (this.rawValue < 0)
			sb.Append(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NegativeSign);
		int abs = (int)this;
		abs = abs < 0 ? -abs : abs;
		sb.Append(abs.ToString());
		ulong fraction = (ulong)(this.rawValue & FRACTION_MASK);
		if (fraction == 0)
			return sb.ToString();

		fraction = this.rawValue < 0 ? FRACTION_RANGE - fraction : fraction;
		fraction *= 1000000L;
		fraction += FRACTION_RANGE >> 1;
		fraction >>= FRACTIONAL_BITS;

		sb.Append(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
		sb.Append(fraction.ToString("D6").TrimEnd('0'));
		return sb.ToString();
	}

    public static sfloat FromRawValue(int x) {
        return new sfloat() { rawValue = x };
    }

    public bool IsFinite() {
        return this != ME.BECS.FixMath.PositiveInfinity && this != ME.BECS.FixMath.NegativeInfinity;
    }

    public bool IsNaN() {
        return this.rawValue == ME.BECS.FixMath.NaN.rawValue;
    }

    public bool IsZero() {
        return this == sfloat.Zero;
    }

    public string ToString(string format) {
        return this.ToString();
    }

    public string ToString(System.Globalization.CultureInfo cultureInfo) {
        return this.ToString();
    }

    public string ToString(string format, IFormatProvider formatProvider) {
        return this.ToString();
    }

    public int CompareTo(sfloat other) {
        if (this.IsNaN() == true) return 0;
        if (other.IsNaN() == true) return 0;
        return this.rawValue.CompareTo(other.rawValue);
    }

}
#endif
