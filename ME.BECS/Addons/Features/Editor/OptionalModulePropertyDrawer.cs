namespace ME.BECS.Editor {
    
    using UnityEngine.UIElements;
    using UnityEditor;

    [CustomPropertyDrawer(typeof(OptionalModule))]
    public class OptionalPropertyDrawer : PropertyDrawer {

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();

            this.DrawField(rootVisualElement, property);
            
            return rootVisualElement;

        }
        
        private void DrawField(VisualElement root, SerializedProperty property) {

            var so = property.serializedObject;

            var group = new VisualElement();
            group.AddToClassList("horizontal-group");

            {
                var obj = property.FindPropertyRelative("obj");
                var field = new UnityEditor.UIElements.ObjectField(property.displayName);
                field.value = obj.objectReferenceValue;
                field.AddToClassList("systems-graph-field");
                if (field.value != null) field.AddToClassList("set");
                field.allowSceneObjects = false;
                //var gType = property.boxedValue.GetType().GenericTypeArguments[0];
                field.objectType = typeof(Module);
                field.RegisterValueChangedCallback((evt) => {
                    so.Update();
                    obj.objectReferenceValue = evt.newValue;
                    so.ApplyModifiedProperties();
                    so.Update();
                    if (obj.objectReferenceValue != null) {
                        field.AddToClassList("set");
                    } else {
                        field.RemoveFromClassList("set");
                    }
                });
                group.Add(field);
            }
            {
                var enabled = property.FindPropertyRelative("enabled");
                var toggle = new Toggle();
                toggle.value = enabled.boolValue;
                toggle.RegisterValueChangedCallback((evt) => {
                    so.Update();
                    enabled.boolValue = evt.newValue;
                    so.ApplyModifiedProperties();
                    so.Update();
                });
                toggle.style.position = new StyleEnum<Position>(Position.Absolute);
                toggle.style.left = -15f;
                group.Add(toggle);
            }

            root.Add(group);

        }
        
    }

}