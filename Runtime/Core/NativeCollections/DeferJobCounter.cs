namespace ME.BECS.NativeCollections {

    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    
    /// <summary>
    /// Defines defer job counter state and operations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DeferJobCounter {

        // [!] For some reason ScheduleParallelForDeferArraySize needs ptr first
        // so that's why we need LayoutKind.Sequential and first void* must be here
        // the second must be uint count
        /// <summary>
        /// Entity handles processed or stored by this operation.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public uint* entities;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public int count;

    }

}