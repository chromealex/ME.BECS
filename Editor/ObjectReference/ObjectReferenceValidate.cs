using System.Linq;

namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEditor.AddressableAssets.Settings;

    public static class ObjectReferenceValidate {

        static ObjectReferenceValidate() {
            Validate();
        }

        [InitializeOnLoadMethod]
        [MenuItem("ME.BECS/Validate Resources")]
        public static void Validate() {
            if (ObjectReferenceRegistry.data == null) {
                ObjectReferenceRegistry.LoadForced();
            }

            var isDirty = false;
            var items = ObjectReferenceRegistry.data.items;
            for (int i = 0; i < items.Length; i++) {
                var item = items[i];
                var asset = item.source ?? item.sourceReference?.editorAsset;
                if (asset != null) {
                    if (IsAssetAddressable(asset) == false) {
                        item.source = asset;
                        item.sourceReference = new UnityEngine.AddressableAssets.AssetReference();
                    } else if (item.source != null) {
                        item.sourceReference = new UnityEngine.AddressableAssets.AssetReference(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(item.source)));
                        item.source = null;
                    }
            
                    var src = item.source ?? item.sourceReference?.editorAsset;
                    if (src != null) {
                        item.sourceType = src.GetType().AssemblyQualifiedName;
                        if (item.customData == null) item.customData = CreateCustomData(src);
                        if (item.customData != null && item.customData.IsValid(src) == true) {
                            item.customData.Validate(src);
                        }
                    } else {
                        item.customData = null;
                        item.sourceType = default;
                    }
                }

                if (isDirty == true || items[i].Equals(item) == false) {
                    items[i] = item;
                    isDirty = true;
                }
            }
            
            items = items.Where(x => x.source != null || x.sourceReference.editorAsset != null).ToArray();
            ObjectReferenceRegistry.data.items = items;
            if (isDirty == true) EditorUtility.SetDirty(ObjectReferenceRegistry.data);
        }
        
        private static IObjectItemData CreateCustomData(UnityEngine.Object src) {
            var types = UnityEditor.TypeCache.GetTypesDerivedFrom<IObjectItemData>();
            foreach (var type in types) {
                var obj = (IObjectItemData)System.Activator.CreateInstance(type);
                if (obj.IsValid(src) == true) {
                    return obj;
                }
            }
            return null;
        }
        
        private static bool IsAssetAddressable(UnityEngine.Object obj) {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));
            return entry != null;
        }
        
    }

}