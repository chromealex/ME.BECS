using Unity.Collections;

namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using System.Runtime.InteropServices;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Identifies an entity by its world, slot and generation; the handle does not keep the entity alive.
    /// </summary>
    [IgnoreProfiler]
    [System.Serializable]
    [System.Diagnostics.DebuggerTypeProxy(typeof(EntProxy))]
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public partial struct Ent : System.IEquatable<Ent>, System.IComparable<Ent> {

        /// <summary>
        /// Entity handle with zero identity fields; it does not identify a live entity.
        /// </summary>
        public static Ent Null => new Ent();

        /// <summary>
        /// Entity slot index within the owning world.
        /// </summary>
        [FieldOffset(0)]
        public uint id;
        /// <summary>
        /// Generation distinguishing this entity from earlier occupants of the same slot.
        /// </summary>
        [FieldOffset(4)]
        public ushort gen;
        /// <summary>
        /// Identifier of the world that owns the entity.
        /// </summary>
        [FieldOffset(6)]
        public ushort worldId;
        /// <summary>
        /// Packed entity slot, generation and world identifier.
        /// </summary>
        [FieldOffset(0)]
        public readonly ulong pack;
        
        /// <summary>
        /// Current component-change version of the entity.
        /// </summary>
        public readonly uint Version {
            [INLINE(256)]
            get {
                var world = this.World;
                return Ents.GetVersion(world.state, in this);
            }
        }

        /// <summary>
        /// Resolves the owning world; the handle does not extend that world's lifetime.
        /// </summary>
        public readonly ref readonly World World {
            [INLINE(256)]
            get => ref Worlds.GetWorld(this.worldId);
        }

        /// <summary>
        /// Returns the change version for the specified component group.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly ushort GetVersion(uint groupId) {
            E.IS_ALIVE(this);
            var world = this.World;
            return Ents.GetVersion(world.state, in this, groupId);
        }

        /// <summary>
        /// Returns the packed 64-bit representation of this value.
        /// </summary>
        [INLINE(256)]
        public readonly ulong ToULong() {
            return this.pack;
        }

        /// <summary>
        /// Initializes <c>Ent</c> from the supplied value.
        /// </summary>
        [INLINE(256)]
        public Ent(ulong value) {
            this = default;
            this.pack = value;
        }

        /// <summary>
        /// Initializes <c>Ent</c> from the supplied ID, world.
        /// </summary>
        [INLINE(256)]
        public Ent(uint id, in World world) {
            this.pack = default;
            this.id = id;
            this.gen = Ents.GetGeneration(world.state, id);
            this.worldId = world.id;
        }

        /// <summary>
        /// Initializes <c>Ent</c> from the supplied ID, state, world ID.
        /// </summary>
        [INLINE(256)]
        public Ent(uint id, safe_ptr<State> state, ushort worldId) {
            this.pack = default;
            this.id = id;
            this.gen = Ents.GetGeneration(state, id);
            this.worldId = worldId;
        }

        /// <summary>
        /// Initializes <c>Ent</c> from the supplied ID, gen, world ID.
        /// </summary>
        [INLINE(256)]
        public Ent(uint id, ushort gen, ushort worldId) {
            this.pack = default;
            this.id = id;
            this.gen = gen;
            this.worldId = worldId;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        [INLINE(256)]
        public bool Equals(Ent other) {
            return this.id == other.id && this.gen == other.gen && this.worldId == other.worldId;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is Ent other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        [INLINE(256)]
        public override int GetHashCode() {
            return (int)this.id ^ (int)this.gen ^ ((int)this.worldId << 16);
        }

        /// <summary>
        /// Converts the supplied value to <c>string</c>.
        /// </summary>
        [INLINE(256)]
        public static implicit operator string(Ent ent) {
            return ent.ToString();
        }

        /// <summary>
        /// Tests equality of the operands.
        /// </summary>
        [INLINE(256)]
        public static bool operator ==(Ent ent1, Ent ent2) {
            return ent1.id == ent2.id && ent1.gen == ent2.gen && ent1.worldId == ent2.worldId;
        }

        /// <summary>
        /// Tests whether the operands differ.
        /// </summary>
        [INLINE(256)]
        public static bool operator !=(Ent ent1, Ent ent2) {
            return !(ent1 == ent2);
        }

        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        [INLINE(256)]
        public override readonly string ToString() {
            var name = new FixedString32Bytes("Ent");
            #if UNITY_EDITOR
            var editorName = this.EditorName;
            if (editorName.IsEmpty == false) name = editorName;
            #endif
            if (this.IsAlive() == true) {
                return $"{name} #{this.id} Gen: {this.gen} (Version: {this.Version}, World: {this.worldId})";
            } else {
                return $"{name} #{this.id} Gen: {this.gen} (World: {this.worldId})";
            }
        }

        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        [INLINE(256)]
        public readonly Unity.Collections.FixedString128Bytes ToString(bool withWorld, bool withVersion = true, bool withGen = true) {
            if (withWorld == true) return this.ToString();
            var name = new FixedString32Bytes("Ent");
            #if UNITY_EDITOR
            var editorName = this.EditorName;
            if (editorName.IsEmpty == false) name = editorName;
            #endif
            var gen = new FixedString32Bytes();
            if (withGen == true) gen = new FixedString32Bytes($" Gen: {this.gen}");
            var version = new FixedString32Bytes();
            if (this.IsAlive() == true && withVersion == true) version = new FixedString32Bytes($" (Version: {this.Version})");
            return $"{name} #{this.id}{gen}{version}";
        }

        /// <summary>
        /// Orders entity handles by slot, then generation, then world identifier.
        /// </summary>
        [INLINE(256)]
        public int CompareTo(Ent other) {
            var idComparison = this.id.CompareTo(other.id);
            if (idComparison != 0) {
                return idComparison;
            }

            var genComparison = this.gen.CompareTo(other.gen);
            if (genComparison != 0) {
                return genComparison;
            }

            return this.worldId.CompareTo(other.worldId);
        }

    }

}