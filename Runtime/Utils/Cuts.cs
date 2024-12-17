namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe struct SafePtr {

        [NativeDisableUnsafePtrRestriction]
        public volatile byte* ptr;
        #if MEMORY_ALLOCATOR_BOUNDS_CHECK
        public uint size;
        #endif
        
        [INLINE(256)]
        public SafePtr(void* ptr, uint size) {
            this.ptr = (byte*)ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            this.size = size;
            #endif
        }

        [INLINE(256)]
        public SafePtr(void* ptr, int size) {
            this.ptr = (byte*)ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            this.size = (uint)size;
            #endif
        }

        [INLINE(256)]
        public static explicit operator SafePtr(void* ptr) {
            return new SafePtr(ptr, 0u);
        }

        [INLINE(256)]
        public static SafePtr operator +(SafePtr safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (safePtr.size > 0u) E.RANGE(index, 0u, safePtr.size);
            return new SafePtr(safePtr.ptr + index, safePtr.size - index);
            #else
            return new SafePtr(safePtr.ptr + index, 0u);
            #endif
        }

        [INLINE(256)]
        public static SafePtr operator -(SafePtr safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            return new SafePtr(safePtr.ptr - index, safePtr.size + index);
            #else
            return new SafePtr(safePtr.ptr - index, 0u);
            #endif
        }

        [INLINE(256)]
        public static SafePtr operator +(SafePtr safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (safePtr.size > 0u) E.RANGE(index, 0u, safePtr.size);
            return new SafePtr(safePtr.ptr + index, safePtr.size - (uint)index);
            #else
            return new SafePtr(safePtr.ptr + index, 0u);
            #endif
        }

        [INLINE(256)]
        public static SafePtr operator -(SafePtr safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            return new SafePtr(safePtr.ptr - index, safePtr.size + (uint)index);
            #else
            return new SafePtr(safePtr.ptr - index, 0u);
            #endif
        }

    }

    public readonly unsafe struct SafePtr<T> where T : unmanaged {

        [NativeDisableUnsafePtrRestriction]
        public readonly T* ptr;
        #if MEMORY_ALLOCATOR_BOUNDS_CHECK
        public readonly uint size;
        #endif

        [INLINE(256)]
        public SafePtr<U> Cast<U>() where U : unmanaged {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            return new SafePtr<U>((U*)this.ptr, this.size);
            #else
            return new SafePtr<U>((U*)this.ptr, 0u);
            #endif
        }

        [INLINE(256)]
        public SafePtr(T* ptr, uint size) {
            this.ptr = ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            this.size = size;
            #endif
        }

        [INLINE(256)]
        public SafePtr(T* ptr, int size) {
            this.ptr = ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            this.size = (uint)size;
            #endif
        }

        public ref T this[int index] {
            [INLINE(256)]
            get {
                #if MEMORY_ALLOCATOR_BOUNDS_CHECK
                if (this.size > 0u) E.RANGE(index, 0u, this.size / TSize<T>.size);
                #endif
                return ref this.ptr[index];
            }
        }
        public ref T this[uint index] {
            [INLINE(256)]
            get {
                #if MEMORY_ALLOCATOR_BOUNDS_CHECK
                if (this.size > 0u) E.RANGE(index, 0u, this.size / TSize<T>.size);
                #endif
                return ref this.ptr[index];
            }
        }

        [INLINE(256)]
        public static implicit operator SafePtr(SafePtr<T> safePtr) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            return new SafePtr(safePtr.ptr, safePtr.size);
            #else
            return new SafePtr(safePtr.ptr, 0u);
            #endif
        }

        [INLINE(256)]
        public static implicit operator SafePtr<T>(SafePtr safePtr) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            return new SafePtr<T>((T*)safePtr.ptr, safePtr.size);
            #else
            return new SafePtr<T>((T*)safePtr.ptr, 0u);
            #endif
        }

        [INLINE(256)]
        public static SafePtr<T> operator +(SafePtr<T> safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (safePtr.size > 0u) E.RANGE(index, 0u, safePtr.size / TSize<T>.size);
            return new SafePtr<T>(safePtr.ptr + index, safePtr.size - index);
            #else
            return new SafePtr<T>(safePtr.ptr + index, 0u);
            #endif
        }

        [INLINE(256)]
        public static SafePtr<T> operator -(SafePtr<T> safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            return new SafePtr<T>(safePtr.ptr - index, safePtr.size + index);
            #else
            return new SafePtr<T>(safePtr.ptr - index, 0u);
            #endif
        }

        [INLINE(256)]
        public static SafePtr<T> operator +(SafePtr<T> safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (safePtr.size > 0u) E.RANGE(index, 0u, safePtr.size / TSize<T>.size);
            return new SafePtr<T>(safePtr.ptr + index, safePtr.size - (uint)index);
            #else
            return new SafePtr<T>(safePtr.ptr + index, 0u);
            #endif
        }

        [INLINE(256)]
        public static SafePtr<T> operator -(SafePtr<T> safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            return new SafePtr<T>(safePtr.ptr - index, safePtr.size + (uint)index);
            #else
            return new SafePtr<T>(safePtr.ptr - index, 0u);
            #endif
        }

    }

    public static unsafe class Cuts {

        public static Unity.Collections.Allocator ALLOCATOR => Constants.ALLOCATOR_DOMAIN;
        
        [INLINE(256)]
        public static ClassPtr<T> _classPtr<T>(T data) where T : class {
            return new ClassPtr<T>(data);
        }

        [INLINE(256)]
        public static int _sizeOf<T>() where T : struct => UnsafeUtility.SizeOf<T>();

        [INLINE(256)]
        public static int _alignOf<T>() where T : struct => UnsafeUtility.AlignOf<T>();

        [INLINE(256)]
        public static void* _addressPtr<T>(ref T val) where T : struct {

            return UnsafeUtility.AddressOf(ref val);

        }

        [INLINE(256)]
        public static SafePtr _address<T>(ref T val) where T : unmanaged {

            return new SafePtr<T>((T*)UnsafeUtility.AddressOf(ref val), TSize<T>.size);

        }

        [INLINE(256)]
        public static SafePtr<T> _addressT<T>(ref T val) where T : unmanaged {

            return new SafePtr<T>((T*)UnsafeUtility.AddressOf(ref val), TSize<T>.size);

        }

        [INLINE(256)]
        public static ref T _ref<T>(T* ptr) where T : unmanaged {

            return ref *ptr;

        }

        [INLINE(256)]
        public static void _ptrToStruct<T>(void* ptr, out T result) where T : unmanaged {
            
            result = *(T*)ptr;
            
        }

        [INLINE(256)]
        public static void _structToPtr<T>(ref T data, void* ptr) where T : unmanaged {
            
            *(T*)ptr = data;
            
        }

        [INLINE(256)]
        public static T* _makeDefault<T>() where T : unmanaged {

            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            LeakDetector.Track(ptr);
            return (T*)ptr;

        }

        [INLINE(256)]
        public static void _resizeArray<T>(ref SafePtr<T> arr, ref uint length, uint newLength, bool free = true) where T : unmanaged {

            if (newLength > length) {

                var size = newLength * TSize<T>.size;
                var ptr = (SafePtr<T>)_make(size, TAlign<T>.alignInt, ALLOCATOR);
                //var ptr = (T*)Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<T>.alignInt);
                LeakDetector.Track(ptr);
                //var ptr = (T*)UnsafeUtility.Malloc(size, TAlign<T>.alignInt, ALLOCATOR);
                _memclear(ptr, size);
                if (arr.ptr != null) {
                    _memcpy(arr, ptr, length * TSize<T>.size);
                    _memclear((SafePtr)(ptr + length), (newLength - length) * TSize<T>.size);
                    /*for (int i = 0; i < length; ++i) {
                        *(ptr + i) = *(arr + i);
                    }

                    for (int i = (int)length; i < newLength; ++i) {
                        *(ptr + i) = default;
                    }*/

                    if (free == true) _free(arr);
                }

                arr = ptr;
                length = newLength;

            }

        }
        
        [INLINE(256)]
        public static ref T2 _as<T1, T2>(ref T1 val) where T1 : unmanaged {
            
            return ref UnsafeUtility.As<T1, T2>(ref val);

        }

        [INLINE(256)]
        public static int _memcmp(void* ptr1, void* ptr2, long size) {
            
            return UnsafeUtility.MemCmp(ptr1, ptr2, size);

        }

        [INLINE(256)]
        public static SafePtr _make(uint size) {

            var ptr = (byte*)Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<byte>.alignInt);
            LeakDetector.Track(ptr);
            return new SafePtr<byte>(ptr, size);

        }

        [INLINE(256)]
        public static SafePtr<T> _make<T>(T obj) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            LeakDetector.Track(ptr);
            *(T*)ptr = obj;
            
            return new SafePtr<T>((T*)ptr, TSize<T>.size);

        }

        [INLINE(256)]
        public static T* _makeArray<T>(in T firstElement, uint length, bool clearMemory = false) where T : unmanaged {
            
            var size = TSize<T>.size * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<T>.alignInt);
            LeakDetector.Track(ptr);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            var tPtr = (T*)ptr;
            *tPtr = firstElement;
            return tPtr;

        }

        [INLINE(256)]
        public static SafePtr<T> _makeArray<T>(uint length, bool clearMemory = true) where T : unmanaged {
            
            var size = TSize<T>.size * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<T>.alignInt);
            LeakDetector.Track(ptr);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            
            return new SafePtr<T>((T*)ptr, size);

        }

        [INLINE(256)]
        public static SafePtr<T> _makeArray<T>(uint length, Unity.Collections.Allocator allocator, bool clearMemory = true) where T : unmanaged {
            
            var size = TSize<T>.size * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, (int)size, TAlign<T>.alignInt);
            LeakDetector.Track(ptr);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            
            return new SafePtr<T>((T*)ptr, size);

        }

        [INLINE(256)]
        public static SafePtr<T> _make<T>(in T obj) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            LeakDetector.Track(ptr);
            *(T*)ptr = obj;
            
            return new SafePtr<T>((T*)ptr, TSize<T>.size);

        }

        [INLINE(256)]
        public static SafePtr<T> _make<T>(in T obj, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, TSize<T>.sizeInt, TAlign<T>.alignInt);
            LeakDetector.Track(ptr);
            *(T*)ptr = obj;
            
            return new SafePtr<T>((T*)ptr, TSize<T>.size);

        }

        [INLINE(256)]
        public static SafePtr _malloc(int size) => _make(size);
        [INLINE(256)]
        public static SafePtr _malloc(uint size) => _make(size);
        [INLINE(256)]
        public static SafePtr _calloc(int size) => _calloc((uint)size);
        [INLINE(256)]
        public static SafePtr _calloc(uint size) {
            var ptr = _make(size);
            _memclear(ptr, size);
            return ptr;
        }
        [INLINE(256)]
        public static SafePtr<T> _malloc<T>(in T obj) where T : unmanaged => _make(in obj);
        [INLINE(256)]
        public static SafePtr<T> _calloc<T>(in T obj) where T : unmanaged {
            var ptr = _make(in obj);
            _memclear(ptr, TSize<T>.size);
            return ptr;
        }

        [INLINE(256)]
        public static void _memclear(SafePtr ptr, long lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (ptr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, ptr.size + 1u);
            #endif
            UnsafeUtility.MemClear(ptr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memclear(SafePtr ptr, uint lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (ptr.size > 0u) E.RANGE(lengthInBytes, 0u, ptr.size + 1u);
            #endif
            UnsafeUtility.MemClear(ptr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memcpy(SafePtr srcPtr, SafePtr dstPtr, int lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (srcPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, srcPtr.size + 1u);
            if (dstPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, dstPtr.size + 1u);
            #endif
            UnsafeUtility.MemCpy(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memcpy(SafePtr srcPtr, SafePtr dstPtr, uint lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (srcPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, srcPtr.size + 1u);
            if (dstPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, dstPtr.size + 1u);
            #endif
            UnsafeUtility.MemCpy(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }
        
        [INLINE(256)]
        public static void _memcpy(SafePtr srcPtr, SafePtr dstPtr, long lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (srcPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, srcPtr.size + 1u);
            if (dstPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, dstPtr.size + 1u);
            #endif
            UnsafeUtility.MemCpy(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memmove(SafePtr srcPtr, SafePtr dstPtr, uint lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (srcPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, srcPtr.size + 1u);
            if (dstPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, dstPtr.size + 1u);
            #endif
            UnsafeUtility.MemMove(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memmove(SafePtr srcPtr, SafePtr dstPtr, long lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (srcPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, srcPtr.size + 1u);
            if (dstPtr.size > 0u) E.RANGE((uint)lengthInBytes, 0u, dstPtr.size + 1u);
            #endif
            UnsafeUtility.MemMove(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _free(SafePtr obj) {

            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (WorldsDomainAllocator.allocatorDomainValid == false) return;
            LeakDetector.Free(obj);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);

        }

        [INLINE(256)]
        public static void _free<T>(SafePtr<T> obj) where T : unmanaged {

            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (WorldsDomainAllocator.allocatorDomainValid == false) return;
            LeakDetector.Free(obj);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);

        }

        [INLINE(256)]
        public static void _free<T>(ref SafePtr<T> obj) where T : unmanaged {

            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (WorldsDomainAllocator.allocatorDomainValid == false) return;
            LeakDetector.Free(obj);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);
            obj = default;

        }

        [INLINE(256)]
        public static void _free(ref SafePtr obj) {
            
            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (WorldsDomainAllocator.allocatorDomainValid == false) return;
            LeakDetector.Free(obj);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);
            obj = default;

        }

        #region MAKE/FREE unity allocator
        [INLINE(256)]
        public static SafePtr _malloc(int size, int align, Unity.Collections.Allocator allocator) => _make(size, align, allocator);
        [INLINE(256)]
        public static SafePtr _calloc(int size, int align, Unity.Collections.Allocator allocator) {
            var ptr = _make(size, align, allocator);
            _memclear(ptr, size);
            return ptr;
        }
        
        [INLINE(256)]
        public static SafePtr _make(int size, int align, Unity.Collections.Allocator allocator) {

            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, size, align);
                LeakDetector.Track(ptr);
                return new SafePtr(ptr, size);
            }
            {
                var ptr = UnsafeUtility.Malloc(size, align, allocator);
                LeakDetector.Track(ptr);
                return new SafePtr(ptr, size);
            }
            
        }

        [INLINE(256)]
        public static SafePtr _make(uint size, int align, Unity.Collections.Allocator allocator) {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, (int)size, align);
                LeakDetector.Track(ptr);
                return new SafePtr(ptr, size);
            }
            {
                var ptr = UnsafeUtility.Malloc(size, align, allocator);
                LeakDetector.Track(ptr);
                return new SafePtr(ptr, size);
            }

        }

        [INLINE(256)]
        public static void _free<T>(SafePtr<T> obj, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                LeakDetector.Free(obj);
                Unity.Collections.AllocatorManager.Free(allocator, obj.ptr);
                return;
            }
            LeakDetector.Free(obj);
            UnsafeUtility.Free(obj.ptr, allocator);

        }

        [INLINE(256)]
        public static void _free(SafePtr obj, Unity.Collections.Allocator allocator) {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                LeakDetector.Free(obj);
                Unity.Collections.AllocatorManager.Free(allocator, obj.ptr);
                return;
            }
            LeakDetector.Free(obj);
            UnsafeUtility.Free(obj.ptr, allocator);

        }
        #endregion

    }

}
