namespace ME.BECS.Network.Editor {
    [UnityEditor.InitializeOnLoad]
    internal static class EntityDrawerReplayMode {
        static EntityDrawerReplayMode() {
            ME.BECS.Editor.EntityDrawer.ReplayModeResolver = IsReplayMode;
        }
        private static bool IsReplayMode(World world) {
            var initializer = WorldInitializers.GetByWorldName(world.Name) as NetworkWorldInitializer;
            // Resolve for the entity's world, independently of the selected replay window.
            return initializer != null && initializer.world.Equals(world) &&
                   initializer.GetModule<NetworkModule>()?.IsInReplayMode() == true;
        }
    }
}
