namespace ME.BECS.Editor {

    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Draws property drawer with dispose values in the Unity Inspector.
    /// </summary>
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

        /// <summary>
        /// Creates property.
        /// </summary>
        public abstract UnityEngine.UIElements.VisualElement CreateProperty(SerializedProperty property);

        /// <summary>
        /// Builds the UI Toolkit editor for the supplied serialized property.
        /// </summary>
        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {
            if (this.init) {
                this.Enable(property);
            }
            return this.CreateProperty(property);
        }

        /// <summary>
        /// Enables the associated component or processing state.
        /// </summary>
        public void Enable(SerializedProperty property) {
            this.init = false;
            EditorApplication.playModeStateChanged += this.PlayModeStateChanged;
            Selection.selectionChanged += this.SelectionChanged;
            this.OnEnable(property);
        }

        /// <summary>
        /// Disables the associated component or processing state.
        /// </summary>
        public void Disable() {
            this.OnDisable();
            EditorApplication.playModeStateChanged -= this.PlayModeStateChanged;
            Selection.selectionChanged -= this.SelectionChanged;
            this.init = true;
        }

        /// <summary>
        /// Destroys the referenced instance and applies its registered destruction handling.
        /// </summary>
        public void Destroy() {
            this.OnDestroy();
            EditorApplication.playModeStateChanged -= this.PlayModeStateChanged;
            Selection.selectionChanged -= this.SelectionChanged;
            this.init = true;
        }

    }

}