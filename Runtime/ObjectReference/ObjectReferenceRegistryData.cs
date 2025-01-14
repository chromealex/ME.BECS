using System.Linq;

namespace ME.BECS {

    public class ObjectReferenceRegistryData : UnityEngine.ScriptableObject {

        [System.Serializable]
        public struct Item {

            public UnityEngine.Object source;
            public uint sourceId;
            public uint references;

            public bool IsValid() {
                return this.source != null;
            }

        }
        
        public uint sourceId;
        public Item[] items = System.Array.Empty<Item>();

        public void OnValidate() {
            this.CleanUp();
        }

        private void CleanUp() {

            this.items = this.items.Where(x => x.source != null).ToArray();

        }

        public UnityEngine.Object GetObjectBySourceId(uint sourceId) {

            for (int i = 0; i < this.items.Length; ++i) {
                if (this.items[i].sourceId == sourceId) {
                    return this.items[i].source;
                }
            }

            return null;

        }

        public uint Add(UnityEngine.Object source, out bool isNew) {

            isNew = false;
            if (source == null) return 0u;

            for (int i = 0; i < this.items.Length; ++i) {
                if (this.items[i].source == source) {
                    ref var item = ref this.items[i];
                    ++item.references;
                    return item.sourceId;
                }
            }

            isNew = true;
            var nextId = this.GetNextId(source);
            {
                // Add new item
                var item = new Item() {
                    sourceId = nextId,
                    source = source,
                    references = 1u,
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

            if (hashId == 0) hashId = 1;

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
                    if (item.references == 0u) return false;
                    --item.references;
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