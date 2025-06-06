namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct ucvalue : System.IEquatable<ucvalue> {

        public const uint PRECISION = 1000u; // 1000 values in 0..1
        public const uint PRECISION_SQRT = 31622u;
        
        public static readonly ucvalue maxValue = new ucvalue(1u * 1000u);
        public static readonly ucvalue minValue = new ucvalue(0u);
        public static readonly ucvalue zero = new ucvalue(0);
        public static readonly ucvalue one = new ucvalue(PRECISION);

        [UnityEngine.SerializeField]
        internal uint value;

        [INLINE(256)]
        public ucvalue(uint rawValue) => this.value = rawValue;

        

        [INLINE(256)]
        public static ucvalue FromFloat(float value) {
            var ms = new ucvalue {
                value = (uint)(value * PRECISION),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator ucvalue(uint value) => new ucvalue(value);
        
        [INLINE(256)]
        public static ucvalue operator *(float value1, ucvalue value2) {
            return new ucvalue((uint)(value1 * value2.value));
        }

        [INLINE(256)]
        public static ucvalue operator *(ucvalue value1, float value2) {
            return new ucvalue((uint)(value1.value * value2));
        }

        [INLINE(256)]
        public static ucvalue operator +(ucvalue value1, ucvalue value2) {
            if (value1 >= maxValue || value2 >= maxValue) {
                return ucvalue.maxValue;
            }
            value1.value += value2.value;
            return value1;
        }
        
        [INLINE(256)]
        public static ucvalue operator -(ucvalue value1, ucvalue value2) {
            if (value2.value > value1.value) {
                value1 = ucvalue.minValue;
            } else {
                value1.value -= value2.value;
            }
            return value1;
        }

        [INLINE(256)]
        public static ucvalue operator *(ucvalue value1, ucvalue value2) {
            return new ucvalue((uint)((ulong)value1.value * value2.value / PRECISION));
        }

        [INLINE(256)]
        public static ucvalue operator /(ucvalue value1, ucvalue value2) {
            value1.value /= value2.value;
            value1.value *= PRECISION;
            return value1;
        }

        [INLINE(256)]
        public static bool operator <(ucvalue value1, ucvalue value2) {
            return value1.value < value2.value;
        }

        [INLINE(256)]
        public static bool operator >(ucvalue value1, ucvalue value2) {
            return value1.value > value2.value;
        }

        [INLINE(256)]
        public static bool operator ==(ucvalue value1, ucvalue value2) {
            return value1.value == value2.value;
        }

        [INLINE(256)]
        public static bool operator !=(ucvalue value1, ucvalue value2) {
            return value1.value != value2.value;
        }

        [INLINE(256)]
        public static bool operator <=(ucvalue value1, ucvalue value2) {
            return value1.value <= value2.value;
        }

        [INLINE(256)]
        public static bool operator >=(ucvalue value1, ucvalue value2) {
            return value1.value >= value2.value;
        }
        
        [INLINE(256)]
        public readonly int CompareTo(ucvalue other) {
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
        public static ucvalue Parse(string s) {
            var val = float.Parse(s);
            return ucvalue.FromFloat(val);
        }

        [INLINE(256)]
        public readonly bool Equals(ucvalue other) {
            return this.value == other.value;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is ucvalue other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.value.GetHashCode();
        }

    }

}