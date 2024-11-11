namespace ME.BECS.FogOfWar {

    using Views;
    using Players;
    using Units;

    public class FogOfWarViewModule : CollectRenderers, IViewApplyState, IViewInitialize {
        
        private bool isVisible;
        protected CreateSystem fow;

        public virtual void OnInitialize(in EntRO ent) {
            
            this.fow = ent.World.GetSystem<CreateSystem>();
            this.UpdateVisibility(in ent, true);
            
        }

        public bool IsVisible() => this.isVisible;

        public virtual void OnBecomeVisible(in EntRO ent) {}
        public virtual void OnBecomeInvisible(in EntRO ent) {}

        public virtual void UpdateVisibility(in EntRO ent, bool forced) {
            this.ApplyFowVisibility(in ent, forced);
        }

        protected void ApplyFowVisibility(in EntRO ent, bool forced) {
            this.ApplyVisibility(in ent, this.IsVisible(in ent), forced);
        }

        public Ent GetTeam(in EntRO ent) => PlayerUtils.GetOwner(in ent).readTeam;
        
        public bool IsVisible(in EntRO ent) {
            if (ent.Has<OwnerComponent>() == false) return true;
            // Neutral player always has index 0
            //if (PlayerUtils.GetOwner(in ent).readIndex == 0u) return true;
            var activePlayer = PlayerUtils.GetActivePlayer();
            var isShadowCopy = ent.TryRead(out FogOfWarShadowCopyComponent shadowCopyComponent);
            if (isShadowCopy == true) {
                if (ent.Has<FogOfWarShadowCopyWasVisible>() == false) return false;
                if (shadowCopyComponent.forTeam != activePlayer.team) return false;
            }
            var state = false;
            if (ent.TryRead(out FogOfWarShadowCopyPointsComponent points) == true) {
                state = this.fow.IsVisibleAny(in activePlayer, in points.points);
            } else {
                state = this.fow.IsVisible(in activePlayer, ent.GetEntity());
                if (activePlayer.readTeam == UnitUtils.GetTeam(in ent)) isShadowCopy = false;
            }
            if (isShadowCopy == true) {
                state = !state;
            }

            return state;
        }

        protected virtual void ApplyVisibility(in EntRO ent, bool state, bool forced = false) {
            if (state != this.isVisible || forced == true) {
                this.isVisible = state;
                foreach (var rnd in this.allRenderers) {
                    rnd.enabled = state;
                }
                if (state == true) {
                    this.OnBecomeVisible(in ent);
                } else {
                    this.OnBecomeInvisible(in ent);
                }
            }
        }

        public virtual void ApplyState(in EntRO ent) {
            
            this.UpdateVisibility(in ent, false);
            
        }

    }

}