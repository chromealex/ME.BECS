namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct uangle : System.IEquatable<uangle> {

        public const uint PRECISION = 100u; // 100 parts in 1 degree
        public const uint PRECISION_SQRT = 10000u;
        
        public static readonly uangle maxValue = new uangle(360u * 100u);
        public static readonly uangle minValue = new uangle(0u);
        public static readonly uangle zero = new uangle(0);
        public static readonly uangle oneDegree = new uangle(PRECISION);

        [UnityEngine.SerializeField]
        internal uint value;

        [INLINE(256)]
        public uangle(uint rawValue) => this.value = rawValue;

        

        [INLINE(256)]
        public static uangle FromFloat(float value) {
            var ms = new uangle {
                value = (uint)(value * PRECISION),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator uangle(uint value) => new uangle(value);
        
        [INLINE(256)]
        public static uangle operator *(float value1, uangle value2) {
            return new uangle((uint)(value1 * value2.value));
        }

        [INLINE(256)]
        public static uangle operator *(uangle value1, float value2) {
            return new uangle((uint)(value1.value * value2));
        }

        [INLINE(256)]
        public static uangle operator +(uangle value1, uangle value2) {
            if (value1 >= maxValue || value2 >= maxValue) {
                return uangle.maxValue;
            }
            value1.value += value2.value;
            return value1;
        }
        
        [INLINE(256)]
        public static uangle operator -(uangle value1, uangle value2) {
            if (value2.value > value1.value) {
                value1 = uangle.minValue;
            } else {
                value1.value -= value2.value;
            }
            return value1;
        }

        [INLINE(256)]
        public static uangle operator *(uangle value1, uangle value2) {
            return new uangle((uint)((ulong)value1.value * value2.value / PRECISION));
        }

        [INLINE(256)]
        public static uangle operator /(uangle value1, uangle value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static bool operator <(uangle value1, uangle value2) {
            return value1.value < value2.value;
        }

        [INLINE(256)]
        public static bool operator >(uangle value1, uangle value2) {
            return value1.value > value2.value;
        }

        [INLINE(256)]
        public static bool operator ==(uangle value1, uangle value2) {
            return value1.value == value2.value;
        }

        [INLINE(256)]
        public static bool operator !=(uangle value1, uangle value2) {
            return value1.value != value2.value;
        }

        [INLINE(256)]
        public static bool operator <=(uangle value1, uangle value2) {
            return value1.value <= value2.value;
        }

        [INLINE(256)]
        public static bool operator >=(uangle value1, uangle value2) {
            return value1.value >= value2.value;
        }
        
        [INLINE(256)]
        public readonly int CompareTo(uangle other) {
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
        public static uangle Parse(string s) {
            var val = float.Parse(s);
            return uangle.FromFloat(val);
        }

        [INLINE(256)]
        public readonly bool Equals(uangle other) {
            return this.value == other.value;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is uangle other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.value.GetHashCode();
        }

    }

}