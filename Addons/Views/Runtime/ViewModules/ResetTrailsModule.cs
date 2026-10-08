using System.Linq;

namespace ME.BECS.Views {

    using UnityEngine;

    /// <summary>
    /// Updates view presentation for reset trails module.
    /// </summary>
    public class ResetTrailsModule : IViewInitialize, IViewOnValidate, IViewEnableFromPool, IViewApplyState {

        /// <summary>
        /// Particle systems used by <c>ResetTrailsModule</c>.
        /// </summary>
        public ParticleSystem[] particleSystems;
        /// <summary>
        /// Trail renderers used by <c>ResetTrailsModule</c>.
        /// </summary>
        public TrailRenderer[] trailRenderers;

        private bool resetTrails;
        private bool[] trailEmitting;

        /// <summary>
        /// Activates presentation state when the view is taken from the pool.
        /// </summary>
        public void OnEnableFromPool(in ViewData viewData) {
            this.Reset();
            this.CompleteReset();
        }

        /// <summary>
        /// Initializes reset trails module state from the supplied context.
        /// </summary>
        public void OnInitialize() => this.Reset();

        /// <summary>
        /// Restores the tracked state to its initial values.
        /// </summary>
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
        /// <summary>
        /// Applies the current logic state to the presentation instance.
        /// </summary>
        public void ApplyState(in ViewData ent) => this.CompleteReset();

        /// <summary>
        /// Refreshes or validates state after values change in the Unity Inspector.
        /// </summary>
        public void OnValidate(GameObject gameObject) {
            this.particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(true)
                .Where(x => x.trails.enabled == true || x.emission.rateOverDistance.mode != ParticleSystemCurveMode.Constant || x.emission.rateOverDistance.constant > 0f).ToArray();
            this.trailRenderers = gameObject.GetComponentsInChildren<TrailRenderer>(true);
        }

    }

}
