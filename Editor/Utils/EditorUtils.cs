using System.Linq;

namespace ME.BECS.Editor {

    public static class EditorUtils {

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
            return UnityEditor.ObjectNames.NicifyVariableName(type.Name);
        }

        public static System.Type GetTypeFromPropertyField(string typeName, bool isType = false) {
            if (isType == true) return System.Type.GetType(typeName);
            if (typeName == string.Empty) return null;
            var splitIndex = typeName.IndexOf(' ');
            var assembly = System.Reflection.Assembly.Load(typeName.Substring(0, splitIndex));
            return assembly.GetType(typeName.Substring(splitIndex + 1));
        }

        public static void ShowPopup(UnityEngine.Rect popupPosition, System.Action<System.Type> onSelect, System.Type baseType, bool unmanagedTypes, bool runtimeAssembliesOnly, bool showNullElement = true) {
            var state = new UnityEditor.IMGUI.Controls.AdvancedDropdownState();
            
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
            var arr = UnityEditor.TypeCache.GetTypesDerivedFrom(baseType).Append(baseType);
            var popup = new ME.BECS.Editor.Extensions.SubclassSelector.AdvancedTypePopup(
                arr.Where(p =>
                              (p.IsPublic || p.IsNestedPublic) &&
                              !p.IsAbstract &&
                              !p.IsGenericType &&
                              !ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.k_UnityObjectType.IsAssignableFrom(p) &&
                              //System.Attribute.IsDefined(p, typeof(System.SerializableAttribute)) &&
                              (filter == null || filter.GetInvocationList().All(x => ((System.Predicate<System.Type>)x).Invoke(p)) == true)
                ),
                ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.k_MaxTypePopupLineCount,
                state,
                showNullElement
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
            foreach (var searchPath in searchPaths) {

                var dirPath = GetDirGUID(searchPath, paths);
                if (dirPath != null) {

                    return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(dirPath));

                }

            }

            if (isRequired == true) {

                throw new System.IO.FileNotFoundException($"Could not find editor resource {path} of type {typeof(T)}");

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

    }

}