namespace ME.BECS.Perks {

    using ME.BECS;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public struct PerkSlotComponent : IConfigComponent {

        public usec cooldown;

    }

    public struct PerkSlotRuntimeComponent : IComponent {

        public usec cooldown;
        public Config perkConfig;
        public Ent perkSource;

    }
    
    public struct IsPerkSlotCooldownReadyComponent : IComponent { }

}