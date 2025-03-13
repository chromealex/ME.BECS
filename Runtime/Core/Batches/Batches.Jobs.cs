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
            this.state.ptr->entities.free.Apply(ref this.state.ptr->allocator);
            Ents.ApplyDestroyed(this.state);

        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct StartParallelJob : IJobSingle {

        [NativeDisableUnsafePtrRestriction]
        public CommandBuffer* buffer;
            
        [INLINE(256)]
        public void Execute() {

            Ents.EnsureFree(this.buffer->state, this.buffer->worldId, this.buffer->count);
            
        }

    }

    [BURST(CompileSynchronously = true)]
    public struct OpenJob : IJobSingle {

        public safe_ptr<State> state;
            
        [INLINE(256)]
        public void Execute() {

            Batches.OpenFromJob(this.state);
            
        }

    }

    [BURST(CompileSynchronously = true)]
    public struct CloseJob : IJobSingle {

        [NativeDisableUnsafePtrRestriction]
        public safe_ptr<State> state;
            
        [INLINE(256)]
        public void Execute() {

            Batches.CloseFromJob(this.state);
            
        }

    }

}