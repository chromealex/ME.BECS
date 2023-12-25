using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;

    public class WorldEntityEditorWindow : UnityEditor.EditorWindow {

        public Ent entity;
        public TempObject tempObject;

        public static void Show(Ent ent) {
            var win = WorldEntityEditorWindow.CreateInstance<WorldEntityEditorWindow>();
            win.entity = ent;
            win.titleContent = new GUIContent(ent, EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-entityview.png"));
            win.Show();
        }

        public class TempObject : ScriptableObject {

            public Ent entity;

        }

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