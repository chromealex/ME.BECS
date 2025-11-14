namespace ME.BECS.Players {

    public struct TeamAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<TeamComponent> teamDataPtr;

        public ref uint teamId => ref this.teamDataPtr.GetOrThrow(this.ent.id, this.ent.gen).id;
        public ref int unitsTreeMask => ref this.teamDataPtr.GetOrThrow(this.ent.id, this.ent.gen).unitsTreeMask;
        public ref int unitsOthersTreeMask => ref this.teamDataPtr.GetOrThrow(this.ent.id, this.ent.gen).unitsOthersTreeMask;
        public ref readonly uint readTeamId => ref this.teamDataPtr.Read(this.ent.id, this.ent.gen).id;
        public ref readonly int readUnitsTreeMask => ref this.teamDataPtr.Read(this.ent.id, this.ent.gen).unitsTreeMask;
        public ref readonly int readUnitsOthersTreeMask => ref this.teamDataPtr.Read(this.ent.id, this.ent.gen).unitsOthersTreeMask;

    }

}