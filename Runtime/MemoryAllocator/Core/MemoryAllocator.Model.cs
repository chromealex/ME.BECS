namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Explicit)]
    public unsafe partial struct MemoryAllocator {

        [FieldOffset(0)]
        [NativeDisableUnsafePtrRestriction]
        public MemZone** zonesList;
        [FieldOffset(8)]
        public uint zonesListCount;
        [FieldOffset(12)]
        public uint zonesListCapacity;
        [FieldOffset(16)]
        public int initialSize;
        [FieldOffset(20)]
        public ushort version;
        [FieldOffset(22)]
        public LockSpinner lockIndex;

    }

}
