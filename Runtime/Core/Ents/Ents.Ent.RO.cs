namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides a read-only API over an entity handle without extending the entity lifetime.
    /// </summary>
    [System.Serializable]
    public struct EntRO {

        [UnityEngine.SerializeField]
        internal Ent ent;

        /// <summary>
        /// Do not use this method to change entity state
        /// </summary>
        /// <returns>Original entity</returns>
        [INLINE(256)]
        public readonly Ent GetEntity() => this.ent;

        /// <summary>
        /// Change version used to detect updates to the associated state.
        /// </summary>
        public readonly uint Version => this.ent.Version;

        /// <summary>
        /// World resolved from the associated world identifier.
        /// </summary>
        public readonly ref readonly World World => ref this.ent.World;

        /// <summary>
        /// Returns version.
        /// </summary>
        [INLINE(256)]
        public readonly ushort GetVersion(uint groupId) => this.ent.GetVersion(groupId);

        /// <summary>
        /// Returns the packed 64-bit representation of this value.
        /// </summary>
        [INLINE(256)]
        public readonly ulong ToULong() => this.ent.ToULong();

        /// <summary>
        /// Tests equality of the operands.
        /// </summary>
        [INLINE(256)]
        public static bool operator ==(EntRO ent1, EntRO ent2) {
            return ent1.ent == ent2.ent;
        }

        /// <summary>
        /// Tests whether the operands differ.
        /// </summary>
        [INLINE(256)]
        public static bool operator !=(EntRO ent1, EntRO ent2) {
            return !(ent1 == ent2);
        }

        /// <summary>
        /// Tests equality of the operands.
        /// </summary>
        [INLINE(256)]
        public static bool operator ==(EntRO ent1, Ent ent2) {
            return ent1.ent == ent2;
        }

        /// <summary>
        /// Tests whether the operands differ.
        /// </summary>
        [INLINE(256)]
        public static bool operator !=(EntRO ent1, Ent ent2) {
            return !(ent1 == ent2);
        }

        /// <summary>
        /// Tests equality of the operands.
        /// </summary>
        [INLINE(256)]
        public static bool operator ==(Ent ent1, EntRO ent2) {
            return ent1 == ent2.ent;
        }

        /// <summary>
        /// Tests whether the operands differ.
        /// </summary>
        [INLINE(256)]
        public static bool operator !=(Ent ent1, EntRO ent2) {
            return !(ent1 == ent2);
        }

        /// <summary>
        /// Converts the supplied value to <c>EntRO</c>.
        /// </summary>
        [INLINE(256)]
        public static implicit operator EntRO(in Ent ent) {
            return new EntRO() { ent = ent };
        }
        
        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        [INLINE(256)]
        public bool Equals(EntRO other) {
            return this.ent.Equals(other.ent);
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is EntRO other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        [INLINE(256)]
        public override int GetHashCode() {
            return this.ent.GetHashCode();
        }

    }

}