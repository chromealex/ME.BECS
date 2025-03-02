using System.Linq;

namespace ME.BECS.Views {

    using UnityEngine;
    
    public class ResetTrailsModule : IViewInitialize, IViewOnValidate, IViewEnableFromPool {

        public ParticleSystem[] particleSystems;
        public TrailRenderer[] trailRenderers;

        public void OnEnableFromPool(in EntRO ent) => this.Reset();

        public void OnInitialize(in EntRO ent) => this.Reset();

        public void Reset() {
            
            foreach (var ps in this.particleSystems) {
                ps.Clear();
            }

            foreach (var tr in this.trailRenderers) {
                tr.Clear();
                tr.time = 0f;
            }

        }

        public void OnValidate(GameObject gameObject) {
            
            this.particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(true).Where(x => x.trails.enabled).ToArray();
            this.trailRenderers = gameObject.GetComponentsInChildren<TrailRenderer>(true);
            
        }

    }

}