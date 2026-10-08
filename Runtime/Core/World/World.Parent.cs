namespace ME.BECS {
    
    using Unity.Burst;

    /// <summary>
    /// Tracks relationships between registered worlds.
    /// </summary>
    public class WorldsParent {

        /// <summary>
        /// Parent worlds used by <c>WorldsParent</c>.
        /// </summary>
        public static readonly SharedStatic<Internal.Array<ushort>> parentWorlds = SharedStatic<Internal.Array<ushort>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsParent>(TAlign<Internal.Array<ushort>>.align, 70001L);

        /// <summary>
        /// Changes the storage size to the requested element count.
        /// </summary>
        public static void Resize(ushort worldId) {
            WorldsParent.parentWorlds.Data.Resize(worldId + 1u);
        }

        /// <summary>
        /// Clears the current worlds parent contents.
        /// </summary>
        public static void Clear(ushort worldId) {
            WorldsParent.parentWorlds.Data.Get(worldId) = default;
        }

    }

    /// <summary>
    /// Owns an ECS simulation state, entity storage and scheduled system work.
    /// </summary>
    public partial struct World {
        
        /// <summary>
        /// Parent entry in the represented hierarchy.
        /// </summary>
        public World parent {
            get => Worlds.GetWorld(WorldsParent.parentWorlds.Data.Get(this.id));
            set => WorldsParent.parentWorlds.Data.Get(this.id) = value.id;
        }

    }
    
}