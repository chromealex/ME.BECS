using ME.BECS.Transforms;

namespace ME.BECS.Perks {

    using ME.BECS;
    using ME.BECS.Players;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Stores per-entity state for perks.
    /// </summary>
    public struct PerksComponent : IComponent {

        /// <summary>
        /// Slots used by <c>PerksComponent</c>.
        /// </summary>
        public ListAuto<Ent> slots;
        
    }

    /// <summary>
    /// Stores per-entity state for is perk active.
    /// </summary>
    public struct IsPerkActiveComponent : IComponent {}
    /// <summary>
    /// Stores per-entity state for is perk passive.
    /// </summary>
    public struct IsPerkPassiveComponent : IComponent {}
    
    /// <summary>
    /// Defines configuration-backed entity data for i perk initialize.
    /// </summary>
    public interface IPerkInitializeComponent : IConfigComponent {

        /// <summary>
        /// Initializes i perk initialize component state from the supplied context.
        /// </summary>
        [INLINE(256)]
        void OnInitialize(in JobInfo jobInfo, in PlayerAspect owner, in PerkAspect perk);

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for i perk.
    /// </summary>
    public interface IPerkComponent : IConfigComponent {

        /// <summary>
        /// Executes the configured work using the supplied inputs.
        /// </summary>
        [INLINE(256)]
        void Run(in JobInfo jobInfo, in PerkAspect perk, in Ent target, uint dt);

    }

    /// <summary>
    /// Defines the operations required by perk parallel component.
    /// </summary>
    public interface IPerkParallelComponent : IPerkComponent {

    }
    
    /// <summary>
    /// Stores per-entity state for is perk initialize required.
    /// </summary>
    public struct IsPerkInitializeRequired : IComponent { }

    /// <summary>
    /// Stores per-entity state for perk.
    /// </summary>
    public struct PerkComponent : IComponent {

        /// <summary>
        /// Slot used by <c>PerkComponent</c>.
        /// </summary>
        public Ent slot;

    }

    /// <summary>
    /// Stores per-entity state for is perk used.
    /// </summary>
    public struct IsPerkUsedComponent : IComponent { }

    /// <summary>
    /// Defines immutable configuration data for activate passive perks.
    /// </summary>
    public struct ActivatePassivePerksComponent : IConfigComponentStatic, IConfigInitialize {

        /// <summary>
        /// Perks used by <c>ActivatePassivePerksComponent</c>.
        /// </summary>
        public MemArrayAuto<Config> perks;

        /// <summary>
        /// Initializes activate passive perks component state from the supplied context.
        /// </summary>
        public void OnInitialize(in Ent ent) {

            var owner = ent.Read<OwnerComponent>().ent;
            if (owner.IsAlive() == true) {
                for (uint i = 0u; i < this.perks.Length; ++i) {
                    var config = this.perks[i];
                    var perk = PerksUtils.AddPassivePerk(JobInfo.Create(ent.worldId), owner.GetAspect<PlayerAspect>(), config);
                    perk.SetParent(ent);
                }
            }

        }

    }

    /// <summary>
    /// Defines default parallel perk component data used by entity processing.
    /// </summary>
    public struct DefaultParallelPerkComponent : IPerkParallelComponent {
        
        /// <summary>
        /// Executes the configured work using the supplied inputs.
        /// </summary>
        public void Run(in JobInfo jobInfo, in PerkAspect perk, in Ent target, uint dt) {
            throw new System.NotImplementedException();
        }

    }

    /// <summary>
    /// Defines default perk component data used by entity processing.
    /// </summary>
    public struct DefaultPerkComponent : IPerkComponent {
        
        /// <summary>
        /// Executes the configured work using the supplied inputs.
        /// </summary>
        public void Run(in JobInfo jobInfo, in PerkAspect perk, in Ent target, uint dt) {
            throw new System.NotImplementedException();
        }

    }

    /// <summary>
    /// Defines default perk initializer component data used by entity processing.
    /// </summary>
    public struct DefaultPerkInitializerComponent : IPerkInitializeComponent {
        
        /// <summary>
        /// Initializes default perk initializer component state from the supplied context.
        /// </summary>
        public void OnInitialize(in JobInfo jobInfo, in PlayerAspect owner, in PerkAspect perk) {
            throw new System.NotImplementedException();
        }

    }

}