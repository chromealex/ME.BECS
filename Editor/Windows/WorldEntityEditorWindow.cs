using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;

    /// <summary>
    /// Provides the Unity Editor window for world entity editor.
    /// </summary>
    public class WorldEntityEditorWindow : UnityEditor.EditorWindow {

        /// <summary>
        /// Entity processed or represented by this value.
        /// </summary>
        public Ent entity;
        /// <summary>
        /// Temp object used by <c>WorldEntityEditorWindow</c>.
        /// </summary>
        public TempObject tempObject;

        /// <summary>
        /// Opens the entity inspector for the specified entity.
        /// </summary>
        public static void Show(Ent ent) {
            var win = WorldEntityEditorWindow.CreateInstance<WorldEntityEditorWindow>();
            win.entity = ent;
            win.titleContent = new GUIContent(ent, EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-entityview.png"));
            win.Show();
        }

        /// <summary>
        /// Retains a temporary Unity object for the scope managed by this wrapper.
        /// </summary>
        public class TempObject : ScriptableObject {

            /// <summary>
            /// Entity processed or represented by this value.
            /// </summary>
            public Ent entity;

        }

        /// <summary>
        /// Builds the editor window's UI Toolkit hierarchy.
        /// </summary>
        public void CreateGUI() {

            var instance = TempObject.CreateInstance<TempObject>();
            this.tempObject = instance;
            instance.entity = this.entity;
            var drawer = new EntityDrawer();
            this.rootVisualElement.Add(drawer.CreatePropertyGUI(new SerializedObject(instance).FindProperty(nameof(TempObject.entity))));
            drawer.SetFoldoutState(true);

        }

        private void OnDestroy() {
            if (this.tempObject != null) TempObject.DestroyImmediate(this.tempObject);
            this.tempObject = null;
        }

    }

}