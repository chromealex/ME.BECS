using System.Linq;

namespace ME.BECS.FogOfWar {

    using Views;
    using Players;

    public class FogOfWarViewModule : IViewApplyState, IViewInitialize, IViewOnValidate {
        
        public UnityEngine.Renderer[] allRenderers;
        public UnityEngine.Renderer[] excludeRenderers;
        private bool isVisible;
        private CreateSystem fow;

        public void OnInitialize(in EntRO ent) {
            
            this.fow = ent.World.GetSystem<CreateSystem>();
            
        }

        public void UpdateVisibility(in EntRO ent) {
            var activePlayer = PlayerUtils.GetActivePlayer();
            this.ApplyVisibility(this.fow.IsVisible(in activePlayer, ent.GetEntity()));
        }

        private void ApplyVisibility(bool state) {
            if (state != this.isVisible) {
                this.isVisible = state;
                foreach (var rnd in this.allRenderers) {
                    rnd.enabled = state;
                }
            }
        }

        public void ApplyState(in EntRO ent) {
            
            this.UpdateVisibility(in ent);
            
        }

        public void OnValidate(UnityEngine.GameObject go) {
            var renderers = go.GetComponentsInChildren<UnityEngine.Renderer>(true).ToList();
            renderers.RemoveAll(x => this.excludeRenderers.Contains(x));
            this.allRenderers = renderers.ToArray();
        }

    }

}