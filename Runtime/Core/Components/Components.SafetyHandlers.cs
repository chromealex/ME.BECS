namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;

    public partial struct Components {

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        private Internal.Array<AtomicSafetyHandle> handlers;
        private LockSpinner handlersLock;
        #endif
        
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        public void DisposeSafetyHandlers() {

            this.handlersLock.Lock();
            if (this.handlers.IsCreated == true) {
                for (uint i = 0u; i < this.handlers.Length; ++i) {
                    ref var handler = ref this.handlers.Get(i);
                    if (AtomicSafetyHandle.IsDefaultValue(handler) == false) AtomicSafetyHandle.Release(handler);
                    handler = default;
                }
                this.handlers.Dispose();
            }
            this.handlersLock.Unlock();
            
        }
        
        public void ResetSafetyHandlers() {

            this.handlersLock.Lock();
            if (this.handlers.IsCreated == true) this.handlers.Dispose();
            this.handlersLock.Unlock();
            
        }
        #endif

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        [INLINE(256)]
        public AtomicSafetyHandle GetSafetyHandler<T>() where T : unmanaged, IComponentBase {

            var counter = StaticTypes.counter;
            if (counter >= this.handlers.Length) {
                this.handlersLock.Lock();
                if (counter >= this.handlers.Length) {
                    this.handlers.Resize(counter + 1u);
                }
                this.handlersLock.Unlock();
            }

            var typeId = StaticTypes<T>.typeId;
            var handler = this.handlers.Get(typeId);
            if (AtomicSafetyHandle.IsDefaultValue(in handler) == true) {
                handler = AtomicSafetyHandle.Create();
                var id = 0;
                Unity.Collections.CollectionHelper.SetStaticSafetyId(ref handler, ref id, StaticTypes<T>.name);
                this.handlers.Get(typeId) = handler;
            }
            
            return this.handlers.Get(typeId);

        }
        #endif

    }

}