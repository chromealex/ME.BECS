using System.Linq;

namespace ME.BECS.Editor {
    
    using UnityEngine.UIElements;

    [UnityEditor.CustomPropertyDrawer(typeof(ViewsProviderAttribute))]
    public class ViewsProviderAttributeDrawer : UnityEditor.PropertyDrawer {

        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(UnityEditor.SerializedProperty property) {

            var modules = ViewsModule.providerInfos.Select(x => x.editorName.ToString()).ToArray();
            var values = ViewsModule.providerInfos.Select(x => x.id).ToList();
            var field = new PopupField<uint>(property.displayName, values, 0, (index) => modules[index], (index) => modules[index]);
            field.index = (int)property.uintValue;
            field.RegisterValueChangedCallback((evt) => {
                property.serializedObject.Update();
                property.uintValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            });
            return field;

        }

    }

}