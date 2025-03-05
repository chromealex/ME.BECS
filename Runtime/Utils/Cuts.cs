namespace ME.BECS {

    using System.Diagnostics;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public readonly unsafe struct safe_ptr {

        [NativeDisableUnsafePtrRestriction]
        public readonly byte* ptr;
        #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
        [NativeDisableUnsafePtrRestriction]
        public readonly byte* lowBound;
        [NativeDisableUnsafePtrRestriction]
        public readonly byte* hiBound;
        public byte* HiBound => this.hiBound;
        public byte* LowBound => this.lowBound;
        #else
        public byte* HiBound => this.ptr;
        public byte* LowBound => this.ptr;
        #endif

        [INLINE(256)]
        public safe_ptr(void* ptr) {
            this.ptr = (byte*)ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            this.lowBound = null;
            this.hiBound = null;
            #endif
        }

        [INLINE(256)]
        public safe_ptr(void* ptr, uint size) {
            this.ptr = (byte*)ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            this.lowBound = this.ptr;
            this.hiBound = this.ptr + size;
            #endif
        }

        [INLINE(256)]
        internal safe_ptr(void* ptr, byte* lowBound, byte* hiBound) {
            this.ptr = (byte*)ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            this.lowBound = lowBound;
            this.hiBound = hiBound;
            #endif
        }

        [INLINE(256)]
        public safe_ptr(void* ptr, int size) : this(ptr, (uint)size) { }

        [INLINE(256)]
        public static explicit operator safe_ptr(void* ptr) {
            return new safe_ptr(ptr, 0u);
        }

        [INLINE(256)]
        public static safe_ptr operator +(safe_ptr safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr(safePtr.ptr + index);
            #endif
        }

        [INLINE(256)]
        public static safe_ptr operator -(safe_ptr safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE(safePtr.ptr - index, safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr(safePtr.ptr - index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr(safePtr.ptr - index);
            #endif
        }

        [INLINE(256)]
        public static safe_ptr operator +(safe_ptr safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr(safePtr.ptr + index);
            #endif
        }

        [INLINE(256)]
        public static safe_ptr operator -(safe_ptr safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE(safePtr.ptr - index, safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr(safePtr.ptr - index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr(safePtr.ptr - index);
            #endif
        }

        #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
        [INLINE(256)]
        public void CheckRange(uint index, uint lowBoundOffset, uint hiBoundOffset) {
            if (this.hiBound != this.lowBound) E.RANGE(this.ptr + index, this.lowBound + lowBoundOffset, this.hiBound + hiBoundOffset);
        }

        [INLINE(256)]
        public static bool CheckOverlaps(safe_ptr srcPtr, safe_ptr dstPtr) {
            if (srcPtr.lowBound != srcPtr.hiBound &&
                dstPtr.lowBound != dstPtr.hiBound) {
                if ((dstPtr.lowBound > srcPtr.lowBound && dstPtr.lowBound < srcPtr.hiBound) ||
                    (dstPtr.hiBound > srcPtr.lowBound && dstPtr.hiBound < srcPtr.hiBound) ||
                    (srcPtr.lowBound > dstPtr.lowBound && srcPtr.lowBound < dstPtr.hiBound) ||
                    (srcPtr.hiBound > dstPtr.lowBound && srcPtr.hiBound < dstPtr.hiBound)) {
                    return true;
                }
            }
            return false;
        }
        #endif

    }

    public readonly unsafe struct safe_ptr<T> where T : unmanaged {

        [NativeDisableUnsafePtrRestriction]
        public readonly T* ptr;
        #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
        [NativeDisableUnsafePtrRestriction]
        public readonly byte* lowBound;
        [NativeDisableUnsafePtrRestriction]
        public readonly byte* hiBound;
        #endif

        [INLINE(256)]
        public safe_ptr(T* ptr) {
            this.ptr = ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            this.lowBound = null;
            this.hiBound = null;
            #endif
        }

        [INLINE(256)]
        public safe_ptr(T* ptr, uint size) {
            this.ptr = ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            this.lowBound = (byte*)ptr;
            this.hiBound = (byte*)ptr + size;
            #endif
        }

        [INLINE(256)]
        public safe_ptr(T* ptr, int size) : this(ptr, (uint)size) { }

        [INLINE(256)]
        internal safe_ptr(T* ptr, byte* lowBound, byte* hiBound) {
            this.ptr = ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            this.lowBound = lowBound;
            this.hiBound = hiBound;
            #endif
        }

        [INLINE(256)]
        public safe_ptr<U> Cast<U>() where U : unmanaged {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            return new safe_ptr<U>((U*)this.ptr, this.lowBound, this.hiBound);
            #else
            return new safe_ptr<U>((U*)this.ptr);
            #endif
        }

        public ref T this[int index] {
            [INLINE(256)]
            get => ref this[(uint)index];
        }

        public ref T this[uint index] {
            [INLINE(256)]
            get {
                #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
                LeakDetector.IsAlive(this);
                if (this.hiBound != this.lowBound) E.RANGE((byte*)(this.ptr + index), this.lowBound, this.hiBound);
                #endif
                return ref this.ptr[index];
            }
        }

        [INLINE(256)]
        public static implicit operator safe_ptr(safe_ptr<T> safePtr) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            return new safe_ptr(safePtr.ptr, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr(safePtr.ptr);
            #endif
        }

        [INLINE(256)]
        public static implicit operator safe_ptr<T>(safe_ptr safePtr) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            return new safe_ptr<T>((T*)safePtr.ptr, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr<T>((T*)safePtr.ptr);
            #endif
        }

        [INLINE(256)]
        public static safe_ptr<T> operator +(safe_ptr<T> safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE((byte*)(safePtr.ptr + index), safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr<T>(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr<T>(safePtr.ptr + index);
            #endif
        }

        [INLINE(256)]
        public static safe_ptr<T> operator -(safe_ptr<T> safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE((byte*)(safePtr.ptr - index), safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr<T>(safePtr.ptr - index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr<T>(safePtr.ptr - index);
            #endif
        }

        [INLINE(256)]
        public static safe_ptr<T> operator +(safe_ptr<T> safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE((byte*)(safePtr.ptr + index), safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr<T>(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr<T>(safePtr.ptr + index);
            #endif
        }

        [INLINE(256)]
        public static safe_ptr<T> operator -(safe_ptr<T> safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE((byte*)(safePtr.ptr - index), safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr<T>(safePtr.ptr - index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr<T>(safePtr.ptr - index);
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
        public static uint _align(uint size, uint alignmentPowerOfTwo) {
            if (alignmentPowerOfTwo == 0u) return size;
            CheckPositivePowerOfTwo(alignmentPowerOfTwo);
            return (size + alignmentPowerOfTwo - 1) & ~(alignmentPowerOfTwo - 1);
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckPositivePowerOfTwo(uint value) {
            var valid = (value > 0) && ((value & (value - 1)) == 0);
            if (valid == false) {
                throw new System.ArgumentException($"Alignment requested: {value} is not a non-zero, positive power of two.");
            }
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
        public static safe_ptr _address<T>(ref T val) where T : unmanaged {

            return new safe_ptr<T>((T*)UnsafeUtility.AddressOf(ref val), TSize<T>.size);

        }

        [INLINE(256)]
        public static safe_ptr<T> _addressT<T>(ref T val) where T : unmanaged {

            return new safe_ptr<T>((T*)UnsafeUtility.AddressOf(ref val), TSize<T>.size);

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
        public static safe_ptr<T> _makeDefault<T>() where T : unmanaged {

            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            var sptr = new safe_ptr<T>((T*)ptr, TSize<T>.size);
            LeakDetector.Track(sptr);
            return sptr;

        }

        [INLINE(256)]
        public static void _resizeArray<T>(ref safe_ptr<T> arr, ref uint length, uint newLength, bool free = true) where T : unmanaged {

            _resizeArray(ALLOCATOR, ref arr, ref length, newLength, free);

        }

        [INLINE(256)]
        public static void _resizeArray<T>(Unity.Collections.Allocator allocator, ref safe_ptr<T> arr, ref uint length, uint newLength, bool free = true) where T : unmanaged {

            if (newLength > length) {

                var size = newLength * TSize<T>.size;
                var ptr = (safe_ptr<T>)_make(size, TAlign<T>.alignInt, allocator);
                _memclear(ptr, size);
                if (arr.ptr != null) {
                    _memcpy(arr, ptr, length * TSize<T>.size);
                    _memclear((safe_ptr)(ptr + length), (newLength - length) * TSize<T>.size);
                    if (free == true) _free(arr, allocator);
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
        public static int _memcmp(safe_ptr ptr1, safe_ptr ptr2, long size) {
            
            return UnsafeUtility.MemCmp(ptr1.ptr, ptr2.ptr, size);

        }

        [INLINE(256)]
        public static safe_ptr _make(uint size) {

            var ptr = (byte*)Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<byte>.alignInt);
            LeakDetector.Track(ptr);
            return new safe_ptr<byte>(ptr, size);

        }

        [INLINE(256)]
        public static safe_ptr<T> _make<T>(T obj) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            *(T*)ptr = obj;
            var sptr = new safe_ptr<T>((T*)ptr, TSize<T>.size);
            LeakDetector.Track(sptr);
            return sptr;

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
        public static safe_ptr<T> _makeArray<T>(uint length, bool clearMemory = true) where T : unmanaged {
            
            var size = TSize<T>.size * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<T>.alignInt);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            var sptr = new safe_ptr<T>((T*)ptr, size);
            LeakDetector.Track(sptr);
            return sptr;

        }

        [INLINE(256)]
        public static safe_ptr<T> _makeArray<T>(uint length, Unity.Collections.Allocator allocator, bool clearMemory = true) where T : unmanaged {
            
            var size = TSize<T>.size * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, (int)size, TAlign<T>.alignInt);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            var sptr = new safe_ptr<T>((T*)ptr, size);
            LeakDetector.Track(sptr);
            return sptr;

        }

        [INLINE(256)]
        public static safe_ptr<T> _makeDefault<T>(in T obj) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            *(T*)ptr = obj;
            var sptr = new safe_ptr<T>((T*)ptr, TSize<T>.size);
            LeakDetector.Track(sptr);
            return sptr;

        }

        [INLINE(256)]
        public static safe_ptr<T> _makeDefault<T>(in T obj, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, TSize<T>.sizeInt, TAlign<T>.alignInt);
            *(T*)ptr = obj;
            var sptr = new safe_ptr<T>((T*)ptr, TSize<T>.size);
            LeakDetector.Track(sptr);
            return sptr;

        }

        [INLINE(256)]
        public static safe_ptr _malloc(int size) => _make(size);
        [INLINE(256)]
        public static safe_ptr _malloc(uint size) => _make(size);
        [INLINE(256)]
        public static safe_ptr _calloc(int size) => _calloc((uint)size);
        [INLINE(256)]
        public static safe_ptr _calloc(uint size) {
            var ptr = _make(size);
            _memclear(ptr, size);
            return ptr;
        }
        [INLINE(256)]
        public static safe_ptr<T> _mallocDefault<T>(in T obj) where T : unmanaged => _makeDefault(in obj);
        [INLINE(256)]
        public static safe_ptr<T> _callocDefault<T>(in T obj) where T : unmanaged {
            var ptr = _makeDefault(in obj);
            _memclear(ptr, TSize<T>.size);
            return ptr;
        }

        [INLINE(256)]
        public static void _memclear(safe_ptr ptr, long lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            ptr.CheckRange((uint)lengthInBytes, 0u, 1u);
            #endif
            UnsafeUtility.MemClear(ptr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memclear(safe_ptr ptr, uint lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            ptr.CheckRange(lengthInBytes, 0u, 1u);
            #endif
            UnsafeUtility.MemClear(ptr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memcpy(safe_ptr srcPtr, safe_ptr dstPtr, int lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            srcPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            dstPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            if (safe_ptr.CheckOverlaps(srcPtr, dstPtr) == true) {
                throw new E.OutOfRangeException($"_memcpy doesnt support overlapped ranges. Use _memmove instead.");
            }
            #endif
            UnsafeUtility.MemCpy(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memcpy(safe_ptr srcPtr, safe_ptr dstPtr, uint lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            srcPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            dstPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            if (safe_ptr.CheckOverlaps(srcPtr, dstPtr) == true) {
                throw new E.OutOfRangeException("_memcpy doesnt support overlapped ranges. Use _memmove instead.");
            }
            #endif
            UnsafeUtility.MemCpy(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }
        
        [INLINE(256)]
        public static void _memcpy(safe_ptr srcPtr, safe_ptr dstPtr, long lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            srcPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            dstPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            if (safe_ptr.CheckOverlaps(srcPtr, dstPtr) == true) {
                throw new E.OutOfRangeException("_memcpy doesnt support overlapped ranges. Use _memmove instead.");
            }
            #endif
            UnsafeUtility.MemCpy(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memmove(safe_ptr srcPtr, safe_ptr dstPtr, uint lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            srcPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            dstPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            #endif
            UnsafeUtility.MemMove(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memmove(safe_ptr srcPtr, safe_ptr dstPtr, long lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            srcPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            dstPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            #endif
            UnsafeUtility.MemMove(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _free(safe_ptr obj) {

            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (WorldsDomainAllocator.allocatorDomainValid == false) return;
            LeakDetector.Free(obj);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);

        }

        [INLINE(256)]
        public static void _free<T>(safe_ptr<T> obj) where T : unmanaged {

            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (WorldsDomainAllocator.allocatorDomainValid == false) return;
            LeakDetector.Free(obj);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);

        }

        [INLINE(256)]
        public static void _free<T>(ref safe_ptr<T> obj) where T : unmanaged {

            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (WorldsDomainAllocator.allocatorDomainValid == false) return;
            LeakDetector.Free(obj);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);
            obj = default;

        }

        [INLINE(256)]
        public static void _free(ref safe_ptr obj) {
            
            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            if (WorldsDomainAllocator.allocatorDomainValid == false) return;
            LeakDetector.Free(obj);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);
            obj = default;

        }

        #region MAKE/FREE unity allocator
        [INLINE(256)]
        public static safe_ptr _malloc(int size, int align, Unity.Collections.Allocator allocator) => _make(size, align, allocator);
        [INLINE(256)]
        public static safe_ptr _calloc(int size, int align, Unity.Collections.Allocator allocator) {
            var ptr = _make(size, align, allocator);
            _memclear(ptr, size);
            return ptr;
        }
        
        [INLINE(256)]
        public static safe_ptr _make(int size, int align, Unity.Collections.Allocator allocator) {

            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, size, align);
                var sptr = new safe_ptr(ptr, size);
                LeakDetector.Track(sptr);
                return sptr;
            }
            {
                var ptr = UnsafeUtility.Malloc(size, align, allocator);
                var sptr = new safe_ptr(ptr, size);
                LeakDetector.Track(sptr);
                return sptr;
            }
            
        }

        [INLINE(256)]
        public static safe_ptr _make(uint size, int align, Unity.Collections.Allocator allocator) {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, (int)size, align);
                var sptr = new safe_ptr(ptr, size);
                LeakDetector.Track(sptr);
                return sptr;
            }
            {
                var ptr = UnsafeUtility.Malloc(size, align, allocator);
                var sptr = new safe_ptr(ptr, size);
                LeakDetector.Track(sptr);
                return sptr;
            }

        }

        [INLINE(256)]
        public static void _free<T>(safe_ptr<T> obj, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                LeakDetector.Free(obj);
                Unity.Collections.AllocatorManager.Free(allocator, obj.ptr);
                return;
            }
            LeakDetector.Free(obj);
            UnsafeUtility.Free(obj.ptr, allocator);

        }

        [INLINE(256)]
        public static void _free(safe_ptr obj, Unity.Collections.Allocator allocator) {
            
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
