namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;

    public partial struct Components {

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal Internal.Array<AtomicSafetyHandle> handlers;
        internal LockSpinner handlersLock;
        
        [INLINE(256)]
        public unsafe void DisposeSafetyHandlers() {

            if (this.handlers.ptr.ptr != null) {
                this.handlersLock.Lock();
                if (this.handlers.ptr.ptr != null) {
                    for (uint i = 0u; i < this.handlers.Length; ++i) {
                        ref var handler = ref this.handlers.Get(i);
                        if (AtomicSafetyHandle.IsDefaultValue(handler) == false) AtomicSafetyHandle.Release(handler);
                        handler = default;
                    }
                    this.handlers.Dispose();
                }
                this.handlersLock.Unlock();
            }

        }
        
        [INLINE(256)]
        public void SafetyHandlersCopyFrom(in Components other) {

            this.DisposeSafetyHandlers();
            
        }
        
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
                this.handlersLock.Lock();
                handler = this.handlers.Get(typeId);
                if (AtomicSafetyHandle.IsDefaultValue(in handler) == true) {
                    handler = AtomicSafetyHandle.Create();
                    var id = 0;
                    Unity.Collections.CollectionHelper.SetStaticSafetyId(ref handler, ref id, StaticTypes<T>.name);
                    this.handlers.Get(typeId) = handler;
                }
                this.handlersLock.Unlock();
            }
            
            return this.handlers.Get(typeId);

        }
        #endif

    }

}