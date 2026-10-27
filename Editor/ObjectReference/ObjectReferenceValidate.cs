namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEditor.AddressableAssets;
    using UnityEditor.AddressableAssets.Settings;

    /// <summary>
    /// Defines object reference validate state and operations.
    /// </summary>
    public static class ObjectReferenceValidate {

        [InitializeOnLoadMethod]
        private static void ScheduleValidation() {
            EditorApplication.delayCall -= Validate;
            EditorApplication.delayCall += Validate;
        }

        /// <summary>
        /// Checks the supplied state against the constraints required by this API.
        /// </summary>
        [MenuItem("ME.BECS/Resources/Validate", priority = 100)]
        public static void Validate() {
            if (ObjectReferenceRegistry.data == null) {
                ObjectReferenceRegistry.LoadForced();
            }

            if (ObjectReferenceRegistry.data == null) {
                UnityEngine.Debug.LogWarning("Object Reference registry not found.");
                return;
            }

            var items = ObjectReferenceRegistry.data.objects;
            Validate(0, items.Length);
        }

        /// <summary>
        /// Validates removed.
        /// </summary>
        [MenuItem("ME.BECS/Resources/Validate (Removed)", priority = 101)]
        public static void ValidateRemoved() {
            
            if (ObjectReferenceRegistry.data.ValidateRemoved() == true) {
                EditorUtility.SetDirty(ObjectReferenceRegistry.data);
            }
            
        }
        
        /// <summary>
        /// Checks the supplied state against the constraints required by this API.
        /// </summary>
        public static void Validate(int offset, int count) {
            if (ObjectReferenceRegistry.data == null) {
                ObjectReferenceRegistry.LoadForced();
            }

            if (ObjectReferenceRegistry.data == null) {
                return;
            }
            if (offset < 0 || count < 0) {
                throw new System.ArgumentOutOfRangeException(offset < 0 ? nameof(offset) : nameof(count));
            }
            var items = ObjectReferenceRegistry.data.objects;
            var end = offset >= items.Length ? items.Length : offset + System.Math.Min(count, items.Length - offset);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            for (int i = offset; i < end; ++i) {
                if (items[i] == null) {
                    continue;
                }
                var item = items[i].data;
                var asset = item.source != null ? item.source : item.sourceReference?.editorAsset;
                var type = string.IsNullOrEmpty(item.sourceType) == true ? null : System.Type.GetType(item.sourceType);
                if (type == null && item.source != null) {
                    type = item.source.GetType();
                }
                if (asset != null) {
                    var customDataBefore = item.customData == null ? null : UnityEngine.JsonUtility.ToJson(item.customData);
                    if (type != null) {
                        var t = asset.GetType();
                        if (t.IsAssignableFrom(type) == false) {
                            if (asset is UnityEngine.GameObject go) {
                                if (typeof(UnityEngine.Component).IsAssignableFrom(type) == true) {
                                    asset = go.GetComponent(type);
                                }
                            }
                        }
                    }
                    if (IsAssetAddressable(asset, settings) == false) {
                        item.source = asset;
                        // Preserve an already empty reference: AssetReference uses
                        // reference equality, so replacing it dirties unchanged assets.
                        if (item.sourceReference == null || string.IsNullOrEmpty(item.sourceReference.AssetGUID) == false) {
                            item.sourceReference = new UnityEngine.AddressableAssets.AssetReference();
                        }
                    } else if (item.source != null || item.sourceReference?.editorAsset is UnityEngine.Component) {
                        var src = item.source != null ? item.source : item.sourceReference?.editorAsset;
                        if (src != null && string.IsNullOrEmpty(item.sourceType) == true) {
                            item.sourceType = src.GetType().AssemblyQualifiedName;
                        }
                        var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
                        if (item.sourceReference == null || item.sourceReference.AssetGUID != guid) {
                            item.sourceReference = new UnityEngine.AddressableAssets.AssetReference(guid);
                        }
                        item.source = null;
                    }

                    {
                        var src = item.source != null ? item.source : item.sourceReference?.editorAsset;
                        if (src != null) {
                            if (string.IsNullOrEmpty(item.sourceType) == true) {
                                item.sourceType = src.GetType().AssemblyQualifiedName;
                            }
                        } else {
                            item.customData = null;
                            item.sourceType = default;
                        }

                        if (item.customData == null) {
                            item.customData = CreateCustomData(src);
                        }
                        if (item.customData != null && item.customData.IsValid(src) == true) {
                            item.customData.Validate(src);
                        }
                    }
                    item.isGameObject = asset is UnityEngine.Component || asset is UnityEngine.GameObject;
                    // Custom data may be a mutable class or a boxed struct shared
                    // with the original ItemInfo. Equals cannot detect in-place edits.
                    var customDataAfter = item.customData == null ? null : UnityEngine.JsonUtility.ToJson(item.customData);
                    if (items[i].data.Equals(item) == false || items[i].data.isGameObject != item.isGameObject ||
                        customDataBefore != customDataAfter) {
                        items[i].data = item;
                        EditorUtility.SetDirty(items[i]);
                    }
                }
            }

            // Updating entries does not change registry membership. Only the old
            // embedded representation needs migration and a directory rescan.
            if (ObjectReferenceRegistry.data.items.Length != 0) {
                EditorUtility.SetDirty(ObjectReferenceRegistry.data);
                ObjectReferenceRegistry.data.Validate();
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
        
        private static bool IsAssetAddressable(UnityEngine.Object obj, AddressableAssetSettings settings) {
            if (settings == null || obj == null) {
                return false;
            }
            if (obj is UnityEngine.Component comp) {
                obj = comp.gameObject;
            }
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)), true);
            return entry != null;
        }
        
    }

}
