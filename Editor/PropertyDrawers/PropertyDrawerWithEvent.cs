namespace ME.BECS.Editor {

    using UnityEditor;
    using UnityEngine;

    public abstract class PropertyDrawerWithDispose : PropertyDrawer {

        private bool init = true;

        ~PropertyDrawerWithDispose() {
            this.Destroy();
        }

        private void PlayModeStateChanged(PlayModeStateChange obj) {
            switch (obj) {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.ExitingPlayMode:
                    this.Destroy();
                    break;
            }
        }

        private void SelectionChanged() {
            this.Disable();
        }

        /// <summary>
        /// Write code for when the property is first displayed or redisplayed.
        /// </summary>
        public abstract void OnEnable(SerializedProperty property);

        /// <summary>
        /// Write code for when the property may be hidden.
        /// </summary>
        public abstract void OnDisable();

        /// <summary>
        /// Write code for when the property is destroyed. (e.g. Releasing resources.)
        /// </summary>
        public abstract void OnDestroy();

        public abstract UnityEngine.UIElements.VisualElement CreateProperty(SerializedProperty property);

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {
            if (this.init) {
                this.Enable(property);
            }
            return this.CreateProperty(property);
        }

        public void Enable(SerializedProperty property) {
            this.init = false;
            EditorApplication.playModeStateChanged += this.PlayModeStateChanged;
            Selection.selectionChanged += this.SelectionChanged;
            this.OnEnable(property);
        }

        public void Disable() {
            this.OnDisable();
            EditorApplication.playModeStateChanged -= this.PlayModeStateChanged;
            Selection.selectionChanged -= this.SelectionChanged;
            this.init = true;
        }

        public void Destroy() {
            this.OnDestroy();
            EditorApplication.playModeStateChanged -= this.PlayModeStateChanged;
            Selection.selectionChanged -= this.SelectionChanged;
            this.init = true;
        }

    }

}