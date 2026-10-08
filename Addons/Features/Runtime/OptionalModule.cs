namespace ME.BECS {

    /// <summary>
    /// Provides lifecycle integration for the optional feature.
    /// </summary>
    [System.Serializable]
    public class OptionalModule {

        /// <summary>
        /// Whether enabled behavior or state is selected.
        /// </summary>
        public bool enabled;
        /// <summary>
        /// Object represented by this entry.
        /// </summary>
        public Module obj;
        
        /// <summary>
        /// Tests whether the context is enabled.
        /// </summary>
        public bool IsEnabled() => this.enabled == true && this.obj != null;

    }
    
    /// <summary>
    /// Supplies optional graph metadata to annotated declarations.
    /// </summary>
    public class OptionalGraphAttribute : System.Attribute {}

}