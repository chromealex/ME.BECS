namespace ME.BECS {

    using System.Diagnostics;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Wraps a native address for typed access; copying the wrapper does not transfer or duplicate ownership.
    /// </summary>
    [IgnoreProfiler]
    public readonly unsafe struct safe_ptr {

        /// <summary>
        /// Native address of the associated storage; ownership is defined by the containing API.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public readonly byte* ptr;
        #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
        /// <summary>
        /// Low bound used by <c>safe_ptr</c>.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public readonly byte* lowBound;
        /// <summary>
        /// Hi bound used by <c>safe_ptr</c>.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public readonly byte* hiBound;
        /// <summary>
        /// Gets hi bound; this implementation returns <c>this.hiBound</c>.
        /// </summary>
        public byte* HiBound => this.hiBound;
        /// <summary>
        /// Gets low bound; this implementation returns <c>this.lowBound</c>.
        /// </summary>
        public byte* LowBound => this.lowBound;
        #else
        /// <summary>
        /// Gets hi bound; this implementation returns <c>this.ptr</c>.
        /// </summary>
        public byte* HiBound => this.ptr;
        /// <summary>
        /// Gets low bound; this implementation returns <c>this.ptr</c>.
        /// </summary>
        public byte* LowBound => this.ptr;
        #endif

        /// <summary>
        /// Initializes <c>safe_ptr</c> from the supplied ptr.
        /// </summary>
        [INLINE(256)]
        public safe_ptr(void* ptr) {
            this.ptr = (byte*)ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            this.lowBound = null;
            this.hiBound = null;
            #endif
        }

        /// <summary>
        /// Initializes <c>safe_ptr</c> from the supplied ptr, size.
        /// </summary>
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

        /// <summary>
        /// Initializes <c>safe_ptr</c> from the supplied ptr, size.
        /// </summary>
        [INLINE(256)]
        public safe_ptr(void* ptr, int size) : this(ptr, (uint)size) { }

        /// <summary>
        /// Converts the supplied value to <c>safe_ptr</c>.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static explicit operator safe_ptr(void* ptr) {
            return new safe_ptr(ptr);
        }

        /// <summary>
        /// Adds the operands.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr operator +(safe_ptr safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr(safePtr.ptr + index);
            #endif
        }

        /// <summary>
        /// Subtracts the operands or negates a single operand.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr operator -(safe_ptr safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE(safePtr.ptr - index, safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr(safePtr.ptr - index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr(safePtr.ptr - index);
            #endif
        }

        /// <summary>
        /// Adds the operands.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr operator +(safe_ptr safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr(safePtr.ptr + index);
            #endif
        }

        /// <summary>
        /// Subtracts the operands or negates a single operand.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
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
        /// <summary>
        /// Checks range.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public void CheckRange(uint index, uint lowBoundOffset, uint hiBoundOffset) {
            if (this.hiBound != this.lowBound) E.RANGE(this.ptr + index, this.lowBound + lowBoundOffset, this.hiBound + hiBoundOffset);
        }

        /// <summary>
        /// Checks overlaps.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
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

    /// <summary>
    /// Wraps a native address for typed access; copying the wrapper does not transfer or duplicate ownership.
    /// </summary>
    [IgnoreProfiler]
    public readonly unsafe struct safe_ptr<T> where T : unmanaged {

        /// <summary>
        /// Native address of the associated storage; ownership is defined by the containing API.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public readonly T* ptr;
        #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
        /// <summary>
        /// Low bound used by <c>safe_ptr</c>.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public readonly byte* lowBound;
        /// <summary>
        /// Hi bound used by <c>safe_ptr</c>.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public readonly byte* hiBound;
        #endif

        /// <summary>
        /// Initializes <c>safe_ptr</c> from the supplied ptr.
        /// </summary>
        [INLINE(256)]
        public safe_ptr(T* ptr) {
            this.ptr = ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            this.lowBound = null;
            this.hiBound = null;
            #endif
        }

        /// <summary>
        /// Initializes <c>safe_ptr</c> from the supplied ptr, size.
        /// </summary>
        [INLINE(256)]
        public safe_ptr(T* ptr, uint size) {
            this.ptr = ptr;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            this.lowBound = (byte*)ptr;
            this.hiBound = (byte*)ptr + size;
            #endif
        }

        /// <summary>
        /// Initializes <c>safe_ptr</c> from the supplied ptr, size.
        /// </summary>
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

        /// <summary>
        /// Reinterprets the value using the requested target type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public safe_ptr<U> Cast<U>() where U : unmanaged {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            return new safe_ptr<U>((U*)this.ptr, this.lowBound, this.hiBound);
            #else
            return new safe_ptr<U>((U*)this.ptr);
            #endif
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public ref T this[int index] {
            [INLINE(256)][IgnoreProfiler]
            get => ref this[(uint)index];
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public ref T this[uint index] {
            [INLINE(256)][IgnoreProfiler]
            get {
                #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
                LeakDetector.IsAlive(this);
                if (this.hiBound != this.lowBound) E.RANGE((byte*)(this.ptr + index), this.lowBound, this.hiBound);
                #endif
                return ref this.ptr[index];
            }
        }

        /// <summary>
        /// Converts the supplied value to <c>safe_ptr</c>.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static implicit operator safe_ptr(safe_ptr<T> safePtr) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            return new safe_ptr(safePtr.ptr, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr(safePtr.ptr);
            #endif
        }

        /// <summary>
        /// Converts the supplied value to <c>safe_ptr&lt;T&gt;</c>.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static implicit operator safe_ptr<T>(safe_ptr safePtr) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            return new safe_ptr<T>((T*)safePtr.ptr, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr<T>((T*)safePtr.ptr);
            #endif
        }

        /// <summary>
        /// Adds the operands.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> operator +(safe_ptr<T> safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE((byte*)(safePtr.ptr + index), safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr<T>(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr<T>(safePtr.ptr + index);
            #endif
        }

        /// <summary>
        /// Subtracts the operands or negates a single operand.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> operator -(safe_ptr<T> safePtr, uint index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE((byte*)(safePtr.ptr - index), safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr<T>(safePtr.ptr - index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr<T>(safePtr.ptr - index);
            #endif
        }

        /// <summary>
        /// Adds the operands.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> operator +(safe_ptr<T> safePtr, int index) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
            LeakDetector.IsAlive(safePtr);
            if (safePtr.hiBound != safePtr.lowBound) E.RANGE((byte*)(safePtr.ptr + index), safePtr.lowBound, safePtr.hiBound);
            return new safe_ptr<T>(safePtr.ptr + index, safePtr.lowBound, safePtr.hiBound);
            #else
            return new safe_ptr<T>(safePtr.ptr + index);
            #endif
        }

        /// <summary>
        /// Subtracts the operands or negates a single operand.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
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

    /// <summary>
    /// Provides low-level native allocation, copying and pointer conversion helpers.
    /// </summary>
    [IgnoreProfiler]
    public static unsafe class Cuts {

        /// <summary>
        /// Gets allocator; this implementation returns <c>Constants.ALLOCATOR_DOMAIN</c>.
        /// </summary>
        public static Unity.Collections.Allocator ALLOCATOR => Constants.ALLOCATOR_DOMAIN;
        
        /// <summary>
        /// Creates a handle-backed reference to the supplied managed object.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ClassPtr<T> _classPtr<T>(T data) where T : class {
            return new ClassPtr<T>(data);
        }

        /// <summary>
        /// Rounds the requested size to the required alignment.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
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
        
        /// <summary>
        /// Returns the native size of the specified type in bytes.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static int _sizeOf<T>() where T : struct => UnsafeUtility.SizeOf<T>();

        /// <summary>
        /// Returns the native alignment required by the specified type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static int _alignOf<T>() where T : struct => UnsafeUtility.AlignOf<T>();

        /// <summary>
        /// Returns a pointer to the supplied value's storage.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void* _addressPtr<T>(ref T val) where T : struct {

            return UnsafeUtility.AddressOf(ref val);

        }

        /// <summary>
        /// Returns the native address of the supplied value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _address<T>(ref T val) where T : unmanaged {

            return new safe_ptr<T>((T*)UnsafeUtility.AddressOf(ref val), TSize<T>.size);

        }

        /// <summary>
        /// Returns the native address of the supplied typed value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> _addressT<T>(ref T val) where T : unmanaged {

            return new safe_ptr<T>((T*)UnsafeUtility.AddressOf(ref val), TSize<T>.size);

        }

        /// <summary>
        /// Returns a typed reference to the supplied native address.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref T _ref<T>(T* ptr) where T : unmanaged {

            return ref *ptr;

        }

        /// <summary>
        /// Reads a structure from its native memory representation.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _ptrToStruct<T>(void* ptr, out T result) where T : unmanaged {
            
            result = *(T*)ptr;
            
        }

        /// <summary>
        /// Writes a structure into native memory.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _structToPtr<T>(ref T data, void* ptr) where T : unmanaged {
            
            *(T*)ptr = data;
            
        }

        /// <summary>
        /// Allocates storage using the default allocator and initializes the requested value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> _makeDefault<T>() where T : unmanaged {

            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            var sptr = new safe_ptr<T>((T*)ptr, TSize<T>.size);
            LeakDetector.Track(sptr, ALLOCATOR);
            return sptr;

        }

        /// <summary>
        /// Resizes native array storage and updates the pointer and capacity.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _resizeArray<T>(ref safe_ptr<T> arr, ref uint length, uint newLength, bool free = true) where T : unmanaged {

            _resizeArray(ALLOCATOR, ref arr, ref length, newLength, free);

        }

        /// <summary>
        /// Resizes native array storage and updates the pointer and capacity.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _resizeArray<T>(Unity.Collections.Allocator allocator, ref safe_ptr<T> arr, ref uint length, uint newLength, bool free = true) where T : unmanaged {

            if (newLength > length) {

                var size = newLength * TSize<T>.size;
                var ptr = (safe_ptr<T>)_make(size, TAlign<T>.alignInt, allocator);
                if (arr.ptr != null) {
                    _memcpy(arr, ptr, length * TSize<T>.size);
                    _memclear((safe_ptr)(ptr + length), (newLength - length) * TSize<T>.size);
                    if (free == true) _free(arr, allocator);
                } else {
                    _memclear(ptr, size);
                }

                arr = ptr;
                length = newLength;

            }

        }

        /// <summary>
        /// Reinterprets the supplied storage as the requested unmanaged type.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref T2 _as<T1, T2>(ref T1 val) where T1 : unmanaged {
            
            return ref UnsafeUtility.As<T1, T2>(ref val);

        }

        /// <summary>
        /// Compares the contents of two native memory ranges.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static int _memcmp(safe_ptr ptr1, safe_ptr ptr2, long size) {
            
            return UnsafeUtility.MemCmp(ptr1.ptr, ptr2.ptr, size);

        }

        /// <summary>
        /// Allocates native storage and initializes the requested value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _make(uint size) {

            var ptr = (byte*)Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<byte>.alignInt);
            LeakDetector.Track(ptr, ALLOCATOR);
            LeakDetector.TrackCount(ptr, ALLOCATOR);
            return new safe_ptr<byte>(ptr, size);

        }

        /// <summary>
        /// Allocates native storage and initializes the requested value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> _make<T>(T obj) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            *(T*)ptr = obj;
            var sptr = new safe_ptr<T>((T*)ptr, TSize<T>.size);
            LeakDetector.Track(sptr, ALLOCATOR);
            LeakDetector.TrackCount(sptr.ptr, ALLOCATOR);
            return sptr;

        }

        /// <summary>
        /// Allocates native array storage for the requested element count.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static T* _makeArray<T>(in T firstElement, uint length, bool clearMemory = false) where T : unmanaged {
            
            var size = TSize<T>.size * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<T>.alignInt);
            LeakDetector.Track(ptr, ALLOCATOR);
            LeakDetector.TrackCount(ptr, ALLOCATOR);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            var tPtr = (T*)ptr;
            *tPtr = firstElement;
            return tPtr;

        }

        /// <summary>
        /// Allocates native array storage for the requested element count.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> _makeArray<T>(uint length, bool clearMemory = true) where T : unmanaged {
            
            var size = TSize<T>.size * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<T>.alignInt);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            var sptr = new safe_ptr<T>((T*)ptr, size);
            LeakDetector.Track(sptr, ALLOCATOR);
            LeakDetector.TrackCount(sptr.ptr, ALLOCATOR);
            return sptr;

        }

        /// <summary>
        /// Allocates native array storage for the requested element count.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> _makeArray<T>(uint length, Unity.Collections.Allocator allocator, bool clearMemory = true) where T : unmanaged {
            
            var size = TSize<T>.size * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, (int)size, TAlign<T>.alignInt);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            var sptr = new safe_ptr<T>((T*)ptr, size);
            LeakDetector.Track(sptr, allocator);
            LeakDetector.TrackCount(sptr.ptr, allocator);
            return sptr;

        }

        /// <summary>
        /// Allocates storage using the default allocator and initializes the requested value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> _makeDefault<T>(in T obj) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            *(T*)ptr = obj;
            var sptr = new safe_ptr<T>((T*)ptr, TSize<T>.size);
            LeakDetector.Track(sptr, ALLOCATOR);
            LeakDetector.TrackCount(sptr.ptr, ALLOCATOR);
            return sptr;

        }

        /// <summary>
        /// Allocates storage using the default allocator and initializes the requested value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> _makeDefault<T>(in T obj, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, TSize<T>.sizeInt, TAlign<T>.alignInt);
            *(T*)ptr = obj;
            var sptr = new safe_ptr<T>((T*)ptr, TSize<T>.size);
            LeakDetector.Track(sptr, allocator);
            LeakDetector.TrackCount(sptr.ptr, allocator);
            return sptr;

        }

        /// <summary>
        /// Allocates an uninitialized native byte range.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _malloc(int size) => _make(size);
        /// <summary>
        /// Allocates an uninitialized native byte range.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _malloc(uint size) => _make(size);
        /// <summary>
        /// Allocates a native byte range initialized to zero.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _calloc(int size) => _calloc((uint)size);
        /// <summary>
        /// Allocates a native byte range initialized to zero.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _calloc(uint size) {
            var ptr = _make(size);
            _memclear(ptr, size);
            return ptr;
        }
        /// <summary>
        /// Allocates uninitialized native memory using the default allocator.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> _mallocDefault<T>(in T obj) where T : unmanaged => _makeDefault(in obj);
        /// <summary>
        /// Allocates zero-initialized native memory using the default allocator.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr<T> _callocDefault<T>(in T obj) where T : unmanaged {
            var ptr = _makeDefault(in obj);
            _memclear(ptr, TSize<T>.size);
            return ptr;
        }

        /// <summary>
        /// Fills a native byte range with zeroes.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _memclear(safe_ptr ptr, long lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            ptr.CheckRange((uint)lengthInBytes, 0u, 1u);
            #endif
            UnsafeUtility.MemClear(ptr.ptr, lengthInBytes);
            
        }

        /// <summary>
        /// Fills a native byte range with zeroes.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _memclear(safe_ptr ptr, uint lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            ptr.CheckRange(lengthInBytes, 0u, 1u);
            #endif
            UnsafeUtility.MemClear(ptr.ptr, lengthInBytes);
            
        }

        /// <summary>
        /// Copies bytes between non-overlapping native ranges.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
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

        /// <summary>
        /// Copies bytes between non-overlapping native ranges.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
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
        
        /// <summary>
        /// Copies bytes between non-overlapping native ranges.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
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

        /// <summary>
        /// Copies bytes between native ranges that may overlap.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _memmove(safe_ptr srcPtr, safe_ptr dstPtr, uint lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            srcPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            dstPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            #endif
            UnsafeUtility.MemMove(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        /// <summary>
        /// Copies bytes between native ranges that may overlap.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _memmove(safe_ptr srcPtr, safe_ptr dstPtr, long lengthInBytes) {
            
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            srcPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            dstPtr.CheckRange((uint)lengthInBytes, 0u, 1u);
            #endif
            UnsafeUtility.MemMove(dstPtr.ptr, srcPtr.ptr, lengthInBytes);
            
        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _free(safe_ptr obj) {

            LeakDetector.Free(obj, ALLOCATOR);
            LeakDetector.UntrackCount(obj.ptr, ALLOCATOR);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);

        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _free<T>(safe_ptr<T> obj) where T : unmanaged {

            LeakDetector.Free(obj, ALLOCATOR);
            LeakDetector.UntrackCount(obj.ptr, ALLOCATOR);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);

        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _free<T>(ref safe_ptr<T> obj) where T : unmanaged {

            LeakDetector.Free(obj, ALLOCATOR);
            LeakDetector.UntrackCount(obj.ptr, ALLOCATOR);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);
            obj = default;

        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _free(ref safe_ptr obj) {
            
            LeakDetector.Free(obj, ALLOCATOR);
            LeakDetector.UntrackCount(obj.ptr, ALLOCATOR);
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj.ptr);
            obj = default;

        }

        #region MAKE/FREE unity allocator
        /// <summary>
        /// Allocates an uninitialized native byte range.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _malloc(int size, int align, Unity.Collections.Allocator allocator) => _make(size, align, allocator);
        /// <summary>
        /// Allocates a native byte range initialized to zero.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _calloc(int size, int align, Unity.Collections.Allocator allocator) {
            var ptr = _make(size, align, allocator);
            _memclear(ptr, size);
            return ptr;
        }
        
        /// <summary>
        /// Allocates native storage and initializes the requested value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _make(int size, int align, Unity.Collections.Allocator allocator) {

            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, size, align);
                var sptr = new safe_ptr(ptr, size);
                LeakDetector.Track(sptr, allocator);
                LeakDetector.TrackCount(sptr.ptr, allocator);
                return sptr;
            }

            {
                var ptr = UnsafeUtility.Malloc(size, align, allocator);
                var sptr = new safe_ptr(ptr, size);
                LeakDetector.Track(sptr, allocator);
                LeakDetector.TrackCount(sptr.ptr, allocator);
                return sptr;
            }
            
        }

        /// <summary>
        /// Allocates native storage and initializes the requested value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static safe_ptr _make(uint size, int align, Unity.Collections.Allocator allocator) {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, (int)size, align);
                var sptr = new safe_ptr(ptr, size);
                LeakDetector.Track(sptr, allocator);
                LeakDetector.TrackCount(sptr.ptr, allocator);
                return sptr;
            }
            {
                var ptr = UnsafeUtility.Malloc(size, align, allocator);
                var sptr = new safe_ptr(ptr, size);
                LeakDetector.Track(sptr, allocator);
                LeakDetector.TrackCount(sptr.ptr, allocator);
                return sptr;
            }

        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _free<T>(safe_ptr<T> obj, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                LeakDetector.Free(obj, allocator);
                LeakDetector.UntrackCount(obj.ptr, allocator);
                Unity.Collections.AllocatorManager.Free(allocator, obj.ptr);
                return;
            }
            LeakDetector.Free(obj, allocator);
            LeakDetector.UntrackCount(obj.ptr, allocator);
            UnsafeUtility.Free(obj.ptr, allocator);

        }

        /// <summary>
        /// Releases native storage using the matching allocator.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void _free(safe_ptr obj, Unity.Collections.Allocator allocator) {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                LeakDetector.Free(obj, allocator);
                LeakDetector.UntrackCount(obj.ptr, allocator);
                Unity.Collections.AllocatorManager.Free(allocator, obj.ptr);
                return;
            }
            LeakDetector.Free(obj, allocator);
            LeakDetector.UntrackCount(obj.ptr, allocator);
            UnsafeUtility.Free(obj.ptr, allocator);

        }
        #endregion

    }

}
