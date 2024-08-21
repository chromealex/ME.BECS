namespace ME.BECS.Players {

    public struct UnitOwnerComponentGroup { }

    [ComponentGroup(typeof(UnitOwnerComponentGroup))]
    public struct OwnerComponent : IComponent {

        public Ent ent;

    }

}