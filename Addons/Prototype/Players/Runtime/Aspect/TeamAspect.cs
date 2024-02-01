namespace ME.BECS.Players {

    public struct TeamAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<TeamComponent> teamDataPtr;

        public ref uint teamId => ref this.teamDataPtr.Get(this.ent.id, this.ent.gen).id;
        public ref int unitsTreeMask => ref this.teamDataPtr.Get(this.ent.id, this.ent.gen).unitsTreeMask;
        public ref int unitsOthersTreeMask => ref this.teamDataPtr.Get(this.ent.id, this.ent.gen).unitsOthersTreeMask;

    }

}