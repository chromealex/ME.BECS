namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.FixedPoint;

    [System.Serializable]
    public partial struct squaternion : System.IEquatable<squaternion> {

        public quaternion value;

        [INLINE(256)]
        public squaternion(float4 rawValue) {
            this.value = new quaternion(rawValue);
        }

        [INLINE(256)]
        public squaternion(sfloat rawValueX, sfloat rawValueY, sfloat rawValueZ, sfloat rawValueW) {
            this.value = new quaternion(rawValueX, rawValueY, rawValueZ, rawValueW);
        }

        [INLINE(256)]
        public static meter3 operator *(squaternion value1, meter3 value2) {
            return math.mul(value1.value, value2);
        }

        [INLINE(256)]
        public static bool operator ==(squaternion value1, squaternion value2) {
            return FixedPoint.math.all(value1.value.value == value2.value.value);
        }

        [INLINE(256)]
        public static bool operator !=(squaternion value1, squaternion value2) {
            return !(value1 == value2);
        }

        [INLINE(256)]
        public readonly int CompareTo(squaternion other) {
            return this.value.value.x.CompareTo(other.value.value.x) ^ this.value.value.y.CompareTo(other.value.value.y) ^ this.value.value.z.CompareTo(other.value.value.z) ^ this.value.value.w.CompareTo(other.value.value.w);
        }

        [INLINE(256)]
        public override readonly string ToString() {
            return this.value.ToString();
        }

        [INLINE(256)]
        public static squaternion Parse(string s) {
            var splitted = s.Split(';');
            if (splitted.Length == 3) {
                var x = float.Parse(splitted[0]);
                var y = float.Parse(splitted[1]);
                var z = float.Parse(splitted[2]);
                var q = quaternion.Euler(x, y, z);
                return new squaternion(q.value);
            }
            return default;
        }

        [INLINE(256)]
        public readonly bool Equals(squaternion other) {
            return this == other;
        }

        [INLINE(256)]
        public override readonly bool Equals(object obj) {
            return obj is squaternion other && this.Equals(other);
        }

        [INLINE(256)]
        public override readonly int GetHashCode() {
            return this.value.GetHashCode();
        }

    }

}