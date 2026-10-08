namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Defines the operations required by object reference ID.
    /// </summary>
    public interface IObjectReferenceId {
        /// <summary>
        /// Id used to locate the associated entry.
        /// </summary>
        uint Id { get; set; }
    }
    
    /// <summary>
    /// References a Unity object through the BECS object registry.
    /// </summary>
    [System.Serializable]
    public struct ObjectReference<T> : IObjectReferenceId where T : UnityEngine.Object {

        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public uint id;

        /// <summary>
        /// Value wrapped or resolved by this instance.
        /// </summary>
        public T Value => ObjectReferenceRegistry.GetObjectBySourceId<T>(this.id);

        /// <summary>
        /// Converts the supplied value to <c>T</c>.
        /// </summary>
        [INLINE(256)]
        public static implicit operator T(ObjectReference<T> reference) {
            return reference.Value;
        }

        /// <summary>
        /// Id used to locate the associated entry.
        /// </summary>
        public uint Id {
            get => this.id;
            set => this.id = value;
        }

        /// <summary>
        /// Loads async.
        /// </summary>
        public HeapReference<UnityEngine.Awaitable<T>> LoadAsync() {
            return new HeapReference<UnityEngine.Awaitable<T>>(ObjectReferenceRegistry.LoadAsync<T>(this.id));
        }

    }

}