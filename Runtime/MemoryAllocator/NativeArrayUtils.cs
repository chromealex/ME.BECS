namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;

    public static unsafe class NativeArrayUtils {

        [INLINE(256)]
        public static void Copy<T>(in MemArrayAuto<T> fromArr,
                                   ref MemArrayAuto<T> arr) where T : unmanaged {
            NativeArrayUtils.Copy(fromArr, 0, ref arr, 0, fromArr.Length);
        }
        
        [INLINE(256)]
        public static void Copy<T>(T[] src, int srcIndex, MemArrayAuto<T> dst, int dstIndex, int length) where T : unmanaged {
            var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc((object) src, System.Runtime.InteropServices.GCHandleType.Pinned);
            var num = gcHandle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy((void*) ((System.IntPtr) dst.GetUnsafePtr().ptr + dstIndex * UnsafeUtility.SizeOf<T>()), (void*) ((System.IntPtr) (void*) num + srcIndex * UnsafeUtility.SizeOf<T>()), (long) (length * UnsafeUtility.SizeOf<T>()));
            gcHandle.Free();
        }

        [INLINE(256)]
        public static void Copy<T>(ref MemoryAllocator allocator, in MemArray<T> fromArr, ref MemArray<T> arr) where T : unmanaged {
            NativeArrayUtils.Copy(ref allocator, fromArr, 0, ref arr, 0, fromArr.Length);
        }

        [INLINE(256)]
        public static void CopyExact<T>(ref MemoryAllocator allocator, in MemArray<T> fromArr, ref MemArray<T> arr) where T : unmanaged {
            NativeArrayUtils.Copy(ref allocator, fromArr, 0, ref arr, 0, fromArr.Length, true);
        }

        [INLINE(256)]
        public static void CopyExact<T>(in MemArrayAuto<T> fromArr, ref MemArrayAuto<T> arr) where T : unmanaged {
            NativeArrayUtils.Copy(fromArr, 0, ref arr, 0, fromArr.Length, true);
        }

        [INLINE(256)]
        public static void Copy<T>(ref MemoryAllocator allocator,
                                   in MemArray<T> fromArr,
                                   uint sourceIndex,
                                   ref MemArray<T> arr,
                                   uint destIndex,
                                   uint length,
                                   bool copyExact = false) where T : unmanaged {

            switch (fromArr.IsCreated) {
                case false when arr.IsCreated == false:
                    return;
                case false when arr.IsCreated == true:
                    arr.Dispose(ref allocator);
                    arr = default;
                    return;
            }

            if (arr.IsCreated == false || (copyExact == false ? arr.Length < fromArr.Length : arr.Length != fromArr.Length)) {
                if (arr.IsCreated == true) arr.Dispose(ref allocator);
                arr = new MemArray<T>(ref allocator, fromArr.Length);
            }

            var size = TSize<T>.size;
            allocator.MemMove(arr.arrPtr, destIndex * size, fromArr.arrPtr, sourceIndex * size, length * size);

        }

        [INLINE(256)]
        public static void Copy<T>(in MemArrayAuto<T> fromArr,
                                   uint sourceIndex,
                                   ref MemArrayAuto<T> arr,
                                   uint destIndex,
                                   uint length,
                                   bool copyExact = false) where T : unmanaged {

            switch (fromArr.IsCreated) {
                case false when arr.IsCreated == false:
                    return;
                case false when arr.IsCreated == true:
                    arr.Dispose();
                    arr = default;
                    return;
            }

            if (arr.IsCreated == false || (copyExact == false ? arr.Length < fromArr.Length : arr.Length != fromArr.Length)) {
                if (arr.IsCreated == true) arr.Dispose();
                arr = new MemArrayAuto<T>(fromArr.ent, fromArr.Length);
            }

            var size = TSize<T>.size;
            _memmove(fromArr.ent.World.state.ptr->allocator.GetUnsafePtr(fromArr.arrPtr, sourceIndex * size), arr.ent.World.state.ptr->allocator.GetUnsafePtr(arr.arrPtr, destIndex * size), length * size);
            
        }

        [INLINE(256)]
        public static void CopyNoChecks<T>(ref MemoryAllocator allocator,
                                           in MemArray<T> fromArr,
                                           uint sourceIndex,
                                           ref MemArray<T> arr,
                                           uint destIndex,
                                           uint length) where T : unmanaged {
            var size = TSize<T>.size;
            allocator.MemCopy(arr.arrPtr, destIndex * size, fromArr.arrPtr, sourceIndex * size, length * size);
        }

        [INLINE(256)]
        public static void CopyNoChecks<T>(in MemArrayAuto<T> fromArr,
                                           uint sourceIndex,
                                           ref MemArrayAuto<T> arr,
                                           uint destIndex,
                                           uint length) where T : unmanaged {
            var size = TSize<T>.size;
            _memmove(fromArr.ent.World.state.ptr->allocator.GetUnsafePtr(fromArr.arrPtr, sourceIndex * size), arr.ent.World.state.ptr->allocator.GetUnsafePtr(arr.arrPtr, destIndex * size), length * size);
        }

    }

}