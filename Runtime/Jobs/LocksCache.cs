namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Burst;
    using ME.BECS.Internal;

    public class ReadWriteSpinnerShared {
        
        public static readonly SharedStatic<Array<Array<ReadWriteNativeSpinner>>> spinners = SharedStatic<Array<Array<ReadWriteNativeSpinner>>>.GetOrCreate<ReadWriteSpinnerShared>();

    }
    
    public static class LocksCache {

        public const uint MAX_ID = 3u;
        
        public const uint COMPONENTS = 1u;
        public const uint ENT_GROUPS = 2u;

        [INLINE(256)]
        public static void Initialize(uint groupId, uint maxIndex) {
            var maxGroupId = MAX_ID;
            ref var groups = ref ReadWriteSpinnerShared.spinners.Data;
            if (maxGroupId >= groups.Length) {
                groups.Resize(maxGroupId);
            }
            ref var data = ref groups.Get(groupId);
            if (maxIndex > data.Length) {
                data.Resize(maxIndex);
                for (uint j = 0u; j < data.Length; ++j) {
                    data.Get(j) = ReadWriteNativeSpinner.Create(Constants.ALLOCATOR_DOMAIN);
                }
            }
        }

        [INLINE(256)]
        public static void Dispose() {
            
        }

        [INLINE(256)]
        public static ref ReadWriteNativeSpinner GetReadWriteSpinner(uint groupId, uint index) {
            return ref ReadWriteSpinnerShared.spinners.Data.Get(groupId).Get(index);
        }

    }

}