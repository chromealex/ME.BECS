using Unity.Collections;

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    [IgnoreProfiler]
    [System.Serializable]
    [System.Diagnostics.DebuggerTypeProxy(typeof(EntProxy))]
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public partial struct Ent : System.IEquatable<Ent>, System.IComparable<Ent> {

        public static Ent Null => new Ent();

        [FieldOffset(0)]
        public uint id;
        [FieldOffset(4)]
        public ushort gen;
        [FieldOffset(6)]
        public ushort worldId;
        [FieldOffset(0)]
        public readonly ulong pack;
        
        public readonly uint Version {
            [INLINE(256)]
            get {
                var world = this.World;
                return Ents.GetVersion(world.state, in this);
            }
        }

        public readonly ref readonly World World {
            [INLINE(256)]
            get => ref Worlds.GetWorld(this.worldId);
        }

        [INLINE(256)][IgnoreProfiler]
        public readonly ushort GetVersion(uint groupId) {
            E.IS_ALIVE(this);
            var world = this.World;
            return Ents.GetVersion(world.state, in this, groupId);
        }

        [INLINE(256)]
        public readonly ulong ToULong() {
            return this.pack;
        }

        [INLINE(256)]
        public Ent(ulong value) {
            this = default;
            this.pack = value;
        }

        [INLINE(256)]
        public Ent(uint id, in World world) {
            this.pack = default;
            this.id = id;
            this.gen = Ents.GetGeneration(world.state, id);
            this.worldId = world.id;
        }

        [INLINE(256)]
        public Ent(uint id, safe_ptr<State> state, ushort worldId) {
            this.pack = default;
            this.id = id;
            this.gen = Ents.GetGeneration(state, id);
            this.worldId = worldId;
        }

        [INLINE(256)]
        public Ent(uint id, ushort gen, ushort worldId) {
            this.pack = default;
            this.id = id;
            this.gen = gen;
            this.worldId = worldId;
        }

        [INLINE(256)]
        public bool Equals(Ent other) {
            return this.id == other.id && this.gen == other.gen;
        }

        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is Ent other && this.Equals(other);
        }

        [INLINE(256)]
        public override int GetHashCode() {
            return (int)this.id ^ (int)this.gen;
        }

        [INLINE(256)]
        public static implicit operator string(Ent ent) {
            return ent.ToString();
        }

        [INLINE(256)]
        public static bool operator ==(Ent ent1, Ent ent2) {
            return ent1.id == ent2.id && ent1.gen == ent2.gen && ent1.worldId == ent2.worldId;
        }

        [INLINE(256)]
        public static bool operator !=(Ent ent1, Ent ent2) {
            return !(ent1 == ent2);
        }

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