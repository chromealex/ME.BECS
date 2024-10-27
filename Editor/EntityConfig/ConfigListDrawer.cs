namespace ME.BECS.Editor {

    using UnityEditor;
    using UnityEngine.UIElements;
    using UnityEditor.UIElements;
    
    public struct ConfigListDrawer {

        public static VisualElement CreatePropertyGUI(SerializedProperty property, SerializedProperty configProperty, EntityConfig config, System.Action<uint> onIdChanged) {

            var root = new VisualElement();
            if (config == null) {
                var lbl = new TextField(property.displayName);
                lbl.isReadOnly = true;
                lbl.value = "ConfigList must be defined in EntityConfig only.";
                root.Add(lbl);
                return root;
            }

            var obj = (ConfigList)PropertyEditorUtils.GetTargetObjectOfProperty(property);
            var gType = obj.type;
            if (gType.IsPrimitive == true) {
                var lbl = new TextField(property.displayName);
                lbl.isReadOnly = true;
                lbl.value = $"ConfigList<{gType.Name}> is not supported because of primitive. Please, use serializable struct instead.";
                root.Add(lbl);
                return root;
            }
            
            var idProp = property.FindPropertyRelative(nameof(ConfigList.id));
            var id = idProp.uintValue;
            var newId = config.GetCollection(id, out var collectionData, out var index);
            if (id != newId) {
                property.serializedObject.Update();
                idProp.uintValue = newId;
                onIdChanged.Invoke(newId);
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }
            for (var i = 0; i < collectionData.array.Count; ++i) {
                var item = collectionData.array[i];
                if (item == null) {
                    item = CreateDefault(gType);
                    collectionData.array[i] = item;
                }
            }

            var items = configProperty.serializedObject.FindProperty("collectionsData").FindPropertyRelative("items");
            if (index >= items.arraySize) return root;
            var prop = items.GetArrayElementAtIndex(index).FindPropertyRelative("array");
            var propField = new PropertyField(prop, configProperty.displayName);
            propField.BindProperty(prop);
            propField.RegisterValueChangeCallback((evt) => {
                for (int i = 0; i < prop.arraySize; ++i) {
                    if (prop.GetArrayElementAtIndex(i).managedReferenceValue == null) {
                        prop.serializedObject.Update();
                        prop.GetArrayElementAtIndex(i).managedReferenceValue = CreateDefault(gType);
                        prop.serializedObject.ApplyModifiedProperties();
                        prop.serializedObject.Update();
                    }
                }
            });
            root.Add(propField);
            
            root.AddToClassList("array-view");

            return root;

        }
        
        private static object CreateDefault(System.Type gType) {
            return System.Activator.CreateInstance(gType);
        }

    }

}