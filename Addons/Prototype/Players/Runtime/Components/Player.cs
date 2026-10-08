namespace ME.BECS.Players {

    /// <summary>
    /// Groups players components for change tracking and queries.
    /// </summary>
    public struct PlayersComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.blue;

    }

    /// <summary>
    /// Stores per-entity state for player.
    /// </summary>
    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct PlayerComponent : IComponent {

        /// <summary>
        /// Index of this entry within its containing storage.
        /// </summary>
        public uint index;
        /// <summary>
        /// Units tree index used to locate the associated entry.
        /// </summary>
        public int unitsTreeIndex;
        /// <summary>
        /// Units others tree mask used to select the applicable bits or entries.
        /// </summary>
        public int unitsOthersTreeMask;
        /// <summary>
        /// Team used by <c>PlayerComponent</c>.
        /// </summary>
        public Ent team;

    }

    /// <summary>
    /// Stores per-entity state for player current selection.
    /// </summary>
    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct PlayerCurrentSelection : IComponent {

        /// <summary>
        /// Current selection used by <c>PlayerCurrentSelection</c>.
        /// </summary>
        public Ent currentSelection;

    }

    /// <summary>
    /// Stores per-entity state for is player defeat tag.
    /// </summary>
    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct IsPlayerDefeatTag : IComponent { }
    /// <summary>
    /// Stores per-entity state for is player victory tag.
    /// </summary>
    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct IsPlayerVictoryTag : IComponent { }

}