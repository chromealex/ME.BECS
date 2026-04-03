using System.Linq;

namespace ME.BECS.Views {

    using UnityEngine;
    
    public class ResetTrailsModule : IViewInitialize, IViewOnValidate, IViewEnableFromPool, IViewApplyState {

        public ParticleSystem[] particleSystems;
        public TrailRenderer[] trailRenderers;

        private bool resetTrails;

        public void OnEnableFromPool(in ViewData viewData) => this.Reset();

        public void OnInitialize(in ViewData viewData) => this.Reset();

        public void Reset() {
            
            foreach (var ps in this.particleSystems) {
                ps.Clear();
                ps.Pause();
                ps.time = 0f;
            }

            foreach (var tr in this.trailRenderers) {
                tr.Clear();
                tr.time = 0f;
            }

            this.resetTrails = true;

        }

        public void ApplyState(in ViewData ent) {

            if (this.resetTrails == true) {

                this.resetTrails = false;
                
                foreach (var ps in this.particleSystems) {
                    ps.Clear();
                    ps.time = 0f;
                    ps.Play();
                }

                foreach (var tr in this.trailRenderers) {
                    tr.Clear();
                    tr.time = 0f;
                }
                
            }

        }

        public void OnValidate(GameObject gameObject) {
            
            this.particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(true).Where(x => x.trails.enabled || x.emission.rateOverDistance.constant > 0f).ToArray();
            this.trailRenderers = gameObject.GetComponentsInChildren<TrailRenderer>(true);
            
        }

    }

}