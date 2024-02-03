namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;

    [System.Serializable]
    [System.Diagnostics.DebuggerTypeProxy(typeof(EntProxy))]
    [StructLayout(LayoutKind.Explicit)]
    public
        // Unity bug: readonly struct inside SerializeReference do not serialized
        #if !UNITY_EDITOR
        readonly 
        #endif
        unsafe struct Ent : System.IEquatable<Ent>, System.IComparable<Ent> {

        public static Ent Null => new Ent();

        #if !UNITY_EDITOR
        [FieldOffset(0)]
        public readonly uint id;
        [FieldOffset(4)]
        public readonly ushort gen;
        [FieldOffset(6)]
        public readonly ushort worldId;
        #else
        [FieldOffset(0)]
        public uint id;
        [FieldOffset(4)]
        public ushort gen;
        [FieldOffset(6)]
        public ushort worldId;
        #endif
        [FieldOffset(0)]
        public readonly ulong pack;
        
        public readonly uint Version {
            [INLINE(256)]
            get {
                var world = this.World;
                return world.state->entities.GetVersion(world.state, in this);
            }
        }

        public readonly ref readonly World World {
            [INLINE(256)]
            get => ref Worlds.GetWorld(this.worldId);
        }

        [INLINE(256)]
        public readonly uint GetVersion(uint groupId) {
            var world = this.World;
            return world.state->entities.GetVersion(world.state, in this, groupId);
        }

        [INLINE(256)]
        public readonly ulong ToULong() {
            return this.pack;
        }

        /// <summary>
        /// Create new entity from Context.world
        /// </summary>
        /// <returns></returns>
        [INLINE(256)]
        public static Ent New() {
            return Ent.New(Context.world.id);
        }

        [INLINE(256)]
        public static Ent New(in World world) {
            return Ent.New(world.id);
        }

        [INLINE(256)]
        public static Ent New(in SystemContext systemContext) {
            return Ent.New(systemContext.world.id);
        }

        [INLINE(256)]
        public static Ent New(ushort worldId) {

            ref readonly var world = ref Worlds.GetWorld(worldId);
            E.IS_IN_TICK(world.state);

            Ent newEnt;
            {
                newEnt = world.state->entities.Add(world.state, worldId, out var reused);
                if (reused == false) {
                    world.state->entities.Lock(world.state, in newEnt);
                    world.state->components.OnEntityAdd(world.state, newEnt.id);
                    world.state->batches.OnEntityAdd(world.state, newEnt.id);
                    world.state->collectionsRegistry.OnEntityAdd(world.state, newEnt.id);
                    world.state->entities.Unlock(world.state, in newEnt);
                }
            }
            {
                world.state->entities.Lock(world.state, in newEnt);
                world.state->archetypes.AddEntity(world.state, newEnt);
                world.state->entities.Unlock(world.state, in newEnt);
            }

            return newEnt;

        }

        [INLINE(256)]
        public Ent(ulong value) {
            this = default;
            this.pack = value;
        }

        [INLINE(256)]
        public Ent(uint id, World world) {
            this.pack = default;
            this.id = id;
            this.gen = world.state->entities.GetGeneration(world.state, id);
            this.worldId = world.id;
        }

        [INLINE(256)]
        public Ent(uint id, State* state, ushort worldId) {
            this.pack = default;
            this.id = id;
            this.gen = state->entities.GetGeneration(state, id);
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
            if (this.World.isCreated == false) return this.ToString(false);
            if (this.IsAlive() == true) {
                return $"Ent #{this.id} Gen: {this.gen} (Version: {this.Version}, World: {this.worldId})";
            } else {
                return $"Ent #{this.id} Gen: {this.gen} (World: {this.worldId})";
            }
        }

        [INLINE(256)]
        public readonly string ToString(bool withWorld, bool withVersion = true) {
            if (withWorld == true) return this.ToString();
            if (this.IsAlive() == true && withVersion == true) {
                return $"Ent #{this.id} Gen: {this.gen} (Version: {this.Version})";
            } else {
                return $"Ent #{this.id} Gen: {this.gen}";
            }
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