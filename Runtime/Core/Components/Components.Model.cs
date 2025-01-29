namespace ME.BECS {
    
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public partial struct Components {

        public LockSpinner lockSharedIndex;
        // hash => SharedComponentStorage<T>
        internal UIntDictionary<MemAllocatorPtr> sharedData;
        // entityId => [typeId => hash]
        internal MemArray<MemArray<uint>> entityIdToHash;
        public MemArray<MemAllocatorPtr> items;

        public int Hash => (int)this.items.Length;

    }

}