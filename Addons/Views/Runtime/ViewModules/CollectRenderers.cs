using System.Linq;

namespace ME.BECS.Views {

    public abstract class CollectRenderers : IViewOnValidate {

        public UnityEngine.Renderer[] allRenderers;
        public UnityEngine.Renderer[] excludeRenderers;
        
        public virtual void OnValidate(UnityEngine.GameObject go) {
            var renderers = go.GetComponentsInChildren<UnityEngine.Renderer>(true).ToList();
            if (this.excludeRenderers != null) renderers.RemoveAll(x => this.excludeRenderers.Contains(x));
            this.allRenderers = renderers.ToArray();
        }

    }

}