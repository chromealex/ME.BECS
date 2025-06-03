// mostly from https://github.com/CodesInChaos/SoftFloat

// Copyright (c) 2011 CodesInChaos
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies
// or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// The MIT License (MIT) - http://www.opensource.org/licenses/mit-license.php
// If you need a different license please contact me

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#if FIXED_POINT_F32
using ME.BECS.FixedPoint;

[System.Serializable]
public struct sfloat : IEquatable<sfloat>, IComparable<sfloat>, IComparable, IFormattable {
    
    public static sfloat MinusOne  { [MethodImpl(256)] get { return FromRaw(Fixed32.Neg1); } }
    public static sfloat Zero      { [MethodImpl(256)] get { return FromRaw(Fixed32.Zero); } }
    public static sfloat Half      { [MethodImpl(256)] get { return FromRaw(Fixed32.Half); } }
    public static sfloat One       { [MethodImpl(256)] get { return FromRaw(Fixed32.One); } }
    public static sfloat Two       { [MethodImpl(256)] get { return FromRaw(Fixed32.Two); } }
    public static sfloat Pi        { [MethodImpl(256)] get { return FromRaw(Fixed32.Pi); } }
    public static sfloat Pi2       { [MethodImpl(256)] get { return FromRaw(Fixed32.Pi2); } }
    public static sfloat PiHalf    { [MethodImpl(256)] get { return FromRaw(Fixed32.PiHalf); } }
    public static sfloat E   { [MethodImpl(256)] get { return FromRaw(Fixed32.E); } }
    public static sfloat Epsilon   { [MethodImpl(256)] get { return FromRaw((int)(1.401298E-45f * 65536.0f)); } }

    public static sfloat MinValue  { [MethodImpl(256)] get { return FromRaw(Fixed32.MinValue); } }
    public static sfloat MaxValue  { [MethodImpl(256)] get { return FromRaw(Fixed32.MaxValue); } }
    
    public static sfloat PositiveInfinity => FromFloat(1f / 0f);
    public static sfloat NegativeInfinity => FromFloat(-1f / 0f);
    public static sfloat NaN => FromFloat(0f / 0f);
    
    public bool IsFinite() => this != NaN && this != PositiveInfinity && this != NegativeInfinity;
    public bool IsNaN() => this == NaN;
    public bool IsZero() => this.rawValue == 0;

    public int rawValue;

    /// <summary>
    /// Creates an sfloat number from a float value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sfloat(float f) {
        return sfloat.FromFloat(f);
    }

    /// <summary>
    /// Converts an sfloat number to a float value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator float(sfloat f) {
        return Fixed32.ToFloat(f.rawValue);
    }

    /// <summary>
    /// Converts an sfloat number to an integer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int(sfloat f) {
        return Fixed32.RoundToInt(f.rawValue);
    }

    /// <summary>
    /// Creates an sfloat number from an integer
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sfloat(int value) {
        return FromInt(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator sfloat(uint value) {
        return FromInt((int)value);
    }
    
    [MethodImpl(256)] public static sfloat FromRaw(int raw) { sfloat v; v.rawValue = raw; return v; }
    [MethodImpl(256)] public static sfloat FromInt(int v) { return FromRaw(Fixed32.FromInt(v)); }
    [MethodImpl(256)] public static sfloat FromFloat(float v) { return FromRaw(Fixed32.FromFloat(v)); }
    [MethodImpl(256)] public static sfloat FromDouble(double v) { return FromRaw(Fixed32.FromDouble(v)); }
    
    // Operators
    public static sfloat operator -(sfloat v1) { return FromRaw(-v1.rawValue); }

    public static sfloat operator +(sfloat v1, sfloat v2) { return FromRaw(v1.rawValue + v2.rawValue); }
    public static sfloat operator -(sfloat v1, sfloat v2) { return FromRaw(v1.rawValue - v2.rawValue); }
    public static sfloat operator *(sfloat v1, sfloat v2) { return FromRaw(Fixed32.Mul(v1.rawValue, v2.rawValue)); }
    public static sfloat operator /(sfloat v1, sfloat v2) { return FromRaw(Fixed32.DivPrecise(v1.rawValue, v2.rawValue)); }
    public static sfloat operator %(sfloat v1, sfloat v2) { return FromRaw(Fixed32.Mod(v1.rawValue, v2.rawValue)); }

    public static sfloat operator +(sfloat v1, int v2) { return FromRaw(v1.rawValue + Fixed32.FromInt(v2)); }
    public static sfloat operator +(int v1, sfloat v2) { return FromRaw(Fixed32.FromInt(v1) + v2.rawValue); }
    public static sfloat operator -(sfloat v1, int v2) { return FromRaw(v1.rawValue - Fixed32.FromInt(v2)); }
    public static sfloat operator -(int v1, sfloat v2) { return FromRaw(Fixed32.FromInt(v1) - v2.rawValue); }
    public static sfloat operator *(sfloat v1, int v2) { return FromRaw(v1.rawValue * (int)v2); }
    public static sfloat operator *(int v1, sfloat v2) { return FromRaw((int)v1 * v2.rawValue); }
    public static sfloat operator /(sfloat v1, int v2) { return FromRaw(v1.rawValue / (int)v2); }
    public static sfloat operator /(int v1, sfloat v2) { return FromRaw(Fixed32.DivPrecise(Fixed32.FromInt(v1), v2.rawValue)); }
    public static sfloat operator %(sfloat v1, int v2) { return FromRaw(Fixed32.Mod(v1.rawValue, Fixed32.FromInt(v2))); }
    public static sfloat operator %(int v1, sfloat v2) { return FromRaw(Fixed32.Mod(Fixed32.FromInt(v1), v2.rawValue)); }

    public static sfloat operator ++(sfloat v1) { return FromRaw(v1.rawValue + Fixed32.One); }
    public static sfloat operator --(sfloat v1) { return FromRaw(v1.rawValue - Fixed32.One); }

    public static bool operator ==(sfloat v1, sfloat v2) { return v1.rawValue == v2.rawValue; }
    public static bool operator !=(sfloat v1, sfloat v2) { return v1.rawValue != v2.rawValue; }
    public static bool operator <(sfloat v1, sfloat v2) { return v1.rawValue < v2.rawValue; }
    public static bool operator <=(sfloat v1, sfloat v2) { return v1.rawValue <= v2.rawValue; }
    public static bool operator >(sfloat v1, sfloat v2) { return v1.rawValue > v2.rawValue; }
    public static bool operator >=(sfloat v1, sfloat v2) { return v1.rawValue >= v2.rawValue; }

    public static bool operator ==(int v1, sfloat v2) { return Fixed32.FromInt(v1) == v2.rawValue; }
    public static bool operator ==(sfloat v1, int v2) { return v1.rawValue == Fixed32.FromInt(v2); }
    public static bool operator !=(int v1, sfloat v2) { return Fixed32.FromInt(v1) != v2.rawValue; }
    public static bool operator !=(sfloat v1, int v2) { return v1.rawValue != Fixed32.FromInt(v2); }
    public static bool operator <(int v1, sfloat v2) { return Fixed32.FromInt(v1) < v2.rawValue; }
    public static bool operator <(sfloat v1, int v2) { return v1.rawValue < Fixed32.FromInt(v2); }
    public static bool operator <=(int v1, sfloat v2) { return Fixed32.FromInt(v1) <= v2.rawValue; }
    public static bool operator <=(sfloat v1, int v2) { return v1.rawValue <= Fixed32.FromInt(v2); }
    public static bool operator >(int v1, sfloat v2) { return Fixed32.FromInt(v1) > v2.rawValue; }
    public static bool operator >(sfloat v1, int v2) { return v1.rawValue > Fixed32.FromInt(v2); }
    public static bool operator >=(int v1, sfloat v2) { return Fixed32.FromInt(v1) >= v2.rawValue; }
    public static bool operator >=(sfloat v1, int v2) { return v1.rawValue >= Fixed32.FromInt(v2); }

    public bool Equals(sfloat other) {
        return this.rawValue == other.rawValue;
    }

    public int CompareTo(sfloat other) {
        return this.rawValue.CompareTo(other.rawValue);
    }

    public int CompareTo(object obj) {
        return obj is sfloat f ? this.CompareTo(f) : throw new ArgumentException("obj");
    }

    public override string ToString() {
        return Fixed32.ToString(this.rawValue);
    }

    public string ToString(string format) {
        return ((float)this).ToString(format);
    }
    
    public string ToString(System.Globalization.CultureInfo cultureInfo) {
        return ((float)this).ToString(cultureInfo);
    }

    public string ToString(string format, IFormatProvider formatProvider) {
        return ((float)this).ToString(format, formatProvider);
    }

}
#else
// Internal representation is identical to IEEE binary32 floating point numbers
[DebuggerDisplay("{ToStringInv()}")]
[System.Serializable]
public struct sfloat : IEquatable<sfloat>, IComparable<sfloat>, IComparable, IFormattable {

    /// <summary>
    /// Raw byte representation of an sfloat number
    /// </summary>
    public uint rawValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private sfloat(uint raw) {
        this.rawValue = raw;
    }

    /// <summary>
    /// Creates an sfloat number from its raw byte representation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sfloat FromRaw(uint raw) {
        return new sfloat(raw);
    }

    public uint RawValue => this.rawValue;

    private uint RawMantissa => this.rawValue & 0x7FFFFF;

    private int Mantissa {
        get {
            if (this.RawExponent != 0) {
                var sign = (uint)((int)this.rawValue >> 31);
                return (int)(((this.RawMantissa | 0x800000) ^ sign) - sign);
            } else {
                var sign = (uint)((int)this.rawValue >> 31);
                return (int)((this.RawMantissa ^ sign) - sign);
            }
        }
    }

    private sbyte Exponent => (sbyte)(this.RawExponent - ExponentBias);
    private byte RawExponent => (byte)(this.rawValue >> MantissaBits);


    private const uint SignMask = 0x80000000;
    private const int MantissaBits = 23;
    private const int ExponentBits = 8;
    private const int ExponentBias = 127;

    private const uint RawZero = 0;
    private const uint RawNaN = 0xFFC00000; // Same as float.NaN
    private const uint RawPositiveInfinity = 0x7F800000;
    private const uint RawNegativeInfinity = RawPositiveInfinity ^ SignMask;
    private const uint RawOne = 0x3F800000;
    private const uint RawMinusOne = RawOne ^ SignMask;
    private const uint RawMaxValue = 0x7F7FFFFF;
    private const uint RawMinValue = 0x7F7FFFFF ^ SignMask;
    private const uint RawEpsilon = 0x00000001;
    private const uint RawLog2OfE = 0;


    public static sfloat Zero => new(0);
    public static sfloat PositiveInfinity => new(RawPositiveInfinity);
    public static sfloat NegativeInfinity => new(RawNegativeInfinity);
    public static sfloat NaN => new(RawNaN);
    public static sfloat One => new(RawOne);
    public static sfloat MinusOne => new(RawMinusOne);
    public static sfloat MaxValue => new(RawMaxValue);
    public static sfloat MinValue => new(RawMinValue);
    public static sfloat Epsilon => new(RawEpsilon);

    public override string ToString() {
        return ((float)this).ToString();
    }

    /// <summary>
    /// Creates an sfloat number from its parts: sign, exponent, mantissa
    /// </summary>
    /// <param name="sign">Sign of the number: false = the number is positive, true = the number is negative</param>
    /// <param name="exponent">Exponent of the number</param>
    /// <param name="mantissa">Mantissa (significand) of the number</param>
    /// <returns></returns>
    public static sfloat FromParts(bool sign, uint exponent, uint mantissa) {
        var g = (sign ? SignMask : 0) | ((exponent & 0xff) << MantissaBits) | (mantissa & ((1 << MantissaBits) - 1));
        return FromRaw(g);
    }

    /// <summary>
    /// Creates an sfloat number from a float value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sfloat(float f) {
        var raw = ReinterpretFloatToInt32(f);
        return new sfloat(raw);
    }

    /// <summary>
    /// Converts an sfloat number to a float value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator float(sfloat f) {
        var raw = f.rawValue;
        return ReinterpretIntToFloat32(raw);
    }

    /// <summary>
    /// Converts an sfloat number to an integer
    /// </summary>
    public static explicit operator int(sfloat f) {
        if (f.Exponent < 0) {
            return 0;
        }

        var shift = MantissaBits - f.Exponent;
        var mantissa = (int)(f.RawMantissa | (1 << MantissaBits));
        var value = shift < 0 ? mantissa << -shift : mantissa >> shift;
        return f.IsPositive() ? value : -value;
    }

    /// <summary>
    /// Creates an sfloat number from an integer
    /// </summary>
    public static explicit operator sfloat(int value) {
        if (value == 0) {
            return Zero;
        }

        if (value == int.MinValue) {
            // special case
            return FromRaw(0xcf000000);
        }

        var negative = value < 0;
        var u = (uint)Math.Abs(value);

        int shifts;

        var lzcnt = clz(u);
        if (lzcnt < 8) {
            var count = 8 - (int)lzcnt;
            u >>= count;
            shifts = -count;
        } else {
            var count = (int)lzcnt - 8;
            u <<= count;
            shifts = count;
        }

        var exponent = (uint)(ExponentBias + MantissaBits - shifts);
        return FromParts(negative, exponent, u);
    }

    public static explicit operator sfloat(uint value) {
        if (value == 0) {
            return Zero;
        }

        var u = value;

        int shifts;

        var lzcnt = clz(u);
        if (lzcnt < 8) {
            var count = 8 - (int)lzcnt;
            u >>= count;
            shifts = -count;
        } else {
            var count = (int)lzcnt - 8;
            u <<= count;
            shifts = count;
        }

        var exponent = (uint)(ExponentBias + MantissaBits - shifts);
        return FromParts(false, exponent, u);
    }

    private static readonly uint[] debruijn32 = new uint[32] {
        0, 31, 9, 30, 3, 8, 13, 29, 2, 5, 7, 21, 12, 24, 28, 19,
        1, 10, 4, 14, 6, 22, 25, 20, 11, 15, 23, 26, 16, 27, 17, 18,
    };

    /// <summary>
    /// Returns the leading zero count of the given 32-bit unsigned integer
    /// </summary>
    private static uint clz(uint x) {
        if (x == 0) {
            return 32;
        }

        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        x++;

        return debruijn32[(x * 0x076be629) >> 27];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sfloat operator -(sfloat f) {
        return new sfloat(f.rawValue ^ 0x80000000);
    }

    private static sfloat InternalAdd(sfloat f1, sfloat f2) {
        var rawExp1 = f1.RawExponent;
        var rawExp2 = f2.RawExponent;
        var deltaExp = rawExp1 - rawExp2;

        if (rawExp1 != 255) {
            //Finite
            if (deltaExp > 25) {
                return f1;
            }

            int man1;
            int man2;
            if (rawExp2 != 0) {
                // man1 = f1.Mantissa
                // http://graphics.stanford.edu/~seander/bithacks.html#ConditionalNegate
                var sign1 = (uint)((int)f1.rawValue >> 31);
                man1 = (int)(((f1.RawMantissa | 0x800000) ^ sign1) - sign1);
                // man2 = f2.Mantissa
                var sign2 = (uint)((int)f2.rawValue >> 31);
                man2 = (int)(((f2.RawMantissa | 0x800000) ^ sign2) - sign2);
            } else {
                // Subnorm
                // man2 = f2.Mantissa
                var sign2 = (uint)((int)f2.rawValue >> 31);
                man2 = (int)((f2.RawMantissa ^ sign2) - sign2);

                man1 = f1.Mantissa;

                rawExp2 = 1;
                if (rawExp1 == 0) {
                    rawExp1 = 1;
                }

                deltaExp = rawExp1 - rawExp2;
            }

            var man = (man1 << 6) + ((man2 << 6) >> deltaExp);
            var absMan = (uint)Math.Abs(man);
            if (absMan == 0) {
                return Zero;
            }

            var msb = absMan >> MantissaBits;
            var rawExp = rawExp1 - 6;
            while (msb == 0) {
                rawExp -= 8;
                absMan <<= 8;
                msb = absMan >> MantissaBits;
            }

            var msbIndex = BitScanReverse8(msb);
            rawExp += msbIndex;
            absMan >>= msbIndex;
            if ((uint)(rawExp - 1) < 254) {
                var raw = ((uint)man & 0x80000000) | ((uint)rawExp << MantissaBits) | (absMan & 0x7FFFFF);
                return new sfloat(raw);
            } else {
                if (rawExp >= 255) {
                    //Overflow
                    return man >= 0 ? PositiveInfinity : NegativeInfinity;
                }

                if (rawExp >= -24) {
                    var raw = ((uint)man & 0x80000000) | (absMan >> (-rawExp + 1));
                    return new sfloat(raw);
                }

                return Zero;
            }
        } else {
            // Special

            if (rawExp2 != 255) {
                // f1 is NaN, +Inf, -Inf and f2 is finite
                return f1;
            }

            // Both not finite
            return f1.rawValue == f2.rawValue ? f1 : NaN;
        }
    }

    public static sfloat operator +(sfloat f1, sfloat f2) {
        return f1.RawExponent - f2.RawExponent >= 0 ? InternalAdd(f1, f2) : InternalAdd(f2, f1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sfloat operator -(sfloat f1, sfloat f2) {
        return f1 + -f2;
    }

    public static sfloat operator *(sfloat f1, sfloat f2) {
        int man1;
        int rawExp1 = f1.RawExponent;
        uint sign1;
        uint sign2;
        if (rawExp1 == 0) {
            // SubNorm
            sign1 = (uint)((int)f1.rawValue >> 31);
            var rawMan1 = f1.RawMantissa;
            if (rawMan1 == 0) {
                if (f2.IsFinite()) {
                    // 0 * f2
                    return new sfloat((f1.rawValue ^ f2.rawValue) & SignMask);
                } else {
                    // 0 * Infinity
                    // 0 * NaN
                    return NaN;
                }
            }

            rawExp1 = 1;
            while ((rawMan1 & 0x800000) == 0) {
                rawMan1 <<= 1;
                rawExp1--;
            }

            //Debug.Assert(rawMan1 >> MantissaBits == 1);
            man1 = (int)((rawMan1 ^ sign1) - sign1);
        } else if (rawExp1 != 255) {
            // Norm
            sign1 = (uint)((int)f1.rawValue >> 31);
            man1 = (int)(((f1.RawMantissa | 0x800000) ^ sign1) - sign1);
        } else {
            // Non finite
            if (f1.rawValue == RawPositiveInfinity) {
                if (f2.IsZero()) {
                    // Infinity * 0
                    return NaN;
                }

                if (f2.IsNaN()) {
                    // Infinity * NaN
                    return NaN;
                }

                if ((int)f2.rawValue >= 0) {
                    // Infinity * f
                    return PositiveInfinity;
                } else {
                    // Infinity * -f
                    return NegativeInfinity;
                }
            } else if (f1.rawValue == RawNegativeInfinity) {
                if (f2.IsZero() || f2.IsNaN()) {
                    // -Infinity * 0
                    // -Infinity * NaN
                    return NaN;
                }

                if ((int)f2.rawValue < 0) {
                    // -Infinity * -f
                    return PositiveInfinity;
                } else {
                    // -Infinity * f
                    return NegativeInfinity;
                }
            } else {
                return f1;
            }
        }

        int man2;
        int rawExp2 = f2.RawExponent;
        if (rawExp2 == 0) {
            // SubNorm
            sign2 = (uint)((int)f2.rawValue >> 31);
            var rawMan2 = f2.RawMantissa;
            if (rawMan2 == 0) {
                if (f1.IsFinite()) {
                    // f1 * 0
                    return new sfloat((f1.rawValue ^ f2.rawValue) & SignMask);
                } else {
                    // Infinity * 0
                    // NaN * 0
                    return NaN;
                }
            }

            rawExp2 = 1;
            while ((rawMan2 & 0x800000) == 0) {
                rawMan2 <<= 1;
                rawExp2--;
            }

            //Debug.Assert(rawMan2 >> MantissaBits == 1);
            man2 = (int)((rawMan2 ^ sign2) - sign2);
        } else if (rawExp2 != 255) {
            // Norm
            sign2 = (uint)((int)f2.rawValue >> 31);
            man2 = (int)(((f2.RawMantissa | 0x800000) ^ sign2) - sign2);
        } else {
            // Non finite
            if (f2.rawValue == RawPositiveInfinity) {
                if (f1.IsZero()) {
                    // 0 * Infinity
                    return NaN;
                }

                if ((int)f1.rawValue >= 0) {
                    // f * Infinity
                    return PositiveInfinity;
                } else {
                    // -f * Infinity
                    return NegativeInfinity;
                }
            } else if (f2.rawValue == RawNegativeInfinity) {
                if (f1.IsZero()) {
                    // 0 * -Infinity
                    return NaN;
                }

                if ((int)f1.rawValue < 0) {
                    // -f * -Infinity
                    return PositiveInfinity;
                } else {
                    // f * -Infinity
                    return NegativeInfinity;
                }
            } else {
                return f2;
            }
        }

        var longMan = (long)man1 * (long)man2;
        var man = (int)(longMan >> MantissaBits);
        //Debug.Assert(man != 0);
        var absMan = (uint)Math.Abs(man);
        var rawExp = rawExp1 + rawExp2 - ExponentBias;
        var sign = (uint)man & 0x80000000;
        if ((absMan & 0x1000000) != 0) {
            absMan >>= 1;
            rawExp++;
        }

        //Debug.Assert(absMan >> MantissaBits == 1);
        if (rawExp >= 255) {
            // Overflow
            return new sfloat(sign ^ RawPositiveInfinity);
        }

        if (rawExp <= 0) {
            // Subnorms/Underflow
            if (rawExp <= -24) {
                return new sfloat(sign);
            }

            absMan >>= -rawExp + 1;
            rawExp = 0;
        }

        var raw = sign | ((uint)rawExp << MantissaBits) | (absMan & 0x7FFFFF);
        return new sfloat(raw);
    }

    public static sfloat operator /(sfloat f1, sfloat f2) {
        if (f1.IsNaN() || f2.IsNaN()) {
            return NaN;
        }

        int man1;
        int rawExp1 = f1.RawExponent;
        uint sign1;
        uint sign2;
        if (rawExp1 == 0) {
            // SubNorm
            sign1 = (uint)((int)f1.rawValue >> 31);
            var rawMan1 = f1.RawMantissa;
            if (rawMan1 == 0) {
                if (f2.IsZero()) {
                    // 0 / 0
                    return NaN;
                } else {
                    // 0 / f
                    return new sfloat((f1.rawValue ^ f2.rawValue) & SignMask);
                }
            }

            rawExp1 = 1;
            while ((rawMan1 & 0x800000) == 0) {
                rawMan1 <<= 1;
                rawExp1--;
            }

            //Debug.Assert(rawMan1 >> MantissaBits == 1);
            man1 = (int)((rawMan1 ^ sign1) - sign1);
        } else if (rawExp1 != 255) {
            // Norm
            sign1 = (uint)((int)f1.rawValue >> 31);
            man1 = (int)(((f1.RawMantissa | 0x800000) ^ sign1) - sign1);
        } else {
            // Non finite
            if (f1.rawValue == RawPositiveInfinity) {
                if (f2.IsZero()) {
                    // Infinity / 0
                    return PositiveInfinity;
                }

                // +-Infinity / Infinity
                return NaN;
            } else if (f1.rawValue == RawNegativeInfinity) {
                if (f2.IsZero()) {
                    // -Infinity / 0
                    return NegativeInfinity;
                }

                // -Infinity / +-Infinity
                return NaN;
            } else {
                // NaN
                return f1;
            }
        }

        int man2;
        int rawExp2 = f2.RawExponent;
        if (rawExp2 == 0) {
            // SubNorm
            sign2 = (uint)((int)f2.rawValue >> 31);
            var rawMan2 = f2.RawMantissa;
            if (rawMan2 == 0) {
                // f / 0
                return new sfloat(((f1.rawValue ^ f2.rawValue) & SignMask) | RawPositiveInfinity);
            }

            rawExp2 = 1;
            while ((rawMan2 & 0x800000) == 0) {
                rawMan2 <<= 1;
                rawExp2--;
            }

            //Debug.Assert(rawMan2 >> MantissaBits == 1);
            man2 = (int)((rawMan2 ^ sign2) - sign2);
        } else if (rawExp2 != 255) {
            // Norm
            sign2 = (uint)((int)f2.rawValue >> 31);
            man2 = (int)(((f2.RawMantissa | 0x800000) ^ sign2) - sign2);
        } else {
            // Non finite
            if (f2.rawValue == RawPositiveInfinity) {
                if (f1.IsZero()) {
                    // 0 / Infinity
                    return Zero;
                }

                if ((int)f1.rawValue >= 0) {
                    // f / Infinity
                    return PositiveInfinity;
                } else {
                    // -f / Infinity
                    return NegativeInfinity;
                }
            } else if (f2.rawValue == RawNegativeInfinity) {
                if (f1.IsZero()) {
                    // 0 / -Infinity
                    return new sfloat(SignMask);
                }

                if ((int)f1.rawValue < 0) {
                    // -f / -Infinity
                    return PositiveInfinity;
                } else {
                    // f / -Infinity
                    return NegativeInfinity;
                }
            } else {
                // NaN
                return f2;
            }
        }

        var longMan = ((long)man1 << MantissaBits) / (long)man2;
        var man = (int)longMan;
        //Debug.Assert(man != 0);
        var absMan = (uint)Math.Abs(man);
        var rawExp = rawExp1 - rawExp2 + ExponentBias;
        var sign = (uint)man & 0x80000000;

        if ((absMan & 0x800000) == 0) {
            absMan <<= 1;
            --rawExp;
        }

        //Debug.Assert(absMan >> MantissaBits == 1);
        if (rawExp >= 255) {
            // Overflow
            return new sfloat(sign ^ RawPositiveInfinity);
        }

        if (rawExp <= 0) {
            // Subnorms/Underflow
            if (rawExp <= -24) {
                return new sfloat(sign);
            }

            absMan >>= -rawExp + 1;
            rawExp = 0;
        }

        var raw = sign | ((uint)rawExp << MantissaBits) | (absMan & 0x7FFFFF);
        return new sfloat(raw);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sfloat operator %(sfloat f1, sfloat f2) {
        return libm.fmodf(f1, f2);
    }

    private static readonly sbyte[] msb = new sbyte[256] {
        -1, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BitScanReverse8(uint b) {
        return msb[b];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint ReinterpretFloatToInt32(float f) {
        return *(uint*)&f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float ReinterpretIntToFloat32(uint i) {
        return *(float*)&i;
    }

    public override bool Equals(object obj) {
        return obj != null && this.GetType() == obj.GetType() && this.Equals((sfloat)obj);
    }

    public bool Equals(sfloat other) {
        if (this.RawExponent != 255) {
            // 0 == -0
            return this.rawValue == other.rawValue || ((this.rawValue & 0x7FFFFFFF) == 0 && (other.rawValue & 0x7FFFFFFF) == 0);
        } else {
            if (this.RawMantissa == 0) {
                // Infinities
                return this.rawValue == other.rawValue;
            } else {
                // NaNs are equal for `Equals` (as opposed to the == operator)
                return other.RawMantissa != 0;
            }
        }
    }

    public override int GetHashCode() {
        if (this.rawValue == SignMask) {
            // +0 equals -0
            return 0;
        }

        if (!this.IsNaN()) {
            return (int)this.rawValue;
        } else {
            // All NaNs are equal
            return unchecked((int)RawNaN);
        }
    }

    public static bool operator ==(sfloat f1, sfloat f2) {
        if (f1.RawExponent != 255) {
            // 0 == -0
            return f1.rawValue == f2.rawValue || ((f1.rawValue & 0x7FFFFFFF) == 0 && (f2.rawValue & 0x7FFFFFFF) == 0);
        } else {
            if (f1.RawMantissa == 0) {
                // Infinities
                return f1.rawValue == f2.rawValue;
            } else {
                //NaNs
                return false;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(sfloat f1, sfloat f2) {
        return !(f1 == f2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(sfloat f1, sfloat f2) {
        return !f1.IsNaN() && !f2.IsNaN() && f1.CompareTo(f2) < 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(sfloat f1, sfloat f2) {
        return !f1.IsNaN() && !f2.IsNaN() && f1.CompareTo(f2) > 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(sfloat f1, sfloat f2) {
        return !f1.IsNaN() && !f2.IsNaN() && f1.CompareTo(f2) <= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(sfloat f1, sfloat f2) {
        return !f1.IsNaN() && !f2.IsNaN() && f1.CompareTo(f2) >= 0;
    }

    public int CompareTo(sfloat other) {
        if (this.IsNaN() && other.IsNaN()) {
            return 0;
        }

        var sign1 = (uint)((int)this.rawValue >> 31);
        var val1 = (int)((this.rawValue ^ (sign1 & 0x7FFFFFFF)) - sign1);

        var sign2 = (uint)((int)other.rawValue >> 31);
        var val2 = (int)((other.rawValue ^ (sign2 & 0x7FFFFFFF)) - sign2);
        return val1.CompareTo(val2);
    }

    public int CompareTo(object obj) {
        return obj is sfloat f ? this.CompareTo(f) : throw new ArgumentException("obj");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsInfinity() {
        return (this.rawValue & 0x7FFFFFFF) == 0x7F800000;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNegativeInfinity() {
        return this.rawValue == RawNegativeInfinity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPositiveInfinity() {
        return this.rawValue == RawPositiveInfinity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNaN() {
        return this.RawExponent == 255 && !this.IsInfinity();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsFinite() {
        return this.RawExponent != 255;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsZero() {
        return (this.rawValue & 0x7FFFFFFF) == 0;
    }

    public string ToString(string format, IFormatProvider formatProvider) {
        return ((float)this).ToString(format, formatProvider);
    }

    public string ToString(string format) {
        return ((float)this).ToString(format);
    }

    public string ToString(IFormatProvider provider) {
        return ((float)this).ToString(provider);
    }

    public string ToStringInv() {
        return ((float)this).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the absolute value of the given sfloat number
    /// </summary>
    public static sfloat Abs(sfloat f) {
        if (f.RawExponent != 255 || f.IsInfinity()) {
            return new sfloat(f.rawValue & 0x7FFFFFFF);
        } else {
            // Leave NaN untouched
            return f;
        }
    }

    /// <summary>
    /// Returns the maximum of the two given sfloat values. Returns NaN iff either argument is NaN.
    /// </summary>
    public static sfloat Max(sfloat val1, sfloat val2) {
        if (val1 > val2) {
            return val1;
        } else if (val1.IsNaN()) {
            return val1;
        } else {
            return val2;
        }
    }

    /// <summary>
    /// Returns the minimum of the two given sfloat values. Returns NaN iff either argument is NaN.
    /// </summary>
    public static sfloat Min(sfloat val1, sfloat val2) {
        if (val1 < val2) {
            return val1;
        } else if (val1.IsNaN()) {
            return val1;
        } else {
            return val2;
        }
    }

    /// <summary>
    /// Returns true if the sfloat number has a positive sign.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPositive() {
        return (this.rawValue & 0x80000000) == 0;
    }

    /// <summary>
    /// Returns true if the sfloat number has a negative sign.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNegative() {
        return (this.rawValue & 0x80000000) != 0;
    }

    public int Sign() {
        if (this.IsNaN()) {
            return 0;
        }

        if (this.IsZero()) {
            return 0;
        } else if (this.IsPositive()) {
            return 1;
        } else {
            return -1;
        }
    }

}
#endif