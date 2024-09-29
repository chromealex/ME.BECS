namespace ME.BECS {
    
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Explicit)]
    public partial struct Components {

        [FieldOffset(0)]
        public LockSpinner lockSharedIndex;
        // hash => SharedComponentStorage<T>
        [FieldOffset(4)]
        internal UIntDictionary<MemAllocatorPtr> sharedData;
        // entityId => [typeId => hash]
        [FieldOffset(4 + UIntDictionary<MemAllocatorPtr>.SIZE)]
        internal MemArray<MemArray<uint>> entityIdToHash;

        [FieldOffset(4 + UIntDictionary<MemAllocatorPtr>.SIZE + MemArrayData.SIZE)]
        public MemArray<MemAllocatorPtr> items;

    }

}