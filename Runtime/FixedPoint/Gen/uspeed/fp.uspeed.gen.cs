namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct uspeed : System.IEquatable<uspeed> {

        public const uint PRECISION = 100u; // 100 values
        public const uint PRECISION_SQRT = 1000u;
        
        public static readonly uspeed maxValue = new uspeed(1u * 100u);
        public static readonly uspeed minValue = new uspeed(0u);
        public static readonly uspeed zero = new uspeed(0);
        public static readonly uspeed one = new uspeed(PRECISION);

        [UnityEngine.SerializeField]
        internal uint value;

        [INLINE(256)]
        public uspeed(uint rawValue) => this.value = rawValue;

        

        [INLINE(256)]
        public static uspeed FromFloat(float value) {
            var ms = new uspeed {
                value = (uint)(value * PRECISION),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator uspeed(uint value) => new uspeed(value);
        
        [INLINE(256)]
        public static uspeed operator *(float value1, uspeed value2) {
            return new uspeed((uint)(value1 * value2.value));
        }

        [INLINE(256)]
        public static uspeed operator *(uspeed value1, float value2) {
            return new uspeed((uint)(value1.value * value2));
        }

        [INLINE(256)]
        public static uspeed operator +(uspeed value1, uspeed value2) {
            if (value1 >= maxValue || value2 >= maxValue) {
                return uspeed.maxValue;
            }
            value1.value += value2.value;
            return value1;
        }
        
        [INLINE(256)]
        public static uspeed operator -(uspeed value1, uspeed value2) {
            if (value2.value > value1.value) {
                value1 = uspeed.minValue;
            } else {
                value1.value -= value2.value;
            }
            return value1;
        }

        [INLINE(256)]
        public static uspeed operator *(uspeed value1, uspeed value2) {
            return new uspeed((uint)((ulong)value1.value * value2.value / PRECISION));
        }

        [INLINE(256)]
        public static uspeed operator /(uspeed value1, uspeed value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static bool operator <(uspeed value1, uspeed value2) {
            return value1.value < value2.value;
        }

        [INLINE(256)]
        public static bool operator >(uspeed value1, uspeed value2) {
            return value1.value > value2.value;
        }

        [INLINE(256)]
        public static bool operator ==(uspeed value1, uspeed value2) {
            return value1.value == value2.value;
        }

        [INLINE(256)]
        public static bool operator !=(uspeed value1, uspeed value2) {
            return value1.value != value2.value;
        }

        [INLINE(256)]
        public static bool operator <=(uspeed value1, uspeed value2) {
            return value1.value <= value2.value;
        }

        [INLINE(256)]
        public static bool operator >=(uspeed value1, uspeed value2) {
            return value1.value >= value2.value;
        }
        
        [INLINE(256)]
        public readonly int CompareTo(uspeed other) {
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
        public static uspeed Parse(string s) {
            var val = float.Parse(s);
            return uspeed.FromFloat(val);
        }

        [INLINE(256)]
        public readonly bool Equals(uspeed other) {
            return this.value == other.value;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is uspeed other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.value.GetHashCode();
        }

    }

}