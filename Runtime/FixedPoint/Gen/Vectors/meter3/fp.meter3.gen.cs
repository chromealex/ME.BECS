namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct meter3 : System.IEquatable<meter3> {

        public static readonly meter3 maxValue = new meter3(new int3(int.MaxValue, int.MaxValue, int.MaxValue));
        public static readonly meter3 minValue = new meter3(new int3(0, 0, 0));
        public static readonly meter3 zero = new meter3(0);
        public static readonly meter3 oneMeter = new meter3(meter.oneMeter.value, meter.oneMeter.value, meter.oneMeter.value);

        public meter x;
        public meter y;
        public meter z;

        [INLINE(256)]
        public meter3(int3 rawValue) {
            this.x = rawValue.x;
            this.y = rawValue.y;
            this.z = rawValue.z;
        }

        [INLINE(256)]
        public meter3(int rawValueX, int rawValueY, int rawValueZ) {
            this.x = rawValueX;
            this.y = rawValueY;
            this.z = rawValueZ;
        }

        [INLINE(256)]
        public static meter3 FromFloat(float3 value) {
            var ms = new meter3 {
                x = meter.FromFloat(value.x),
                y = meter.FromFloat(value.y),
                z = meter.FromFloat(value.z),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static meter3 FromSFloat(ME.BECS.FixedPoint.float3 value) {
            var ms = new meter3 {
                x = meter.FromSFloat(value.x),
                y = meter.FromSFloat(value.y),
                z = meter.FromSFloat(value.z),
            };
            return ms;
        }

        [INLINE(256)]
        public static implicit operator meter3(int3 value) => new meter3(value);
        
        [INLINE(256)]
        public static meter3 operator +(meter3 value1, meter3 value2) {
            value1.x += value2.x;
            value1.y += value2.y;
            value1.z += value2.z;
            return value1;
        }
        
        [INLINE(256)]
        public static meter3 operator -(meter3 value1, meter3 value2) {
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
            return value1;
        }

        [INLINE(256)]
        public static meter3 operator *(meter3 value1, meter value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            return value1;
        }

        [INLINE(256)]
        public static meter3 operator /(meter3 value1, meter value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            return value1;
        }

        [INLINE(256)]
        public static bool operator ==(meter3 value1, meter3 value2) {
            return value1.x == value2.x && value1.y == value2.y && value1.z == value2.z;
        }

        [INLINE(256)]
        public static bool operator !=(meter3 value1, meter3 value2) {
            return value1.x != value2.x || value1.y != value2.y || value1.z != value2.z;
        }

        [INLINE(256)]
        public readonly int CompareTo(meter3 other) {
            return this.x.CompareTo(other.x) ^ this.y.CompareTo(other.y) ^ this.y.CompareTo(other.z);
        }

        [INLINE(256)]
        public readonly float3 ToFloat() {
            return new float3(this.x.ToFloat(), this.y.ToFloat(), this.z.ToFloat());
        }

        [INLINE(256)]
        public readonly int3 ToValue() {
            return new int3(this.x.ToValue(), this.y.ToValue(), this.z.ToValue());
        }

        [INLINE(256)]
        public override readonly string ToString() {
            return $"({this.x}; {this.y}; {this.z})";
        }

        [INLINE(256)]
        public static meter3 Parse(string s) {
            var splitted = s.Split(';');
            if (splitted.Length == 3) {
                return meter3.FromFloat(new float3(float.Parse(splitted[0]), float.Parse(splitted[1]), float.Parse(splitted[2])));
            }
            return default;
        }

        [INLINE(256)]
        public readonly bool Equals(meter3 other) {
            return this == other;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is meter3 other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.x.GetHashCode() ^ this.y.GetHashCode() ^ this.z.GetHashCode();
        }

    }

}