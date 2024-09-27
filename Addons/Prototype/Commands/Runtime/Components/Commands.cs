namespace ME.BECS.Commands {

    using Unity.Mathematics;
    
    public struct CommandComponentsGroup {
        
        public static UnityEngine.Color color = UnityEngine.Color.yellow;

    }

    public interface ICommandComponent : IComponent {

        public float3 TargetPosition { get; }

    }

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct BuildInProgress : IComponent {

        public Ent building;

    }

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct BuildingInProgress : IComponent {

        public LockSpinner lockSpinner;
        public float value;
        public float timeToBuild;
        public ListAuto<Ent> builders;
        
    }

}