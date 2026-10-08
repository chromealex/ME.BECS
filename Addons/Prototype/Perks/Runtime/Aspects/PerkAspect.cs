using ME.BECS.Transforms;

namespace ME.BECS.Perks {
    
    using ME.BECS.Players;

    /// <summary>
    /// Provides typed access to the entity components used for perk.
    /// </summary>
    public partial struct PerkAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }
        
        /// <summary>
        /// Native pointer or typed storage accessor for component.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<PerkComponent> componentPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for owner component.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<OwnerComponent> ownerComponentPtr;

        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        public readonly ref PerkComponent component => ref this.componentPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component.
        /// </summary>
        public readonly ref readonly PerkComponent readComponent => ref this.componentPtr.Read(this.ent.id, this.ent.gen);

        /// <summary>
        /// Owner component used by <c>PerkAspect</c>.
        /// </summary>
        public readonly ref OwnerComponent ownerComponent => ref this.ownerComponentPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to owner component.
        /// </summary>
        public readonly ref readonly OwnerComponent readOwnerComponent => ref this.ownerComponentPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Owner associated with this entry.
        /// </summary>
        public readonly ref Ent owner => ref this.ownerComponent.ent;
        /// <summary>
        /// Read-only access to owner.
        /// </summary>
        public readonly ref readonly Ent readOwner => ref this.readOwnerComponent.ent;
        
        /// <summary>
        /// Perk type used by <c>PerkAspect</c>.
        /// </summary>
        public readonly PerkType perkType => this.readComponent.slot.Read<PerkSlotComponent>().perkType;

        /// <summary>
        /// Marks the perk as used and releases it when its type is continuous.
        /// </summary>
        public readonly void Use() {
            this.ent.SetTag<IsPerkUsedComponent>(true);
            this.Release();
        }

        /// <summary>
        /// Destroys a continuous perk and removes its slot release marker; returns false for other perk types.
        /// </summary>
        public readonly bool Release() {
            if (this.perkType == PerkType.Continuous) {
                this.component.slot.Remove<IsPerkCanBeReleased>();
                this.ent.DestroyHierarchy();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Creates a copy of the supplied state using the requested allocation context.
        /// </summary>
        public readonly PerkAspect Clone() {
            var ent = this.ent.Clone();
            ent.SetActive(true);
            return ent.GetAspect<PerkAspect>();
        }

    }

}