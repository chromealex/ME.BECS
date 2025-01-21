namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

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