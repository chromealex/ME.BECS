namespace ME.BECS.Views {

    public class ViewsRegistryData : UnityEngine.ScriptableObject {

        [System.Serializable]
        public struct Item {

            public EntityView prefab;
            public uint prefabId;
            public uint references;

        }
        
        public uint prefabId;
        public Item[] items = System.Array.Empty<Item>();

        public EntityView GetEntityViewByPrefabId(uint prefabId) {

            for (int i = 0; i < this.items.Length; ++i) {
                if (this.items[i].prefabId == prefabId) {
                    return this.items[i].prefab;
                }
            }

            return null;

        }

        public uint Add(EntityView prefab, out bool isNew) {

            isNew = false;
            if (prefab == null) return 0u;
            
            for (int i = 0; i < this.items.Length; ++i) {
                if (this.items[i].prefab == prefab) {
                    ref var item = ref this.items[i];
                    ++item.references;
                    return item.prefabId;
                }
            }

            isNew = true;
            {
                // Add new item
                var item = new Item() {
                    prefabId = ++this.prefabId,
                    prefab = prefab,
                    references = 1,
                };
                System.Array.Resize(ref this.items, this.items.Length + 1);
                this.items[this.items.Length - 1] = item;
                return this.prefabId;
            }

        }

        public bool Remove(EntityView prefab) {
            
            for (int i = 0; i < this.items.Length; ++i) {
                if (this.items[i].prefab == prefab) {
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