namespace ME.BECS.FogOfWar {

    using Players;
    using ME.BECS.Transforms;

    public class FogOfWarShadowCopyViewModule : FogOfWarViewModule {
        
        public override void UpdateVisibility(in EntRO ent, bool forced) {
            this.ApplyFowShadowCopyVisibility(in ent, forced);
        }

        public Ent GetShadowCopyTeam(in EntRO ent) => ent.Read<FogOfWarShadowCopyComponent>().forTeam;

        protected void ApplyFowShadowCopyVisibility(in EntRO ent, bool forced) {
            var activePlayer = PlayerUtils.GetActivePlayer();

            var objectForTeam = this.GetShadowCopyTeam(in ent);
            if (activePlayer.readTeam != objectForTeam) {
                this.ApplyVisibility(in ent, false, forced);
                return;
            }
            
            if (ent.TryRead(out FogOfWarShadowCopyPointsComponent points) == true) {
                this.ApplyVisibility(in ent, this.fow.IsVisibleAny(in activePlayer, in points.points) == false && this.fow.IsExploredAny(in activePlayer, in points.points) == true, forced);
            } else {
                var position = ent.GetAspect<TransformAspect>().GetWorldMatrixPosition();
                this.ApplyVisibility(in ent, this.fow.IsVisible(in activePlayer, in position) == false && this.fow.IsExplored(in activePlayer, in position) == true, forced);
            }
        }

    }

}