namespace ME.BECS.FogOfWar {

    /// <summary>
    /// Filters candidates according to the fog of war sub filter condition.
    /// </summary>
    public struct FogOfWarSubFilter : ISubFilter<Ent> {

        /// <summary>
        /// Fog-of-war state used by this operation.
        /// </summary>
        public CreateSystem fow;
        /// <summary>
        /// For team used by <c>FogOfWarSubFilter</c>.
        /// </summary>
        public Ent forTeam;
        
        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        public bool IsValid(in Ent ent, in NativeTrees.AABB2D bounds) {

            if (ent.IsAlive() == false) return false;
            
            var team = ME.BECS.Players.PlayerUtils.GetOwner(in ent).readTeam;
            if (team == this.forTeam) {
                return true;
            }

            return this.fow.IsVisible(in this.forTeam, in ent);

        }

    }

}