namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct umeter2 : System.IEquatable<umeter2> {

        public static readonly umeter2 maxValue = new umeter2(new uint2(uint.MaxValue, uint.MaxValue));
        public static readonly umeter2 minValue = new umeter2(new uint2(0u, 0u));
        public static readonly umeter2 zero = new umeter2(0u);
        public static readonly umeter2 oneMeter = new umeter2(umeter.oneMeter.value, umeter.oneMeter.value);

        public umeter x;
        public umeter y;

        [INLINE(256)]
        public umeter2(uint2 rawValue) {
            this.x = rawValue.x;
            this.y = rawValue.y;
        }

        [INLINE(256)]
        public umeter2(uint rawValueX, uint rawValueY) {
            this.x = rawValueX;
            this.y = rawValueY;
        }

        [INLINE(256)]
        public static umeter2 FromFloat(float2 value) {
            var ms = new umeter2 {
                x = umeter.FromFloat(value.x),
                y = umeter.FromFloat(value.y),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator umeter2(uint2 value) => new umeter2(value);
        
        [INLINE(256)]
        public static umeter2 operator +(umeter2 value1, umeter2 value2) {
            value1.x += value2.x;
            value1.y += value2.y;
            return value1;
        }
        
        [INLINE(256)]
        public static umeter2 operator -(umeter2 value1, umeter2 value2) {
            if (value2.x > value1.x) {
                value1.x = 0u;
            } else {
                value1.x -= value2.x;
            }
            if (value2.y > value1.y) {
                value1.y = 0u;
            } else {
                value1.y -= value2.y;
            }
            return value1;
        }

        [INLINE(256)]
        public static umeter2 operator *(umeter2 value1, umeter value2) {
            value1.x *= value2;
            value1.y *= value2;
            return value1;
        }

        [INLINE(256)]
        public static umeter2 operator /(umeter2 value1, umeter value2) {
            value1.x *= value2;
            value1.y *= value2;
            return value1;
        }

        [INLINE(256)]
        public static bool operator ==(umeter2 value1, umeter2 value2) {
            return value1.x == value2.x && value1.y == value2.y;
        }

        [INLINE(256)]
        public static bool operator !=(umeter2 value1, umeter2 value2) {
            return value1.x != value2.x || value1.y != value2.y;
        }

        [INLINE(256)]
        public readonly int CompareTo(umeter2 other) {
            return this.x.CompareTo(other.x) ^ this.y.CompareTo(other.y);
        }

        [INLINE(256)]
        public readonly float2 ToFloat() {
            return new float2(this.x.ToFloat(), this.y.ToFloat());
        }

        [INLINE(256)]
        public readonly uint2 ToValue() {
            return new uint2(this.x.ToValue(), this.y.ToValue());
        }

        [INLINE(256)]
        public override readonly string ToString() {
            return $"({this.x}; {this.y})";
        }

        [INLINE(256)]
        public static umeter2 Parse(string s) {
            var splitted = s.Split(';');
            if (splitted.Length == 2) {
                return umeter2.FromFloat(new float2(float.Parse(splitted[0]), float.Parse(splitted[1])));
            }
            return default;
        }

        [INLINE(256)]
        public readonly bool Equals(umeter2 other) {
            return this == other;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is umeter2 other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.x.GetHashCode() ^ this.y.GetHashCode();
        }

    }

}