namespace ME.BECS.Players {

    /// <summary>
    /// Provides typed access to the entity components used for team.
    /// </summary>
    public partial struct TeamAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for team.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<TeamComponent> teamDataPtr;

        /// <summary>
        /// Team id used to locate the associated entry.
        /// </summary>
        public ref uint teamId => ref this.teamDataPtr.GetOrThrow(this.ent.id, this.ent.gen).id;
        /// <summary>
        /// Units tree mask used to select the applicable bits or entries.
        /// </summary>
        public ref int unitsTreeMask => ref this.teamDataPtr.GetOrThrow(this.ent.id, this.ent.gen).unitsTreeMask;
        /// <summary>
        /// Units others tree mask used to select the applicable bits or entries.
        /// </summary>
        public ref int unitsOthersTreeMask => ref this.teamDataPtr.GetOrThrow(this.ent.id, this.ent.gen).unitsOthersTreeMask;
        /// <summary>
        /// Read-only access to team ID.
        /// </summary>
        public ref readonly uint readTeamId => ref this.teamDataPtr.Read(this.ent.id, this.ent.gen).id;
        /// <summary>
        /// Read-only access to units tree mask.
        /// </summary>
        public ref readonly int readUnitsTreeMask => ref this.teamDataPtr.Read(this.ent.id, this.ent.gen).unitsTreeMask;
        /// <summary>
        /// Read-only access to units others tree mask.
        /// </summary>
        public ref readonly int readUnitsOthersTreeMask => ref this.teamDataPtr.Read(this.ent.id, this.ent.gen).unitsOthersTreeMask;

    }

}