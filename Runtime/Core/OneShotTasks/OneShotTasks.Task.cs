namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe struct Task {

        public OneShotType type;
        public Ent ent;
        public uint typeId;
        public uint groupId;
        public MemAllocatorPtr data;

        [INLINE(256)]
        public void* GetData(State* state) => MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in this.data.ptr);

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {
            this.data.Dispose(ref allocator);
        }

    }

}