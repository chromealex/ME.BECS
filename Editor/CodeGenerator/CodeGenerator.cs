using System.Reflection;

namespace ME.BECS.Editor {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using System.Linq;

    public abstract class CustomCodeGenerator {

        public System.Collections.Generic.List<CodeGenerator.AssemblyInfo> asms;
        public bool editorAssembly;
        
        protected bool IsValidTypeForAssembly(System.Type type) {
            
            var asm = type.Assembly.GetName().Name;
            var info = this.asms.FirstOrDefault(x => x.name == asm);
            if (this.editorAssembly == false && info.isEditor == true) return false;
            if (this.editorAssembly == true && info.isEditor == false) return false;
            return true;

        }
        
        public virtual void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
        }

        public virtual CodeGenerator.MethodDefinition AddMethod(System.Collections.Generic.List<System.Type> references) {
            return new CodeGenerator.MethodDefinition();
        }

        public static string GetTypeName(System.Type type) {
            return type.FullName.Replace("+", ".").Replace("`1", "");
        }

        public static string GetDataTypeName(System.Type type) {
            return type.Namespace + "." + type.Name.Replace("+", ".").Replace("`1", "");
        }

    }
    
    public static class CodeGenerator {

        public struct MethodDefinition {

            public string methodName;
            public string type;
            public string definition;
            public string content;

        }
        
        public struct AssemblyInfo {

            public string name;
            public string[] includePlatforms;
            public string[] references;
            public bool isEditor;

            public AssemblyInfo Init() {

                this.isEditor = false;
                if (this.includePlatforms != null) {
                    var hasEditor = System.Array.IndexOf(this.includePlatforms, "Editor") >= 0;
                    this.isEditor = hasEditor == true && this.includePlatforms.Length == 1;
                }

                if (this.references != null) {
                    for (int i = 0; i < this.references.Length; ++i) {
                        ref var r = ref this.references[i];
                        if (r.StartsWith("GUID:") == true) {
                            var asmName = UnityEditor.AssetDatabase.GUIDToAssetPath(r.Substring(5, r.Length - 5));
                            if (string.IsNullOrEmpty(asmName) == false) {
                                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(asmName);
                                if (asset != null) {
                                    var txt = asset.name;
                                    r = txt;
                                }
                            }
                        }
                    }
                }

                return this;

            }

            public bool HasReference(string asm) {
                return System.Array.IndexOf(this.references, asm) >= 0;
            }

        }
        
        public const string ECS = "ME.BECS";
        public const string AWAKE_METHOD = "BurstCompileOnAwake";
        public const string UPDATE_METHOD = "BurstCompileOnUpdate";
        public const string DESTROY_METHOD = "BurstCompileOnDestroy";

        private static System.Collections.Generic.List<AssemblyInfo> loadedAssemblies;
        public static System.Collections.Generic.List<AssemblyInfo> GetAssembliesInfo() {
            if (loadedAssemblies == null) {
                var list = new System.Collections.Generic.List<AssemblyInfo>();
                var asmdefs = UnityEditor.AssetDatabase.FindAssets("t:asmdef");
                foreach (var guid in asmdefs) {
                    var asmPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var info = System.IO.File.ReadAllText(asmPath);
                    list.Add(UnityEngine.JsonUtility.FromJson<AssemblyInfo>(info).Init());
                }

                loadedAssemblies = list;
            }

            return loadedAssemblies;
        }
        
        static CodeGenerator() {
            
            UnityEngine.Application.logMessageReceived += OnLogAdded;
            UnityEngine.Application.logMessageReceivedThreaded += OnLogAdded;

        }

        [UnityEditor.Callbacks.DidReloadScripts]
        public static void OnScriptsReload() {
            
            UnityEngine.Application.logMessageReceived += OnLogAdded;
            UnityEngine.Application.logMessageReceivedThreaded += OnLogAdded;

            RegenerateBurstAOT();
            
        }

        public struct VariantInfo {

            public System.Collections.Generic.KeyValuePair<string, string>[] variables;
            public string filenamePostfix;

        }

        public static void GenerateComponentsParallelFor() {

            var variables = new System.Collections.Generic.Dictionary<string, string>() {
                { "inref", "ref" },
                { "RWRO", "RW" },
            };
            var postfixes = new VariantInfo[] {
                new VariantInfo() {
                    filenamePostfix = ".ref",
                    variables = new [] {
                        new System.Collections.Generic.KeyValuePair<string, string>("inref", "ref"),
                        new System.Collections.Generic.KeyValuePair<string, string>("RWRO", "RW"),
                    },
                },
            };
            var templates = UnityEditor.AssetDatabase.FindAssets("t:TextAsset .Tpl");
            foreach (var guid in templates) {

                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var dir = System.IO.Path.GetDirectoryName(path);

                var text = System.IO.File.ReadAllText(path);
                foreach (var postfix in postfixes) {
                    foreach (var key in postfix.variables) {
                        variables[key.Key] = key.Value;
                    }
                    var maxCount = 10;
                    for (int i = 1; i < maxCount; ++i) {
                        var keys = new System.Collections.Generic.Dictionary<string, int>() {
                            { "count", i },
                        };
                        var filePath = dir + System.IO.Path.DirectorySeparatorChar + System.IO.Path.GetFileName(path).Replace(".Tpl.txt", $"{i}{postfix.filenamePostfix}.cs");
                        
                        var tpl = new Tpl(text);
                        System.IO.File.WriteAllText(filePath, tpl.GetString(keys, variables));
                        UnityEditor.AssetDatabase.ImportAsset(filePath);
                    }
                }

            }
            
            /*
            var text = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/BECS/Runtime/Jobs/Components/Jobs.ComponentsParallelFor.Tpl.txt").text;
            var result = "";
            var tpl = new Tpl(text);
            UnityEngine.Debug.Log(tpl.GetString(new System.Collections.Generic.Dictionary<string, int>() {
                { "count", 10 },
            }));
            */

        }

        public static void RegenerateBurstAOT() {

            var list = GetAssembliesInfo();
            {
                var dir = $"Assets/{ECS}.BurstHelper/Runtime";
                Build(list, dir);
            }
            {
                var dir = $"Assets/{ECS}.BurstHelper/Editor";
                Build(list, dir, editorAssembly: true);
            }
            
        }

        private static bool HasComponentCustomSharedHash(System.Type type) {

            var m = type.GetMethod(nameof(IComponentShared.GetHash), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) {
                var hasMethod = type.GetInterfaceMap(typeof(IComponentShared)).TargetMethods.Any(m => m.IsPrivate == true && m.Name == typeof(IComponentShared).FullName + "." + nameof(IComponentShared.GetHash));
                return hasMethod;
            }
            return true;

        }

        private static bool IsTagType(System.Type type) {

            if (System.Runtime.InteropServices.Marshal.SizeOf(type) <= 1 &&
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0) {
                return true;
            }
            
            return false;
            
        }

        private static void OnLogAdded(string condition, string stackTrace, UnityEngine.LogType type) {

            if (type == UnityEngine.LogType.Exception ||
                type == UnityEngine.LogType.Error) {
                if (condition.Contains($"{ECS}.BurstHelper.cs") == true ||
                    stackTrace.Contains($"{ECS}.BurstHelper.cs") == true) {
                    if (condition.Contains("does not exist in the namespace") == true) {
                        // Remove files
                        {
                            var dir = $"Assets/{ECS}.BurstHelper/Runtime";
                            var path = @$"{dir}/{ECS}.BurstHelper.cs";
                            UnityEditor.AssetDatabase.DeleteAsset(path);
                        }
                        {
                            var dir = $"Assets/{ECS}.BurstHelper/Editor";
                            var path = @$"{dir}/{ECS}.BurstHelper.cs";
                            UnityEditor.AssetDatabase.DeleteAsset(path);
                        }
                    }
                }
            }
            
        }

        private static void Build(System.Collections.Generic.List<AssemblyInfo> asms, string dir, bool editorAssembly = false) {

            var postfix = string.Empty;
            if (editorAssembly == true) {
                postfix = "Editor";
            } else {
                postfix = "Runtime";
            }

            var customCodeGenerators = UnityEditor.TypeCache.GetTypesDerivedFrom<CustomCodeGenerator>();
            var generators = customCodeGenerators.Select(x => (CustomCodeGenerator)System.Activator.CreateInstance(x)).ToArray();
            
            if (System.IO.Directory.Exists(dir) == false) {
                System.IO.Directory.CreateDirectory(dir);
            }
            
            var componentTypes = new System.Collections.Generic.List<System.Type>();
            {
                var path = @$"{dir}/{ECS}.BurstHelper.cs";
                var template = EditorUtils.LoadResource<UnityEngine.TextAsset>("ME.BECS.Resources/Templates/Types-Template.txt").text;
                //var template = "namespace " + ECS + " {\n [UnityEngine.Scripting.PreserveAttribute] public static unsafe class AOTBurstHelper { \n[UnityEngine.Scripting.PreserveAttribute] \npublic static void AOT() { \n{{CONTENT}} \n}\n }\n }";
                var content = new System.Collections.Generic.List<string>();
                var typesContent = new System.Collections.Generic.List<string>();
                var types = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ISystem));
                var burstedTypes = UnityEditor.TypeCache.GetTypesWithAttribute<BURST>();
                var burstDiscardedTypes = UnityEditor.TypeCache.GetMethodsWithAttribute<WithoutBurstAttribute>();
                var typesAwake = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IAwake));
                var typesUpdate = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IUpdate));
                var typesDestroy = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IDestroy));
                foreach (var type in types) {

                    if (type.IsValueType == false) continue;
                    var asm = type.Assembly;
                    var name = asm.GetName().Name;
                    var info = asms.FirstOrDefault(x => x.name == name);
                    if (editorAssembly == false && info.isEditor == true) continue;

                    if (type.IsVisible == false) continue;
                    
                    var systemType = type.FullName.Replace("+", ".");
                    content.Add($"StaticSystemTypes<{systemType}>.Validate();");
                    typesContent.Add($"StaticSystemTypes<{systemType}>.Validate();");

                    var isBursted = (burstedTypes.Contains(type) == true);
                    var hasAwake = typesAwake.Contains(type);
                    var hasUpdate = typesUpdate.Contains(type);
                    var hasDestroy = typesDestroy.Contains(type);
                    //if (burstedTypes.Contains(type) == false) continue;
                    
                    var awakeBurst = hasAwake == true && burstDiscardedTypes.Contains(type.GetMethod(nameof(IAwake.OnAwake))) == false;
                    var updateBurst = hasUpdate == true && burstDiscardedTypes.Contains(type.GetMethod(nameof(IUpdate.OnUpdate))) == false;
                    var destroyBurst = hasDestroy == true && burstDiscardedTypes.Contains(type.GetMethod(nameof(IDestroy.OnDestroy))) == false;
                    if (awakeBurst == true) {
                        if (isBursted == true) content.Add($"{AWAKE_METHOD}<{systemType}>.MakeMethod(null);");
                    }

                    if (updateBurst == true) {
                        if (isBursted == true) content.Add($"{UPDATE_METHOD}<{systemType}>.MakeMethod(null);");
                    }

                    if (destroyBurst == true) {
                        if (isBursted == true) content.Add($"{DESTROY_METHOD}<{systemType}>.MakeMethod(null);");
                    }
                    
                    if (hasAwake == true) content.Add($"{AWAKE_METHOD}NoBurst<{systemType}>.MakeMethod(null);");
                    if (hasUpdate == true) content.Add($"{UPDATE_METHOD}NoBurst<{systemType}>.MakeMethod(null);");
                    if (hasDestroy == true) content.Add($"{DESTROY_METHOD}NoBurst<{systemType}>.MakeMethod(null);");

                    if (awakeBurst == true) content.Add($"BurstCompileMethod.MakeAwake<{systemType}>(default);");
                    if (updateBurst == true) content.Add($"BurstCompileMethod.MakeUpdate<{systemType}>(default);");
                    if (destroyBurst == true) content.Add($"BurstCompileMethod.MakeDestroy<{systemType}>(default);");

                }
                
                var components = UnityEditor.TypeCache.GetTypesWithAttribute<ComponentGroupAttribute>();
                foreach (var component in components) {

                    var asm = component.Assembly.GetName().Name;
                    var info = asms.FirstOrDefault(x => x.name == asm);
                    if (editorAssembly == false && info.isEditor == true) continue;
                    
                    var attr = (ComponentGroupAttribute)component.GetCustomAttribute(typeof(ComponentGroupAttribute));
                    var systemType = component.FullName.Replace("+", ".");
                    var groupType = attr.groupType.FullName.Replace("+", ".");
                    var str = $"StaticTypes<{systemType}>.ApplyGroup(typeof({groupType}));";
                    typesContent.Add(str);
                    componentTypes.Add(component);

                }

                {
                    var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponent>();
                    foreach (var component in allComponents) {

                        if (component.IsValueType == false) continue;

                        var asm = component.Assembly.GetName().Name;
                        var info = asms.FirstOrDefault(x => x.name == asm);
                        if (editorAssembly == false && info.isEditor == true) continue;

                        var isTagType = IsTagType(component);
                        var isTag = isTagType.ToString().ToLower();
                        var type = component.FullName.Replace("+", ".");
                        var str = $"StaticTypes<{type}>.Validate(isTag: {isTag});";
                        typesContent.Add(str);
                        componentTypes.Add(component);
                        if (isTagType == false) {
                            if (component.GetProperty("Default", BindingFlags.Static | BindingFlags.Public) != null) {
                                str = $"StaticTypesDefaultValue<{type}>.value.Data = {type}.Default;";
                                typesContent.Add(str);
                                str = $"StaticTypesHasDefaultValue<{type}>.value.Data = true;";
                                typesContent.Add(str);
                            }
                        }
                        content.Add($"StaticTypes<{type}>.AOT();");
                        
                    }
                }
                {
                    var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentShared>();
                    foreach (var component in allComponents) {

                        if (component.IsValueType == false) continue;

                        var asm = component.Assembly.GetName().Name;
                        var info = asms.FirstOrDefault(x => x.name == asm);
                        if (editorAssembly == false && info.isEditor == true) continue;

                        var isTag = IsTagType(component).ToString().ToLower();
                        var hasCustomHash = HasComponentCustomSharedHash(component);
                        var type = component.FullName.Replace("+", ".");
                        var str = $"StaticTypes<{type}>.ValidateShared(isTag: {isTag}, hasCustomHash: {hasCustomHash.ToString().ToLower()});";
                        typesContent.Add(str);
                        componentTypes.Add(component);
                        content.Add($"StaticTypesShared<{type}>.AOT();");

                    }
                }
                {
                    var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentStatic>();
                    foreach (var component in allComponents) {

                        if (component.IsValueType == false) continue;

                        var asm = component.Assembly.GetName().Name;
                        var info = asms.FirstOrDefault(x => x.name == asm);
                        if (editorAssembly == false && info.isEditor == true) continue;

                        var isTag = IsTagType(component).ToString().ToLower();
                        var type = component.FullName.Replace("+", ".");
                        var str = $"StaticTypes<{type}>.ValidateStatic(isTag: {isTag});";
                        typesContent.Add(str);
                        componentTypes.Add(component);
                        content.Add($"StaticTypesStatic<{type}>.AOT();");

                    }
                }

                var methodRegistryContents = System.Array.Empty<string>();
                var methodContents = System.Array.Empty<string>();
                var methods = new System.Collections.Generic.List<MethodDefinition>();
                {
                    
                    foreach (var customCodeGenerator in generators) {
                        customCodeGenerator.asms = asms;
                        customCodeGenerator.editorAssembly = editorAssembly;
                        customCodeGenerator.AddInitialization(typesContent, componentTypes);
                        methods.Add(customCodeGenerator.AddMethod(componentTypes));
                        componentTypes.Add(customCodeGenerator.GetType());
                    }

                }

                methodRegistryContents = methods.Where(x => x.definition != null).Select(x => {
                    return $"WorldStaticCallbacks.RegisterCallback<{x.type}>({x.methodName});";
                }).ToArray();

                methodContents = methods.Where(x => x.definition != null).Select(x => {
                    return $"public static void {x.methodName}({x.definition}) {{\n{x.content}\n}}";
                }).ToArray();

                var newContent = template.Replace("{{CONTENT}}", string.Join("\n", content));
                newContent = newContent.Replace("{{CUSTOM_METHOD_REGISTRY}}", string.Join("\n", methodRegistryContents));
                newContent = newContent.Replace("{{CUSTOM_METHODS}}", string.Join("\n", methodContents));
                newContent = newContent.Replace("{{CONTENT_TYPES}}", string.Join("\n", typesContent));
                newContent = newContent.Replace("{{EDITOR}}", editorAssembly == true ? ".Editor" : string.Empty);
                var prevContent = System.IO.File.Exists(path) == true ? System.IO.File.ReadAllText(path) : string.Empty;
                if (prevContent != newContent) {
                    System.IO.File.WriteAllText(path, newContent);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }
            }
            {
                var path = @$"{dir}/{ECS}.BurstHelper.{postfix}.asmdef";
                var template = string.Empty;
                if (editorAssembly == true) {
                    template = @"{
                        ""name"": """ + ECS + @".BurstHelper." + postfix + @""",
                        ""references"": [
                            ""{{CONTENT}}""
                            ],
                        ""includePlatforms"": [
                            ""Editor""
                        ],
                        ""allowUnsafeCode"": true
                    }";
                } else {
                    template = @"{
                        ""name"": """ + ECS + @".BurstHelper." + postfix + @""",
                        ""references"": [
                            ""{{CONTENT}}""
                            ],
                        ""allowUnsafeCode"": true
                    }";
                }

                var content = new System.Collections.Generic.HashSet<string>();
                var types = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ISystem));
                foreach (var type in types) {
                    var asm = type.Assembly.GetName().Name;
                    var info = asms.FirstOrDefault(x => x.name == asm);
                    if (editorAssembly == false && info.isEditor == true) continue;
                    content.Add(asm);
                }

                foreach (var type in componentTypes) {
                    var asm = type.Assembly.GetName().Name;
                    var info = asms.FirstOrDefault(x => x.name == asm);
                    if (editorAssembly == false && info.isEditor == true) continue;
                    content.Add(asm);
                }
                
                // load references
                foreach (var asm in content.ToArray()) {
                    var asmInfo = asms.FirstOrDefault(x => x.name == asm);
                    if (asmInfo.references != null) {
                        foreach (var refAsm in asmInfo.references) {
                            var info = asms.FirstOrDefault(x => x.name == refAsm);
                            if (editorAssembly == false && info.isEditor == true) continue;
                            content.Add(refAsm);
                        }
                    }
                }

                var newContent = template.Replace("{{CONTENT}}", string.Join(@""",""", content));
                var prevContent = System.IO.File.Exists(path) == true ? System.IO.File.ReadAllText(path) : string.Empty;
                if (prevContent != newContent) {
                    System.IO.File.WriteAllText(path, newContent);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }
            }
            
        }

    }

}