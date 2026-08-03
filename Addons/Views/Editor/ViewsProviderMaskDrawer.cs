using System.Linq;
using ME.BECS.Addons.Views.Runtime.Providers;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;

namespace ME.BECS.Editor {

    using UnityEngine.UIElements;

    [UnityEditor.CustomPropertyDrawer(typeof(ViewsProviderMaskAttribute))]
    public class ViewsProviderMaskAttributeDrawer : UnityEditor.PropertyDrawer {

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            var modules = ViewsModule.providerInfos.Select(x => x.editorName.ToString()).ToArray();
            var values = ViewsModule.providerInfos.Select(x => (int)x.id).ToArray();

            var maskField = new MaskField(property.displayName, modules.ToList(), 0);

            int ConvertToMask(uint flags) {
                int mask = 0;
                for (int i = 0; i < values.Length; i++) {
                    var offsetMask = 1 << values[i];
                    if ((flags & offsetMask) != 0)
                        mask |= offsetMask;
                }
                return mask;
            }


            uint ConvertToFlags(int mask) {
                uint flags = 0u;
                for (int i = 0; i < values.Length; i++) {
                    var offsetMask = 1u << values[i];
                    if ((mask & offsetMask) != 0)
                        flags |= offsetMask;
                }
                return flags;
            }

            maskField.value = ConvertToMask(property.uintValue);

            maskField.RegisterValueChangedCallback(evt => {
                property.serializedObject.Update();
                property.uintValue = ConvertToFlags(evt.newValue);
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            });

            return maskField;

        }

    }

}
