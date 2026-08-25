namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    public interface IObjectReferenceId {
        uint Id { get; set; }
    }
    
    [System.Serializable]
    public struct ObjectReference<T> : IObjectReferenceId where T : UnityEngine.Object {

        public uint id;

        public T Value => ObjectReferenceRegistry.GetObjectBySourceId<T>(this.id);

        [INLINE(256)]
        public static implicit operator T(ObjectReference<T> reference) {
            return reference.Value;
        }

        public uint Id {
            get => this.id;
            set => this.id = value;
        }

        public HeapReference<UnityEngine.Awaitable<T>> LoadAsync() {
            return new HeapReference<UnityEngine.Awaitable<T>>(ObjectReferenceRegistry.LoadAsync<T>(this.id));
        }

    }

}