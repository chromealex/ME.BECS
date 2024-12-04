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
        [INLINE(256)]
        public AtomicSafetyHandle GetSafetyHandler<T>() where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            if (typeId >= this.handlers.Length) {
                this.handlersLock.Lock();
                if (typeId >= this.handlers.Length) {
                    this.handlers.Resize(typeId + 1u);
                }
                this.handlersLock.Unlock();
            }

            var handler = this.handlers.Get(typeId);
            if (AtomicSafetyHandle.IsDefaultValue(in handler) == true) {
                handler = AtomicSafetyHandle.Create();
                var id = 0;
                Unity.Collections.CollectionHelper.SetStaticSafetyId(ref handler, ref id, StaticTypes<T>.name);
                this.handlers.Get(typeId) = handler;
            }
            
            return this.handlers.Get(typeId);

        }
        
        [INLINE(256)]
        public AtomicSafetyHandle GetSafetyHandler(uint typeId) {

            if (typeId >= this.handlers.Length) {
                this.handlersLock.Lock();
                if (typeId >= this.handlers.Length) {
                    this.handlers.Resize(typeId + 1u);
                }
                this.handlersLock.Unlock();
            }

            var handler = this.handlers.Get(typeId);
            if (AtomicSafetyHandle.IsDefaultValue(in handler) == true) {
                handler = AtomicSafetyHandle.Create();
                //AtomicSafetyHandle.SetStaticSafetyId(ref handler, AtomicSafetyHandle.NewStaticSafetyId(&nameBytes, nameBytes.Length));
                this.handlers.Get(typeId) = handler;
            }
            
            return this.handlers.Get(typeId);

        }
        #endif

    }

}