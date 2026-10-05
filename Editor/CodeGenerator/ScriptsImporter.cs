namespace ME.BECS.Editor {

    // Read-only compatibility reader for the deferred CopyFrom investigation.
    // Production source-input export does not use this index. In particular, this
    // type must not run on imports or parse scripts/update the old cache anymore.
    public static class ScriptsImporter {

        private const string FILENAME = "ImportCache.cache";

        [System.Serializable]
        public struct Data {

            [System.Serializable]
            public struct Item : System.IEquatable<Item> {

                public string ns;
                public string className;
                public string type;
                public string[] assetPath;

                public bool Equals(Item other) {
                    return this.ns == other.ns && this.className == other.className && this.type == other.type;
                }

                public bool PathsEquals(string[] other) {
                    if (other.Length != this.assetPath.Length) return false;
                    for (var i = 0; i < this.assetPath.Length; i++) {
                        if (other[i] != this.assetPath[i]) return false;
                    }
                    return true;
                }

                public override bool Equals(object obj) {
                    return obj is Item other && this.Equals(other);
                }

                public override int GetHashCode() {
                    return System.HashCode.Combine(this.ns, this.className, this.type);
                }

                public string GetKey() {
                    return this.type;
                }

            }

            public System.Collections.Generic.List<Item> items;

        }

        private static Data data;
        private static readonly System.Collections.Generic.Dictionary<string, Data.Item> cache = new System.Collections.Generic.Dictionary<string, Data.Item>();

        private static Data LoadData() {
            var dir = $"{CodeGenerator.ECS}.Cache";
            var path = $"{dir}/{FILENAME}";
            if (System.IO.File.Exists(path) == true) {
                var data = System.IO.File.ReadAllText(path);
                return UnityEngine.JsonUtility.FromJson<Data>(data);
            }
            return default;
        }
        
        public static string[] FindScript(System.Type type) {
            if (data.items == null) {
                data = LoadData();
                cache.Clear();
                if (data.items != null) {
                    foreach (var item in data.items) {
                        string s = item.GetKey();
                        if (string.IsNullOrEmpty(s) == false) cache.Add(s, item);
                    }
                }
            }
            if (data.items == null) return null;
            var key = type.AssemblyQualifiedName;
            cache.TryGetValue(key, out var elem);
            return elem.assetPath;
        }

    }

}
