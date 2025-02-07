namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct svalue : System.IEquatable<svalue> {

        public const int PRECISION = 1000; // 1000 values in 0..1
        public const int PRECISION_SQRT = 31622;
        
        public static readonly svalue maxValue = new svalue(int.MaxValue);
        public static readonly svalue minValue = new svalue(0);
        public static readonly svalue zero = new svalue(0);
        public static readonly svalue one = new svalue(PRECISION);

        [UnityEngine.SerializeField]
        internal int value;

        [INLINE(256)]
        public svalue(int rawValue) => this.value = rawValue;

        

        [INLINE(256)]
        public static svalue FromFloat(float value) {
            var ms = new svalue {
                value = (int)(value * PRECISION),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator svalue(int value) => new svalue(value);
        
        [INLINE(256)]
        public static svalue operator *(float value1, svalue value2) {
            return new svalue((int)(value1 * value2.value));
        }

        [INLINE(256)]
        public static svalue operator *(svalue value1, float value2) {
            return new svalue((int)(value1.value * value2));
        }

        [INLINE(256)]
        public static svalue operator +(svalue value1, svalue value2) {
            if (value1 >= maxValue || value2 >= maxValue) {
                return svalue.maxValue;
            }
            value1.value += value2.value;
            return value1;
        }
        
        [INLINE(256)]
        public static svalue operator -(svalue value1, svalue value2) {
            if (value2.value > value1.value) {
                value1 = svalue.minValue;
            } else {
                value1.value -= value2.value;
            }
            return value1;
        }

        [INLINE(256)]
        public static svalue operator *(svalue value1, svalue value2) {
            return new svalue((int)((long)value1.value * value2.value / PRECISION));
        }

        [INLINE(256)]
        public static svalue operator /(svalue value1, svalue value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static bool operator <(svalue value1, svalue value2) {
            return value1.value < value2.value;
        }

        [INLINE(256)]
        public static bool operator >(svalue value1, svalue value2) {
            return value1.value > value2.value;
        }

        [INLINE(256)]
        public static bool operator ==(svalue value1, svalue value2) {
            return value1.value == value2.value;
        }

        [INLINE(256)]
        public static bool operator !=(svalue value1, svalue value2) {
            return value1.value != value2.value;
        }

        [INLINE(256)]
        public static bool operator <=(svalue value1, svalue value2) {
            return value1.value <= value2.value;
        }

        [INLINE(256)]
        public static bool operator >=(svalue value1, svalue value2) {
            return value1.value >= value2.value;
        }
        
        [INLINE(256)]
        public readonly int CompareTo(svalue other) {
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
        public static svalue Parse(string s) {
            var val = float.Parse(s);
            return svalue.FromFloat(val);
        }

        [INLINE(256)]
        public readonly bool Equals(svalue other) {
            return this.value == other.value;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is svalue other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.value.GetHashCode();
        }

    }

}