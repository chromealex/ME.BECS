namespace ME.BECS.Players {

    public struct PlayerAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<PlayerComponent> playerDataPtr;

        public uint readIndex => this.playerDataPtr.Read(this.ent.id, this.ent.gen).index;
        public ref uint index => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).index;
        public ref int unitsTreeIndex => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).unitsTreeIndex;
        public ref int unitsOthersTreeMask => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).unitsOthersTreeMask;
        public int unitsTreeMask => 1 << this.unitsTreeIndex;
        public Ent team => this.playerDataPtr.Get(this.ent.id, this.ent.gen).team;
        public ref Ent currentSelection => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).currentSelection;

    }

}