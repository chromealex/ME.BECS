namespace ME.BECS.Players {

    /// <summary>
    /// Groups unit owner components for change tracking and queries.
    /// </summary>
    public struct UnitOwnerComponentGroup {
        
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.white;
        
    }

    /// <summary>
    /// Stores per-entity state for owner.
    /// </summary>
    [ComponentGroup(typeof(UnitOwnerComponentGroup))]
    public struct OwnerComponent : IComponent {

        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent;

    }

    /// <summary>
    /// Stores per-entity state for owner changed event.
    /// </summary>
    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct OwnerChangedEvent : IComponent {

        /// <summary>
        /// Prev owner used by <c>OwnerChangedEvent</c>.
        /// </summary>
        public Ent prevOwner;

    }

}