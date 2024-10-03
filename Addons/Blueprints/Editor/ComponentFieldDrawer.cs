using System.Linq;

namespace ME.BECS.Blueprints.Editor {

    using UnityEditor;
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    
    [CustomPropertyDrawer(typeof(ComponentField))]
    [CustomPropertyDrawer(typeof(StaticComponentField))]
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
                    var fields = type.GetMembers(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Where(x => {
                        if (x is System.Reflection.FieldInfo) return true;
                        return x is System.Reflection.PropertyInfo prop && prop.CanRead == true;
                    }).ToList();
                    field.choices = fields.Select(x => {
                        if (x is System.Reflection.FieldInfo fieldInfo) {
                            return $"{fieldInfo.FieldType.Name} {x.Name}";
                        }
                        if (x is System.Reflection.PropertyInfo propertyInfo) {
                            return $"{propertyInfo.PropertyType.Name} {x.Name}";
                        }
                        return null;
                    }).ToList();
                    field.index = System.Array.IndexOf(field.choices.ToArray(), property.FindPropertyRelative("fieldName").stringValue);
                    if (field.index < 0 && field.choices.Count > 0) {
                        field.index = 0;
                    }
                    field.RegisterValueChangedCallback(evt => {
                        property.serializedObject.Update();
                        property.FindPropertyRelative("fieldName").stringValue = fields[field.index].Name;
                        property.serializedObject.ApplyModifiedProperties();
                        property.serializedObject.Update();
                    });
                } else {
                    field.index = -1;
                }
            }

        }

    }

}