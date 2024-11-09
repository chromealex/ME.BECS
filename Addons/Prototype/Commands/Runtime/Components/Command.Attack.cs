namespace ME.BECS.Commands {
    
    using Unity.Mathematics;
    using ME.BECS.Transforms;

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct CommandAttack : ICommandComponent {

        public float3 TargetPosition => this.target.GetAspect<TransformAspect>().GetWorldMatrixPosition();

        public Ent target;

    }

    [ComponentGroup(typeof(CommandComponentsGroup))]
    public struct UnitAttackCommandComponent : IComponent {

        public Ent target;

    }

}