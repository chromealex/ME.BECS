using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SceneEntity), true)]
    public class SceneEntityEditor : Editor {

        public override VisualElement CreateInspectorGUI() {
            
            var root = new VisualElement();
            var so = this.serializedObject;

            var usePrefabProp = so.FindProperty("usePrefab");
            var usePrefabField = new PropertyField(usePrefabProp, "Use Prefab");
            usePrefabField.Bind(so);
            root.Add(usePrefabField);

            var worldNameField = this.CreatePropertyField("worldName", "World Name");
            var prefabField = this.CreatePropertyField("prefab", "Prefab");
            var entityViewField = this.CreatePropertyField("entityView", "Entity View");
            var configField = this.CreatePropertyField("config", "Config");
            var providerIdField = this.CreatePropertyField("providerId", "Provider ID");

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

        private VisualElement CreatePropertyField(string propertyName, string label = null) {
            var property = this.serializedObject.FindProperty(propertyName);
            if (property != null) {
                var field = new PropertyField(property, label ?? propertyName);
                field.Bind(this.serializedObject);
                return field;
            } else {
                Debug.LogWarning($"Не удалось найти SerializedProperty с именем: {propertyName}");
                return new Label($"[Missing] {propertyName}");
            }
        }

    }

}