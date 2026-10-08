using System.Linq;

namespace ME.BECS.Views {

    /// <summary>
    /// Updates view presentation for collect renderers.
    /// </summary>
    public abstract class CollectRenderers : IViewOnValidate {

        /// <summary>
        /// All renderers used by <c>CollectRenderers</c>.
        /// </summary>
        public UnityEngine.Renderer[] allRenderers;
        /// <summary>
        /// Exclude renderers used by <c>CollectRenderers</c>.
        /// </summary>
        public UnityEngine.Renderer[] excludeRenderers;
        
        /// <summary>
        /// Refreshes or validates state after values change in the Unity Inspector.
        /// </summary>
        public virtual void OnValidate(UnityEngine.GameObject go) {
            var renderers = go.GetComponentsInChildren<UnityEngine.Renderer>(true).ToList();
            if (this.excludeRenderers != null) renderers.RemoveAll(x => this.excludeRenderers.Contains(x));
            this.allRenderers = renderers.ToArray();
        }

    }

}