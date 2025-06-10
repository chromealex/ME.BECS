namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct ApplyJob : IJobSingle {

        public safe_ptr<State> state;
            
        [INLINE(256)]
        public void Execute() {

            Batches.ApplyFromJob(this.state);
            this.state.ptr->entities.free.Apply(in this.state.ptr->allocator);
            Ents.ApplyDestroyed(this.state);

        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct StartParallelJob : IJobSingle {

        [NativeDisableUnsafePtrRestriction]
        public CommandBuffer* buffer;
        public JobInfo jobInfo;

        [INLINE(256)]
        public void Execute() {

            Ents.EnsureFree(this.buffer->state, this.buffer->worldId, this.buffer->count * this.jobInfo.itemsPerCall);
            
        }

    }

}