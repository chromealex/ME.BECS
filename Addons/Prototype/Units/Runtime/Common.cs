namespace ME.BECS.Units {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Identifies a unit layer used by selection and spatial queries.
    /// </summary>
    [System.SerializableAttribute]
    public struct Layer {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public uint value;

    }

    /// <summary>
    /// Selects unit layers through a bit mask.
    /// </summary>
    [System.SerializableAttribute]
    public struct LayerMask {

        /// <summary>
        /// Mask used by <c>LayerMask</c>.
        /// </summary>
        public uint mask;

        /// <summary>
        /// Tests whether the specified value is present.
        /// </summary>
        [INLINE(256)]
        public bool Contains(Layer layer) => (this.mask & layer.value) == layer.value;

    }
}