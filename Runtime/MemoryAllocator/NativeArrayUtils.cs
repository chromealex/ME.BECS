namespace ME.BECS {
    
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe class NativeArrayUtils {

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(ref MemoryAllocator allocator,
                                   in MemArrayAuto<T> fromArr,
                                   ref MemArrayAuto<T> arr) where T : unmanaged {
            
            NativeArrayUtils.Copy(ref allocator, fromArr, 0, ref arr, 0, fromArr.Length);
            
        }

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(ref MemoryAllocator allocator,
                                   in MemArray<T> fromArr,
                                   ref MemArray<T> arr) where T : unmanaged {
            
            NativeArrayUtils.Copy(ref allocator, fromArr, 0, ref arr, 0, fromArr.Length);
            
        }

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void CopyExact<T>(ref MemoryAllocator allocator, 
                                        in MemArray<T> fromArr,
                                        ref MemArray<T> arr) where T : unmanaged {
            
            NativeArrayUtils.Copy(ref allocator, fromArr, 0, ref arr, 0, fromArr.Length, true);
            
        }

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void CopyExact<T>(ref MemoryAllocator allocator, 
                                        in MemArrayAuto<T> fromArr,
                                        ref MemArrayAuto<T> arr) where T : unmanaged {
            
            NativeArrayUtils.Copy(ref allocator, fromArr, 0, ref arr, 0, fromArr.Length, true);
            
        }

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(ref MemoryAllocator allocator, 
                                   in MemArray<T> fromArr,
                                   uint sourceIndex,
                                   ref MemArray<T> arr,
                                   uint destIndex,
                                   uint length,
                                   bool copyExact = false) where T : unmanaged {

            switch (fromArr.isCreated) {
                case false when arr.isCreated == false:
                    return;

                case false when arr.isCreated == true:
                    arr.Dispose(ref allocator);
                    arr = default;
                    return;
            }

            if (arr.isCreated == false || (copyExact == false ? arr.Length < fromArr.Length : arr.Length != fromArr.Length)) {

                if (arr.isCreated == true) arr.Dispose(ref allocator);
                arr = new MemArray<T>(ref allocator, fromArr.Length);
                
            }

            var size = TSize<T>.size;
            allocator.MemMove(arr.arrPtr, destIndex * size, fromArr.arrPtr, sourceIndex * size, length * size);

        }

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(ref MemoryAllocator allocator, 
                                   in MemArrayAuto<T> fromArr,
                                   uint sourceIndex,
                                   ref MemArrayAuto<T> arr,
                                   uint destIndex,
                                   uint length,
                                   bool copyExact = false) where T : unmanaged {

            switch (fromArr.isCreated) {
                case false when arr.isCreated == false:
                    return;

                case false when arr.isCreated == true:
                    arr.Dispose();
                    arr = default;
                    return;
            }

            if (arr.isCreated == false || (copyExact == false ? arr.Length < fromArr.Length : arr.Length != fromArr.Length)) {

                if (arr.isCreated == true) arr.Dispose();
                arr = new MemArrayAuto<T>(fromArr.ent, fromArr.Length);
                
            }

            var size = TSize<T>.size;
            allocator.MemMove(arr.arrPtr, destIndex * size, fromArr.arrPtr, sourceIndex * size, length * size);

        }

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void CopyNoChecks<T>(ref MemoryAllocator allocator,
                                           in MemArray<T> fromArr,
                                           uint sourceIndex,
                                           ref MemArray<T> arr,
                                           uint destIndex,
                                           uint length) where T : unmanaged {

            var size = sizeof(T);
            allocator.MemCopy(arr.arrPtr, destIndex * size, fromArr.arrPtr, sourceIndex * size, length * size);

        }

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void CopyNoChecks<T>(ref MemoryAllocator allocator,
                                           in MemArrayAuto<T> fromArr,
                                           uint sourceIndex,
                                           ref MemArrayAuto<T> arr,
                                           uint destIndex,
                                           uint length) where T : unmanaged {

            var size = sizeof(T);
            allocator.MemCopy(arr.arrPtr, destIndex * size, fromArr.arrPtr, sourceIndex * size, length * size);

        }

    }

}