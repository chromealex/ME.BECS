namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Burst;
    using ME.BECS.Internal;

    public class ReadWriteSpinnerComponentsShared {
        
        public static readonly SharedStatic<Array<Array<ReadWriteNativeSpinner>>> spinners = SharedStatic<Array<Array<ReadWriteNativeSpinner>>>.GetOrCreate<ReadWriteSpinnerComponentsShared>();

    }
    
    public static class LocksCache {

        [INLINE(256)]
        public static void InitializeComponents(uint maxGroupId, uint maxIndex) {
            ref var groups = ref ReadWriteSpinnerComponentsShared.spinners.Data;
            if (maxGroupId >= groups.Length) {
                groups.Resize(maxGroupId);
            }

            for (uint i = 0u; i < groups.Length; ++i) {
                ref var data = ref groups.Get(i);
                if (maxIndex > data.Length) {
                    data.Resize(maxIndex);
                    for (uint j = 0u; j < data.Length; ++j) {
                        data.Get(j) = ReadWriteNativeSpinner.Create(Constants.ALLOCATOR_DOMAIN);
                    }
                }
            }
        }

        [INLINE(256)]
        public static void Dispose() {
            
        }

        [INLINE(256)]
        public static ref ReadWriteNativeSpinner GetComponentsReadWriteSpinner(uint groupId, uint index) {
            return ref ReadWriteSpinnerComponentsShared.spinners.Data.Get(groupId).Get(index);
        }

    }

}