namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    [System.Serializable]
    public partial struct umeter3 : System.IEquatable<umeter3> {

        public static readonly umeter3 maxValue = new umeter3(new uint3(uint.MaxValue, uint.MaxValue, uint.MaxValue));
        public static readonly umeter3 minValue = new umeter3(new uint3(0u, 0u, 0u));
        public static readonly umeter3 zero = new umeter3(0u);
        public static readonly umeter3 oneMeter = new umeter3(umeter.oneMeter.value, umeter.oneMeter.value, umeter.oneMeter.value);

        public umeter x;
        public umeter y;
        public umeter z;

        [INLINE(256)]
        public umeter3(uint3 rawValue) {
            this.x = rawValue.x;
            this.y = rawValue.y;
            this.z = rawValue.z;
        }

        [INLINE(256)]
        public umeter3(uint rawValueX, uint rawValueY, uint rawValueZ) {
            this.x = rawValueX;
            this.y = rawValueY;
            this.z = rawValueZ;
        }

        [INLINE(256)]
        public static umeter3 FromFloat(float3 value) {
            var ms = new umeter3 {
                x = umeter.FromFloat(value.x),
                y = umeter.FromFloat(value.y),
                z = umeter.FromFloat(value.z),
            };
            return ms;
        }
        
        [INLINE(256)]
        public static implicit operator umeter3(uint3 value) => new umeter3(value);
        
        [INLINE(256)]
        public static umeter3 operator +(umeter3 value1, umeter3 value2) {
            value1.x += value2.x;
            value1.y += value2.y;
            value1.z += value2.z;
            return value1;
        }
        
        [INLINE(256)]
        public static umeter3 operator -(umeter3 value1, umeter3 value2) {
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
            return value1;
        }

        [INLINE(256)]
        public static umeter3 operator *(umeter3 value1, umeter value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            return value1;
        }

        [INLINE(256)]
        public static umeter3 operator /(umeter3 value1, umeter value2) {
            value1.x *= value2;
            value1.y *= value2;
            value1.z *= value2;
            return value1;
        }

        [INLINE(256)]
        public static bool operator ==(umeter3 value1, umeter3 value2) {
            return value1.x == value2.x && value1.y == value2.y && value1.z == value2.z;
        }

        [INLINE(256)]
        public static bool operator !=(umeter3 value1, umeter3 value2) {
            return value1.x != value2.x || value1.y != value2.y || value1.z != value2.z;
        }

        [INLINE(256)]
        public readonly int CompareTo(umeter3 other) {
            return this.x.CompareTo(other.x) ^ this.y.CompareTo(other.y) ^ this.y.CompareTo(other.z);
        }

        [INLINE(256)]
        public readonly float3 ToFloat() {
            return new float3(this.x.ToFloat(), this.y.ToFloat(), this.z.ToFloat());
        }

        [INLINE(256)]
        public readonly uint3 ToValue() {
            return new uint3(this.x.ToValue(), this.y.ToValue(), this.z.ToValue());
        }

        [INLINE(256)]
        public override readonly string ToString() {
            return $"({this.x}; {this.y}; {this.z})";
        }

        [INLINE(256)]
        public static umeter3 Parse(string s) {
            var splitted = s.Split(';');
            if (splitted.Length == 3) {
                return umeter3.FromFloat(new float3(float.Parse(splitted[0]), float.Parse(splitted[1]), float.Parse(splitted[2])));
            }
            return default;
        }

        [INLINE(256)]
        public readonly bool Equals(umeter3 other) {
            return this == other;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is umeter3 other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.x.GetHashCode() ^ this.y.GetHashCode() ^ this.z.GetHashCode();
        }

    }

}