namespace ME.BECS {
    
    using Unity.Burst;

    public class WorldsParent {

        public static readonly SharedStatic<Internal.Array<ushort>> parentWorlds = SharedStatic<Internal.Array<ushort>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsParent>(TAlign<Internal.Array<ushort>>.align, 70001L);

        public static void Resize(ushort worldId) {
            WorldsParent.parentWorlds.Data.Resize(worldId + 1u);
        }

        public static void Clear(ushort worldId) {
            WorldsParent.parentWorlds.Data.Get(worldId) = default;
        }

    }

    public partial struct World {
        
        public World parent {
            get => Worlds.GetWorld(WorldsParent.parentWorlds.Data.Get(this.id));
            set => WorldsParent.parentWorlds.Data.Get(this.id) = value.id;
        }

    }
    
}