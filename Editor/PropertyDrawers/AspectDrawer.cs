using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static ME.BECS.Cuts;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(IAspect), true)]
    public class AspectDrawer : PropertyDrawer {

        private TempObject tempObject;
        private VisualElement prevRoot;

        public class TempObject : ScriptableObject {

            public Ent entity;

        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            var aspect = (IAspect)PropertyEditorUtils.GetTargetObjectOfProperty(property);

            var root = new VisualElement();
            this.CreateGUI(root, aspect.ent);
            return root;

        }

        public void CreateGUI(VisualElement root, Ent ent) {

            if (this.tempObject != null && this.tempObject.entity == ent) {
                root.Add(this.prevRoot);
                root.AddToClassList("aspect-view");
                return;
            }
            if (this.tempObject != null) Object.DestroyImmediate(this.tempObject);
            var instance = TempObject.CreateInstance<TempObject>();
            this.tempObject = instance;
            instance.entity = ent;
            var drawer = new EntityDrawer();
            this.prevRoot = drawer.CreatePropertyGUI(new SerializedObject(instance).FindProperty(nameof(TempObject.entity)));
            root.Add(this.prevRoot);
            root.AddToClassList("aspect-view");
            drawer.SetFoldoutState(true);

        }

        ~AspectDrawer() {
            if (this.tempObject != null) EditorApplication.delayCall += () => TempObject.DestroyImmediate(this.tempObject);
            this.tempObject = null;
        }

    }

}