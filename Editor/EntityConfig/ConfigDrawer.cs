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
            if (UnityEngine.Application.isPlaying == false) EntityConfigsRegistry.Initialize();
            
            var container = new UnityEngine.UIElements.VisualElement();
            ME.BECS.Editor.EditorUIUtils.ApplyDefaultStyles(container);
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
        
        public override void OnGUI(UnityEngine.Rect position, SerializedProperty property, UnityEngine.GUIContent label) {

            var sourceId = property.FindPropertyRelative(nameof(Config.sourceId));
            if (sourceId == null) {
                EditorGUI.LabelField(position, label.text, "sourceId not found");
                return;
            }

            if (UnityEngine.Application.isPlaying == false) EntityConfigsRegistry.Initialize();
            
            var currentConfig = EntityConfigsRegistry.GetEntityConfigBySourceId(sourceId.uintValue);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            
            var newConfig = EditorGUI.ObjectField(position, label, currentConfig, typeof(EntityConfig), false) as EntityConfig;

            if (EditorGUI.EndChangeCheck()) {
                var previousId = sourceId.uintValue;
                
                var newId = ME.BECS.Editor.ObjectReferenceRegistryUtils.Assign(currentConfig, newConfig);
                sourceId.uintValue = newId;

                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

    }

}