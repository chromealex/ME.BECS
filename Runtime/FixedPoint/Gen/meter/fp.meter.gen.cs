namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct meter : System.IEquatable<meter> {

        public const int PRECISION = 1000; // 1000 values in 1 meter
        public const int PRECISION_SQRT = 31622;
        
        public static readonly meter maxValue = new meter(int.MaxValue);
        public static readonly meter minValue = new meter(0);
        public static readonly meter zero = new meter(0);
        public static readonly meter oneMeter = new meter(PRECISION);

        [UnityEngine.SerializeField]
        internal int value;

        [INLINE(256)]
        public meter(int rawValue) => this.value = rawValue;

        

        [INLINE(256)]
        public static meter FromFloat(float value) {
            var ms = new meter {
                value = (int)(value * PRECISION),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static meter FromSFloat(sfloat value) {
            var ms = new meter {
                value = (int)(value * PRECISION),
            };
            return ms;
        }

        [INLINE(256)]
        public static implicit operator meter(int value) => new meter(value);
        
        [INLINE(256)]
        public static meter operator *(float value1, meter value2) {
            return new meter((int)(value1 * value2.value));
        }

        [INLINE(256)]
        public static meter operator *(meter value1, float value2) {
            return new meter((int)(value1.value * value2));
        }

        [INLINE(256)]
        public static meter operator +(meter value1, meter value2) {
            if (value1 >= maxValue || value2 >= maxValue) {
                return meter.maxValue;
            }
            value1.value += value2.value;
            return value1;
        }
        
        [INLINE(256)]
        public static meter operator -(meter value1, meter value2) {
            if (value2.value > value1.value) {
                value1 = meter.minValue;
            } else {
                value1.value -= value2.value;
            }
            return value1;
        }

        [INLINE(256)]
        public static meter operator *(meter value1, meter value2) {
            return new meter((int)((long)value1.value * value2.value / PRECISION));
        }

        [INLINE(256)]
        public static meter operator /(meter value1, meter value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static bool operator <(meter value1, meter value2) {
            return value1.value < value2.value;
        }

        [INLINE(256)]
        public static bool operator >(meter value1, meter value2) {
            return value1.value > value2.value;
        }

        [INLINE(256)]
        public static bool operator ==(meter value1, meter value2) {
            return value1.value == value2.value;
        }

        [INLINE(256)]
        public static bool operator !=(meter value1, meter value2) {
            return value1.value != value2.value;
        }

        [INLINE(256)]
        public static bool operator <=(meter value1, meter value2) {
            return value1.value <= value2.value;
        }

        [INLINE(256)]
        public static bool operator >=(meter value1, meter value2) {
            return value1.value >= value2.value;
        }
        
        [INLINE(256)]
        public readonly int CompareTo(meter other) {
            return this.value.CompareTo(other.value);
        }

        [INLINE(256)]
        public readonly float ToFloat() {
            return this.value / (float)PRECISION;
        }

        [INLINE(256)]
        public readonly int ToValue() {
            return this.value / PRECISION;
        }

        [INLINE(256)]
        public override readonly string ToString() {
            return (this.value / (float)PRECISION).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        [INLINE(256)]
        public static meter Parse(string s) {
            var val = float.Parse(s);
            return meter.FromFloat(val);
        }

        [INLINE(256)]
        public readonly bool Equals(meter other) {
            return this.value == other.value;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is meter other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.value.GetHashCode();
        }

    }

}