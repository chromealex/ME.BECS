namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct umeter : System.IEquatable<umeter> {

        public const uint PRECISION = 1000u; // 1000 values in 1 meter
        public const uint PRECISION_SQRT = 31622u;
        
        public static readonly umeter maxValue = new umeter(uint.MaxValue);
        public static readonly umeter minValue = new umeter(0u);
        public static readonly umeter zero = new umeter(0);
        public static readonly umeter oneMeter = new umeter(PRECISION);

        [UnityEngine.SerializeField]
        internal uint value;

        [INLINE(256)]
        public umeter(uint rawValue) => this.value = rawValue;

        

        [INLINE(256)]
        public static umeter FromFloat(float value) {
            var ms = new umeter {
                value = (uint)(value * PRECISION),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator umeter(uint value) => new umeter(value);
        
        [INLINE(256)]
        public static umeter operator *(float value1, umeter value2) {
            return new umeter((uint)(value1 * value2.value));
        }

        [INLINE(256)]
        public static umeter operator *(umeter value1, float value2) {
            return new umeter((uint)(value1.value * value2));
        }

        [INLINE(256)]
        public static umeter operator +(umeter value1, umeter value2) {
            if (value1 >= maxValue || value2 >= maxValue) {
                return umeter.maxValue;
            }
            value1.value += value2.value;
            return value1;
        }
        
        [INLINE(256)]
        public static umeter operator -(umeter value1, umeter value2) {
            if (value2.value > value1.value) {
                value1 = umeter.minValue;
            } else {
                value1.value -= value2.value;
            }
            return value1;
        }

        [INLINE(256)]
        public static umeter operator *(umeter value1, umeter value2) {
            return new umeter((uint)((ulong)value1.value * value2.value / PRECISION));
        }

        [INLINE(256)]
        public static umeter operator /(umeter value1, umeter value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static bool operator <(umeter value1, umeter value2) {
            return value1.value < value2.value;
        }

        [INLINE(256)]
        public static bool operator >(umeter value1, umeter value2) {
            return value1.value > value2.value;
        }

        [INLINE(256)]
        public static bool operator ==(umeter value1, umeter value2) {
            return value1.value == value2.value;
        }

        [INLINE(256)]
        public static bool operator !=(umeter value1, umeter value2) {
            return value1.value != value2.value;
        }

        [INLINE(256)]
        public static bool operator <=(umeter value1, umeter value2) {
            return value1.value <= value2.value;
        }

        [INLINE(256)]
        public static bool operator >=(umeter value1, umeter value2) {
            return value1.value >= value2.value;
        }
        
        [INLINE(256)]
        public readonly int CompareTo(umeter other) {
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
        public static umeter Parse(string s) {
            var val = float.Parse(s);
            return umeter.FromFloat(val);
        }

        [INLINE(256)]
        public readonly bool Equals(umeter other) {
            return this.value == other.value;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is umeter other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.value.GetHashCode();
        }

    }

}