using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Views.Editor {

    using BECS.Views;

    [CustomPropertyDrawer(typeof(ViewModules))]
    public class ViewModulesDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var items = property.FindPropertyRelative(nameof(ViewModules.items));
            
            var container = new UnityEngine.UIElements.VisualElement();
            ME.BECS.Editor.EditorUIUtils.ApplyDefaultStyles(container);
            container.Add(new PropertyField(items, property.displayName));
            return container;
            
        }

    }

    [CustomPropertyDrawer(typeof(ViewModules.Module))]
    public class ViewModulesModuleDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var module = property.FindPropertyRelative(nameof(ViewModules.Module.module));
            var enabledState = property.FindPropertyRelative(nameof(ViewModules.Module.enabled));
            
            var container = new UnityEngine.UIElements.VisualElement();
            ME.BECS.Editor.EditorUIUtils.ApplyDefaultStyles(container);
            container.AddToClassList("views-module");
            container.AddToClassList("horizontal-group-stretch");
            var toggle = new Toggle(string.Empty);
            toggle.value = enabledState.boolValue;
            toggle.RegisterValueChangedCallback((evt) => {
                if (evt.newValue != enabledState.boolValue) {
                    module.serializedObject.Update();
                    property.FindPropertyRelative(nameof(ViewModules.Module.enabled)).boolValue = evt.newValue;
                    module.serializedObject.ApplyModifiedProperties();
                    module.serializedObject.Update();
                }
            });
            container.Add(toggle);
            container.Add(new PropertyField(module, module.managedReferenceValue?.GetType().Name ?? module.displayName));
            return container;
            
        }

    }

}