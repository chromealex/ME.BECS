namespace ME.BECS.Units {
    
    using Unity.Mathematics;
    using System.Runtime.InteropServices;

    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AgentType {

        [UnityEngine.HideInInspector]
        public uint typeId;
        public float radius;
        public float avoidanceRange;
        public float maxSlope;
        public float height;

    }

}