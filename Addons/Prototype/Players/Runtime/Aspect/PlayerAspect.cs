namespace ME.BECS.Players {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    /// <summary>
    /// Provides typed access to the entity components used for player.
    /// </summary>
    public partial struct PlayerAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for player.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<PlayerComponent> playerDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for player current selection.
        /// </summary>
        public AspectDataPtr<PlayerCurrentSelection> playerCurrentSelectionDataPtr;

        /// <summary>
        /// Read-only access to index.
        /// </summary>
        public readonly uint readIndex => this.playerDataPtr.Read(this.ent.id, this.ent.gen).index;
        /// <summary>
        /// Index of this entry within its containing storage.
        /// </summary>
        public readonly ref uint index => ref this.playerDataPtr.GetOrThrow(this.ent.id, this.ent.gen).index;
        /// <summary>
        /// Units tree index used to locate the associated entry.
        /// </summary>
        public readonly ref int unitsTreeIndex => ref this.playerDataPtr.GetOrThrow(this.ent.id, this.ent.gen).unitsTreeIndex;
        /// <summary>
        /// Units others tree mask used to select the applicable bits or entries.
        /// </summary>
        public readonly ref int unitsOthersTreeMask => ref this.playerDataPtr.GetOrThrow(this.ent.id, this.ent.gen).unitsOthersTreeMask;
        /// <summary>
        /// Read-only access to units tree index.
        /// </summary>
        public readonly ref readonly int readUnitsTreeIndex => ref this.playerDataPtr.Read(this.ent.id, this.ent.gen).unitsTreeIndex;
        /// <summary>
        /// Read-only access to units others tree mask.
        /// </summary>
        public readonly ref readonly int readUnitsOthersTreeMask => ref this.playerDataPtr.Read(this.ent.id, this.ent.gen).unitsOthersTreeMask;
        /// <summary>
        /// Units tree mask used to select the applicable bits or entries.
        /// </summary>
        public readonly int unitsTreeMask => 1 << this.readUnitsTreeIndex;
        
        /// <summary>
        /// Team used by <c>PlayerAspect</c>.
        /// </summary>
        public readonly ref Ent team => ref this.playerDataPtr.GetOrThrow(this.ent.id, this.ent.gen).team;
        /// <summary>
        /// Read-only access to team.
        /// </summary>
        public readonly Ent readTeam => this.playerDataPtr.Read(this.ent.id, this.ent.gen).team;
        
        /// <summary>
        /// Current selection used by <c>PlayerAspect</c>.
        /// </summary>
        public readonly ref Ent currentSelection => ref this.playerCurrentSelectionDataPtr.Get(this.ent.id, this.ent.gen).currentSelection;
        /// <summary>
        /// Read-only access to current selection.
        /// </summary>
        public readonly ref readonly Ent readCurrentSelection => ref this.playerCurrentSelectionDataPtr.Read(this.ent.id, this.ent.gen).currentSelection;

        /// <summary>
        /// Sets defeat.
        /// </summary>
        [INLINE(256)]
        public void SetDefeat() => this.ent.SetTag<IsPlayerDefeatTag>(true);

        /// <summary>
        /// Sets victory.
        /// </summary>
        [INLINE(256)]
        public void SetVictory() => this.ent.SetTag<IsPlayerVictoryTag>(true);

    }

}