using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Views.Editor {

    [CustomPropertyDrawer(typeof(Config))]
    [CustomPropertyDrawer(typeof(ConfigDrawerAttribute))]
    public class ConfigDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var sourceId = property.FindPropertyRelative(nameof(Config.sourceId));
            if (sourceId == null) return null;
            EntityConfigsRegistry.Initialize();
            
            var container = new UnityEngine.UIElements.VisualElement();
            container.styleSheets.Add(ME.BECS.Editor.EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/DefaultStyles.uss"));
            {
                var objectField = new ObjectField(property.displayName);
                objectField.objectType = typeof(EntityConfig);
                objectField.allowSceneObjects = false;
                objectField.value = EntityConfigsRegistry.GetEntityConfigBySourceId(sourceId.uintValue);
                objectField.RegisterValueChangedCallback((evt) => {
                    if (evt.newValue == evt.previousValue) return;
                    var pId = ME.BECS.Editor.ObjectReferenceRegistryUtils.Assign((EntityConfig)evt.previousValue, (EntityConfig)evt.newValue);
                    var prop = property.serializedObject.FindProperty(sourceId.propertyPath);
                    prop.serializedObject.Update();
                    sourceId.uintValue = pId;
                    prop.serializedObject.ApplyModifiedProperties();
                    prop.serializedObject.Update();
                });
                container.Add(objectField);
            }
            return container;
            
        }

    }

}