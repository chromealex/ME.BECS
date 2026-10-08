namespace ME.BECS {
    
    using static Cuts;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    /// <summary>
    /// Owns an ECS simulation state, entity storage and scheduled system work.
    /// </summary>
    public unsafe partial struct World {
        
        /// <summary>
        /// Creates a copy of the supplied state using the requested allocation context.
        /// </summary>
        [INLINE(256)]
        public World Clone() {
            E.IS_CREATED(this);
            World newWorld = default;
            newWorld.CopyFrom(this);
            return newWorld;
        }

        /// <summary>
        /// Copies the supplied source state into this world instance.
        /// </summary>
        [INLINE(256)]
        public void CopyFrom(in World srcWorld) {
            
            E.IS_CREATED(srcWorld);

            Batches.Apply(srcWorld.id, srcWorld.state);

            // Dispose current state
            if (this.state.ptr != null) {
                Worlds.ReleaseWorld(this);
                this.state.ptr->Dispose();
                _free(this.state);
            }

            this = srcWorld;
            // Create new state
            this.state = _make(new State());
            this.state.ptr->CopyFrom(*srcWorld.state.ptr);
            Worlds.AddWorld(ref this);

        }

    }

}