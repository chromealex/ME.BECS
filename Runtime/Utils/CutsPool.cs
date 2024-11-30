
namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public static unsafe class CutsPool {

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
        public static T* _makeArray<T>(uint elementsCount) where T : unmanaged {
            
            return Cuts._makeArray<T>(elementsCount);
            //return Pools.Pop<T>(elementsCount);
            
        }

        [INLINE(256)]
        public static T* _makeArray<T>(uint elementsCount, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            return Cuts._makeArray<T>(elementsCount, allocator);
            
        }

        [INLINE(256)]
        public static T* _make<T>() where T : unmanaged {

            return Cuts._makeDefault<T>();
            //return Pools.Pop<T>();
            
        }

        [INLINE(256)]
        public static T* _make<T>(T obj) where T : unmanaged {

            return Cuts._make(obj);
            //return Pools.Pop<T>(obj);
            
        }

        [INLINE(256)]
        public static T* _make<T>(T obj, Unity.Collections.Allocator allocator) where T : unmanaged {

            return Cuts._make(obj, allocator);
            
        }

        [INLINE(256)]
        public static T* _make<T>(in T obj) where T : unmanaged {

            return Cuts._make(in obj);
            //return Pools.Pop<T>(obj);

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
        public static void _memmove(void* srcPtr, void* dstPtr, uint lengthInBytes) {
            
            UnsafeUtility.MemMove(dstPtr, srcPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _free<T>(ref T* obj) where T : unmanaged {
            
            Cuts._free(ref obj);
            //Pools.Push(ref obj);
            
        }

        [INLINE(256)]
        public static void _free<T>(T* obj) where T : unmanaged {
            
            Cuts._free(obj);
            //Pools.Push(obj);

        }

        [INLINE(256)]
        public static void _freeArray<T>(T* obj, uint elementsCount) where T : unmanaged {
            
            Cuts._free(obj);
            //Pools.Push(obj, elementsCount);

        }

        [INLINE(256)]
        public static void _freeArray<T>(T* obj, uint elementsCount, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            Cuts._free(obj, allocator);

        }

        #region MAKE/FREE unity allocator
        [INLINE(256)]
        public static void* _make(int size, int align, Unity.Collections.Allocator allocator) {
            
            return Cuts._make(size, align, allocator);

        }
        
        [INLINE(256)]
        public static void* _make(uint size, int align, Unity.Collections.Allocator allocator) {

            return Cuts._make(size, align, allocator);

        }

        [INLINE(256)]
        public static void _free<T>(T* obj, Unity.Collections.Allocator allocator) where T : unmanaged {

            Cuts._free(obj, allocator);

        }

        [INLINE(256)]
        public static void _free(void* obj, Unity.Collections.Allocator allocator) {
            
            Cuts._free(obj, allocator);

        }
        #endregion

    }

}