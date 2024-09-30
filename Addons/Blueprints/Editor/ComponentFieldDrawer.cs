using System.Linq;

namespace ME.BECS.Blueprints.Editor {

    using UnityEditor;
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    
    [CustomPropertyDrawer(typeof(ComponentField))]
    public class ComponentFieldDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var container = new VisualElement();
            var component = property.FindPropertyRelative("component");
            DropdownField field;
            {
                field = new DropdownField("Field");
                UpdateField();
            }
            {
                var componentProp = new PropertyField(component);
                componentProp.BindProperty(component);
                componentProp.RegisterValueChangeCallback((evt) => {
                    UpdateField();
                });
                container.Add(componentProp);
            }
            container.Add(field);
            
            return container;
            
            void UpdateField() {
                var type = ME.BECS.Editor.EditorUtils.GetTypeFromPropertyField(component.managedReferenceFullTypename);
                if (type != null) {
                    var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    field.choices = fields.Select(x => x.Name).ToList();
                    field.index = System.Array.IndexOf(field.choices.ToArray(), property.FindPropertyRelative("fieldName").stringValue);
                    field.RegisterValueChangedCallback(evt => {
                        property.serializedObject.Update();
                        property.FindPropertyRelative("fieldName").stringValue = evt.newValue;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                    });
                }
            }

        }

    }

}