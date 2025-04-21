namespace ME.BECS.NativeCollections {

    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DeferJobCounter {

        // [!] For some reason ScheduleParallelForDeferArraySize needs ptr first
        // so that's why we need LayoutKind.Sequential and first void* must be here
        // the second must be uint count
        [NativeDisableUnsafePtrRestriction]
        public uint* entities;
        public int count;

    }

}