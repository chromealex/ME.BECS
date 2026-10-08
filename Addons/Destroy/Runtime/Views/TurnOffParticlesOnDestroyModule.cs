using System.Linq;

namespace ME.BECS.Views {

    using UnityEngine;
    
    /// <summary>
    /// Updates view presentation for turn off particles on destroy module.
    /// </summary>
    public class TurnOffParticlesOnDestroyModule : IViewApplyState, IViewOnValidate {

        /// <summary>
        /// Particle systems used by <c>TurnOffParticlesOnDestroyModule</c>.
        /// </summary>
        public ParticleSystem[] particleSystems;

        /// <summary>
        /// Applies the current logic state to the presentation instance.
        /// </summary>
        public void ApplyState(in ViewData viewData) {

            EntRO ent = viewData;
            if (ent.HasDestroyLifetime() == true) {
                foreach (var ps in this.particleSystems) {
                    ps.Stop();
                }
            }
            
        }

        /// <summary>
        /// Refreshes or validates state after values change in the Unity Inspector.
        /// </summary>
        public void OnValidate(GameObject gameObject) {
            
            this.particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(true).ToArray();
            
        }

    }

}