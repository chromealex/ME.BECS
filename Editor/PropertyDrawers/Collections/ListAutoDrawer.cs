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

            if (list.isCreated == false) {
                var label = new Label("Collection is not created");
                root.Add(label);
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