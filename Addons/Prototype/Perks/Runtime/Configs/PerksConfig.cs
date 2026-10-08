namespace ME.BECS.Perks {

    /// <summary>
    /// Defines immutable configuration data for perks config.
    /// </summary>
    public struct PerksConfig : IConfigComponentStatic {

        /// <summary>
        /// Configurations supplying values for this operation.
        /// </summary>
        public MemArrayAuto<Config> configs;

    }

}