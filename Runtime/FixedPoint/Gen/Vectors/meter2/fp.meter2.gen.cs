namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct meter2 : System.IEquatable<meter2> {

        public static readonly meter2 maxValue = new meter2(new int2(int.MaxValue, int.MaxValue));
        public static readonly meter2 minValue = new meter2(new int2(0, 0));
        public static readonly meter2 zero = new meter2(0);
        public static readonly meter2 oneMeter = new meter2(meter.oneMeter.value, meter.oneMeter.value);

        public meter x;
        public meter y;

        [INLINE(256)]
        public meter2(int2 rawValue) {
            this.x = rawValue.x;
            this.y = rawValue.y;
        }

        [INLINE(256)]
        public meter2(int rawValueX, int rawValueY) {
            this.x = rawValueX;
            this.y = rawValueY;
        }

        [INLINE(256)]
        public static meter2 FromFloat(float2 value) {
            var ms = new meter2 {
                x = meter.FromFloat(value.x),
                y = meter.FromFloat(value.y),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator meter2(int2 value) => new meter2(value);
        
        [INLINE(256)]
        public static meter2 operator +(meter2 value1, meter2 value2) {
            value1.x += value2.x;
            value1.y += value2.y;
            return value1;
        }
        
        [INLINE(256)]
        public static meter2 operator -(meter2 value1, meter2 value2) {
            if (value2.x > value1.x) {
                value1.x = 0;
            } else {
                value1.x -= value2.x;
            }
            if (value2.y > value1.y) {
                value1.y = 0;
            } else {
                value1.y -= value2.y;
            }
            return value1;
        }

        [INLINE(256)]
        public static meter2 operator *(meter2 value1, meter value2) {
            value1.x *= value2;
            value1.y *= value2;
            return value1;
        }

        [INLINE(256)]
        public static meter2 operator /(meter2 value1, meter value2) {
            value1.x *= value2;
            value1.y *= value2;
            return value1;
        }

        [INLINE(256)]
        public static bool operator ==(meter2 value1, meter2 value2) {
            return value1.x == value2.x && value1.y == value2.y;
        }

        [INLINE(256)]
        public static bool operator !=(meter2 value1, meter2 value2) {
            return value1.x != value2.x || value1.y != value2.y;
        }

        [INLINE(256)]
        public readonly int CompareTo(meter2 other) {
            return this.x.CompareTo(other.x) ^ this.y.CompareTo(other.y);
        }

        [INLINE(256)]
        public readonly float2 ToFloat() {
            return new float2(this.x.ToFloat(), this.y.ToFloat());
        }

        [INLINE(256)]
        public readonly int2 ToValue() {
            return new int2(this.x.ToValue(), this.y.ToValue());
        }

        [INLINE(256)]
        public override readonly string ToString() {
            return $"({this.x}; {this.y})";
        }

        [INLINE(256)]
        public static meter2 Parse(string s) {
            var splitted = s.Split(';');
            if (splitted.Length == 2) {
                return meter2.FromFloat(new float2(float.Parse(splitted[0]), float.Parse(splitted[1])));
            }
            return default;
        }

        [INLINE(256)]
        public readonly bool Equals(meter2 other) {
            return this == other;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is meter2 other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.x.GetHashCode() ^ this.y.GetHashCode();
        }

    }

}