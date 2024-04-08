namespace ME.BECS.Commands {
    
    using Unity.Mathematics;

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct CommandMove : ICommandComponent {

        public float3 TargetPosition => this.targetPosition;

        // Move params
        public float3 targetPosition;

    }

}