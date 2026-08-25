namespace ME.BECS.Perks {

    using ME.BECS;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    public enum PerkType : byte {
        Immediately,
        Continuous,
    }
    
    public struct PerkSlotComponent : IConfigComponent {

        public PerkType perkType;
        public usec cooldown;

    }

    public struct PerkSlotRuntimeComponent : IComponent {

        public usec cooldown;
        public Config perkConfig;
        public Ent perkSource;
        public uint slotIndex;

    }
    
    public struct IsPerkSlotCooldownReadyComponent : IComponent { }
    
    public struct IsPerkCanBeReleased : IComponent {

        public Ent instance;

    }

}