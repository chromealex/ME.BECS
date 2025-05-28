using System.Linq;

namespace ME.BECS.Editor {

    public class ScriptsImporter : UnityEditor.AssetPostprocessor {

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
        
        private static void SaveData(Data data) {
            data.items = data.items.OrderBy(x => x.type).ThenBy(x => x.className).ToList();
            var dir = $"{CodeGenerator.ECS}.Cache";
            var path = $"{dir}/{FILENAME}";
            System.IO.File.WriteAllText(path, UnityEngine.JsonUtility.ToJson(data, true));
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {

            var isDirty = false;
            if (movedAssets != null && movedAssets.Length > 0) {

                data = LoadData();

                for (var index = 0; index < movedAssets.Length; ++index) {
                    var assetPath = movedAssets[index];
                    if (assetPath.EndsWith(".cs") == true) {
                        var fromPath = movedFromAssetPaths[index];
                        for (int i = data.items.Count - 1; i >= 0; --i) {
                            var item = data.items[i];
                            var idx = System.Array.IndexOf(item.assetPath, fromPath);
                            if (idx >= 0) {
                                item.assetPath[idx] = assetPath;
                                data.items[i] = item;
                                isDirty = true;
                            }
                        }
                    }
                }

                if (isDirty == true) {
                    SaveData(data);
                    data = default;
                    isDirty = false;
                }

            }
            if (deletedAssets != null && deletedAssets.Length > 0) {

                data = LoadData();

                foreach (var assetPath in deletedAssets) {
                    if (assetPath.EndsWith(".cs") == true) {
                        if (DeleteByPath(assetPath) == true) isDirty = true;
                    }
                }

                if (isDirty == true) {
                    SaveData(data);
                    data = default;
                    isDirty = false;
                }

            }
            if (importedAssets != null && importedAssets.Length > 0) {

                data = LoadData();

                foreach (var assetPath in importedAssets) {
                    if (assetPath.EndsWith(".cs") == true &&
                        assetPath.Contains("ME.BECS.Gen") == false) {
                        var script = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(assetPath);
                        if (script != null) {
                            {
                                var classNames = GetClassNames(script.text);
                                DeleteByPath(assetPath);
                                foreach (var className in classNames) {
                                    AddClassName(className.ns, className.name, assetPath);
                                    isDirty = true;
                                }
                                /*var matches = System.Text.RegularExpressions.Regex.Matches(script.text, @$"\b(class|struct)\b\s+(.*?)\s*{{",
                                                                                           System.Text.RegularExpressions.RegexOptions.Singleline);
                                for (int i = 0; i < matches.Count; ++i) {
                                    var className = matches[i].Groups[1].Value;
                                    var nsMatches = System.Text.RegularExpressions.Regex.Matches(script.text, @$"namespace\s+(.*?)\s*[;|{{].*?\b(class|struct)\b\s+{className}\s*{{",
                                                                                                 System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.RightToLeft);
                                    var ns = string.Empty;
                                    if (nsMatches.Count > 0) {
                                        ns = nsMatches[0].Groups[1].Value;
                                        var m = System.Text.RegularExpressions.Regex.Matches(nsMatches[0].Groups[1].Value, @$"(.*?)\s*[;|{{]",
                                                                                            System.Text.RegularExpressions.RegexOptions.Singleline);
                                        if (m.Count > 0) ns = m[0].Groups[1].Value;
                                    }
                                    AddClassName(ns, className, assetPath);
                                    isDirty = true;
                                }*/
                            }
                        }
                    }
                }

            }

            if (isDirty == true) {
                SaveData(data);
                data = default;
                isDirty = false;
            }

        }

        private static bool DeleteByPath(string assetPath) {
            var isDirty = false;
            if (data.items == null) return false;
            for (int i = data.items.Count - 1; i >= 0; --i) {
                var item = data.items[i];
                var idx = System.Array.IndexOf(item.assetPath, assetPath);
                if (idx >= 0) {
                    if (item.assetPath.Length == 1) {
                        data.items.RemoveAt(i);
                    } else {
                        item.assetPath[idx] = item.assetPath[^1];
                        System.Array.Resize(ref item.assetPath, item.assetPath.Length - 1);
                        data.items[i] = item;
                    }
                    isDirty = true;
                }
            }
            return isDirty;
        }

        private struct TypeInfo {

            public string name;
            public string ns;
            public int nestingLevel;

        }

        private static TypeInfo[] GetClassNames(string text) {
            var namespaceStack = new System.Collections.Generic.Stack<string>();
            var typeStack = new System.Collections.Generic.Stack<string>();
            var foundClasses = new System.Collections.Generic.List<TypeInfo>();
            var braceStack = new System.Collections.Generic.Stack<int>();

            var namespaceRegex = new System.Text.RegularExpressions.Regex(@"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)");
            var typeRegex = new System.Text.RegularExpressions.Regex(@"\b(class|struct)\s+([A-Za-z_][A-Za-z0-9_]*)\b");

            var braceDepth = 0;

            var lines = text.Split('\n');
            foreach (var line in lines) {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("//")) {
                    continue;
                }

                var nsMatch = namespaceRegex.Match(trimmedLine);
                if (nsMatch.Success == true) {
                    namespaceStack.Push(nsMatch.Groups[1].Value);
                }

                var typeMatch = typeRegex.Match(trimmedLine);
                if (typeMatch.Success == true) {
                    //var kind = typeMatch.Groups[1].Value;
                    var name = typeMatch.Groups[2].Value;

                    typeStack.Push(name);
                    braceStack.Push(braceDepth);

                    var fullParts = new System.Collections.Generic.List<string>();
                    if (namespaceStack.Count > 0) {
                        fullParts.AddRange(namespaceStack);
                    }
                    fullParts.AddRange(typeStack.Reverse());

                    foundClasses.Add(new TypeInfo {
                        ns = string.Join(".", namespaceStack),
                        name = GetParts(fullParts),
                        nestingLevel = typeStack.Count,
                    });
                }

                braceDepth += CountChar(trimmedLine, '{');
                braceDepth -= CountChar(trimmedLine, '}');

                while (braceStack.Count > 0 && braceStack.Peek() >= braceDepth) {
                    braceStack.Pop();
                    typeStack.Pop();
                }
            }

            return foundClasses.ToArray();

            static string GetParts(System.Collections.Generic.List<string> list) {
                var str = new System.Text.StringBuilder();
                for (var index = 0; index < list.Count; ++index) {
                    var item = list[index];
                    if (index == list.Count - 1) {
                        str.Append(item);
                    } else {
                        if (index > 0) {
                            str.Append(item);
                            str.Append("+");
                        } else {
                            str.Append(item);
                            str.Append(".");
                        }
                    }
                }
                return str.ToString();
            }
            
            static int CountChar(string line, char c) {
                var count = 0;
                foreach (var ch in line) {
                    if (ch == c) {
                        count++;
                    }
                }

                return count;
            }

        }

        private static void AddClassName(string ns, string className, string assetPath) {
            var type = System.Type.GetType(className);
            if (type == null) {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
                    type = asm.GetType(className);
                    if (type != null) break;
                }
            }
            if (data.items == null) data.items = new System.Collections.Generic.List<Data.Item>();
            var item = new Data.Item() {
                ns = ns,
                className = className,
                type = type != null ? type.AssemblyQualifiedName : null,
            };
            var found = false;
            for (int i = 0; i < data.items.Count; ++i) {
                var elem = data.items[i];
                if (elem.Equals(item) == true) {
                    var idx = System.Array.IndexOf(elem.assetPath, assetPath);
                    if (idx < 0) {
                        System.Array.Resize(ref elem.assetPath, elem.assetPath.Length + 1);
                        elem.assetPath[^1] = assetPath;
                    }
                    data.items[i] = elem;
                    found = true;
                }
            }
            if (found == false) {
                item.assetPath = new[] {assetPath};
                data.items.Add(item);
            }
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