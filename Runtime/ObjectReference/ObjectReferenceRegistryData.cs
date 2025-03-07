using System.Linq;

namespace ME.BECS {

    public struct ObjectItem {

        public UnityEngine.Object source;
        public UnityEngine.AddressableAssets.AssetReference sourceReference;
        public System.Type sourceType;
        public uint sourceId;
        public IObjectItemData data;
        
        public ObjectItem(ItemInfo data) {
            this.source = data.source;
            this.sourceReference = data.sourceReference;
            this.sourceId = data.sourceId;
            this.sourceType = System.Type.GetType(data.sourceType);
            this.data = data.customData;
        }

        public bool IsValid() {
            if (this.source == null && this.sourceReference.IsValid() == false) {
                return false;
            }
            return true;
        }

        public T Load<T>() where T : UnityEngine.Object {
            if (this.source != null) {
                if (this.source is T obj) return obj;
                return null;
            }
            if (this.sourceReference == null || this.sourceReference.IsValid() == false) return null;
            var op = this.sourceReference.LoadAssetAsync<T>();
            op.WaitForCompletion();
            return op.Result;
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
    public struct ItemInfo {

        public UnityEngine.Object source;
        public UnityEngine.AddressableAssets.AssetReference sourceReference;
        public string sourceType;
        public uint sourceId;
        public uint referencesCount;

        [UnityEngine.SerializeReference]
        public IObjectItemData customData;

        public bool IsValid() {
            return this.source != null;
        }

        public void OnValidate() {
            this.sourceType = this.source.GetType().AssemblyQualifiedName;
            #if UNITY_EDITOR
            if (this.sourceReference.IsValid() == false && this.source != null) {
                this.sourceReference = new UnityEngine.AddressableAssets.AssetReference(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(this.source)));
                if (this.sourceReference.IsValid() == true) {
                    this.source = null;
                }
            }

            var src = this.source ?? this.sourceReference.editorAsset;
            if (src != null) {
                if (this.customData == null) this.customData = CreateCustomData(src);
                if (this.customData != null && this.customData.IsValid(src) == true) {
                    this.customData.Validate(src);
                }
            }
            #endif
        }

        #if UNITY_EDITOR
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
        #endif

    }

    public class ObjectReferenceRegistryData : UnityEngine.ScriptableObject {

        public uint sourceId;
        public ItemInfo[] items = System.Array.Empty<ItemInfo>();

        [UnityEngine.ContextMenu("Call OnValidate")]
        public void OnValidate() {
            this.CleanUp();
            for (int i = 0; i < this.items.Length; ++i) {
                this.items[i].OnValidate();
            }
        }

        private void CleanUp() {

            this.items = this.items.Where(x => x.source != null || x.sourceReference.IsValid() == true).ToArray();

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
                if (this.items[i].source == source) {
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