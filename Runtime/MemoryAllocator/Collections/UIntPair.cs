namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides u int pair storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public readonly struct UIntPair : System.IEquatable<UIntPair> {

        /// <summary>
        /// Type id1 used by <c>UIntPair</c>.
        /// </summary>
        public readonly uint typeId1;
        /// <summary>
        /// Type id2 used by <c>UIntPair</c>.
        /// </summary>
        public readonly uint typeId2;

        /// <summary>
        /// Initializes <c>UIntPair</c> from the supplied type id1, type id2.
        /// </summary>
        [INLINE(256)]
        public UIntPair(uint typeId1, uint typeId2) {
            this.typeId1 = typeId1;
            this.typeId2 = typeId2;
        }

        /// <summary>
        /// Returns hash.
        /// </summary>
        [INLINE(256)]
        public uint GetHash() {
            return this.typeId1 ^ this.typeId2;
        }

        /// <summary>
        /// Tests equality of the operands.
        /// </summary>
        [INLINE(256)]
        public static bool operator ==(in UIntPair p1, in UIntPair p2) {
            return p1.typeId1 == p2.typeId1 && p1.typeId2 == p2.typeId2;
        }

        /// <summary>
        /// Tests whether the operands differ.
        /// </summary>
        [INLINE(256)]
        public static bool operator !=(UIntPair p1, UIntPair p2) {
            return !(p1 == p2);
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        [INLINE(256)]
        public bool Equals(UIntPair other) {
            return this.typeId1 == other.typeId1 && this.typeId2 == other.typeId2;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is UIntPair other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        [INLINE(256)]
        public override int GetHashCode() {
            return (int)this.GetHash();
        }

        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString() {
            return $"Pair: {this.typeId1}, {this.typeId2}";
        }

    }

}