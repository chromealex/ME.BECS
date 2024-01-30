namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public readonly struct UIntPair : System.IEquatable<UIntPair> {

        public readonly uint typeId1;
        public readonly uint typeId2;

        [INLINE(256)]
        public UIntPair(uint typeId1, uint typeId2) {
            this.typeId1 = typeId1;
            this.typeId2 = typeId2;
        }

        [INLINE(256)]
        public uint GetHash() {
            return this.typeId1 ^ this.typeId2;
        }

        [INLINE(256)]
        public static bool operator ==(in UIntPair p1, in UIntPair p2) {
            return p1.typeId1 == p2.typeId1 && p1.typeId2 == p2.typeId2;
        }

        [INLINE(256)]
        public static bool operator !=(UIntPair p1, UIntPair p2) {
            return !(p1 == p2);
        }

        [INLINE(256)]
        public bool Equals(UIntPair other) {
            return this.typeId1 == other.typeId1 && this.typeId2 == other.typeId2;
        }

        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is UIntPair other && this.Equals(other);
        }

        [INLINE(256)]
        public override int GetHashCode() {
            return (int)this.GetHash();
        }

        public override string ToString() {
            return $"Pair: {this.typeId1}, {this.typeId2}";
        }

    }

}