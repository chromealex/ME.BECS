
using System;

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe struct ClassPtr<T> : IEquatable<ClassPtr<T>> where T : class {

        [NativeDisableUnsafePtrRestriction]
        private System.IntPtr ptr;
        [NativeDisableUnsafePtrRestriction]
        private System.Runtime.InteropServices.GCHandle gcHandle;

        public bool IsValid => this.ptr.ToPointer() != null;

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

        public bool Equals(ClassPtr<T> other)
        {
            return other.ptr == ptr;
        }
    }

    public static unsafe class Cuts {

        public static Unity.Collections.Allocator ALLOCATOR => Constants.ALLOCATOR_PERSISTENT_ST.ToAllocator;
        
        [INLINE(256)]
        public static ClassPtr<T> _classPtr<T>(T data) where T : class {
            return new ClassPtr<T>(data);
        }

        [INLINE(256)]
        public static int _sizeOf<T>() where T : struct => UnsafeUtility.SizeOf<T>();

        [INLINE(256)]
        public static int _alignOf<T>() where T : struct => UnsafeUtility.AlignOf<T>();

        [INLINE(256)]
        public static void* _address<T>(ref T val) where T : struct {

            return UnsafeUtility.AddressOf(ref val);

        }

        [INLINE(256)]
        public static T* _addressT<T>(ref T val) where T : unmanaged {

            return (T*)UnsafeUtility.AddressOf(ref val);

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
            //var ptr = UnsafeUtility.Malloc(TSize<T>.sizeInt, TAlign<T>.alignInt, ALLOCATOR);
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
                var ptr = (T*)Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<T>.alignInt);
                //var ptr = (T*)UnsafeUtility.Malloc(size, TAlign<T>.alignInt, ALLOCATOR);
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
        public static ref T2 _as<T1, T2>(ref T1 val) where T1 : unmanaged {
            
            return ref UnsafeUtility.As<T1, T2>(ref val);

        }

        [INLINE(256)]
        public static int _memcmp(void* ptr1, void* ptr2, long size) {
            
            return UnsafeUtility.MemCmp(ptr1, ptr2, size);

        }

        [INLINE(256)]
        public static byte* _make(uint size) {
            
            var ptr = (byte*)Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<byte>.alignInt);
            return ptr;
            //var ptr = UnsafeUtility.Malloc(size, TAlign<byte>.alignInt, ALLOCATOR);
            //return (byte*)ptr;

        }

        [INLINE(256)]
        public static T* _make<T>(T obj) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            //var ptr = UnsafeUtility.Malloc(TSize<T>.sizeInt, TAlign<T>.alignInt, ALLOCATOR);
            *(T*)ptr = obj;
            
            return (T*)ptr;

        }

        [INLINE(256)]
        public static T* _makeArray<T>(in T firstElement, uint length, bool clearMemory = false) where T : unmanaged {
            
            var size = TSize<T>.size * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<T>.alignInt);
            //var ptr = UnsafeUtility.Malloc(size, TAlign<T>.alignInt, ALLOCATOR);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            var tPtr = (T*)ptr;
            *tPtr = firstElement;
            return tPtr;

        }

        [INLINE(256)]
        public static T* _makeArray<T>(uint length, bool clearMemory = true) where T : unmanaged {
            
            var size = TSize<T>.sizeInt * length;
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, (int)size, TAlign<T>.alignInt);
            //var ptr = UnsafeUtility.Malloc(size, TAlign<T>.alignInt, ALLOCATOR);
            if (clearMemory == true) UnsafeUtility.MemClear(ptr, size);
            
            return (T*)ptr;

        }

        [INLINE(256)]
        public static T* _make<T>(in T obj) where T : unmanaged {
            
            var ptr = Unity.Collections.AllocatorManager.Allocate(ALLOCATOR, TSize<T>.sizeInt, TAlign<T>.alignInt);
            //var ptr = UnsafeUtility.Malloc(TSize<T>.sizeInt, TAlign<T>.alignInt, ALLOCATOR);
            *(T*)ptr = obj;
            
            return (T*)ptr;

        }

        [INLINE(256)]
        public static void _memclear(void* ptr, long lengthInBytes) {
            
            UnsafeUtility.MemClear(ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memclear(void* ptr, uint lengthInBytes) {
            
            UnsafeUtility.MemClear(ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memcpy(void* srcPtr, void* dstPtr, int lengthInBytes) {
            
            UnsafeUtility.MemCpy(dstPtr, srcPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memcpy(void* srcPtr, void* dstPtr, uint lengthInBytes) {
            
            UnsafeUtility.MemCpy(dstPtr, srcPtr, lengthInBytes);
            
        }
        
        [INLINE(256)]
        public static void _memcpy(void* srcPtr, void* dstPtr, long lengthInBytes) {
            
            UnsafeUtility.MemCpy(dstPtr, srcPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memmove(void* srcPtr, void* dstPtr, uint lengthInBytes) {
            
            UnsafeUtility.MemMove(dstPtr, srcPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memmove(void* srcPtr, void* dstPtr, long lengthInBytes) {
            
            UnsafeUtility.MemMove(dstPtr, srcPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _free<T>(ref T* obj) where T : unmanaged {
            
            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj);
            //UnsafeUtility.Free(obj, ALLOCATOR);
            obj = null;

        }

        [INLINE(256)]
        public static void _free<T>(T* obj) where T : unmanaged {

            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj);
            //UnsafeUtility.Free(obj, ALLOCATOR);

        }

        [INLINE(256)]
        public static void _free(ref void* obj) {
            
            if (WorldsPersistentAllocator.allocatorPersistentValid == false) return;
            Unity.Collections.AllocatorManager.Free(ALLOCATOR, obj);
            //UnsafeUtility.Free(obj, ALLOCATOR);
            obj = null;

        }

        #region MAKE/FREE unity allocator
        [INLINE(256)]
        public static void* _make(int size, int align, Unity.Collections.Allocator allocator) {

            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, size, align);
                return ptr;
            }
            return UnsafeUtility.Malloc(size, align, allocator);

        }

        [INLINE(256)]
        public static void* _make(uint size, int align, Unity.Collections.Allocator allocator) {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                var ptr = Unity.Collections.AllocatorManager.Allocate(allocator, (int)size, align);
                return ptr;
            }
            return UnsafeUtility.Malloc(size, align, allocator);

        }

        [INLINE(256)]
        public static void _free<T>(T* obj, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                Unity.Collections.AllocatorManager.Free(allocator, obj);
                return;
            }
            UnsafeUtility.Free(obj, allocator);

        }

        [INLINE(256)]
        public static void _free(void* obj, Unity.Collections.Allocator allocator) {
            
            if (allocator >= Unity.Collections.Allocator.FirstUserIndex) {
                Unity.Collections.AllocatorManager.Free(allocator, obj);
                return;
            }
            UnsafeUtility.Free(obj, allocator);

        }
        #endregion

    }

}