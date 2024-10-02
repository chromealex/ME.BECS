namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    [System.Serializable]
    public struct ObjectReference<T> where T : UnityEngine.Object {

        public uint id;

        public T Value => ObjectReferenceRegistry.GetObjectBySourceId<T>(this.id);

        [INLINE(256)]
        public static implicit operator T(ObjectReference<T> reference) {
            return reference.Value;
        }

    }

}