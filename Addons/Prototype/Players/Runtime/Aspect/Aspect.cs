namespace ME.BECS.Players {

    public struct PlayerAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<PlayerComponent> playerDataPtr;

        public readonly uint readIndex => this.playerDataPtr.Read(this.ent.id, this.ent.gen).index;
        public readonly ref uint index => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).index;
        public readonly ref int unitsTreeIndex => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).unitsTreeIndex;
        public readonly ref int unitsOthersTreeMask => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).unitsOthersTreeMask;
        public readonly int unitsTreeMask => 1 << this.unitsTreeIndex;
        public readonly Ent team => this.playerDataPtr.Get(this.ent.id, this.ent.gen).team;
        public readonly ref Ent currentSelection => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).currentSelection;
        
        public readonly Ent readTeam => this.playerDataPtr.Read(this.ent.id, this.ent.gen).team;
        public readonly ref readonly Ent readCurrentSelection => ref this.playerDataPtr.Read(this.ent.id, this.ent.gen).currentSelection;

    }

}