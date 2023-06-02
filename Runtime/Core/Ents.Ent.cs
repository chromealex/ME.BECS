namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public class ComponentGroupAttribute : System.Attribute {

        public readonly ushort groupId;

        public ComponentGroupAttribute(ushort groupId) {
            this.groupId = groupId;
        }

    }
    
    [System.Serializable]
    [System.Diagnostics.DebuggerTypeProxy(typeof(EntProxy))]
    public readonly unsafe struct Ent : System.IEquatable<Ent> {

        public static Ent Null => new Ent();
        
        public readonly uint id;
        public readonly ushort gen;
        public readonly ushort worldId;
        public uint Version {
            get {
                var world = this.World;
                return world.state->entities.GetVersion(world.state, this.id);
            }
        }

        public ref readonly World World {
            [INLINE(256)]
            get => ref Worlds.GetWorld(this.worldId);
        }

        [INLINE(256)]
        public uint GetVersion(ushort groupId) {
            var world = this.World;
            return world.state->entities.GetVersion(world.state, this.id, groupId);
        }

        [INLINE(256)]
        public ulong ToULong() {
            return ((ulong)this.id << 32) | ((uint)this.gen << 16 | this.worldId);
        }

        /// <summary>
        /// Get entity from Context.world
        /// </summary>
        /// <param name="id"></param>
        [INLINE(256)]
        public Ent(uint id) {
            this.id = id;
            this.worldId = Context.world.id;
            this.gen = default;
            if (Context.world.state->entities.IsAlive(Context.world.state, id, out var gen) == true) {
                this.gen = gen;
            }
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
            Ent newEnt;
            JobUtils.Lock(ref world.state->entities.lockIndex);
            {
                newEnt = world.state->entities.Add(world.state, worldId, out var reused);
                if (reused == false) {
                    world.state->components.OnEntityAdd(world.state, newEnt.id);
                    world.state->batches.OnEntityAdd(world.state, newEnt.id);
                }
            }
            JobUtils.Unlock(ref world.state->entities.lockIndex);
            JobUtils.Lock(ref world.state->archetypes.lockIndex);
            {
                world.state->archetypes.AddEntity(world.state, newEnt);
            }
            JobUtils.Unlock(ref world.state->archetypes.lockIndex);

            return newEnt;

        }

        [INLINE(256)]
        public Ent(ulong value) {
            this.id = (uint)(value >> 32);
            var key = value & 0xffffffff;
            this.gen = (ushort)(key >> 16);
            this.worldId = (ushort)(key & 0xffff);
        }

        [INLINE(256)]
        public Ent(uint id, World world) {
            this.id = id;
            this.gen = world.state->entities.GetGeneration(world.state, id);
            this.worldId = world.id;
        }

        [INLINE(256)]
        public Ent(uint id, State* state, ushort worldId) {
            this.id = id;
            this.gen = state->entities.GetGeneration(state, id);
            this.worldId = worldId;
        }

        [INLINE(256)]
        public Ent(uint id, ushort gen, ushort worldId) {
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
        public override string ToString() {
            if (this.World.isCreated == false) return this.ToString(false);
            if (this.IsAlive() == true) {
                return $"Ent #{this.id} Gen: {this.gen} (Version: {this.Version}, World: {this.worldId})";
            } else {
                return $"Ent #{this.id} Gen: {this.gen} (World: {this.worldId})";
            }
        }

        [INLINE(256)]
        public string ToString(bool withWorld) {
            if (withWorld == true) return this.ToString();
            if (this.IsAlive() == true) {
                return $"Ent #{this.id} Gen: {this.gen} (Version: {this.Version})";
            } else {
                return $"Ent #{this.id} Gen: {this.gen}";
            }
        }

    }

}