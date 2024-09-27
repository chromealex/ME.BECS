using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(IUnmanagedList), true)]
    public class ListAutoDrawer : PropertyDrawer {

        private TempObject tempObject;
        private VisualElement prevRoot;

        public class TempObject : ScriptableObject {

            [System.Serializable]
            public struct Element {

                [SerializeReference]
                public object obj;

            }
            
            public Element[] arr;

        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            //var list = (IUnmanagedList)property.boxedValue;
            var list = (IUnmanagedList)PropertyEditorUtils.GetTargetObjectOfProperty(property);
            var root = new VisualElement();
            this.CreateGUI(root, list, property);
            return root;

        }

        public void CreateGUI(VisualElement root, IUnmanagedList list, SerializedProperty property) {

            if (list.IsCreated == false) {
                var container = new VisualElement();
                container.AddToClassList("unity-base-field");
                container.AddToClassList("unity-integer-field");
                container.AddToClassList("unity-base-text-field");
                container.AddToClassList("unity-base-field__aligned");
                container.AddToClassList("unity-base-field__inspector-field");
                root.Add(container);
                var label = new Label(property.displayName);
                label.AddToClassList("unity-base-field__label");
                label.AddToClassList("unity-base-text-field__label");
                label.AddToClassList("unity-property-field__label");
                label.AddToClassList("unity-base-field__label--with-dragger");
                label.AddToClassList("unity-integer-field__label");
                container.Add(label);
                var val = new Label("Collection is not created");
                val.AddToClassList("unity-base-field__input");
                container.Add(val);
                return;
            }
            var arr = list.ToManagedArray();
            if (this.tempObject != null && this.tempObject.arr.Length == arr.Length) {
                root.Add(this.prevRoot);
                root.AddToClassList("array-view");
                return;
            }
            if (this.tempObject != null) Object.DestroyImmediate(this.tempObject);
            var instance = TempObject.CreateInstance<TempObject>();
            this.tempObject = instance;
            instance.arr = arr.Select(x => new TempObject.Element() { obj = x }).ToArray();
            var prop = new SerializedObject(instance).FindProperty(nameof(TempObject.arr));
            var field = new PropertyField(prop, property.displayName);
            field.BindProperty(prop);
            this.prevRoot = field;
            root.Add(this.prevRoot);
            root.AddToClassList("array-view");
            //drawer.SetFoldoutState(true);

        }

        ~ListAutoDrawer() {
            if (this.tempObject != null) {
                EditorApplication.delayCall += () => TempObject.DestroyImmediate(this.tempObject);
            }
            this.tempObject = null;
        }

    }

}