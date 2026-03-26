using ME.BECS.Transforms;

namespace ME.BECS.Perks {
    
    using ME.BECS.Players;

    public struct PerkAspect : IAspect {
        
        public Ent ent { get; set; }
        
        [QueryWith]
        public AspectDataPtr<PerkComponent> componentPtr;
        [QueryWith]
        public AspectDataPtr<OwnerComponent> ownerComponentPtr;

        public readonly ref PerkComponent component => ref this.componentPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly PerkComponent readComponent => ref this.componentPtr.Read(this.ent.id, this.ent.gen);

        public readonly ref OwnerComponent ownerComponent => ref this.ownerComponentPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly OwnerComponent readOwnerComponent => ref this.ownerComponentPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref Ent owner => ref this.ownerComponent.ent;
        public readonly ref readonly Ent readOwner => ref this.readOwnerComponent.ent;
        
        public readonly PerkType perkType => this.readComponent.slot.Read<PerkSlotComponent>().perkType;

        public readonly void Use() {
            this.ent.SetTag<IsPerkUsedComponent>(true);
            this.Release();
        }

        public readonly bool Release() {
            if (this.perkType == PerkType.Continuous) {
                this.component.slot.Remove<IsPerkCanBeReleased>();
                this.ent.DestroyHierarchy();
                return true;
            }
            return false;
        }

        public readonly PerkAspect Clone() {
            var ent = this.ent.Clone();
            ent.SetActive(true);
            return ent.GetAspect<PerkAspect>();
        }

    }

}