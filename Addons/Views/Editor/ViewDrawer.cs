using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Views.Editor {

    using BECS.Views;

    [CustomPropertyDrawer(typeof(View))]
    public class ViewDrawer : PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property) {

            var viewSource = property.FindPropertyRelative(nameof(View.viewSource));
            var prefabId = viewSource.FindPropertyRelative(nameof(View.viewSource.prefabId));
            var providerId = viewSource.FindPropertyRelative(nameof(View.viewSource.providerId));
            
            var container = new UnityEngine.UIElements.VisualElement();
            container.styleSheets.Add(ME.BECS.Editor.EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/DefaultStyles.uss"));
            container.AddToClassList("horizontal-group");
            {
                var objectField = new ObjectField(property.displayName);
                objectField.objectType = typeof(EntityView);
                objectField.allowSceneObjects = false;
                objectField.value = ViewsRegistry.GetEntityViewByPrefabId(prefabId.uintValue);
                objectField.RegisterValueChangedCallback((evt) => {
                    if (evt.newValue == evt.previousValue) return;
                    var pId = ViewsRegistry.Assign((EntityView)evt.previousValue, (EntityView)evt.newValue);
                    var prop = property.serializedObject.FindProperty(prefabId.propertyPath);
                    prop.serializedObject.Update();
                    prefabId.uintValue = pId;
                    prop.serializedObject.ApplyModifiedProperties();
                    prop.serializedObject.Update();
                });
                container.Add(objectField);
            }
            {
                var choices = ViewsModule.providerInfos.Select(x => x.editorName.ToString()).ToList();
                var provider = new DropdownField(choices, (int)ViewsModule.GAMEOBJECT_PROVIDER_ID);
                provider.AddToClassList("providers-dropdown");
                provider.index = (int)providerId.uintValue;
                provider.RegisterValueChangedCallback((evt) => {
                    var prop = property.serializedObject.FindProperty(prefabId.propertyPath);
                    prop.serializedObject.Update();
                    providerId.uintValue = (uint)choices.IndexOf(evt.newValue);
                    prop.serializedObject.ApplyModifiedProperties();
                    prop.serializedObject.Update();
                });
                container.Add(provider);
            }
            return container;
            
        }

    }

}