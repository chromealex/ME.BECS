namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    
    [BURST]
    public unsafe struct ApplyJobParallel : IJobParallelFor {

        [NativeDisableUnsafePtrRestriction]
        public State* state;
            
        [INLINE(256)]
        public void Execute(int index) {
            
            this.state->batches.ApplyFromJobThread(this.state, (uint)index);
            
        }

    }

}