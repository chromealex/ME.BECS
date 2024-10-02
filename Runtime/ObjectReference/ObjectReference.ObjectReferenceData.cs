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
            {
                // Add new item
                var item = new Item() {
                    sourceId = ++this.sourceId,
                    source = source,
                    references = 1,
                };
                System.Array.Resize(ref this.items, this.items.Length + 1);
                this.items[this.items.Length - 1] = item;
                return this.sourceId;
            }

        }

        public bool Remove(UnityEngine.Object source) {
            
            for (int i = 0; i < this.items.Length; ++i) {
                if (this.items[i].source == source) {
                    ref var item = ref this.items[i];
                    --item.references;
                    if (item.references == 0u) {
                        if (this.items.Length == 1) {
                            this.items = System.Array.Empty<Item>();
                        } else {
                            this.items[i] = this.items[this.items.Length - 1];
                            System.Array.Resize(ref this.items, this.items.Length - 1);
                        }

                        return true;
                    }
                }
            }

            return false;

        }

    }

}