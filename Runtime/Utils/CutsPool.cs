
namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides pooled native storage used by temporary ECS operations.
    /// </summary>
    [IgnoreProfiler]
    public static unsafe class CutsPool {

        /// <summary>
        /// Creates a handle-backed reference to the supplied managed object.
        /// </summary>
        [INLINE(256)]
        public static ClassPtr<T> _classPtr<T>(T data) where T : class {
            return new ClassPtr<T>(data);
        }

        /// <summary>
        /// Returns the native address of the supplied value.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr<T> _address<T>(ref T val) where T : unmanaged {

            return new safe_ptr<T>((T*)UnsafeUtility.AddressOf(ref val), TSize<T>.size);

        }

        /// <summary>
        /// Returns a typed reference to the supplied native address.
        /// </summary>
        [INLINE(256)]
        public static ref T _ref<T>(T* ptr) where T : unmanaged {

            return ref *ptr;

        }

        /// <summary>
        /// Allocates native array storage for the requested element count.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr<T> _makeArray<T>(uint elementsCount) where T : unmanaged {
            
            return Cuts._makeArray<T>(elementsCount);
            //return Pools.Pop<T>(elementsCount);
            
        }

        /// <summary>
        /// Allocates native array storage for the requested element count.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr<T> _makeArray<T>(uint elementsCount, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            return Cuts._makeArray<T>(elementsCount, allocator);
            
        }

        /// <summary>
        /// Allocates native array storage for the requested element count.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr<T> _makeArray<T>(uint elementsCount, Unity.Collections.Allocator allocator, bool clearMemory) where T : unmanaged {
            
            return Cuts._makeArray<T>(elementsCount, allocator, clearMemory);
            
        }

        /// <summary>
        /// Allocates native storage and initializes the requested value.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr<T> _make<T>() where T : unmanaged {

            return Cuts._makeDefault<T>();
            //return Pools.Pop<T>();
            
        }

        /// <summary>
        /// Allocates native storage and initializes the requested value.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr<T> _make<T>(T obj) where T : unmanaged {

            return Cuts._make(obj);
            //return Pools.Pop<T>(obj);
            
        }

        /// <summary>
        /// Allocates storage using the default allocator and initializes the requested value.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr<T> _makeDefault<T>(T obj, Unity.Collections.Allocator allocator) where T : unmanaged {

            return Cuts._makeDefault(obj, allocator);
            
        }

        /// <summary>
        /// Allocates storage using the default allocator and initializes the requested value.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr<T> _makeDefault<T>(in T obj) where T : unmanaged => Cuts._makeDefault(in obj);
        
        /// <summary>
        /// Fills a native byte range with zeroes.
        /// </summary>
        [INLINE(256)]
        public static void _memclear(safe_ptr ptr, uint lengthInBytes) {
            
            Cuts._memclear(ptr, lengthInBytes);
            
        }

        /// <summary>
        /// Copies bytes between non-overlapping native ranges.
        /// </summary>
        [INLINE(256)]
        public static void _memcpy(safe_ptr srcPtr, safe_ptr dstPtr, int lengthInBytes) {
            
            Cuts._memcpy(srcPtr, dstPtr, lengthInBytes);
            
        }

        /// <summary>
        /// Copies bytes between non-overlapping native ranges.
        /// </summary>
        [INLINE(256)]
        public static void _memcpy(safe_ptr srcPtr, safe_ptr dstPtr, uint lengthInBytes) {
            
            Cuts._memcpy(srcPtr, dstPtr, lengthInBytes);
            
        }

        /// <summary>
        /// Copies bytes between native ranges that may overlap.
        /// </summary>
        [INLINE(256)]
        public static void _memmove(safe_ptr srcPtr, safe_ptr dstPtr, uint lengthInBytes) {
            
            Cuts._memmove(srcPtr, dstPtr, lengthInBytes);
            
        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)]
        public static void _free<T>(ref safe_ptr<T> obj) where T : unmanaged {
            
            Cuts._free(ref obj);
            //Pools.Push(ref obj);
            
        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)]
        public static void _free<T>(safe_ptr<T> obj) where T : unmanaged {
            
            Cuts._free(obj);
            //Pools.Push(obj);

        }

        /// <summary>
        /// Frees native array storage with the supplied allocator.
        /// </summary>
        [INLINE(256)]
        public static void _freeArray<T>(safe_ptr<T> obj, uint elementsCount) where T : unmanaged {
            
            Cuts._free(obj);
            //Pools.Push(obj, elementsCount);

        }

        /// <summary>
        /// Frees native array storage with the supplied allocator.
        /// </summary>
        [INLINE(256)]
        public static void _freeArray<T>(safe_ptr<T> obj, uint elementsCount, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            Cuts._free(obj, allocator);

        }

        #region MAKE/FREE unity allocator
        /// <summary>
        /// Allocates native storage and initializes the requested value.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr _make(int size, int align, Unity.Collections.Allocator allocator) {
            
            return Cuts._make(size, align, allocator);

        }
        
        /// <summary>
        /// Allocates native storage and initializes the requested value.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr _make(uint size, int align, Unity.Collections.Allocator allocator) {

            return Cuts._make(size, align, allocator);

        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)]
        public static void _free<T>(safe_ptr<T> obj, Unity.Collections.Allocator allocator) where T : unmanaged {

            Cuts._free(obj, allocator);

        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)]
        public static void _free(safe_ptr obj, Unity.Collections.Allocator allocator) {
            
            Cuts._free(obj, allocator);

        }
        #endregion

    }

}