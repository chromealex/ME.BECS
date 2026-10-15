using UnityEngine;

namespace ME.BECS.Views {

    /// <summary>Explicit support for view modules with independent managed owner state.</summary>
    public interface IBatchedParticleViewModule : IViewModule {
        IBatchedParticleViewModule CreateBatchInstance();
        void PrepareBatch(in ViewData view, float dt, bool newOwner);
    }

    /// <summary>Opt-in authoring for owner-isolated native simulation or entity-driven meshes.</summary>
    [DisallowMultipleComponent]
    public sealed class BatchedParticleView : MonoBehaviour {
        public enum Mode { NativeSimulation, MeshInstance }
        public Mode mode;

        // Authored demo orbit motion used by the blood emission shapes.
        [System.Serializable]
        public struct Orbit {
            public Transform transform;
            public float radius;
            public float speed;
        }
        public Orbit[] orbits = System.Array.Empty<Orbit>();
    }

}
