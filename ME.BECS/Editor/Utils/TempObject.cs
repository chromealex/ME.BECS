namespace ME.BECS.Editor {

    using UnityEngine;
    
    public class TempObject : ScriptableObject {

        [SerializeReference]
        [NonReorderable]
        public object[] data;

        [SerializeReference]
        [NonReorderable]
        public object[] dataShared;

    }

}