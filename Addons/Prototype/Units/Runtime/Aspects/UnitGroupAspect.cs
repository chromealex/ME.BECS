namespace ME.BECS.Units {

    public struct UnitGroupAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<GroupComponent> groupDataPtr;

        public readonly ref ListAuto<Ent> units => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).units;
        public readonly ref MemArrayAuto<Ent> targets => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).targets;
        public readonly ref float volume => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).volume;

        public readonly void Add(in UnitAspect unit) => Utils.AddToGroup(in this, in unit);

    }

}