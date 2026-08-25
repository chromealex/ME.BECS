namespace ME.BECS.Units {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    [System.SerializableAttribute]
    public struct Layer {

        public uint value;

    }

    [System.SerializableAttribute]
    public struct LayerMask {

        public uint mask;

        [INLINE(256)]
        public bool Contains(Layer layer) => (this.mask & layer.value) == layer.value;

    }
}