
namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public struct ClassPtr<T> where T : class {

        [NativeDisableUnsafePtrRestriction]
        private System.IntPtr ptr;
        [NativeDisableUnsafePtrRestriction]
        private System.Runtime.InteropServices.GCHandle gcHandle;

        public T Value => (T)this.gcHandle.Target;

        public ClassPtr(T data) {
            this.gcHandle = (data != null ? System.Runtime.InteropServices.GCHandle.Alloc(data) : default);
            this.ptr = System.Runtime.InteropServices.GCHandle.ToIntPtr(this.gcHandle);
        }

        public void Dispose() {
            if (this.gcHandle.IsAllocated == true) {
                this.gcHandle.Free();
            }
        }

    }

    public static unsafe class Cuts {

        public const Unity.Collections.Allocator ALLOCATOR = Unity.Collections.Allocator.Persistent;

        [INLINE(256)]
        public static ClassPtr<T> _classPtr<T>(T data) where T : class {
            return new ClassPtr<T>(data);
        }

        [INLINE(256)]
        public static T* _address<T>(ref T val) where T : unmanaged {

            return (T*)UnsafeUtility.AddressOf(ref val);

        }

        [INLINE(256)]
        public static ref T _ref<T>(T* ptr) where T : unmanaged {

            return ref *ptr;

        }

        [INLINE(256)]
        public static T* _makeDefault<T>() where T : unmanaged {
            
            var ptr = UnsafeUtility.Malloc(TSize<T>.sizeInt, TAlign<T>.alignInt, ALLOCATOR);
            return (T*)ptr;

        }

        [INLINE(256)]
        public static void _resizeArray<T>(ref T** arr, uint length, uint newLength) where T : unmanaged {

            var ptr = (T**)UnsafeUtility.Malloc(newLength * sizeof(T*), TAlign<byte>.alignInt, ALLOCATOR);
            if (arr != null) {
                for (int i = 0; i < length; ++i) {
                    ptr[i] = arr[i];
                }
                UnsafeUtility.Free(arr, ALLOCATOR);
            }
            arr = ptr;

        }

        [INLINE(256)]
        public static void _resizeArray<T>(ref T* arr, ref uint length, uint newLength, bool free = true) where T : unmanaged {

            if (newLength > length) {

                var size = newLength * TSize<T>.size;
                var ptr = (T*)UnsafeUtility.Malloc(size, TAlign<T>.alignInt, ALLOCATOR);
                UnsafeUtility.MemClear(ptr, size);
                if (arr != null) {
                    //_memcpy(arr, ptr, length * TSize<T>.size);
                    for (int i = 0; i < length; ++i) {
                        *(ptr + i) = *(arr + i);
                    }

                    for (int i = (int)length; i < newLength; ++i) {
                        *(ptr + i) = default;
                    }

                    if (free == true) _free(arr);
                }

                arr = ptr;
                length = newLength;

            }

        }

        [INLINE(256)]
        public static byte* _make(uint size) {
            
            var ptr = UnsafeUtility.Malloc(size, TAlign<byte>.alignInt, ALLOCATOR);
            return (byte*)ptr;

        }

        [INLINE(256)]
        public static T* _make<T>(T obj) where T : unmanaged {
            
            var ptr = UnsafeUtility.Malloc(TSize<T>.sizeInt, TAlign<T>.alignInt, ALLOCATOR);
            *(T*)ptr = obj;
            
            return (T*)ptr;

        }

        [INLINE(256)]
        public static T* _makeArray<T>(in T firstElement, uint length, bool clearMemory = false) where T : unmanaged {
            
            var size = TSize<T>.sizeInt * length;
            var ptr = UnsafeUtility.Malloc(size, TAlign<T>.alignInt, ALLOCATOR);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            var tPtr = (T*)ptr;
            *tPtr = firstElement;
            return tPtr;

        }

        [INLINE(256)]
        public static T* _makeArray<T>(uint length, bool clearMemory = true) where T : unmanaged {
            
            var size = TSize<T>.sizeInt * length;
            var ptr = UnsafeUtility.Malloc(size, TAlign<T>.alignInt, ALLOCATOR);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            
            return (T*)ptr;

        }

        [INLINE(256)]
        public static T* _make<T>(in T obj) where T : unmanaged {
            
            var ptr = UnsafeUtility.Malloc(TSize<T>.sizeInt, TAlign<T>.alignInt, ALLOCATOR);
            *(T*)ptr = obj;
            
            return (T*)ptr;

        }

        [INLINE(256)]
        public static void _memclear(void* ptr, uint lengthInBytes) {
            
            UnsafeUtility.MemClear(ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memcpy(void* srcPtr, void* dstPtr, uint lengthInBytes) {
            
            UnsafeUtility.MemCpy(dstPtr, srcPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memmove(void* srcPtr, void* dstPtr, uint lengthInBytes) {
            
            UnsafeUtility.MemMove(dstPtr, srcPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _free<T>(ref T* obj) where T : unmanaged {
            
            UnsafeUtility.Free(obj, ALLOCATOR);
            obj = null;

        }

        [INLINE(256)]
        public static void _free<T>(T* obj) where T : unmanaged {
            
            UnsafeUtility.Free(obj, ALLOCATOR);
            obj = null;

        }

        [INLINE(256)]
        public static void _free(ref void* obj) {
            
            UnsafeUtility.Free(obj, ALLOCATOR);
            obj = null;

        }

    }

}