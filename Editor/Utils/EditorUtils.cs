using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor {

    public static class EditorUtils {

        public struct AspectItem : System.IEquatable<AspectItem> {

            public string value;
            public System.Type type;
            public ComponentGroupItem.ComponentMetaInfo info;
            public int index;

            public AspectItem(ComponentGroupsXml.AspectItem data) {

                this.type = data.type != null ? System.Type.GetType(data.type) : null;
                this.value = UnityEditor.ObjectNames.NicifyVariableName(this.type != null ? this.type.Name : UNKNOWN_GROUP_NAME);
                this.index = data.id;
                this.info = new ComponentGroupItem.ComponentMetaInfo(this.type);
                this.info.editorComment = data.info.editorComment;
                this.info.defaultEditorComment = this.type?.GetCustomAttribute<EditorCommentAttribute>()?.comment;
                
            }

            public bool Equals(AspectItem other) {
                return Equals(this.type, other.type);
            }

            public override bool Equals(object obj) {
                return obj is AspectItem other && this.Equals(other);
            }

            public override int GetHashCode() {
                return (this.type != null ? this.type.GetHashCode() : 0);
            }

        }
        
        public struct ComponentGroupItem : System.IEquatable<ComponentGroupItem> {

            public class ComponentMetaInfo : System.IEquatable<ComponentMetaInfo> {

                public System.Type type;
                public UnityEditor.MonoScript file;
                public int lineNumber;
                public int columnNumber;
                public bool fileIsReady;
                public System.Action<bool> onFileReady;
                public bool isBuiltIn;
                public string editorComment;
                public string defaultEditorComment;

                public ComponentMetaInfo(System.Type type) {
                    this.type = type;
                    this.file = null;
                    this.isBuiltIn = type != null ? type.AssemblyQualifiedName.Contains("ME.BECS") : false;
                    if (this.isBuiltIn == false) {
                        EditorUtils.FindComponentFromStructName(type.Name, type.Namespace, (x, lineNumber, columnNumber) => {
                            this.file = x;
                            this.lineNumber = lineNumber;
                            this.columnNumber = columnNumber;
                            this.fileIsReady = true;
                            this.onFileReady?.Invoke(this.file != null);
                        });
                    }
                }

                public string GetEditorComment() {
                    var result = string.IsNullOrEmpty(this.editorComment) == true ? this.defaultEditorComment : this.editorComment;
                    if (string.IsNullOrEmpty(result) == false) {
                        return $"<b>{this.type.Name}</b>\n{result}";
                    }

                    return string.Empty;
                }

                public FieldInfo[] GetFields() {
                    return this.type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                public string GetTooltip() {
                    return this.file != null ? this.file.name + ":" + this.lineNumber + ",col:" + this.columnNumber : string.Empty;
                }

                public bool Equals(ComponentMetaInfo other) {
                    if (other is null) {
                        return false;
                    }

                    if (ReferenceEquals(this, other)) {
                        return true;
                    }

                    return Equals(this.type, other.type);
                }

                public override bool Equals(object obj) {
                    if (obj is null) {
                        return false;
                    }

                    if (ReferenceEquals(this, obj)) {
                        return true;
                    }

                    if (obj.GetType() != this.GetType()) {
                        return false;
                    }

                    return this.Equals((ComponentMetaInfo)obj);
                }

                public override int GetHashCode() {
                    return (this.type != null ? this.type.GetHashCode() : 0);
                }

            }
            
            public string value;
            public System.Type type;
            public System.Collections.Generic.List<ComponentMetaInfo> components;
            public int index;
            public int order;

            public ComponentGroupItem(ComponentGroupsXml.Item data) {

                this.type = data.type != null ? System.Type.GetType(data.type) : null;
                this.value = UnityEditor.ObjectNames.NicifyVariableName(this.type != null ? this.type.Name : UNKNOWN_GROUP_NAME);
                this.index = data.id;
                this.order = this.type != null ? 1 : 100;
                this.components = EditorUtils.GetComponentsByGroup(this.type).Select(x => {
                    var meta = new ComponentMetaInfo(x);
                    meta.editorComment = data.components.FirstOrDefault(c => c.type == x.AssemblyQualifiedName)?.editorComment;
                    meta.defaultEditorComment = x.GetCustomAttribute<EditorCommentAttribute>()?.comment;
                    return meta;
                }).ToList();

            }

            public bool Equals(ComponentGroupItem other) {
                return Equals(this.type, other.type);
            }

            public override bool Equals(object obj) {
                return obj is ComponentGroupItem other && this.Equals(other);
            }

            public override int GetHashCode() {
                return (this.type != null ? this.type.GetHashCode() : 0);
            }

        }

        private static readonly string UNKNOWN_GROUP_NAME = "Ungrouped";

        [System.Serializable]
        [System.Xml.Serialization.XmlRoot("ComponentGroupsXml")]
        public class ComponentGroupsXml {

            [System.Serializable]
            public class Item {

                [System.Serializable]
                public class ComponentMeta {

                    public string type;
                    public string editorComment;

                }

                public int id;
                public string type;
                [System.Xml.Serialization.XmlArrayAttribute("Components")]
                [System.Xml.Serialization.XmlArrayItemAttribute("Component", typeof(ComponentMeta))]
                public ComponentMeta[] components;

            }

            [System.Serializable]
            public class AspectItem {

                public int id;
                public string type;
                public Item.ComponentMeta info;

            }

            public int nextId;
            [System.Xml.Serialization.XmlArrayAttribute("Items")]
            [System.Xml.Serialization.XmlArrayItemAttribute("Item", typeof(Item))]
            public Item[] items;

            public int nextAspectId;
            [System.Xml.Serialization.XmlArrayAttribute("Aspects")]
            [System.Xml.Serialization.XmlArrayItemAttribute("AspectItem", typeof(AspectItem))]
            public AspectItem[] aspectItems;

        }

        public static void LoadComponentGroups() {

            var dir = "ME.BECS.Cache";
            var file = "ComponentGroups.xml";
            var path = $"{dir}/{file}";
            if (System.IO.File.Exists(path) == true) {
                
                componentGroups = new System.Collections.Generic.List<ComponentGroupItem>();
                aspects = new System.Collections.Generic.List<AspectItem>();
                var ser = new System.Xml.Serialization.XmlSerializer(typeof(ComponentGroupsXml));
                using (var reader = System.Xml.XmlReader.Create(path))
                {
                    var data = (ComponentGroupsXml)ser.Deserialize(reader);
                    componentGroupsNextId = data.nextId;
                    if (data.items != null) {
                        foreach (var item in data.items) {

                            var elem = new ComponentGroupItem(item);
                            if (item.type != null && elem.type == null) {
                                // missing type
                            } else {
                                componentGroups.Add(elem);
                            }

                        }
                    }

                    aspectNextId = data.nextAspectId;
                    if (data.aspectItems != null) {
                        foreach (var item in data.aspectItems) {

                            var elem = new AspectItem(item);
                            if (item.type != null && elem.type == null) {
                                // missing type
                            } else {
                                aspects.Add(elem);
                            }

                        }
                    }
                }

            }

        }
        
        public static void SaveComponentGroups() {

            var dir = "ME.BECS.Cache";
            var file = "ComponentGroups.xml";
            var path = $"{dir}/{file}";
            if (System.IO.Directory.Exists(dir) == false) {
                System.IO.Directory.CreateDirectory(dir);
            }

            if (System.IO.File.Exists(path) == true) {
                System.IO.File.WriteAllText(path, string.Empty);
            }

            {
                var data = new ComponentGroupsXml();
                data.items = new ComponentGroupsXml.Item[componentGroups.Count];
                data.aspectItems = new ComponentGroupsXml.AspectItem[aspects.Count];
                data.nextId = componentGroupsNextId;
                var i = 0;
                foreach (var group in componentGroups) {
                    data.items[i] = new ComponentGroupsXml.Item() {
                        id = group.index,
                        type = group.type != null ? group.type.AssemblyQualifiedName : null,
                        components = group.components.Select(x => new ComponentGroupsXml.Item.ComponentMeta() { type = x.type.AssemblyQualifiedName, editorComment = x.editorComment }).ToArray(),
                    };
                    ++i;
                }
                data.nextAspectId = aspectNextId;
                i = 0;
                foreach (var group in aspects) {
                    data.aspectItems[i] = new ComponentGroupsXml.AspectItem() {
                        id = group.index,
                        type = group.type != null ? group.type.AssemblyQualifiedName : null,
                        info = new ComponentGroupsXml.Item.ComponentMeta() { editorComment = group.info.editorComment },
                    };
                    ++i;
                }
                var ser = new System.Xml.Serialization.XmlSerializer(typeof(ComponentGroupsXml));
                var writer = new System.Xml.XmlTextWriter(path, System.Text.Encoding.UTF8);
                writer.Formatting = System.Xml.Formatting.Indented;
                writer.Indentation = 4;
                ser.Serialize(writer, data);
                writer.Dispose();
                
            }

        }

        public static System.Collections.Generic.List<System.Type> GetComponentsByGroup(System.Type type) {

            var result = new System.Collections.Generic.List<System.Type>();
            if (type != null) {
                
                var asms = CodeGenerator.GetAssembliesInfo();
                var components = UnityEditor.TypeCache.GetTypesWithAttribute<ComponentGroupAttribute>();
                foreach (var component in components) {
                    
                    var asm = component.Assembly.GetName().Name;
                    var info = asms.FirstOrDefault(x => x.name == asm);
                    if (info.isEditor == true) continue;

                    var attr = component.GetCustomAttribute<ComponentGroupAttribute>();
                    if (attr != null && attr.groupType == type) {

                        result.Add(component);

                    }

                }

            } else {
                
                var asms = CodeGenerator.GetAssembliesInfo();
                var components = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponent>();
                foreach (var component in components) {
                    
                    var asm = component.Assembly.GetName().Name;
                    var info = asms.FirstOrDefault(x => x.name == asm);
                    if (info.isEditor == true) continue;
                    if (component.IsInterface == true) continue;

                    var attr = component.GetCustomAttribute<ComponentGroupAttribute>();
                    if (attr == null) {

                        result.Add(component);

                    }

                }
                
            }

            return result.OrderBy(x => x.FullName).ToList();

        }

        public static AspectItem GetAspect(System.Type type) {
            if (aspects == null) LoadComponentGroups();
            if (aspects == null) return default;
            return aspects.FirstOrDefault(x => x.type == type);
        }

        private static int aspectNextId;
        private static System.Collections.Generic.List<AspectItem> aspects;
        public static System.Collections.Generic.List<AspectItem> GetAspects() {
            
            if (aspects != null && aspects.Count > 0) return aspects;

            LoadComponentGroups();

            var groups = aspects != null ? new System.Collections.Generic.HashSet<AspectItem>(aspects) : new System.Collections.Generic.HashSet<AspectItem>();
            var asms = CodeGenerator.GetAssembliesInfo();
            var aspectsItems = UnityEditor.TypeCache.GetTypesDerivedFrom<IAspect>();
            var componentsList = new System.Collections.Generic.List<System.Type>(aspectsItems.Count);
            foreach (var component in aspectsItems) {
                
                var asm = component.Assembly.GetName().Name;
                var info = asms.FirstOrDefault(x => x.name == asm);
                if (info.isEditor == true) continue;
                if (component.IsInterface == true) continue;

                componentsList.Add(component);
            }

            foreach (var component in componentsList) {
                
                var group = new AspectItem() {
                    type = component,
                };
                if (groups.Contains(group) == false) {
                    group.value = UnityEditor.ObjectNames.NicifyVariableName(component.Name);
                    group.info = new ComponentGroupItem.ComponentMetaInfo(component);
                    group.index = ++aspectNextId;
                    groups.Add(group);
                }
                groups.TryGetValue(group, out group);
                var meta = new ComponentGroupItem.ComponentMetaInfo(component);
                if (group.info == null || group.info.editorComment != meta.editorComment) {
                    group.info = meta;
                    groups.Remove(group);
                    groups.Add(group);
                }
            }
            
            aspects = groups.ToList();
            SaveComponentGroups();
            return aspects;
            
        }

        private static int componentGroupsNextId;
        private static System.Collections.Generic.List<ComponentGroupItem> componentGroups;
        public static System.Collections.Generic.List<ComponentGroupItem> GetComponentGroups(bool withUnknownGroup = true) {

            if (componentGroups != null) {
                if (withUnknownGroup == false) return componentGroups.Where(x => x.type != null).ToList();
                return componentGroups;
            }

            LoadComponentGroups();
            
            var groups = componentGroups != null ? new System.Collections.Generic.HashSet<ComponentGroupItem>(componentGroups) : new System.Collections.Generic.HashSet<ComponentGroupItem>();
            var asms = CodeGenerator.GetAssembliesInfo();
            var components = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponent>();
            var componentsList = new System.Collections.Generic.List<System.Type>(components.Count);
            foreach (var component in components) {
                
                var asm = component.Assembly.GetName().Name;
                var info = asms.FirstOrDefault(x => x.name == asm);
                if (info.isEditor == true) continue;
                if (component.IsInterface == true) continue;

                componentsList.Add(component);
            }

            foreach (var component in componentsList) {
                
                var attr = component.GetCustomAttribute<ComponentGroupAttribute>();
                if (withUnknownGroup == false) {
                    if (attr == null) continue;
                }
                var group = new ComponentGroupItem() {
                    type = attr != null ? attr.groupType : null,
                };
                if (groups.Contains(group) == false) {
                    group.value = UnityEditor.ObjectNames.NicifyVariableName(attr != null ? attr.groupType.Name : UNKNOWN_GROUP_NAME);
                    if (attr != null) {
                        group.order = 1;
                    } else {
                        group.order = 100;
                    }
                    group.components = new System.Collections.Generic.List<ComponentGroupItem.ComponentMetaInfo>();
                    group.index = ++componentGroupsNextId;
                    groups.Add(group);
                }

                groups.TryGetValue(group, out group);
                var meta = new ComponentGroupItem.ComponentMetaInfo(component);
                if (group.components.Contains(meta) == false) {
                    group.components.Add(meta);
                }

            }
            
            var result = groups.OrderBy(x => x.order).ToList();
            SaveComponentGroups();
            componentGroups = result;
            if (withUnknownGroup == false) return componentGroups.Where(x => x.type != null).ToList();
            return componentGroups;

        }
        
        public static string GetEntityName(Ent ent) {
            return ent.ToString(withWorld: false);
        }

        public static string BytesToString(int bytes) {

            var postfix = "B";
            var value = (float)bytes;
            if (value / 1024f > 1f) {

                value /= 1024f;
                postfix = "KB";
                if (value / 1024f > 1f) {

                    value /= 1024f;
                    postfix = "MB";

                }

            }
            
            return value.ToString("0.#") + " " + postfix;

        }

        public static int BytesToInt(uint bytes, out byte cat) {

            cat = 0;
            var value = bytes;
            if (value % 1024 == 0) {

                value /= 1024;
                cat = 1;
                if (value % 1024 == 0) {

                    value /= 1024;
                    cat = 2;

                }

            }
            
            return (int)value;

        }

        public static uint IntToBytes(int val, int cat) {

            if (cat == 1) {
                return (uint)val * 1024;
            } else if (cat == 2) {
                return (uint)val * 1024 * 1024;
            }

            return (uint)val;

        }

        public static string GetComponentName(System.Type type) {
            if (type == null) return "<null>";
            return UnityEditor.ObjectNames.NicifyVariableName(type.Namespace?.Length > 0 ? type.FullName.Substring(type.Namespace.Length + 1) : type.Name);
        }

        public static string GetComponentNamespace(System.Type type) {
            if (type == null) return "<null>";
            return type.Namespace;
        }

        public static string GetComponentFullName(System.Type type) {
            if (type == null) return "<null>";
            return UnityEditor.ObjectNames.NicifyVariableName(type.FullName);
        }

        public static System.Type GetTypeFromPropertyField(string typeName, bool isType = false) {
            if (isType == true) return System.Type.GetType(typeName);
            if (typeName == string.Empty) return null;
            var splitIndex = typeName.IndexOf(' ');
            var assembly = System.Reflection.Assembly.Load(typeName.Substring(0, splitIndex));
            return assembly.GetType(typeName.Substring(splitIndex + 1));
        }

        public static void ShowPopup(UnityEngine.Rect popupPosition, System.Action<System.Type> onSelect, System.Type baseType, bool unmanagedTypes, bool runtimeAssembliesOnly, bool showNullElement = true) {

            var assembliesInfo = CodeGenerator.GetAssembliesInfo();

            System.Predicate<System.Type> filter = null;
            if (unmanagedTypes == true) {
                filter += type => {
                    if (type.IsValueType == false || ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.IsUnmanaged(type) == false) return false;
                    return true;
                };
            }

            if (runtimeAssembliesOnly == true) {
                filter += type => {
                    var asm = type.Assembly;
                    var name = asm.GetName().Name;
                    var found = false;
                    foreach (var asmInfo in assembliesInfo) {
                        if (asmInfo.name == name) {
                            if (asmInfo.isEditor == true) return false;
                            found = true;
                            break;
                        }
                    }
                    return found;
                };
            }
            var types = UnityEditor.TypeCache.GetTypesDerivedFrom(baseType).Append(baseType).ToArray();
            var arr = types.Where(p =>
                                      (p.IsPublic || p.IsNestedPublic) &&
                                      !p.IsAbstract &&
                                      !p.IsGenericType &&
                                      !ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.k_UnityObjectType.IsAssignableFrom(p) &&
                                      //System.Attribute.IsDefined(p, typeof(System.SerializableAttribute)) &&
                                      (filter == null || filter.GetInvocationList().All(x => ((System.Predicate<System.Type>)x).Invoke(p)) == true));
            ShowPopup(popupPosition, onSelect, arr.ToArray(), showNullElement);
            
        }
        
        public static void ShowPopup(UnityEngine.Rect popupPosition, System.Action<System.Type> onSelect, System.Type[] types, bool showNullElement = true) {
            
            var state = new UnityEditor.IMGUI.Controls.AdvancedDropdownState();
            var popup = new ME.BECS.Editor.Extensions.SubclassSelector.AdvancedTypePopup(types,
                ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.k_MaxTypePopupLineCount,
                state,
                showNullElement,
                new UnityEngine.Vector2(200f, 0f)
            );
            popup.OnItemSelected += item => {
                var type = item.Type;
                onSelect.Invoke(type);
            };
            
            popup.Show(popupPosition);

        }

        private static readonly string[] searchPaths = new[] {
            "Packages/com.ME.BECS/",
            "Packages/com.me.becs/",
            "Assets/BECS/",
            "Assets/ME.BECS/",
            "Assets/ME.BECS-submodule/",
            "Assets/BECS-submodule/",
            "Assets/becs-submodule/",
            "Assets/ECS/",
            "Assets/",
        };
        
        public static T LoadResource<T>(string path, bool isRequired = true) where T : UnityEngine.Object {
            
            foreach (var searchPath in searchPaths) {

                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"{searchPath}Editor/EditorResources/{path}");
                if (data != null) return data;

            }

            foreach (var searchPath in searchPaths) {

                var rootDir = $"{searchPath}Addons";
                if (System.IO.Directory.Exists(rootDir) == true) {
                    var dirs = System.IO.Directory.GetDirectories(rootDir);
                    foreach (var dir in dirs) {

                        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"{dir}/Editor/EditorResources/{path}");
                        if (data != null) return data;

                    }
                }

            }

            var paths = path.Split('/');
            {
                var dirPath = GetDirGUID("Assets", paths);
                if (dirPath != null) {

                    var obj = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(dirPath));
                    if (obj != null) return obj;

                }
            }

            {
                var fileNameWithoutExtension = System.IO.Path.GetDirectoryName(path) + "/" + System.IO.Path.GetFileNameWithoutExtension(path);
                var obj = UnityEngine.Resources.Load<T>(fileNameWithoutExtension);
                if (obj != null) return obj;
            }

            if (isRequired == true) {

                throw new System.IO.FileNotFoundException($"Could not find editor resource {path} of type {typeof(T)} (resource path: {System.IO.Path.GetDirectoryName(path) + "/" + System.IO.Path.GetFileNameWithoutExtension(path)})");

            }
            
            return null;
            
        }

        private static string GetDirGUID(string rootDir, string[] paths, int index = 0, string guid = null) {

            if (index >= paths.Length) return guid;
            
            //rootDir = rootDir.TrimEnd('/');
            var srcDir = paths[index];
            /*var dirs = System.IO.Directory.GetDirectories(rootDir);
            var directories = new System.Collections.Generic.List<string>();
            var filter = System.IO.Path.GetFileNameWithoutExtension(srcDir);
            foreach (var dir in dirs) {
                if (dir == filter) directories.Add(dir);
            }*/
            var directories = UnityEditor.AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(srcDir), new string[] { rootDir });
            foreach (var dirGuid in directories) {

                var nextDir = UnityEditor.AssetDatabase.GUIDToAssetPath(dirGuid);
                var d = GetDirGUID(nextDir, paths, index + 1, dirGuid);
                if (d != null) {
                    return d;
                }

            }
            
            return null;
            
        }

        public struct AspectItemTypeInfo {

            public System.Type fieldType;
            public bool required;
            public bool config;

        }
        
        public static AspectItemTypeInfo[] GetAspectTypes(System.Type type) {

            var result = new System.Collections.Generic.List<AspectItemTypeInfo>();
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields) {

                if (typeof(IAspectData).IsAssignableFrom(field.FieldType) == true) {

                    var required = field.GetCustomAttribute<QueryWithAttribute>() != null;
                    var itemType = field.FieldType.GetGenericArguments()[0];
                    result.Add(new AspectItemTypeInfo() {
                        fieldType = itemType,
                        required = required,
                        config = typeof(IConfigComponent).IsAssignableFrom(itemType),
                    });
                            
                }
                        
            }

            return result.OrderByDescending(x => x.required).ThenByDescending(x => x.config).ToArray();

        }

        public static bool TryGetComponentGroupColor(System.Type componentType, out UnityEngine.Color color) {

            color = default;
            if (componentType == null) return false;
            var componentGroupAttribute = componentType.GetCustomAttribute<ComponentGroupAttribute>();
            if (componentGroupAttribute != null) {
                return TryGetGroupColor(componentGroupAttribute.groupType, out color);
            }

            return false;

        }

        public static bool TryGetGroupColor(System.Type groupType, out UnityEngine.Color color) {

            color = default;
            if (groupType == null) return false;
            var field = groupType.GetField("color", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) {
                color = (UnityEngine.Color)field.GetValue(null);
                return true;
            }

            return false;

        }

        public struct ScriptMetaInfo {

            public string text;
            public string name;
            public UnityEditor.MonoScript script;

        }
        
        private static System.Collections.Generic.List<ScriptMetaInfo> scriptsMetaInfo;
        
        public static void FindComponentFromStructName(string structName, string @namespace, System.Action<UnityEditor.MonoScript, int, int> callback) {

            if (scriptsMetaInfo == null) {

                var scriptGUIDs = UnityEditor.AssetDatabase.FindAssets($"t:script");
                if (scriptGUIDs.Length == 0) return;

                scriptsMetaInfo = new System.Collections.Generic.List<ScriptMetaInfo>(scriptGUIDs.Length);
                foreach (var scriptGUID in scriptGUIDs) {
                    var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(scriptGUID);
                    var script = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(assetPath);
                    if (script != null) {
                        scriptsMetaInfo.Add(new ScriptMetaInfo() { text = script.text, name = script.name, script = script, });
                    }
                }

            }

            System.Threading.ThreadPool.QueueUserWorkItem((_) => {

                for (var index = 0; index < scriptsMetaInfo.Count; ++index) {
                    var script = scriptsMetaInfo[index];
                    if ((string.IsNullOrEmpty(@namespace) == true ||
                         System.Text.RegularExpressions.Regex.IsMatch(script.text, @$"namespace\s+{@namespace}", System.Text.RegularExpressions.RegexOptions.Singleline) == true)) {

                        var match = System.Text.RegularExpressions.Regex.Match(script.text, @$"struct\s+{structName}", System.Text.RegularExpressions.RegexOptions.Singleline);
                        if (match.Groups.Count > 0 && match.Groups[0].Success == true) {
                            
                            var pos = LineFromPos(script.text, match.Index, out var columnNumber);
                            callback.Invoke(script.script, pos, columnNumber + 7);
                            return;

                        }
                    }
                }
            });

        }
        
        public static int LineFromPos(string input, int indexPosition, out int columnNumber) {
            var lineNumber = 1;
            var lastIndex = 0;
            for (int i = 0; i < indexPosition; ++i) {
                if (input[i] == '\n') {
                    ++lineNumber;
                    lastIndex = i;
                }
            }
            columnNumber = indexPosition - lastIndex;
            return lineNumber;
        }

        public static bool UpdateComponentScript(ComponentGroupItem.ComponentMetaInfo component, System.Type type, bool state) {

            var text = component.file.text;
            var path = UnityEditor.AssetDatabase.GetAssetPath(component.file);
            var interfaceType = type.Name;
            if (state == true) {
                // Add new interface if not exist
                var match = System.Text.RegularExpressions.Regex.Match(text, @"struct\s+" + component.type.Name + @"\s*:\s*(.*?)\s*{");
                if (match.Success == true && match.Groups.Count == 2) {
                    var interfaces = match.Groups[1].Value.Trim().Split(',', System.StringSplitOptions.RemoveEmptyEntries).ToList();
                    var found = false;
                    foreach (var interfaceItem in interfaces) {
                        if (interfaceItem == interfaceType) {
                            found = true;
                            break;
                        }
                    }

                    if (found == true) {
                        // interface already added
                        return false;
                    }

                    interfaces.Add(" " + interfaceType);
                    var str = string.Join(",", interfaces).Trim();
                    var regex = new System.Text.RegularExpressions.Regex(@"(struct\s+" + component.type.Name + @"\s*:\s*)(.*?)(\s*{)");
                    var result = regex.Replace(text, (match) => {
                        return match.Groups[1].Value + str + match.Groups[3].Value;
                    });
                    System.IO.File.WriteAllText(path, result);
                    return true;
                }
            } else {
                // Remove interface if it exists
                var match = System.Text.RegularExpressions.Regex.Match(text, @"struct\s+" + component.type.Name + @"\s*:\s*(.*?)\s*{");
                if (match.Success == true && match.Groups.Count == 2) {
                    var interfaces = match.Groups[1].Value.Trim().Split(',', System.StringSplitOptions.RemoveEmptyEntries).ToList();
                    var found = false;
                    foreach (var interfaceItem in interfaces) {
                        if (interfaceItem == interfaceType) {
                            found = true;
                            interfaces.Remove(interfaceType);
                            break;
                        }
                    }

                    if (found == true) {
                        if (interfaces.Count == 0) interfaces.Add(nameof(IComponent));
                        var str = string.Join(",", interfaces).Trim();
                        var regex = new System.Text.RegularExpressions.Regex(@"(struct\s+" + component.type.Name + @"\s*:\s*)(.*?)(\s*{)");
                        var result = regex.Replace(text, (match) => { return match.Groups[1].Value + str + match.Groups[3].Value; });
                        System.IO.File.WriteAllText(path, result);
                        return true;
                    }
                }
            }

            return false;

        }

        public static CodeGenerator.AssemblyInfo GetAssemblyInfo(ME.BECS.Extensions.GraphProcessor.BaseGraph graph) {
            var path = UnityEditor.AssetDatabase.GetAssetPath(graph);
            path = path.Replace("\\", "/");
            var splitted = path.Split('/');
            for (int i = splitted.Length - 1; i >= 0; --i) {
                var dir = string.Join("/", splitted, 0, i);
                var asms = UnityEditor.AssetDatabase.FindAssets("t:asmdef", new string[] { dir });
                foreach (var guid in asms) {
                    var p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var info = System.IO.File.ReadAllText(p);
                    return UnityEngine.JsonUtility.FromJson<CodeGenerator.AssemblyInfo>(info).Init();
                }
            }

            return default;
        }

        public static string GetCodeName(string name) {
            name = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z]", "");
            return name;
        }

        public static string GetTypeName(System.Type type) {
            if (type.IsGenericType == true) {
                var first = type.FullName.Split('[')[0].Replace("+", ".").Replace("`1", "");
                return $"{first}<{GetTypeName(type.GenericTypeArguments[0])}>";
            }
            return type.FullName.Replace("+", ".").Replace("`1", "");
        }

        public static string GetDataTypeName(System.Type type) {
            return type.Namespace + "." + type.Name.Replace("+", ".").Replace("`1", "");
        }

        public static string FormatCode(string[] content, int indentSize = 4, int defaultIndent = 2) {

            var result = new System.Text.StringBuilder(content.Length * 256);
            var indent = defaultIndent;
            for (int i = 0; i < content.Length; ++i) {
                var line = content[i];
                var open = line.Contains('{');
                var close = line.Contains('}');
                if (close == true) --indent;
                for (int j = 0; j < indent; ++j) {
                    result.Append(' ', indentSize);
                }
                result.Append(line);
                result.Append('\n');
                if (open == true) ++indent;
            }
            
            return result.ToString();

        }

        public static string ReFormatCode(string text) {

            text = new System.Text.RegularExpressions.Regex(@"^[^\S\n]+(.+?)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline).Replace(text, "$1");
            return FormatCode(text.Split("\n"), defaultIndent: 0);

        }

    }

}