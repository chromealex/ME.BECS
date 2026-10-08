namespace ME.BECS.Perks {
    
    using ME.BECS;
    using ME.BECS.Jobs;
    using BURST = Unity.Burst.BurstCompileAttribute;

    /// <summary>
    /// Defines slot initialization state state and operations.
    /// </summary>
    public ref struct SlotInitializationState {

        /// <summary>
        /// Start cooldown used by <c>SlotInitializationState</c>.
        /// </summary>
        public usec startCooldown;

    }

    /// <summary>
    /// Defines perk source state and operations.
    /// </summary>
    public ref struct PerkSource {

        /// <summary>
        /// Source data or instance used by this operation.
        /// </summary>
        public Ent source;

        /// <summary>
        /// Creates a copy of the supplied state using the requested allocation context.
        /// </summary>
        public readonly Ent Clone() {
            var ent = this.source.Clone();
            ent.SetActive(true);
            ent.SetTag<IsPerkInitializeRequired>(true);
            return ent;
        }

    }
    
    /// <summary>
    /// Coordinates perks during the ECS system lifecycle.
    /// </summary>
    [BURST]
    public partial struct PerksSystem : IUpdate {

        /// <summary>
        /// Executes cooldown work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct CooldownJob : IJobForComponents<PerkSlotRuntimeComponent> {
            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            [InjectDeltaTime]
            public uint dt;
            /// <summary>
            /// Processes cooldown using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref PerkSlotRuntimeComponent perk) {
                perk.cooldown -= this.dt;
                if (perk.cooldown <= 0u) {
                    ent.SetTag<IsPerkSlotCooldownReadyComponent>(true);
                }
            }
        }

        /// <summary>
        /// Updates perks system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {
            context.Query().Without<IsPerkSlotCooldownReadyComponent>().Schedule<CooldownJob, PerkSlotRuntimeComponent>().AddDependency(ref context);
        }

    }

}