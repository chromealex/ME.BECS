namespace ME.BECS.Editor {

    using UnityEngine;
    
    public class TempObject : ScriptableObject {

        [SerializeReference]
        [NonReorderable]
        public object[] data;
        public bool[] dataHas;

        [SerializeReference]
        [NonReorderable]
        public object[] dataShared;
        public bool[] dataSharedHas;

    }

}