using System.Linq;

namespace ME.BECS.FogOfWar {

    using Views;
    using Players;

    public class FogOfWarViewModule : IViewApplyState, IViewInitialize, IViewOnValidate {
        
        public UnityEngine.Renderer[] allRenderers;
        public UnityEngine.Renderer[] excludeRenderers;
        private bool isVisible;
        protected CreateSystem fow;

        public virtual void OnInitialize(in EntRO ent) {
            
            this.fow = ent.World.GetSystem<CreateSystem>();
            this.UpdateVisibility(in ent, true);
            
        }

        public virtual void OnBecomeVisible(in EntRO ent) {}
        public virtual void OnBecomeInvisible(in EntRO ent) {}

        public virtual void UpdateVisibility(in EntRO ent, bool forced) {
            this.ApplyFowVisibility(in ent, forced);
        }

        protected void ApplyFowVisibility(in EntRO ent, bool forced) {
            var activePlayer = PlayerUtils.GetActivePlayer();
            this.ApplyVisibility(in ent, this.fow.IsVisible(in activePlayer, ent.GetEntity()), forced);
        }

        protected void ApplyVisibility(in EntRO ent, bool state, bool forced = false) {
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

        public virtual void OnValidate(UnityEngine.GameObject go) {
            var renderers = go.GetComponentsInChildren<UnityEngine.Renderer>(true).ToList();
            renderers.RemoveAll(x => this.excludeRenderers.Contains(x));
            this.allRenderers = renderers.ToArray();
        }

    }

}