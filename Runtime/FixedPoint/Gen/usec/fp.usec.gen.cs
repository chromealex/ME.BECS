namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct usec : System.IEquatable<usec> {

        public const uint PRECISION = 1000u; // 1 second = 1000 ms, we do not care about values < 1 ms
        public const uint PRECISION_SQRT = 31622u;
        
        public static readonly usec maxValue = new usec(uint.MaxValue);
        public static readonly usec minValue = new usec(0u);
        public static readonly usec zero = new usec(0);
        public static readonly usec oneSecond = new usec(PRECISION);

        [UnityEngine.SerializeField]
        internal uint value;

        [INLINE(256)]
        public usec(uint rawValue) => this.value = rawValue;

        

        [INLINE(256)]
        public static usec FromFloat(float value) {
            var ms = new usec {
                value = (uint)(value * PRECISION),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator usec(uint value) => new usec(value);
        
        [INLINE(256)]
        public static usec operator *(float value1, usec value2) {
            return new usec((uint)(value1 * value2.value));
        }

        [INLINE(256)]
        public static usec operator *(usec value1, float value2) {
            return new usec((uint)(value1.value * value2));
        }

        [INLINE(256)]
        public static usec operator +(usec value1, usec value2) {
            if (value1 >= maxValue || value2 >= maxValue) {
                return usec.maxValue;
            }
            value1.value += value2.value;
            return value1;
        }
        
        [INLINE(256)]
        public static usec operator -(usec value1, usec value2) {
            if (value2.value > value1.value) {
                value1 = usec.minValue;
            } else {
                value1.value -= value2.value;
            }
            return value1;
        }

        [INLINE(256)]
        public static usec operator *(usec value1, usec value2) {
            return new usec((uint)((ulong)value1.value * value2.value / PRECISION));
        }

        [INLINE(256)]
        public static usec operator /(usec value1, usec value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static bool operator <(usec value1, usec value2) {
            return value1.value < value2.value;
        }

        [INLINE(256)]
        public static bool operator >(usec value1, usec value2) {
            return value1.value > value2.value;
        }

        [INLINE(256)]
        public static bool operator ==(usec value1, usec value2) {
            return value1.value == value2.value;
        }

        [INLINE(256)]
        public static bool operator !=(usec value1, usec value2) {
            return value1.value != value2.value;
        }

        [INLINE(256)]
        public static bool operator <=(usec value1, usec value2) {
            return value1.value <= value2.value;
        }

        [INLINE(256)]
        public static bool operator >=(usec value1, usec value2) {
            return value1.value >= value2.value;
        }
        
        [INLINE(256)]
        public readonly int CompareTo(usec other) {
            return this.value.CompareTo(other.value);
        }

        [INLINE(256)]
        public readonly float ToFloat() {
            return this.value / (float)PRECISION;
        }

        [INLINE(256)]
        public readonly uint ToValue() {
            return this.value / PRECISION;
        }

        [INLINE(256)]
        public override readonly string ToString() {
            return (this.value / (float)PRECISION).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        [INLINE(256)]
        public static usec Parse(string s) {
            var val = float.Parse(s);
            return usec.FromFloat(val);
        }

        [INLINE(256)]
        public readonly bool Equals(usec other) {
            return this.value == other.value;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is usec other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.value.GetHashCode();
        }

    }

}