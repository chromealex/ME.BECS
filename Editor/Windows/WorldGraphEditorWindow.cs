using UnityEditor;

namespace ME.BECS.Editor {

    // Retain the window type and asset GUID so existing Unity layouts open the new viewer.
    /// <summary>
    /// Provides the Unity Editor window for world graph editor.
    /// </summary>
    public unsafe partial class WorldGraphEditorWindow : EditorWindow {

        /// <summary>
        /// World used by the containing operation.
        /// </summary>
        public World world;
        private WorldAllocatorEditorWindow allocatorWindow;
        private JournalEditorWindow journalWindow;
        private readonly System.Collections.Generic.List<World> aliveWorlds = new System.Collections.Generic.List<World>();

        /// <summary>
        /// Opens or focuses the associated editor window.
        /// </summary>
        public static void ShowWindow() {
            var window = CreateInstance<WorldGraphEditorWindow>();
            EditorUIUtils.ApplyWindowIcon(window, "Worlds Viewer", "ME.BECS.Resources/Icons/icon-worldviewer.png");
            window.Show();
        }

        private void UpdateWorlds() {
            this.aliveWorlds.Clear();
            var worlds = Worlds.GetWorlds();
            for (int i = 0; i < worlds.Length; ++i) {
                var item = worlds.Get(i).world;
                if (item.isCreated) this.aliveWorlds.Add(item);
            }
        }
    }
}
