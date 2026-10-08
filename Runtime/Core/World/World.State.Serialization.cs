namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Stores the native simulation state used by a world and its snapshots.
    /// </summary>
    public partial struct State {

        /// <summary>
        /// Allocator used to access or manage the associated native storage.
        /// </summary>
        public MemoryAllocator allocator;
        /// <summary>
        /// Entity handles processed or stored by this operation.
        /// </summary>
        public Ents entities;
        /// <summary>
        /// One shot tasks used by <c>State</c>.
        /// </summary>
        public OneShotTasks oneShotTasks;
        /// <summary>
        /// Component storage or descriptors used by this operation.
        /// </summary>
        public Components components;
        /// <summary>
        /// Random used by <c>State</c>.
        /// </summary>
        public RandomData random;
        /// <summary>
        /// Collections registry used by <c>State</c>.
        /// </summary>
        public CollectionsRegistry collectionsRegistry;
        /// <summary>
        /// Auto destroy registry used by <c>State</c>.
        /// </summary>
        public AutoDestroyRegistry autoDestroyRegistry;
        /// <summary>
        /// Tick used by <c>State</c>.
        /// </summary>
        public ulong tick;
        /// <summary>
        /// State accessed by the containing operation.
        /// </summary>
        public byte state;
        /// <summary>
        /// Tick check used by <c>State</c>.
        /// </summary>
        public byte tickCheck;
        /// <summary>
        /// Lifecycle phase in which the update runs.
        /// </summary>
        public ushort updateType;
        /// <summary>
        /// Seed used by <c>State</c>.
        /// </summary>
        public uint seed;

    }

    /// <summary>
    /// Stores the native simulation state used by a world and its snapshots.
    /// </summary>
    public partial struct State {

        /// <summary>
        /// Writes collection metadata to the stream without serializing the backing allocator blocks.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.entities);
            writer.Write(this.oneShotTasks);
            writer.Write(this.components);
            writer.Write(this.random);
            writer.Write(this.collectionsRegistry);
            writer.Write(this.autoDestroyRegistry);
            writer.Write(this.tick);
            writer.Write(this.state);
            writer.Write(this.tickCheck);
            writer.Write(this.updateType);
            writer.Write(this.seed);
        }

        /// <summary>
        /// Restores collection metadata from the stream; backing allocator storage is restored separately.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.entities);
            reader.Read(ref this.oneShotTasks);
            reader.Read(ref this.components);
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

    /// <summary>
    /// Writes typed values to a native stream buffer.
    /// </summary>
    public partial struct StreamBufferWriter {

        /// <summary>
        /// Writes the supplied value to stream buffer writer.
        /// </summary>
        [INLINE(256)]
        public void Write(OneShotTasks value) {
            value.SerializeHeaders(ref this);
        }

        /// <summary>
        /// Writes the supplied value to stream buffer writer.
        /// </summary>
        [INLINE(256)]
        public void Write(Components value) {
            value.SerializeHeaders(ref this);
        }

        /// <summary>
        /// Writes the supplied value to stream buffer writer.
        /// </summary>
        [INLINE(256)]
        public void Write(Ents value) {
            value.SerializeHeaders(ref this);
        }

        /// <summary>
        /// Writes the supplied value to stream buffer writer.
        /// </summary>
        [INLINE(256)]
        public void Write(RandomData value) {
            value.SerializeHeaders(ref this);
        }

        /// <summary>
        /// Writes the supplied value to stream buffer writer.
        /// </summary>
        [INLINE(256)]
        public void Write(CollectionsRegistry value) {
            value.SerializeHeaders(ref this);
        }

        /// <summary>
        /// Writes the supplied value to stream buffer writer.
        /// </summary>
        [INLINE(256)]
        public void Write(AutoDestroyRegistry value) {
            value.SerializeHeaders(ref this);
        }


    }

    /// <summary>
    /// Reads typed values from a native stream buffer.
    /// </summary>
    public partial struct StreamBufferReader {

        /// <summary>
        /// Reads the requested value from stream buffer reader.
        /// </summary>
        [INLINE(256)]
        public void Read(ref OneShotTasks value) {
            value.DeserializeHeaders(ref this);
        }

        /// <summary>
        /// Reads the requested value from stream buffer reader.
        /// </summary>
        [INLINE(256)]
        public void Read(ref Components value) {
            value.DeserializeHeaders(ref this);
        }

        /// <summary>
        /// Reads the requested value from stream buffer reader.
        /// </summary>
        [INLINE(256)]
        public void Read(ref Ents value) {
            value.DeserializeHeaders(ref this);
        }

        /// <summary>
        /// Reads the requested value from stream buffer reader.
        /// </summary>
        [INLINE(256)]
        public void Read(ref RandomData value) {
            value.DeserializeHeaders(ref this);
        }

        /// <summary>
        /// Reads the requested value from stream buffer reader.
        /// </summary>
        [INLINE(256)]
        public void Read(ref CollectionsRegistry value) {
            value.DeserializeHeaders(ref this);
        }

        /// <summary>
        /// Reads the requested value from stream buffer reader.
        /// </summary>
        [INLINE(256)]
        public void Read(ref AutoDestroyRegistry value) {
            value.DeserializeHeaders(ref this);
        }


    }

}