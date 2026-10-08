using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Provides Unity Editor controls for scene entity editor.
    /// </summary>
    [CustomEditor(typeof(SceneEntity), true)]
    public class SceneEntityEditor : Editor {

        /// <summary>
        /// Builds the UI Toolkit inspector for the inspected object.
        /// </summary>
        public override VisualElement CreateInspectorGUI() {
            
            var root = new VisualElement();
            var so = this.serializedObject;

            var usePrefabProp = so.FindProperty("usePrefab");
            var usePrefabField = new PropertyField(usePrefabProp, "Use Prefab");
            usePrefabField.Bind(so);
            root.Add(usePrefabField);

            var worldNameField = this.CreatePropertyField("worldName");
            var prefabField = this.CreatePropertyField("prefab");
            var entityViewField = this.CreatePropertyField("entityView");
            var configField = this.CreatePropertyField("config");
            var providerIdField = this.CreatePropertyField("providerId");

            root.Add(worldNameField);
            root.Add(configField);

            root.Add(prefabField);
            root.Add(entityViewField);
            root.Add(providerIdField);

            void UpdateVisibility() {
                var usePrefab = usePrefabProp.boolValue;
                prefabField.style.display = usePrefab ? DisplayStyle.Flex : DisplayStyle.None;
                entityViewField.style.display = usePrefab ? DisplayStyle.None : DisplayStyle.Flex;
                providerIdField.style.display = usePrefab ? DisplayStyle.None : DisplayStyle.Flex;
            }

            UpdateVisibility();

            usePrefabField.RegisterValueChangeCallback(evt => {
                so.ApplyModifiedProperties();
                UpdateVisibility();
            });

            return root;
            
        }

        private VisualElement CreatePropertyField(string propertyName) {
            var property = this.serializedObject.FindProperty(propertyName);
            if (property != null) {
                var field = new PropertyField(property, property.displayName);
                field.Bind(this.serializedObject);
                return field;
            } else {
                Debug.LogWarning($"Can't find SerializedProperty with the name `{propertyName}`");
                return new Label($"[Missing] {propertyName}");
            }
        }

    }

}