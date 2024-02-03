namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;

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

        public readonly uint Version => this.ent.Version;

        public readonly ref readonly World World => ref this.ent.World;

        [INLINE(256)]
        public readonly uint GetVersion(uint groupId) => this.ent.GetVersion(groupId);

        [INLINE(256)]
        public readonly ulong ToULong() => this.ent.ToULong();

        [INLINE(256)]
        public static bool operator ==(EntRO ent1, EntRO ent2) {
            return ent1.ent == ent2.ent;
        }

        [INLINE(256)]
        public static bool operator !=(EntRO ent1, EntRO ent2) {
            return !(ent1 == ent2);
        }

        [INLINE(256)]
        public static bool operator ==(EntRO ent1, Ent ent2) {
            return ent1.ent == ent2;
        }

        [INLINE(256)]
        public static bool operator !=(EntRO ent1, Ent ent2) {
            return !(ent1 == ent2);
        }

        [INLINE(256)]
        public static bool operator ==(Ent ent1, EntRO ent2) {
            return ent1 == ent2.ent;
        }

        [INLINE(256)]
        public static bool operator !=(Ent ent1, EntRO ent2) {
            return !(ent1 == ent2);
        }

        [INLINE(256)]
        public static implicit operator EntRO(in Ent ent) {
            return new EntRO() { ent = ent };
        }
        
        [INLINE(256)]
        public bool Equals(EntRO other) {
            return this.ent.Equals(other.ent);
        }

        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is EntRO other && this.Equals(other);
        }

        [INLINE(256)]
        public override int GetHashCode() {
            return this.ent.GetHashCode();
        }

    }

}