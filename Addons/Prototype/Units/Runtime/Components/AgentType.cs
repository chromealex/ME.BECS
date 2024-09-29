namespace ME.BECS.Units {
    
    using Unity.Mathematics;
    using System.Runtime.InteropServices;

    [System.Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct AgentType {

        [FieldOffset(0)]
        [UnityEngine.HideInInspector]
        public uint typeId;
        [FieldOffset(4)]
        public float radius;
        [FieldOffset(8)]
        public float avoidanceRange;
        [FieldOffset(12)]
        public float maxSlope;
        [FieldOffset(16)]
        public float height;

    }

}