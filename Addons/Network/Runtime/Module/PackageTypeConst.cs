namespace ME.BECS.Network {

    /// <summary>
    /// Defines the byte identifiers used to distinguish network package kinds.
    /// </summary>
    public static class PackageTypeConst {

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public const byte Data = 1;
        /// <summary>
        /// Sync used by <c>PackageTypeConst</c>.
        /// </summary>
        public const byte Sync = 2;
        /// <summary>
        /// Ping used by <c>PackageTypeConst</c>.
        /// </summary>
        public const byte Ping = 3;
        /// <summary>
        /// Patch used by <c>PackageTypeConst</c>.
        /// </summary>
        public const byte Patch = 4;

    }

}
