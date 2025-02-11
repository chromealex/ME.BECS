namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct meter4 : System.IEquatable<meter4> {

        public static readonly meter4 maxValue = new meter4(new int4(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
        public static readonly meter4 minValue = new meter4(new int4(0, 0, 0, 0));
        public static readonly meter4 zero = new meter4(0);
        public static readonly meter4 oneMeter = new meter4(meter.oneMeter.value, meter.oneMeter.value, meter.oneMeter.value, meter.oneMeter.value);

        public meter x;
        public meter y;
        public meter z;
        public meter w;

        [INLINE(256)]
        public meter4(int4 rawValue) {
            this.x = rawValue.x;
            this.y = rawValue.y;
            this.z = rawValue.z;
            this.w = rawValue.w;
        }

        [INLINE(256)]
        public meter4(int rawValueX, int rawValueY, int rawValueZ, int rawValueW) {
            this.x = rawValueX;
            this.y = rawValueY;
            this.z = rawValueZ;
            this.w = rawValueW;
        }

        [INLINE(256)]
        public static meter4 FromFloat(float4 value) {
            var ms = new meter4 {
                x = meter.FromFloat(value.x),
                y = meter.FromFloat(value.y),
                z = meter.FromFloat(value.z),
                w = meter.FromFloat(value.w),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator meter4(int4 value) => new meter4(value);
        
        [INLINE(256)]
        public static meter4 operator +(meter4 value1, meter4 value2) {
            value1.x += value2.x;
            value1.y += value2.y;
            value1.z += value2.z;
            value1.w += value2.w;
            return value1;
        }
        
        [INLINE(256)]
        public static meter4 operator -(meter4 value1, meter4 value2) {
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
            if (value2.z > value1.z) {
                value1.z = 0;
            } else {
                value1.z -= value2.z;
            }
            if (value2.w > value1.w) {
                value1.w = 0;
            } else {
                value1.w -= value2.w;
            }
            return value1;
        }

        [INLINE(256)]
        public static meter4 operator *(meter4 value1, meter value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            value1.w *= value2;
            return value1;
        }

        [INLINE(256)]
        public static meter4 operator /(meter4 value1, meter value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            value1.w *= value2;
            return value1;
        }

        [INLINE(256)]
        public static bool operator ==(meter4 value1, meter4 value2) {
            return value1.x == value2.x && value1.y == value2.y && value1.z == value2.z && value1.w == value2.w;
        }

        [INLINE(256)]
        public static bool operator !=(meter4 value1, meter4 value2) {
            return value1.x != value2.x || value1.y != value2.y || value1.z != value2.z || value1.w != value2.w;
        }

        [INLINE(256)]
        public readonly int CompareTo(meter4 other) {
            return this.x.CompareTo(other.x) ^ this.y.CompareTo(other.y) ^ this.y.CompareTo(other.z) ^ this.y.CompareTo(other.w);
        }

        [INLINE(256)]
        public readonly float4 ToFloat() {
            return new float4(this.x.ToFloat(), this.y.ToFloat(), this.z.ToFloat(), this.w.ToFloat());
        }

        [INLINE(256)]
        public readonly int4 ToValue() {
            return new int4(this.x.ToValue(), this.y.ToValue(), this.z.ToValue(), this.w.ToValue());
        }

        [INLINE(256)]
        public override readonly string ToString() {
            return $"({this.x}; {this.y}; {this.z}; {this.w})";
        }

        [INLINE(256)]
        public static meter4 Parse(string s) {
            var splitted = s.Split(';');
            if (splitted.Length == 4) {
                return meter4.FromFloat(new float4(float.Parse(splitted[0]), float.Parse(splitted[1]), float.Parse(splitted[2]), float.Parse(splitted[3])));
            }
            return default;
        }

        [INLINE(256)]
        public readonly bool Equals(meter4 other) {
            return this == other;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is meter4 other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.x.GetHashCode() ^ this.y.GetHashCode() ^ this.z.GetHashCode() ^ this.w.GetHashCode();
        }

    }

}