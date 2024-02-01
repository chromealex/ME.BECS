namespace ME.BECS {

    internal abstract unsafe class CacheBase {

        public abstract void Apply(State* state, in Ent ent);
        public abstract void BuildCache(object component);
        public abstract bool Is<TComponent>() where TComponent : unmanaged;

    }

    internal unsafe class CacheData<TComponent> : CacheBase where TComponent : unmanaged, IComponent {

        public TComponent data;
        private System.Type type;
        
        public override void Apply(State* state, in Ent ent) {
            state->batches.Set(in ent, this.data, state);
        }

        public override void BuildCache(object component) {
            this.data = (TComponent)component;
            this.type = typeof(TComponent);
        }

        public override bool Is<T>() {
            return this.type == typeof(T);
        }

    }

    internal unsafe class CacheSharedData<TComponent> : CacheBase where TComponent : unmanaged, IComponentShared {

        public TComponent data;
        private System.Type type;
        
        public override void Apply(State* state, in Ent ent) {
            state->batches.SetShared<TComponent>(in ent, this.data, state);
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