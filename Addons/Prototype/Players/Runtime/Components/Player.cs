namespace ME.BECS.Players {

    public struct PlayerComponent : IComponent {

        public uint index;
        public int unitsTreeIndex;
        public int unitsOthersTreeMask;
        public Ent team;

    }

    public struct PlayerCurrentSelection : IComponent {

        public Ent currentSelection;

    }

}