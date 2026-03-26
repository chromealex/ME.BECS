namespace ME.BECS.Perks {

    using ME.BECS;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

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

    }
    
    public struct IsPerkSlotCooldownReadyComponent : IComponent { }
    
    public struct IsPerkCanBeReleased : IComponent {

        public Ent instance;

    }

}