namespace ME.BECS.Players {

    /// <summary>
    /// Stores per-entity state for team.
    /// </summary>
    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct TeamComponent : IComponent {

        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public uint id;
        /// <summary>
        /// Units tree mask used to select the applicable bits or entries.
        /// </summary>
        public int unitsTreeMask;
        /// <summary>
        /// Units others tree mask used to select the applicable bits or entries.
        /// </summary>
        public int unitsOthersTreeMask;

    }

}