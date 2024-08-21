namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct ApplyJob : IJobSingle {

        [NativeDisableUnsafePtrRestriction]
        public State* state;
            
        [INLINE(256)]
        public void Execute() {

            this.state->batches.ApplyFromJob(this.state);
            this.state->entities.free.Apply(ref this.state->allocator);
            this.state->entities.ApplyDestroyed(this.state);

        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct StartParallelJob : IJobSingle {

        [NativeDisableUnsafePtrRestriction]
        public CommandBuffer* buffer;
            
        [INLINE(256)]
        public void Execute() {

            this.buffer->state->entities.EnsureFree(this.buffer->state, this.buffer->worldId, this.buffer->count);
            
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct OpenJob : IJobSingle {

        [NativeDisableUnsafePtrRestriction]
        public State* state;
            
        [INLINE(256)]
        public void Execute() {

            this.state->batches.OpenFromJob(this.state);
            
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct CloseJob : IJobSingle {

        [NativeDisableUnsafePtrRestriction]
        public State* state;
            
        [INLINE(256)]
        public void Execute() {

            this.state->batches.CloseFromJob(this.state);
            
        }

    }

}