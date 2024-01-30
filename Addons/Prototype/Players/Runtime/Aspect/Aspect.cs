namespace ME.BECS.Players {

    public struct PlayerAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<PlayerComponent> playerDataPtr;
        [QueryWith]
        public AspectDataPtr<TeamComponent> teamDataPtr;

        public uint readIndex => this.playerDataPtr.Read(this.ent.id, this.ent.gen).index;
        public ref uint index => ref this.playerDataPtr.Get(this.ent.id, this.ent.gen).index;
        public ref uint teamId => ref this.teamDataPtr.Get(this.ent.id, this.ent.gen).id;

    }

}