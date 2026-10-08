namespace ME.BECS {

    /// <summary>
    /// Defines config list state and operations.
    /// </summary>
    [System.Serializable]
    public struct ConfigList {

        /// <summary>
        /// Type descriptor used by the associated operation.
        /// </summary>
        public System.Type type;
        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public uint id;

    }

}