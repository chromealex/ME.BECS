namespace ME.BECS {
    
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using System.Runtime.InteropServices;

    /// <summary>
    /// Defines components data used by entity processing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Components {

        /// <summary>
        /// Lock shared index used to locate the associated entry.
        /// </summary>
        public LockSpinner lockSharedIndex;
        // (sharedTypeId << 32 | hash) => SharedComponentStorage<T>
        internal ULongDictionary<MemAllocatorPtr> sharedData;
        // entityId * sharedTypesCount + sharedTypeId => hash
        internal MemArray<uint> entityIdToHash;
        internal uint sharedTypesCount;
        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public MemArray<MemAllocatorPtr> items;
        // Derived capacity cache and its synchronization are intentionally not serialized.
        internal uint entitiesCapacity;
        private LockSpinner resizeLock;
        private bbool allowStaticStorage;

        /// <summary>
        /// Indicates hash.
        /// </summary>
        public int Hash => (int)this.items.Length;

        /// <summary>
        /// Writes collection metadata to the stream without serializing the backing allocator blocks.
        /// </summary>
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.lockSharedIndex);
            writer.Write(this.sharedData);
            writer.Write(this.entityIdToHash);
            writer.Write(this.sharedTypesCount);
            writer.Write(this.items);
            writer.Write(this.allowStaticStorage);
        }

        /// <summary>
        /// Restores collection metadata from the stream; backing allocator storage is restored separately.
        /// </summary>
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.lockSharedIndex);
            reader.Read(ref this.sharedData);
            reader.Read(ref this.entityIdToHash);
            reader.Read(ref this.sharedTypesCount);
            reader.Read(ref this.items);
            reader.Read(ref this.allowStaticStorage);
        }

    }

}
