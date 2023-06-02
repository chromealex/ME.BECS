namespace ME.BECS {

    internal abstract unsafe class CacheBase {

        public abstract void Apply(State* state, uint entId, ushort entGen);
        public abstract void BuildCache(object component);
        public abstract bool Is<TComponent>() where TComponent : unmanaged;

    }

    internal unsafe class CacheData<TComponent> : CacheBase where TComponent : unmanaged {

        public TComponent data;
        private System.Type type;
        
        public override void Apply(State* state, uint entId, ushort entGen) {
            state->batches.Set(entId, entGen, this.data, state);
        }

        public override void BuildCache(object component) {
            this.data = (TComponent)component;
            this.type = typeof(TComponent);
        }

        public override bool Is<T>() {
            return this.type == typeof(T);
        }

    }

}