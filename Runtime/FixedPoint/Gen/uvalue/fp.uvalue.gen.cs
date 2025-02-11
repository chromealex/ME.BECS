namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct uvalue : System.IEquatable<uvalue> {

        public const uint PRECISION = 1000u; // 1000 values in 0..1
        public const uint PRECISION_SQRT = 31622u;
        
        public static readonly uvalue maxValue = new uvalue(uint.MaxValue);
        public static readonly uvalue minValue = new uvalue(0u);
        public static readonly uvalue zero = new uvalue(0);
        public static readonly uvalue one = new uvalue(PRECISION);

        [UnityEngine.SerializeField]
        internal uint value;

        [INLINE(256)]
        public uvalue(uint rawValue) => this.value = rawValue;

        

        [INLINE(256)]
        public static uvalue FromFloat(float value) {
            var ms = new uvalue {
                value = (uint)(value * PRECISION),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator uvalue(uint value) => new uvalue(value);
        
        [INLINE(256)]
        public static uvalue operator *(float value1, uvalue value2) {
            return new uvalue((uint)(value1 * value2.value));
        }

        [INLINE(256)]
        public static uvalue operator *(uvalue value1, float value2) {
            return new uvalue((uint)(value1.value * value2));
        }

        [INLINE(256)]
        public static uvalue operator +(uvalue value1, uvalue value2) {
            if (value1 >= maxValue || value2 >= maxValue) {
                return uvalue.maxValue;
            }
            value1.value += value2.value;
            return value1;
        }
        
        [INLINE(256)]
        public static uvalue operator -(uvalue value1, uvalue value2) {
            if (value2.value > value1.value) {
                value1 = uvalue.minValue;
            } else {
                value1.value -= value2.value;
            }
            return value1;
        }

        [INLINE(256)]
        public static uvalue operator *(uvalue value1, uvalue value2) {
            return new uvalue((uint)((ulong)value1.value * value2.value / PRECISION));
        }

        [INLINE(256)]
        public static uvalue operator /(uvalue value1, uvalue value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static bool operator <(uvalue value1, uvalue value2) {
            return value1.value < value2.value;
        }

        [INLINE(256)]
        public static bool operator >(uvalue value1, uvalue value2) {
            return value1.value > value2.value;
        }

        [INLINE(256)]
        public static bool operator ==(uvalue value1, uvalue value2) {
            return value1.value == value2.value;
        }

        [INLINE(256)]
        public static bool operator !=(uvalue value1, uvalue value2) {
            return value1.value != value2.value;
        }

        [INLINE(256)]
        public static bool operator <=(uvalue value1, uvalue value2) {
            return value1.value <= value2.value;
        }

        [INLINE(256)]
        public static bool operator >=(uvalue value1, uvalue value2) {
            return value1.value >= value2.value;
        }
        
        [INLINE(256)]
        public readonly int CompareTo(uvalue other) {
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
        public static uvalue Parse(string s) {
            var val = float.Parse(s);
            return uvalue.FromFloat(val);
        }

        [INLINE(256)]
        public readonly bool Equals(uvalue other) {
            return this.value == other.value;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is uvalue other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.value.GetHashCode();
        }

    }

}