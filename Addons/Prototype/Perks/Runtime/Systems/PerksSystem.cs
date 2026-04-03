namespace ME.BECS.Perks {
    
    using ME.BECS;
    using ME.BECS.Jobs;
    using BURST = Unity.Burst.BurstCompileAttribute;

    public ref struct SlotInitializationState {

        public usec startCooldown;

    }

    public ref struct PerkSource {

        public Ent source;

        public readonly Ent Clone() {
            var ent = this.source.Clone();
            ent.SetActive(true);
            ent.SetTag<IsPerkInitializeRequired>(true);
            return ent;
        }

    }
    
    [BURST]
    public struct PerksSystem : IUpdate {

        [BURST]
        public struct CooldownJob : IJobForComponents<PerkSlotRuntimeComponent> {
            [InjectDeltaTime]
            public uint dt;
            public void Execute(in JobInfo jobInfo, in Ent ent, ref PerkSlotRuntimeComponent perk) {
                perk.cooldown -= this.dt;
                if (perk.cooldown <= 0u) {
                    ent.SetTag<IsPerkSlotCooldownReadyComponent>(true);
                }
            }
        }

        public void OnUpdate(ref SystemContext context) {
            context.Query().Without<IsPerkSlotCooldownReadyComponent>().Schedule<CooldownJob, PerkSlotRuntimeComponent>().AddDependency(ref context);
        }

    }

}