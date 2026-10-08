namespace ME.BECS.Views {

    using UnityEngine;
    
    /// <summary>
    /// Updates view presentation for animator view module.
    /// </summary>
    public class AnimatorViewModule : IViewModule {

        /// <summary>
        /// Animator used to present entity state.
        /// </summary>
        public Animator animator;
        /// <summary>
        /// Points used by the associated geometry or query.
        /// </summary>
        public Transform[] points = System.Array.Empty<Transform>();

    }

}