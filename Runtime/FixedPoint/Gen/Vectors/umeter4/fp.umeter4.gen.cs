namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct umeter4 : System.IEquatable<umeter4> {

        public static readonly umeter4 maxValue = new umeter4(new uint4(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue));
        public static readonly umeter4 minValue = new umeter4(new uint4(0u, 0u, 0u, 0u));
        public static readonly umeter4 zero = new umeter4(0u);
        public static readonly umeter4 oneMeter = new umeter4(umeter.oneMeter.value, umeter.oneMeter.value, umeter.oneMeter.value, umeter.oneMeter.value);

        public umeter x;
        public umeter y;
        public umeter z;
        public umeter w;

        [INLINE(256)]
        public umeter4(uint4 rawValue) {
            this.x = rawValue.x;
            this.y = rawValue.y;
            this.z = rawValue.z;
            this.w = rawValue.w;
        }

        [INLINE(256)]
        public umeter4(uint rawValueX, uint rawValueY, uint rawValueZ, uint rawValueW) {
            this.x = rawValueX;
            this.y = rawValueY;
            this.z = rawValueZ;
            this.w = rawValueW;
        }

        [INLINE(256)]
        public static umeter4 FromFloat(float4 value) {
            var ms = new umeter4 {
                x = umeter.FromFloat(value.x),
                y = umeter.FromFloat(value.y),
                z = umeter.FromFloat(value.z),
                w = umeter.FromFloat(value.w),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator umeter4(uint4 value) => new umeter4(value);
        
        [INLINE(256)]
        public static umeter4 operator +(umeter4 value1, umeter4 value2) {
            value1.x += value2.x;
            value1.y += value2.y;
            value1.z += value2.z;
            value1.w += value2.w;
            return value1;
        }
        
        [INLINE(256)]
        public static umeter4 operator -(umeter4 value1, umeter4 value2) {
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
            if (value2.z > value1.z) {
                value1.z = 0u;
            } else {
                value1.z -= value2.z;
            }
            if (value2.w > value1.w) {
                value1.w = 0u;
            } else {
                value1.w -= value2.w;
            }
            return value1;
        }

        [INLINE(256)]
        public static umeter4 operator *(umeter4 value1, umeter value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            value1.w *= value2;
            return value1;
        }

        [INLINE(256)]
        public static umeter4 operator /(umeter4 value1, umeter value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            value1.w *= value2;
            return value1;
        }

        [INLINE(256)]
        public static bool operator ==(umeter4 value1, umeter4 value2) {
            return value1.x == value2.x && value1.y == value2.y && value1.z == value2.z && value1.w == value2.w;
        }

        [INLINE(256)]
        public static bool operator !=(umeter4 value1, umeter4 value2) {
            return value1.x != value2.x || value1.y != value2.y || value1.z != value2.z || value1.w != value2.w;
        }

        [INLINE(256)]
        public readonly int CompareTo(umeter4 other) {
            return this.x.CompareTo(other.x) ^ this.y.CompareTo(other.y) ^ this.y.CompareTo(other.z) ^ this.y.CompareTo(other.w);
        }

        [INLINE(256)]
        public readonly float4 ToFloat() {
            return new float4(this.x.ToFloat(), this.y.ToFloat(), this.z.ToFloat(), this.w.ToFloat());
        }

        [INLINE(256)]
        public readonly uint4 ToValue() {
            return new uint4(this.x.ToValue(), this.y.ToValue(), this.z.ToValue(), this.w.ToValue());
        }

        [INLINE(256)]
        public override readonly string ToString() {
            return $"({this.x}; {this.y}; {this.z}; {this.w})";
        }

        [INLINE(256)]
        public static umeter4 Parse(string s) {
            var splitted = s.Split(';');
            if (splitted.Length == 4) {
                return umeter4.FromFloat(new float4(float.Parse(splitted[0]), float.Parse(splitted[1]), float.Parse(splitted[2]), float.Parse(splitted[3])));
            }
            return default;
        }

        [INLINE(256)]
        public readonly bool Equals(umeter4 other) {
            return this == other;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is umeter4 other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.x.GetHashCode() ^ this.y.GetHashCode() ^ this.z.GetHashCode() ^ this.w.GetHashCode();
        }

    }

}