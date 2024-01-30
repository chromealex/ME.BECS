namespace ME.BECS.Pathfinding {

    using Unity.Mathematics;
    
    public struct GraphMaskComponent : IConfigComponent {

        public float2 offset;
        public float2 size;
        public byte cost;
        public float height;
        public bool isDirty;

    }

}