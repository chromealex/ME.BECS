using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ME.BECS.Editor.Extensions.SubclassSelector;
using Unity.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(WorldProperties))]
    public unsafe class WorldPropertiesDrawer : PropertyDrawer {

        private static StyleSheet styleSheetBase;
        
        private void LoadStyle() {
            if (WorldPropertiesDrawer.styleSheetBase == null) {
                WorldPropertiesDrawer.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldProperties.uss");
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(WorldPropertiesDrawer.styleSheetBase);

            this.DrawProperties(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void DrawProperties(VisualElement root, SerializedProperty property) {

            var so = property.serializedObject;
            var foldout = new Foldout();
            foldout.AddToClassList("foldout");
            foldout.text = "World Properties";
            { // Name field
                var container = new VisualElement();
                container.AddToClassList("field");
                foldout.Add(container);
                var prop = property.FindPropertyRelative(nameof(WorldProperties.name));
                var nameField = new TextField(64 / sizeof(char), false, false, default);
                nameField.label = prop.displayName;
                nameField.value = ((FixedString64Bytes)prop.boxedValue).ToString();
                nameField.RegisterValueChangedCallback((evt) => {
                    so.Update();
                    prop.boxedValue = (FixedString64Bytes)evt.newValue;
                    so.ApplyModifiedProperties();
                    so.Update();
                });
                container.Add(nameField);
                EditorUIUtils.DrawTooltip(container, prop);
            }
            { // State properties
                var container = new VisualElement();
                container.AddToClassList("properties-block");
                foldout.Add(container);
                var header = new Label("State Properties");
                header.AddToClassList("properties-block-header");
                container.Add(header);
                var prop = property.FindPropertyRelative(nameof(WorldProperties.stateProperties));
                var archetypesCapacity = prop.FindPropertyRelative(nameof(WorldProperties.stateProperties.archetypesCapacity));
                var entitiesCapacity = prop.FindPropertyRelative(nameof(WorldProperties.stateProperties.entitiesCapacity));
                var queriesCapacity = prop.FindPropertyRelative(nameof(WorldProperties.stateProperties.queriesCapacity));
                var storageCapacity = prop.FindPropertyRelative(nameof(WorldProperties.stateProperties.storageCapacity));
                var sharedComponentsCapacity = prop.FindPropertyRelative(nameof(WorldProperties.stateProperties.sharedComponentsCapacity));
                var oneShotTasksCapacity = prop.FindPropertyRelative(nameof(WorldProperties.stateProperties.oneShotTasksCapacity));
                var worldMode = prop.FindPropertyRelative(nameof(WorldProperties.stateProperties.mode));
                EditorUIUtils.DrawUIntField(container, entitiesCapacity, 1);
                EditorUIUtils.DrawUIntField(container, storageCapacity, 1);
                EditorUIUtils.DrawUIntField(container, archetypesCapacity, 1);
                EditorUIUtils.DrawUIntField(container, queriesCapacity);
                EditorUIUtils.DrawUIntField(container, sharedComponentsCapacity);
                EditorUIUtils.DrawUIntField(container, oneShotTasksCapacity);
                EditorUIUtils.DrawEnumField<WorldMode>(container, worldMode);
            }
            { // Allocator properties
                var container = new VisualElement();
                container.AddToClassList("properties-block");
                foldout.Add(container);
                var header = new Label("Allocator Properties");
                header.AddToClassList("properties-block-header");
                container.Add(header);
                var prop = property.FindPropertyRelative(nameof(WorldProperties.allocatorProperties));
                var sizeInBytesCapacity = prop.FindPropertyRelative(nameof(WorldProperties.allocatorProperties.sizeInBytesCapacity));
                EditorUIUtils.DrawBytesField(container, sizeInBytesCapacity);
            }
            root.Add(foldout);

        }

    }

}