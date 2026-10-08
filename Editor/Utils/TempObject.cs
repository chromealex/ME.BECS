namespace ME.BECS.Editor {

    using UnityEngine;
    
    /// <summary>
    /// Retains a temporary Unity object for the scope managed by this wrapper.
    /// </summary>
    public class TempObject : ScriptableObject {

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        [SerializeReference]
        [NonReorderable]
        public object[] data;
        /// <summary>
        /// Data has used by <c>TempObject</c>.
        /// </summary>
        public bool[] dataHas;

        /// <summary>
        /// Data shared used by <c>TempObject</c>.
        /// </summary>
        [SerializeReference]
        [NonReorderable]
        public object[] dataShared;
        /// <summary>
        /// Data shared has used by <c>TempObject</c>.
        /// </summary>
        public bool[] dataSharedHas;

    }

}