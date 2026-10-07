using System.Linq;

namespace ME.BECS.Views {

    using UnityEngine;

    public class ResetTrailsModule : IViewInitialize, IViewOnValidate, IViewEnableFromPool, IViewApplyState {

        public ParticleSystem[] particleSystems;
        public TrailRenderer[] trailRenderers;

        private bool resetTrails;
        private bool[] trailEmitting;

        public void OnEnableFromPool(in ViewData viewData) {
            this.Reset();
            this.CompleteReset();
        }

        public void OnInitialize() => this.Reset();

        public void Reset() {
            foreach (var ps in this.particleSystems) {
                if (ps == null) continue;
                ps.Pause(withChildren: false);
                ps.Clear(withChildren: false);
            }

            if (this.resetTrails == false) {
                if (this.trailEmitting == null || this.trailEmitting.Length != this.trailRenderers.Length) {
                    this.trailEmitting = new bool[this.trailRenderers.Length];
                }
                for (int i = 0; i < this.trailRenderers.Length; ++i) {
                    var tr = this.trailRenderers[i];
                    if (tr == null) continue;
                    this.trailEmitting[i] = tr.emitting;
                    tr.emitting = false;
                    tr.Clear();
                }
            }
            this.resetTrails = true;
        }

        private void CompleteReset() {
            if (this.resetTrails == false) return;
            this.resetTrails = false;

            foreach (var ps in this.particleSystems) {
                if (ps == null) continue;
                // Restart at the committed spawn pose, before restarting emission.
                // Each selected system is reset independently; do not restart unrelated children.
                ps.Simulate(t: 0f, withChildren: false, restart: true, fixedTimeStep: false);
                ps.Clear(withChildren: false);
                ps.Play(withChildren: false);
            }

            for (int i = 0; i < this.trailRenderers.Length; ++i) {
                var tr = this.trailRenderers[i];
                if (tr == null) continue;
                tr.Clear();
                tr.emitting = this.trailEmitting[i];
            }
        }

        // Retained for serialized ApplyState module indexes and explicit Reset() calls.
        public void ApplyState(in ViewData ent) => this.CompleteReset();

        public void OnValidate(GameObject gameObject) {
            this.particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(true)
                .Where(x => x.trails.enabled == true || x.emission.rateOverDistance.mode != ParticleSystemCurveMode.Constant || x.emission.rateOverDistance.constant > 0f).ToArray();
            this.trailRenderers = gameObject.GetComponentsInChildren<TrailRenderer>(true);
        }

    }

}
