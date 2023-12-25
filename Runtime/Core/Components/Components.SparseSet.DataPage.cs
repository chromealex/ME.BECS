namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using static Cuts;

    public unsafe struct DataPage {

        public MemPtr data;
        public bool isCreated;
        public LockSpinner lockIndex;

        public uint GetReservedSizeInBytes(State* state, uint dataSize, uint dataPerPage) {

            if (this.isCreated == true) {
                return dataSize * dataPerPage;
            }
            return 0u;

        }

    }

}