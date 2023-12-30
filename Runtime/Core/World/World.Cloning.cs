namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct World {
        
        [INLINE(256)]
        public World Clone() {
            E.IS_CREATED(this);
            World newWorld = default;
            newWorld.CopyFrom(this);
            return newWorld;
        }

        [INLINE(256)]
        public void CopyFrom(in World srcWorld) {
            
            E.IS_CREATED(srcWorld);

            Batches.Apply(srcWorld.state);

            // Dispose current state
            if (this.state != null) {
                Worlds.ReleaseWorld(this);
                this.state->Dispose();
                _free(this.state);
            }

            this = srcWorld;
            // Create new state
            this.state = _make(new State());
            this.state->CopyFrom(*srcWorld.state);
            Worlds.AddWorld(ref this);

        }

    }

}