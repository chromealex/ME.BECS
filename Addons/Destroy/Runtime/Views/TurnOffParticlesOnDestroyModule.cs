using System.Linq;

namespace ME.BECS.Views {

    using UnityEngine;
    
    public class TurnOffParticlesOnDestroyModule : IViewApplyState, IViewOnValidate {

        public ParticleSystem[] particleSystems;

        public void ApplyState(in EntRO ent) {

            if (ent.HasDestroyLifetime() == true) {
                foreach (var ps in this.particleSystems) {
                    ps.Stop();
                }
            }
            
        }

        public void OnValidate(GameObject gameObject) {
            
            this.particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(true).ToArray();
            
        }

    }

}