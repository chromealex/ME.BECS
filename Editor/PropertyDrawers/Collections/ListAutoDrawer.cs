using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(IUnmanagedList), true)]
    public class ListAutoDrawer : PropertyDrawer {

        private TempObject tempObject;

        public class TempObject : ScriptableObject {

            [System.Serializable]
            public struct Element {

                [SerializeReference]
                public object obj;

            }
            
            public Element[] arr;

        }

        public class TempListObject : ScriptableObject {

            public ConfigList arr;

        }

        private static ScriptableObject CreateTempListObject(uint id, System.Type type) {
            var so = ScriptableObject.CreateInstance<TempListObject>();
            so.arr = new ConfigList() {
                id = id,
                type = type,
            };
            return so;
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            var list = (IUnmanagedList)PropertyEditorUtils.GetTargetObjectOfProperty(property);
            ValidateDefault(property, ref list);
            var root = new VisualElement();
            this.CreateGUI(root, list, property);
            return root;

        }

        public void CreateGUI(VisualElement root, IUnmanagedList list, SerializedProperty property) {

            if (property.serializedObject.targetObject is EntityConfig config) {
                SerializedProperty id = null;
                if (list is IMemArray) {
                    id = property.FindPropertyRelative("data").FindPropertyRelative("Length");
                } else if (list is IMemList) {
                    id = property.FindPropertyRelative("Count");
                }

                if (id == null) {
                    root.Add(new Label("Unsupported collection type"));
                    return;
                }
                var so = CreateTempListObject(id.uintValue, list.GetType().GenericTypeArguments[0]);
                var p = new SerializedObject(so).FindProperty(nameof(TempListObject.arr));
                var elem = ConfigListDrawer.CreatePropertyGUI(p, property, config, (newId) => {
                    id.serializedObject.Update();
                    id.uintValue = newId;
                    id.serializedObject.ApplyModifiedProperties();
                    id.serializedObject.Update();
                });
                root.Add(elem);
                return;
            }
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
            if (this.tempObject != null) Object.DestroyImmediate(this.tempObject);
            var instance = ScriptableObject.CreateInstance<TempObject>();
            this.tempObject = instance;
            instance.arr = arr.Select(x => new TempObject.Element() { obj = x }).ToArray();
            var prop = new SerializedObject(instance).FindProperty(nameof(TempObject.arr));
            var field = new PropertyField(prop, property.displayName);
            field.BindProperty(prop);
            root.Add(field);
            root.AddToClassList("array-view");
            //drawer.SetFoldoutState(true);

        }

        private static void ValidateDefault(SerializedProperty property, ref IUnmanagedList list) {
            if (list == null) {
                PropertyEditorUtils.GetTargetObjectOfProperty(property, out var fieldType);
                property.boxedValue = System.Activator.CreateInstance(fieldType);
                list = (IUnmanagedList)PropertyEditorUtils.GetTargetObjectOfProperty(property);
            }
        }

        ~ListAutoDrawer() {
            if (this.tempObject != null) {
                EditorApplication.delayCall += () => Object.DestroyImmediate(this.tempObject);
            }
            this.tempObject = null;
        }

    }

}