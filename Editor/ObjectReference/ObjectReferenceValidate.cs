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

            var items = ObjectReferenceRegistry.data.items;
            Validate(0, items.Length);
        }
        
        public static void Validate(int offset, int count) {
            if (ObjectReferenceRegistry.data == null) {
                ObjectReferenceRegistry.LoadForced();
            }

            var isDirty = false;
            var items = ObjectReferenceRegistry.data.items;
            for (int i = offset; i < (count < items.Length ? offset + count : items.Length); ++i) {
                var item = items[i];
                var asset = item.source != null ? item.source : item.sourceReference?.editorAsset;
                var type = string.IsNullOrEmpty(item.sourceType) == true ? null : System.Type.GetType(item.sourceType);
                if (type == null && item.source != null) {
                    type = item.source.GetType();
                }
                if (asset != null) {
                    if (type != null) {
                        var t = asset.GetType();
                        if (t.IsAssignableFrom(type) == false) {
                            if (asset is UnityEngine.GameObject go) {
                                asset = go.GetComponent(type);
                            }
                        }
                    }
                    if (IsAssetAddressable(asset) == false) {
                        item.source = asset;
                        item.sourceReference = new UnityEngine.AddressableAssets.AssetReference();
                    } else if (item.source != null || item.sourceReference.editorAsset is UnityEngine.Component) {
                        var src = item.source != null ? item.source : item.sourceReference?.editorAsset;
                        if (src != null && string.IsNullOrEmpty(item.sourceType) == true) item.sourceType = src.GetType().AssemblyQualifiedName;
                        item.sourceReference = new UnityEngine.AddressableAssets.AssetReference(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item.source)));//CreateAssetForType(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item.source)), type);
                        item.source = null;
                    }

                    {
                        var src = item.source != null ? item.source : item.sourceReference?.editorAsset;
                        if (src != null) {
                            if (string.IsNullOrEmpty(item.sourceType) == true) item.sourceType = src.GetType().AssemblyQualifiedName;
                        } else {
                            item.customData = null;
                            item.sourceType = default;
                        }

                        if (item.customData == null) item.customData = CreateCustomData(src);
                        if (item.customData != null && item.customData.IsValid(src) == true) {
                            item.customData.Validate(src);
                        }
                    }
                    item.isGameObject = asset is UnityEngine.Component || asset is UnityEngine.GameObject;
                }

                if (isDirty == true || items[i].Equals(item) == false) {
                    items[i] = item;
                    isDirty = true;
                }
            }

            if (isDirty == true) {
                items = items.Where(x => x.source != null || x.sourceReference.editorAsset != null).ToArray();
                ObjectReferenceRegistry.data.items = items;
                EditorUtility.SetDirty(ObjectReferenceRegistry.data);
            }
            
        }

        private static UnityEngine.AddressableAssets.AssetReference CreateAssetForType(string guid, System.Type type) {
            var method = typeof(ObjectReferenceValidate).GetMethod(nameof(CreateAssetRefForType), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var gMethod = method.MakeGenericMethod(type);
            return gMethod.Invoke(null, new object[] { guid }) as UnityEngine.AddressableAssets.AssetReference;
        }
        
        private static UnityEngine.AddressableAssets.AssetReference CreateAssetRefForType<T>(string guid) where T : UnityEngine.Object {
            return new UnityEngine.AddressableAssets.AssetReferenceT<T>(guid);
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
            if (obj is UnityEngine.Component comp) {
                obj = comp.gameObject;
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)), true);
            return entry != null;
        }
        
    }

}