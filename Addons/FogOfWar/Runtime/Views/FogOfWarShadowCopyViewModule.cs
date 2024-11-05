namespace ME.BECS.FogOfWar {

    using Players;
    using ME.BECS.Transforms;

    public class FogOfWarShadowCopyViewModule : FogOfWarViewModule {
        
        public override void UpdateVisibility(in EntRO ent, bool forced) {
            this.ApplyFowShadowCopyVisibility(in ent, forced);
        }

        protected void ApplyFowShadowCopyVisibility(in EntRO ent, bool forced) {
            var activePlayer = PlayerUtils.GetActivePlayer();
            var objectForTeam = ent.Read<FogOfWarShadowCopyComponent>().forTeam;
            if (activePlayer.readTeam != objectForTeam) {
                this.ApplyVisibility(in ent, false, forced);
                return;
            }
            var position = ent.GetAspect<TransformAspect>().GetWorldMatrixPosition();
            this.ApplyVisibility(in ent, this.fow.IsVisible(in activePlayer, in position) == false && this.fow.IsExplored(in activePlayer, in position) == true, forced);
        }

    }

}