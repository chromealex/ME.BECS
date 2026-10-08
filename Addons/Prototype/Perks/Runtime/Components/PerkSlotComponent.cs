namespace ME.BECS.Perks {

    using ME.BECS;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Defines the supported perk type values.
    /// </summary>
    public enum PerkType : byte {
        /// <summary>
        /// Immediately option for <c>PerkType</c>.
        /// </summary>
        Immediately,
        /// <summary>
        /// Continuous option for <c>PerkType</c>.
        /// </summary>
        Continuous,
    }
    
    /// <summary>
    /// Defines configuration-backed entity data for perk slot.
    /// </summary>
    public struct PerkSlotComponent : IConfigComponent {

        /// <summary>
        /// Perk type used by <c>PerkSlotComponent</c>.
        /// </summary>
        public PerkType perkType;
        /// <summary>
        /// Cooldown used by <c>PerkSlotComponent</c>.
        /// </summary>
        public usec cooldown;

    }

    /// <summary>
    /// Stores per-entity state for perk slot runtime.
    /// </summary>
    public struct PerkSlotRuntimeComponent : IComponent {

        /// <summary>
        /// Cooldown used by <c>PerkSlotRuntimeComponent</c>.
        /// </summary>
        public usec cooldown;
        /// <summary>
        /// Perk config used by <c>PerkSlotRuntimeComponent</c>.
        /// </summary>
        public Config perkConfig;
        /// <summary>
        /// Perk source used by <c>PerkSlotRuntimeComponent</c>.
        /// </summary>
        public Ent perkSource;
        /// <summary>
        /// Slot index used to locate the associated entry.
        /// </summary>
        public uint slotIndex;

    }
    
    /// <summary>
    /// Stores per-entity state for is perk slot cooldown ready.
    /// </summary>
    public struct IsPerkSlotCooldownReadyComponent : IComponent { }
    
    /// <summary>
    /// Stores per-entity state for is perk can be released.
    /// </summary>
    public struct IsPerkCanBeReleased : IComponent {

        /// <summary>
        /// Instance associated with this entry.
        /// </summary>
        public Ent instance;

    }

}