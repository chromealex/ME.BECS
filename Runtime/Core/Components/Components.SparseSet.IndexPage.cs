namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using static Cuts;

    public unsafe struct IndexPage {

        public bool IsCreated => this.generations.isCreated;
        public MemArray<uint> entToDataIdx;
        public MemArray<uint> dataIdxToEnt;
        public MemArray<ushort> generations;
        public MemArray<byte> states;
        public uint headIndex;
        public LockSpinner lockIndex;

        public uint GetReservedSizeInBytes(State* state) {

            var size = 0u;
            if (this.generations.isCreated == true) {
                size += this.entToDataIdx.GetReservedSizeInBytes();
                size += this.dataIdxToEnt.GetReservedSizeInBytes();
                size += this.states.GetReservedSizeInBytes();
                size += this.generations.GetReservedSizeInBytes();
            }

            return size;

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            
            this.entToDataIdx.BurstMode(in allocator, state);
            this.dataIdxToEnt.BurstMode(in allocator, state);
            this.states.BurstMode(in allocator, state);
            this.generations.BurstMode(in allocator, state);
            
        }

    }

}