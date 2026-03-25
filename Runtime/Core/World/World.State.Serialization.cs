namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public partial struct State {

        public MemoryAllocator allocator;
        public Ents entities;
        public OneShotTasks oneShotTasks;
        public Components components;
        #if !ENABLE_BECS_FLAT_QUERIES
        public Archetypes archetypes;
        public Queries queries;
        #endif
        public RandomData random;
        public CollectionsRegistry collectionsRegistry;
        public AutoDestroyRegistry autoDestroyRegistry;
        public ulong tick;
        public byte state;
        public byte tickCheck;
        public ushort updateType;
        public uint seed;

    }

    public partial struct State {

        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.entities);
            writer.Write(this.oneShotTasks);
            writer.Write(this.components);
            #if !ENABLE_BECS_FLAT_QUERIES
            writer.Write(this.archetypes);
            writer.Write(this.queries);
            #endif
            writer.Write(this.random);
            writer.Write(this.collectionsRegistry);
            writer.Write(this.autoDestroyRegistry);
            writer.Write(this.tick);
            writer.Write(this.state);
            writer.Write(this.tickCheck);
            writer.Write(this.updateType);
            writer.Write(this.seed);
        }

        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.entities);
            reader.Read(ref this.oneShotTasks);
            reader.Read(ref this.components);
            #if !ENABLE_BECS_FLAT_QUERIES
            reader.Read(ref this.archetypes);
            reader.Read(ref this.queries);
            #endif
            reader.Read(ref this.random);
            reader.Read(ref this.collectionsRegistry);
            reader.Read(ref this.autoDestroyRegistry);
            reader.Read(ref this.tick);
            reader.Read(ref this.state);
            reader.Read(ref this.tickCheck);
            reader.Read(ref this.updateType);
            reader.Read(ref this.seed);
        }

    }

    public partial struct StreamBufferWriter {

        [INLINE(256)]
        public void Write(OneShotTasks value) {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write(Components value) {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write(Ents value) {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write(RandomData value) {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write(CollectionsRegistry value) {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write(AutoDestroyRegistry value) {
            value.SerializeHeaders(ref this);
        }

        #if !ENABLE_BECS_FLAT_QUERIES
        [INLINE(256)]
        public void Write(Archetypes value) {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write(Queries value) {
            value.SerializeHeaders(ref this);
        }
        #endif

    }

    public partial struct StreamBufferReader {

        [INLINE(256)]
        public void Read(ref OneShotTasks value) {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read(ref Components value) {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read(ref Ents value) {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read(ref RandomData value) {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read(ref CollectionsRegistry value) {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read(ref AutoDestroyRegistry value) {
            value.DeserializeHeaders(ref this);
        }

        #if !ENABLE_BECS_FLAT_QUERIES
        [INLINE(256)]
        public void Read(ref Archetypes value) {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read(ref Queries value) {
            value.DeserializeHeaders(ref this);
        }
        #endif

    }

}