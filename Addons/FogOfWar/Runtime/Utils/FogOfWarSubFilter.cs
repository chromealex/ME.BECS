namespace ME.BECS.FogOfWar {

    public struct FogOfWarSubFilter : ISubFilter<Ent> {

        public CreateSystem fow;
        public Ent forTeam;
        
        public bool IsValid(in Ent ent, in NativeTrees.AABB bounds) {

            if (ent.IsAlive() == false) return false;
            
            var team = ME.BECS.Players.PlayerUtils.GetOwner(in ent).readTeam;
            if (team == this.forTeam) {
                return true;
            }

            return this.fow.IsVisible(in this.forTeam, in ent);

        }

    }

}