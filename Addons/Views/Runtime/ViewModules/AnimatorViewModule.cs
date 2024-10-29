namespace ME.BECS.Views {

    using UnityEngine;
    
    public class AnimatorViewModule : IViewApplyState {

        public Animator animator;
        public Transform[] points = System.Array.Empty<Transform>();

        public void ApplyState(in EntRO ent) {

        }

    }

}