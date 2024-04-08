namespace ME.BECS.Commands {
    
    using Unity.Mathematics;

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct CommandBuild : ICommandComponent {

        public float3 TargetPosition => this.snappedPosition;

        public float3 snappedPosition;
        public quaternion rotation;
        public uint2 size;
        public float height;
        public uint buildingTypeId;
        public float timeToBuild;
        public Ent owner;
        public Ent building;

    }

}