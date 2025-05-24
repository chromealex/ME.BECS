namespace ME.BECS.Players {

    public struct PlayersComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.blue;

    }

    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct PlayerComponent : IComponent {

        public uint index;
        public int unitsTreeIndex;
        public int unitsOthersTreeMask;
        public Ent team;

    }

    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct PlayerCurrentSelection : IComponent {

        public Ent currentSelection;

    }

    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct IsPlayerDefeatTag : IComponent { }
    [ComponentGroup(typeof(PlayersComponentGroup))]
    public struct IsPlayerVictoryTag : IComponent { }

}