namespace ME.BECS.Players {

    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct TeamComponent : IComponent {

        public uint id;
        public int unitsTreeMask;
        public int unitsOthersTreeMask;

    }

}