namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    /// <summary>
    /// Provides helper operations for utils.
    /// </summary>
    [IgnoreProfiler]
    public static class Utils {

        /// <summary>
        /// Computes a hash of the supplied data.
        /// </summary>
        [INLINE(256)]
        public static int Hash(uint v1, uint v2) {
            int hash = 23;
            hash = hash * 31 + v1.GetHashCode();
            hash = hash * 31 + v2.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Computes a hash of the supplied data.
        /// </summary>
        [INLINE(256)]
        public static int Hash(uint v1, uint v2, uint v3) {
            int hash = 23;
            hash = hash * 31 + v1.GetHashCode();
            hash = hash * 31 + v2.GetHashCode();
            hash = hash * 31 + v3.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Computes a hash of the supplied data.
        /// </summary>
        [INLINE(256)]
        public static int Hash(uint v1) {
            int hash = 23;
            hash = hash * 31 + v1.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Computes a hash of the supplied data.
        /// </summary>
        [INLINE(256)]
        public static int Hash(int v1, int v2, int v3, ulong v4) {
            int hash = 23;
            hash = hash * 31 + v1;
            hash = hash * 31 + v2;
            hash = hash * 31 + v3;
            hash = hash * 31 + v4.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Disposes the native pointer-array storage owned by this operation.
        /// </summary>
        public static unsafe void DisposePtrArray(Unity.Collections.NativeArray<System.IntPtr> array, Unity.Collections.AllocatorManager.AllocatorHandle allocator) {
            foreach (var ptr in array) {
                Unity.Collections.LowLevel.Unsafe.UnsafeUtility.Free((void*)ptr, allocator.ToAllocator);
            }
            array.Dispose();
        }

    }

}