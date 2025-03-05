
namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public static unsafe class CutsPool {

        [INLINE(256)]
        public static ClassPtr<T> _classPtr<T>(T data) where T : class {
            return new ClassPtr<T>(data);
        }

        [INLINE(256)]
        public static safe_ptr<T> _address<T>(ref T val) where T : unmanaged {

            return new safe_ptr<T>((T*)UnsafeUtility.AddressOf(ref val), TSize<T>.size);

        }

        [INLINE(256)]
        public static ref T _ref<T>(T* ptr) where T : unmanaged {

            return ref *ptr;

        }

        [INLINE(256)]
        public static safe_ptr<T> _makeArray<T>(uint elementsCount) where T : unmanaged {
            
            return Cuts._makeArray<T>(elementsCount);
            //return Pools.Pop<T>(elementsCount);
            
        }

        [INLINE(256)]
        public static safe_ptr<T> _makeArray<T>(uint elementsCount, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            return Cuts._makeArray<T>(elementsCount, allocator);
            
        }

        [INLINE(256)]
        public static safe_ptr<T> _make<T>() where T : unmanaged {

            return Cuts._makeDefault<T>();
            //return Pools.Pop<T>();
            
        }

        [INLINE(256)]
        public static safe_ptr<T> _make<T>(T obj) where T : unmanaged {

            return Cuts._make(obj);
            //return Pools.Pop<T>(obj);
            
        }

        [INLINE(256)]
        public static safe_ptr<T> _makeDefault<T>(T obj, Unity.Collections.Allocator allocator) where T : unmanaged {

            return Cuts._makeDefault(obj, allocator);
            
        }

        [INLINE(256)]
        public static safe_ptr<T> _makeDefault<T>(in T obj) where T : unmanaged => Cuts._makeDefault(in obj);
        
        [INLINE(256)]
        public static void _memclear(safe_ptr ptr, uint lengthInBytes) {
            
            Cuts._memclear(ptr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memcpy(safe_ptr srcPtr, safe_ptr dstPtr, int lengthInBytes) {
            
            Cuts._memcpy(srcPtr, dstPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memcpy(safe_ptr srcPtr, safe_ptr dstPtr, uint lengthInBytes) {
            
            Cuts._memcpy(srcPtr, dstPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _memmove(safe_ptr srcPtr, safe_ptr dstPtr, uint lengthInBytes) {
            
            Cuts._memmove(srcPtr, dstPtr, lengthInBytes);
            
        }

        [INLINE(256)]
        public static void _free<T>(ref safe_ptr<T> obj) where T : unmanaged {
            
            Cuts._free(ref obj);
            //Pools.Push(ref obj);
            
        }

        [INLINE(256)]
        public static void _free<T>(safe_ptr<T> obj) where T : unmanaged {
            
            Cuts._free(obj);
            //Pools.Push(obj);

        }

        [INLINE(256)]
        public static void _freeArray<T>(safe_ptr<T> obj, uint elementsCount) where T : unmanaged {
            
            Cuts._free(obj);
            //Pools.Push(obj, elementsCount);

        }

        [INLINE(256)]
        public static void _freeArray<T>(safe_ptr<T> obj, uint elementsCount, Unity.Collections.Allocator allocator) where T : unmanaged {
            
            Cuts._free(obj, allocator);

        }

        #region MAKE/FREE unity allocator
        [INLINE(256)]
        public static safe_ptr _make(int size, int align, Unity.Collections.Allocator allocator) {
            
            return Cuts._make(size, align, allocator);

        }
        
        [INLINE(256)]
        public static safe_ptr _make(uint size, int align, Unity.Collections.Allocator allocator) {

            return Cuts._make(size, align, allocator);

        }

        [INLINE(256)]
        public static void _free<T>(safe_ptr<T> obj, Unity.Collections.Allocator allocator) where T : unmanaged {

            Cuts._free(obj, allocator);

        }

        [INLINE(256)]
        public static void _free(safe_ptr obj, Unity.Collections.Allocator allocator) {
            
            Cuts._free(obj, allocator);

        }
        #endregion

    }

}