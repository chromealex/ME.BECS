namespace ME.BECS.Perks {
    
    using ME.BECS.Transforms;
    using ME.BECS.Players;

    public static class PerksUtils {

        /// <summary>
        /// Add passive perk to owner
        /// This method requires IPerkInitializeComponent::OnInitialize method in your perk component
        /// </summary>
        /// <param name="jobInfo">Context</param>
        /// <param name="owner">Player owner</param>
        /// <param name="perkConfig">Perk config</param>
        /// <returns>Perk instance (not initialized)</returns>
        public static Ent AddPassivePerk(in JobInfo jobInfo, in PlayerAspect owner, Config perkConfig) {
            
            var ent = Ent.New(in jobInfo, "Perk");
            var perk = ent.Set<PerkAspect>();
            perk.owner = owner.ent;
            perkConfig.Apply(in ent);

            if (ent.Has<IsPerkPassiveComponent>() == false) {
                throw new System.Exception();
            }

            ent.SetTag<IsPerkInitializeRequired>(true);

            return ent;

        }

        /// <summary>
        /// Add active slot for perk
        /// </summary>
        /// <param name="jobInfo">Context</param>
        /// <param name="owner">Player owner</param>
        /// <param name="perkConfig">Perk Config</param>
        /// <param name="initializationState">Initialization state</param>
        /// <returns>Slot index</returns>
        public static uint AddSlot(in JobInfo jobInfo, in PlayerAspect owner, Config perkConfig, SlotInitializationState initializationState) {

            var perksEnt = owner.ent;
            ref var perks = ref perksEnt.Get<PerksComponent>();
            if (perks.slots.Count == 0u) perks.slots = new ListAuto<Ent>(perksEnt, 4u);
            
            var slot = Ent.New(jobInfo, "PerkSlot");
            
            var ent = Ent.New(in jobInfo, "Perk");
            ent.SetActive(false);
            var perk = ent.Set<PerkAspect>();
            perk.owner = owner.ent;
            perk.component.slot = slot;
            perkConfig.Apply(in ent);
            
            if (ent.Has<IsPerkActiveComponent>() == false) {
                throw new System.Exception();
            }

            slot.Set(ent.Read<PerkSlotComponent>());
            slot.Set(new PerkSlotRuntimeComponent() {
                cooldown = initializationState.startCooldown,
                perkConfig = perkConfig,
                perkSource = ent,
            });
            if (initializationState.startCooldown == 0u) slot.SetTag<IsPerkSlotCooldownReadyComponent>(true);
            slot.SetParent(perksEnt);
            
            var index = perks.slots.Add(slot);
            return index;
            
        }

        /// <summary>
        /// Returns true if slot is ready to use
        /// </summary>
        /// <param name="owner">Player owner</param>
        /// <param name="index">Slot index</param>
        /// <returns></returns>
        public static bool CanUseSlot(in PlayerAspect owner, uint index) {
            var perksEnt = owner.ent;
            ref readonly var perks = ref perksEnt.Read<PerksComponent>();
            if (index >= perks.slots.Count) {
                return false;
            }
            var slot = perks.slots[index];
            if (slot.Has<IsPerkSlotCooldownReadyComponent>() == false) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Use slot by index
        /// </summary>
        /// <param name="owner">Player owner</param>
        /// <param name="index">Slot index</param>
        /// <returns>PerkSource or default</returns>
        public static PerkSource UseSlot(in PlayerAspect owner, uint index) {
            var perksEnt = owner.ent;
            ref readonly var perks = ref perksEnt.Read<PerksComponent>();
            var slot = perks.slots[index];
            if (slot.Has<IsPerkSlotCooldownReadyComponent>() == false) {
                return default;
            }
            slot.Remove<IsPerkSlotCooldownReadyComponent>();
            slot.Get<PerkSlotRuntimeComponent>().cooldown = slot.Read<PerkSlotComponent>().cooldown;
            var source = slot.Read<PerkSlotRuntimeComponent>().perkSource;
            return new PerkSource() {
                source = source,
            };
        }

        /// <summary>
        /// Use perk source (see UseSlot)
        /// </summary>
        /// <param name="perk">Perk source</param>
        /// <returns>Entity instance or default</returns>
        public static Ent AddPerk(in PerkSource perk) {
            if (perk.source.IsAlive() == false) return default;
            return perk.Clone();
        }
        
    }

}