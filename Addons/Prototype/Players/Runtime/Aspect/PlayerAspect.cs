namespace ME.BECS.Players {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public struct PlayerAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<PlayerComponent> playerDataPtr;
        public AspectDataPtr<PlayerCurrentSelection> playerCurrentSelectionDataPtr;

        public readonly uint readIndex => this.playerDataPtr.Read(this.ent.id, this.ent.gen).index;
        public readonly ref uint index => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).index;
        public readonly ref int unitsTreeIndex => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).unitsTreeIndex;
        public readonly ref int unitsOthersTreeMask => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).unitsOthersTreeMask;
        public readonly ref readonly int readUnitsTreeIndex => ref this.playerDataPtr.Read(this.ent.id, this.ent.gen).unitsTreeIndex;
        public readonly ref readonly int readUnitsOthersTreeMask => ref this.playerDataPtr.Read(this.ent.id, this.ent.gen).unitsOthersTreeMask;
        public readonly int unitsTreeMask => 1 << this.readUnitsTreeIndex;
        
        public readonly ref Ent team => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).team;
        public readonly Ent readTeam => this.playerDataPtr.Read(this.ent.id, this.ent.gen).team;
        
        public readonly ref Ent currentSelection => ref this.playerCurrentSelectionDataPtr.Get(this.ent.id, this.ent.gen).currentSelection;
        public readonly ref readonly Ent readCurrentSelection => ref this.playerCurrentSelectionDataPtr.Read(this.ent.id, this.ent.gen).currentSelection;

        [INLINE(256)]
        public void SetDefeat() => this.ent.SetTag<IsPlayerDefeatTag>(true);

        [INLINE(256)]
        public void SetVictory() => this.ent.SetTag<IsPlayerVictoryTag>(true);

    }

}