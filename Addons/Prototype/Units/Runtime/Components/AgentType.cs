namespace ME.BECS.Units {

    [System.Serializable]
    public struct AgentType {

        [UnityEngine.HideInInspector]
        public uint typeId;
        
        public float radius;
        public float avoidanceRange;
        public float maxSlope;
        
    }

}