namespace ME.BECS.Effects {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Provides typed access to the entity components used for effect.
    /// </summary>
    public partial struct EffectAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for effect.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<EffectComponent> effectDataPtr;
        
    }

}