using UnityEditor;

namespace ME.BECS.Editor {

    // Retain the window type and asset GUID so existing Unity layouts open the new viewer.
    public unsafe partial class WorldGraphEditorWindow : EditorWindow {

        public World world;
        private WorldAllocatorEditorWindow allocatorWindow;
        private JournalEditorWindow journalWindow;
        private readonly System.Collections.Generic.List<World> aliveWorlds = new System.Collections.Generic.List<World>();

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
