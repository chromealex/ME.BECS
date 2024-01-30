namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct ApplyJob : IJob {

        [NativeDisableUnsafePtrRestriction]
        public State* state;
            
        [INLINE(256)]
        public void Execute() {

            this.state->batches.ApplyFromJob(this.state);
            
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct OpenJob : IJob {

        [NativeDisableUnsafePtrRestriction]
        public State* state;
            
        [INLINE(256)]
        public void Execute() {

            this.state->batches.OpenFromJob(this.state);
            
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct CloseJob : IJob {

        [NativeDisableUnsafePtrRestriction]
        public State* state;
            
        [INLINE(256)]
        public void Execute() {

            this.state->batches.CloseFromJob(this.state);
            
        }

    }

}