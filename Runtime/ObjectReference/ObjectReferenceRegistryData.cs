namespace ME.BECS {

    public readonly struct ObjectItem {

        public readonly UnityEngine.Object source;
        public readonly UnityEngine.AddressableAssets.AssetReference sourceReference;
        public readonly System.Type sourceType;
        public readonly uint sourceId;
        public readonly bool isGameObject;
        public readonly IObjectItemData data;
        
        public ObjectItem(ItemInfo data) {
            this.source = data.source;
            this.sourceReference = data.sourceReference;
            this.sourceId = data.sourceId;
            this.sourceType = System.Type.GetType(data.sourceType);
            this.data = data.customData;
            this.isGameObject = data.isGameObject;
        }

        public bool IsValid() {
            if (this.source == null && this.sourceType == null) {
                return false;
            }
            return true;
        }

        public T Load<T>() where T : UnityEngine.Object {
            if (this.source != null) {
                if (this.source is T obj) return obj;
                return null;
            }
            if (this.sourceReference == null || string.IsNullOrEmpty(this.sourceReference.AssetGUID) == true) return null;
            if (this.isGameObject == true) {
                UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<UnityEngine.Object> op;
                if (this.sourceReference.OperationHandle.IsValid() == true) {
                    op = this.sourceReference.OperationHandle.Convert<UnityEngine.Object>();
                } else {
                    op = this.sourceReference.LoadAssetAsync<UnityEngine.Object>();
                    op.WaitForCompletion();
                }

                if (op.Result is UnityEngine.GameObject go) {
                    return go.GetComponent<T>();
                }
                return op.Result as T;
            } else {
                UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<T> op;
                if (this.sourceReference.OperationHandle.IsValid() == true) {
                    op = this.sourceReference.OperationHandle.Convert<T>();
                } else {
                    op = this.sourceReference.LoadAssetAsync<T>();
                    op.WaitForCompletion();
                }
                return op.Result;
            }
        }

        public int GetInstanceID() {
            if (this.source != null) return this.source.GetInstanceID();
            return (int)this.sourceId;
        }

        public bool Is<T>() {
            if (this.source is T) return true;
            if (typeof(T).IsAssignableFrom(this.sourceType) == true) return true;
            return false;
        }

    }

    public interface IObjectItemData {

        bool IsValid(UnityEngine.Object obj);
        void Validate(UnityEngine.Object obj);

    }
    
    [System.Serializable]
    public struct ItemInfo : System.IEquatable<ItemInfo> {

        public UnityEngine.Object source;
        public UnityEngine.AddressableAssets.AssetReference sourceReference;
        public bool isGameObject;
        public string sourceType;
        public uint sourceId;
        public uint referencesCount;

        [UnityEngine.SerializeReference]
        public IObjectItemData customData;

        public void CleanUpLoadedAssets() {
            if (this.sourceReference.IsValid() == true) this.sourceReference.ReleaseAsset();
        }

        public bool Equals(ItemInfo other) {
            return this.source == other.source && Equals(this.sourceReference, other.sourceReference) && this.sourceType == other.sourceType && this.sourceId == other.sourceId && this.referencesCount == other.referencesCount && Equals(this.customData, other.customData);
        }

        public override bool Equals(object obj) {
            return obj is ItemInfo other && this.Equals(other);
        }

        public override int GetHashCode() {
            return System.HashCode.Combine(this.source, this.sourceReference, this.sourceType, this.sourceId, this.referencesCount, this.customData);
        }

        public bool Is<T>(bool ignoreErrors = false) {
            if (this.source is T) return true;
            if (string.IsNullOrEmpty(this.sourceType) == true) {
                if (ignoreErrors == false) UnityEngine.Debug.LogError($"SourceType is empty for source {this.source} ({this.sourceReference}) isGameObject: {this.isGameObject} sourceId: {this.sourceId}");
            } else {
                if (typeof(T).IsAssignableFrom(System.Type.GetType(this.sourceType)) == true) return true;
            }
            return false;
        }
        
        public bool Is(UnityEngine.Object obj) {
            if (this.source == obj) return true;
            #if UNITY_EDITOR
            if (this.sourceReference != null) {
                if (obj is UnityEngine.Component comp) {
                    if (this.sourceReference.editorAsset == comp.gameObject) return true;
                }
                if (this.sourceReference.editorAsset == obj) return true;
            }
            #endif
            return false;
        }

    }

    public class ObjectReferenceRegistryData : UnityEngine.ScriptableObject {

        public uint sourceId;
        public ItemInfo[] items = System.Array.Empty<ItemInfo>();

        public void CleanUpLoadedAssets() {
            foreach (var item in this.items) {
                item.CleanUpLoadedAssets();
            }
        }
        
        public ObjectItem GetObjectBySourceId(uint sourceId) {

            for (int i = 0; i < this.items.Length; ++i) {
                if (this.items[i].sourceId == sourceId) {
                    return new ObjectItem(this.items[i]);
                }
            }

            return default;

        }

        public uint Add(UnityEngine.Object source, out bool isNew) {

            isNew = false;
            if (source == null) return 0u;

            for (int i = 0; i < this.items.Length; ++i) {
                if (this.items[i].Is(source) == true) {
                    ref var item = ref this.items[i];
                    ++item.referencesCount;
                    return item.sourceId;
                }
            }

            isNew = true;
            var nextId = this.GetNextId(source);
            {
                // Add new item
                var item = new ItemInfo() {
                    sourceId = nextId,
                    source = source,
                    referencesCount = 1u,
                };
                System.Array.Resize(ref this.items, this.items.Length + 1);
                this.items[this.items.Length - 1] = item;
                return nextId;
            }

        }

        private uint GetNextId(UnityEngine.Object source) {
            #if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(source);
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            var hashId = 0u;
            for (int i = 0; i < guid.Length; ++i) {
                hashId ^= (uint)(guid[i] + 31);
            }

            if (hashId == 0u) hashId = 1u;

            while (true) {
                // Set unique next id
                var has = false;
                for (int i = 0; i < this.items.Length; ++i) {
                    ref var item = ref this.items[i];
                    if (item.sourceId == hashId) {
                        ++hashId;
                        has = true;
                        break;
                    }
                }
                if (has == false) break;
            }

            if (hashId > this.sourceId) this.sourceId = hashId;
            return hashId;
            #else
            var nextId = ++this.sourceId;
            return nextId;
            #endif
        }

        public bool Remove(UnityEngine.Object source) {
            
            for (int i = 0; i < this.items.Length; ++i) {
                if (this.items[i].source == source) {
                    ref var item = ref this.items[i];
                    if (item.referencesCount == 0u) return false;
                    --item.referencesCount;
                    // if (item.references == 0u) {
                    //     if (this.items.Length == 1) {
                    //         this.items = System.Array.Empty<Item>();
                    //     } else {
                    //         this.items[i] = this.items[this.items.Length - 1];
                    //         System.Array.Resize(ref this.items, this.items.Length - 1);
                    //     }
                    //
                    //     return true;
                    // }
                }
            }

            return false;

        }

    }

}