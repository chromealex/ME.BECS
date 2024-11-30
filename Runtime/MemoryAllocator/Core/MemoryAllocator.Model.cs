namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe partial struct MemoryAllocator {

        [NativeDisableUnsafePtrRestriction]
        public MemZone** zonesList;
        public uint zonesListCount;
        public uint zonesListCapacity;
        public int initialSize;
        public LockSpinner lockIndex;
        public ushort version;

    }

}
