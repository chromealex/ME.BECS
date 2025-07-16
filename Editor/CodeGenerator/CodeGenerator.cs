using System.Reflection;

namespace ME.BECS.Editor {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using System.Linq;
    using scg = System.Collections.Generic;

    public struct FileContent {

        public string filename;
        public string content;

    }

    public class Cache {

        [System.Serializable]
        public struct CachedItem {

            public string[] hashCodes;
            [UnityEngine.SerializeReference]
            public object data;

        }

        public readonly struct Key {

            private readonly System.Type type;
            private readonly string method;
            private readonly string key;

            public Key(System.Type type, string method, string key) {
                this.type = type;
                this.method = method;
                this.key = key;
            }

            public override string ToString() {
                return $"{this.type.AssemblyQualifiedName}:{this.method}:{this.key}";
            }

        }

        private string dir;
        private string filename;
        private string method;
        private string key;

        private System.Collections.Generic.Dictionary<string, CachedItem> cacheData;
        private bool isDirty;

        public void Add<T>(System.Type type, T data) {

            var scriptsPath = ScriptsImporter.FindScript(type);
            if (scriptsPath == null) return;
            foreach (string scriptPath in scriptsPath) {
                var hashCode = scriptPath != null ? Md5(scriptPath) : null;
                if (hashCode == null) continue;
                {
                    var key = new Key(type, this.method, this.key).ToString();
                    if (this.cacheData.TryGetValue(key, out var item) == true) {
                        System.Array.Resize(ref item.hashCodes, item.hashCodes.Length + 1);
                        item.hashCodes[^1] = hashCode;
                        this.cacheData[key] = item;
                    } else {
                        this.cacheData.Add(key, new CachedItem() {
                            hashCodes = new[] { hashCode },
                            data = data,
                        });
                    }
                    this.isDirty = true;
                }
            }

        }

        public bool TryGetValue<T>(System.Type key, out T value) {
            var cacheIsInvalid = true;
            if (this.cacheData.TryGetValue(new Key(key, this.method, this.key).ToString(), out var cachedItem) == true) {
                cacheIsInvalid = false;
                var scriptsPath = ScriptsImporter.FindScript(key);
                foreach (string scriptPath in scriptsPath) {
                    var monoScriptHashCode = scriptPath != null ? Md5(scriptPath) : null;
                    if (System.Array.IndexOf(cachedItem.hashCodes, monoScriptHashCode) == -1) {
                        cacheIsInvalid = true;
                        break;
                    }
                }
            }

            if (cacheIsInvalid == false) {
                
                if (cachedItem.data is T data) {
                    value = data;
                    return true;
                }
                if (typeof(T) == typeof(scg::List<string>)) {
                    var list = new scg::List<string>();
                    foreach (var item in (Newtonsoft.Json.Linq.JArray)cachedItem.data) {
                        list.Add(item.ToString());
                    }
                    value = (T)(object)list;
                    return true;
                }
                value = (T)((Newtonsoft.Json.Linq.JObject)cachedItem.data).ToObject(typeof(T));
                return true;
                
            }
            value = default;
            return false;
        }

        public void SetKey(string key) {
            this.key = key;
        }

        private static string Md5(string scriptPath) {
            var text = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(scriptPath)?.text;
            if (text == null) return null;
            using (var md5 = System.Security.Cryptography.MD5.Create()) {
                var bytes = System.Text.Encoding.UTF8.GetBytes(text);
                var computeHash = md5.ComputeHash(bytes);
                return System.BitConverter.ToString(computeHash);
            }
        }

        internal void Load(string dir, string filename) {

            this.dir = dir;
            this.filename = filename;
            this.isDirty = false;
            //var ms = System.Diagnostics.Stopwatch.StartNew();
            var path = $"{this.dir}/{this.filename}";
            var loadedCache = System.IO.File.Exists(path) == true ? System.IO.File.ReadAllText(path) : null;
            if (loadedCache == null) {
                this.cacheData = new System.Collections.Generic.Dictionary<string, CachedItem>();
            } else {
                this.cacheData = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, CachedItem>>(loadedCache);
            }
            //UnityEngine.Debug.Log($"Cache {this.filename} loaded in {ms.ElapsedMilliseconds}ms");

        }

        internal void SetMethod(string method) {
            this.method = method;
            // this.cacheData.Clear();
        }

        internal void Push() {

            if (this.isDirty == false) return;

            var path = $"{this.dir}/{this.filename}";
            var dir = System.IO.Path.GetDirectoryName(path);
            if (System.IO.Directory.Exists(dir) == false) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(this.cacheData));
            UnityEditor.AssetDatabase.ImportAsset(path);

            this.isDirty = false;
            // this.cacheData.Clear();

        }

    }

    public abstract class CustomCodeGenerator {

        public Cache cache;

        public string dir;
        public System.Collections.Generic.List<AssemblyInfo> asms;
        public bool editorAssembly;
        public UnityEditor.TypeCache.TypeCollection burstedTypes;
        public UnityEditor.TypeCache.MethodCollection burstDiscardedTypes;
        public System.Collections.Generic.List<System.Type> systems;

        public bool IsValidTypeForAssembly(System.Type type, bool runtimeInEditor = true) {

            return EditorUtils.IsValidTypeForAssembly(this.editorAssembly, type, this.asms, runtimeInEditor);

        }

        public virtual void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) { }

        public virtual scg::List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
        }

        public virtual string AddPublicContent() {
            return string.Empty;
        }

        public virtual FileContent[] AddFileContent(System.Collections.Generic.List<System.Type> references) {
            return null;
        }

    }

    public class CodeGeneratorImporter : UnityEditor.AssetPostprocessor {

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
            foreach (var path in importedAssets) {
                if (path.EndsWith(".cs") == true &&
                    path.Contains("ME.BECS.Gen.cs") == false) {
                    //UnityEngine.Debug.Log($"Destroy helper because of {path}");
                    //CodeGenerator.Destroy();
                    break;
                }
            }
        }

    }
    
    public static class CodeGenerator {
        
        public struct MethodDefinition {

            public string methodName;
            public string customMethodParamsCall;
            public string type;
            public string registerMethodName;
            public string definition;
            public string content;
            public bool burstCompile;
            public string pInvoke;

            public string GetMethodParamsCall() {
                if (this.customMethodParamsCall != null) return this.customMethodParamsCall;
                return this.methodName;
            }

        }

        public const string ECS = "ME.BECS";
        public const string AWAKE_METHOD = "BurstCompileOnAwake";
        public const string START_METHOD = "BurstCompileOnStart";
        public const string UPDATE_METHOD = "BurstCompileOnUpdate";
        public const string DESTROY_METHOD = "BurstCompileOnDestroy";
        public const string DRAWGIZMOS_METHOD = "BurstCompileOnDrawGizmos";

        static CodeGenerator() {

            UnityEngine.Application.logMessageReceived -= OnLogAdded;
            UnityEngine.Application.logMessageReceivedThreaded -= OnLogAdded;
            UnityEngine.Application.logMessageReceived += OnLogAdded;
            UnityEngine.Application.logMessageReceivedThreaded += OnLogAdded;

        }

        [UnityEditor.Callbacks.DidReloadScripts]
        public static void OnScriptsReload() {

            UnityEngine.Application.logMessageReceived -= OnLogAdded;
            UnityEngine.Application.logMessageReceivedThreaded -= OnLogAdded;
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
                {"inref", "ref"},
                {"RWRO", "RW"},
            };
            var postfixes = new VariantInfo[] {
                new VariantInfo() {
                    filenamePostfix = ".ref",
                    variables = new[] {
                        new System.Collections.Generic.KeyValuePair<string, string>("inref", "ref"),
                        new System.Collections.Generic.KeyValuePair<string, string>("GetRead", "Get"),
                        new System.Collections.Generic.KeyValuePair<string, string>("RWRO", "RW"),
                    },
                },
                /*new VariantInfo() {
                    filenamePostfix = ".in",
                    variables = new [] {
                        new System.Collections.Generic.KeyValuePair<string, string>("inref", "in"),
                        new System.Collections.Generic.KeyValuePair<string, string>("GetRead", "Read"),
                        new System.Collections.Generic.KeyValuePair<string, string>("RWRO", "RO"),
                    },
                },*/
            };
            var templates = UnityEditor.AssetDatabase.FindAssets("t:TextAsset .Tpl");
            foreach (var guid in templates) {

                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var dir = System.IO.Path.GetDirectoryName(path);

                var fileName = System.IO.Path.GetFileName(path);
                dir = $"{dir}/{fileName.Replace(".Tpl.txt", string.Empty)}";
                var text = System.IO.File.ReadAllText(path);
                foreach (var postfix in postfixes) {
                    foreach (var key in postfix.variables) {
                        variables[key.Key] = key.Value;
                    }

                    if (System.IO.Directory.Exists(dir) == false) {
                        System.IO.Directory.CreateDirectory(dir);
                    }

                    const uint maxCount = 10u;
                    uint variationsCount = 0u;
                    if (path.EndsWith("_var.Tpl.txt") == true) {
                        variationsCount = 5u;
                        for (int i = 1; i < maxCount; ++i) {
                            var keys = new System.Collections.Generic.Dictionary<string, int>() {
                                {"countAspects", i},
                            };
                            variables["countAspects"] = i.ToString();
                            for (int j = 1; j < variationsCount; ++j) {
                                keys["countComponents"] = j;
                                keys["count"] = i + j;
                                variables["PREFIX"] = $"{i}_{j}";
                                variables["countComponents"] = j.ToString();
                                var filePath = $"{dir}/{fileName.Replace(".Tpl.txt", $"{i}_{j}{postfix.filenamePostfix}.cs")}";
                                var tpl = new Tpl(text);
                                System.IO.File.WriteAllText(filePath, tpl.GetString(keys, variables));
                                UnityEditor.AssetDatabase.ImportAsset(filePath);
                            }
                        }
                    } else {
                        for (int i = 1; i < maxCount; ++i) {
                            var keys = new System.Collections.Generic.Dictionary<string, int>() {
                                {"count", i},
                            };
                            variables["PREFIX"] = $"{i}";
                            var filePath = $"{dir}/{fileName.Replace(".Tpl.txt", $"{i}{postfix.filenamePostfix}.cs")}";
                            var tpl = new Tpl(text);
                            System.IO.File.WriteAllText(filePath, tpl.GetString(keys, variables));
                            UnityEditor.AssetDatabase.ImportAsset(filePath);
                        }
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

        public static void Destroy() {
            {
                var dir = $"Assets/{ECS}.Gen/Runtime";
                var path = @$"{dir}/{ECS}.Gen.cs";
                UnityEditor.AssetDatabase.DeleteAsset(path);
            }
            {
                var dir = $"Assets/{ECS}.Gen/Editor";
                var path = @$"{dir}/{ECS}.Gen.cs";
                UnityEditor.AssetDatabase.DeleteAsset(path);
            }
        }

        public static void RegenerateBurstAOT(bool forced = false) {

            if (UnityEngine.Application.isBatchMode == true && forced == false) return;

            UnityEditor.EditorPrefs.SetInt("ME.BECS.CodeGenerator.TempError", UnityEditor.EditorPrefs.GetInt("ME.BECS.CodeGenerator.TempError", 0) + 1);

            var list = EditorUtils.GetAssembliesInfo();
            {
                var dir = $"Assets/{ECS}.Gen/Runtime";
                Build(list, dir);
            }
            {
                var dir = $"Assets/{ECS}.Gen/Editor";
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

            /*if (type == UnityEngine.LogType.Exception ||
                type == UnityEngine.LogType.Error) {
                if (condition.Contains($"{ECS}.Gen.cs") == true ||
                    stackTrace.Contains($"{ECS}.Gen.cs") == true) {
                    if (condition.Contains("CS0426") == true) {
                        // Remove files
                        UnityEngine.Debug.Log("Regenerating burst helper: " + UnityEditor.EditorPrefs.GetInt("ME.BECS.CodeGenerator.TempError", 0));
                        if (UnityEditor.EditorPrefs.GetInt("ME.BECS.CodeGenerator.TempError", 0) % 2 == 0) return;
                        Destroy();
                    }
                }
            }*/

        }

        public const string PROGRESS_BAR_CAPTION = "[ ME.BECS ] CodeGenerator";

        private static void Build(System.Collections.Generic.List<AssemblyInfo> asms, string dir, bool editorAssembly = false) {

            string postfix;
            if (editorAssembly == true) {
                postfix = "Editor";
            } else {
                postfix = "Runtime";
            }

            var customCodeGenerators = UnityEditor.TypeCache.GetTypesDerivedFrom<CustomCodeGenerator>().OrderBy(x => x.FullName);
            var generators = customCodeGenerators.Select(x => (CustomCodeGenerator)System.Activator.CreateInstance(x)).ToArray();

            if (System.IO.Directory.Exists(dir) == false) {
                System.IO.Directory.CreateDirectory(dir);
            }

            UnityEditor.EditorUtility.DisplayProgressBar(PROGRESS_BAR_CAPTION, $"Build {dir}", 0f);
            var componentTypes = new System.Collections.Generic.List<System.Type>();
            try {
                var path = @$"{dir}/{ECS}.Gen.cs";
                var filesPath = @$"{dir}/{ECS}.Files";
                string template = null;
                if (editorAssembly == true) {
                    template = EditorUtils.LoadResource<UnityEngine.TextAsset>($"ME.BECS.Resources/Templates/Types-Editor-Template.txt").text;
                } else {
                    template = EditorUtils.LoadResource<UnityEngine.TextAsset>($"ME.BECS.Resources/Templates/Types-Template.txt").text;
                }
                string fileTemplate = null;
                if (editorAssembly == true) {
                    fileTemplate = EditorUtils.LoadResource<UnityEngine.TextAsset>($"ME.BECS.Resources/Templates/Types-Editor-FileTemplate.txt").text;
                } else {
                    fileTemplate = EditorUtils.LoadResource<UnityEngine.TextAsset>($"ME.BECS.Resources/Templates/Types-FileTemplate.txt").text;
                }

                //var template = "namespace " + ECS + " {\n [UnityEngine.Scripting.PreserveAttribute] public static unsafe class AOTBurstHelper { \n[UnityEngine.Scripting.PreserveAttribute] \npublic static void AOT() { \n{{CONTENT}} \n}\n }\n }";
                var aotContent = new System.Collections.Generic.List<string>();
                var typesContent = new System.Collections.Generic.List<string>();
                var types = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ISystem)).OrderBy(x => x.FullName).ToList();
                PatchSystemsList(types);
                var burstedTypes = UnityEditor.TypeCache.GetTypesWithAttribute<BURST>();
                var burstDiscardedTypes = UnityEditor.TypeCache.GetMethodsWithAttribute<WithoutBurstAttribute>();
                /*var typesAwake = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IAwake)).OrderBy(x => x.FullName).ToList();
                PatchSystemsList(typesAwake);
                var typesStart = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IStart)).OrderBy(x => x.FullName).ToList();
                PatchSystemsList(typesStart);
                var typesUpdate = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IUpdate)).OrderBy(x => x.FullName).ToList();
                PatchSystemsList(typesUpdate);
                var typesDestroy = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IDestroy)).OrderBy(x => x.FullName).ToList();
                PatchSystemsList(typesDestroy);
                var typesDrawGizmos = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IDrawGizmos)).OrderBy(x => x.FullName).ToList();
                PatchSystemsList(typesDrawGizmos);*/
                aotContent.Add("var nullContext = new SystemContext();");
                for (var index = 0; index < types.Count; ++index) {

                    var type = types[index];
                    if (type.IsValueType == false) continue;
                    var asm = type.Assembly;
                    var name = asm.GetName().Name;
                    var info = asms.FirstOrDefault(x => x.name == name);
                    if (editorAssembly == false && info.isEditor == true) continue;

                    if (type.IsVisible == false) continue;

                    var systemType = EditorUtils.GetTypeName(type);
                    aotContent.Add($"StaticSystemTypes<{systemType}>.Validate();");
                    typesContent.Add($"StaticSystemTypes<{systemType}>.Validate();");

                    var isBursted = (burstedTypes.Contains(type) == true);
                    var hasAwake = typeof(IAwake).IsAssignableFrom(type);
                    var hasStart = typeof(IStart).IsAssignableFrom(type);
                    var hasUpdate = typeof(IUpdate).IsAssignableFrom(type);
                    var hasDestroy = typeof(IDestroy).IsAssignableFrom(type);
                    var hasDrawGizmos = typeof(IDrawGizmos).IsAssignableFrom(type);
                    //if (burstedTypes.Contains(type) == false) continue;

                    var awakeBurst = hasAwake == true && burstDiscardedTypes.Contains(type.GetMethod(nameof(IAwake.OnAwake))) == false;
                    var startBurst = hasStart == true && burstDiscardedTypes.Contains(type.GetMethod(nameof(IStart.OnStart))) == false;
                    var updateBurst = hasUpdate == true && burstDiscardedTypes.Contains(type.GetMethod(nameof(IUpdate.OnUpdate))) == false;
                    var destroyBurst = hasDestroy == true && burstDiscardedTypes.Contains(type.GetMethod(nameof(IDestroy.OnDestroy))) == false;
                    var drawGizmosBurst = hasDrawGizmos == true && burstDiscardedTypes.Contains(type.GetMethod(nameof(IDrawGizmos.OnDrawGizmos))) == false;
                    if (awakeBurst == true) {
                        if (isBursted == true) aotContent.Add($"{AWAKE_METHOD}<{systemType}>.MakeMethod(null);");
                    }

                    if (startBurst == true) {
                        if (isBursted == true) aotContent.Add($"{START_METHOD}<{systemType}>.MakeMethod(null);");
                    }

                    if (updateBurst == true) {
                        if (isBursted == true) aotContent.Add($"{UPDATE_METHOD}<{systemType}>.MakeMethod(null);");
                    }

                    if (destroyBurst == true) {
                        if (isBursted == true) aotContent.Add($"{DESTROY_METHOD}<{systemType}>.MakeMethod(null);");
                    }

                    if (drawGizmosBurst == true) {
                        if (isBursted == true) aotContent.Add($"{DRAWGIZMOS_METHOD}<{systemType}>.MakeMethod(null);");
                    }
                    
                    if (hasAwake == true) aotContent.Add($"{AWAKE_METHOD}NoBurst<{systemType}>.MakeMethod(null);");
                    if (hasStart == true) aotContent.Add($"{START_METHOD}NoBurst<{systemType}>.MakeMethod(null);");
                    if (hasUpdate == true) aotContent.Add($"{UPDATE_METHOD}NoBurst<{systemType}>.MakeMethod(null);");
                    if (hasDestroy == true) aotContent.Add($"{DESTROY_METHOD}NoBurst<{systemType}>.MakeMethod(null);");
                    if (hasDrawGizmos == true) aotContent.Add($"{DRAWGIZMOS_METHOD}NoBurst<{systemType}>.MakeMethod(null);");

                    if (hasAwake == true) aotContent.Add($"new {systemType}().OnAwake(ref nullContext);");
                    if (hasStart == true) aotContent.Add($"new {systemType}().OnStart(ref nullContext);");
                    if (hasUpdate == true) aotContent.Add($"new {systemType}().OnUpdate(ref nullContext);");
                    if (hasDestroy == true) aotContent.Add($"new {systemType}().OnDestroy(ref nullContext);");
                    if (hasDrawGizmos == true) aotContent.Add($"new {systemType}().OnDrawGizmos(ref nullContext);");

                    if (awakeBurst == true) aotContent.Add($"BurstCompileMethod.MakeAwake<{systemType}>(default);");
                    if (startBurst == true) aotContent.Add($"BurstCompileMethod.MakeStart<{systemType}>(default);");
                    if (updateBurst == true) aotContent.Add($"BurstCompileMethod.MakeUpdate<{systemType}>(default);");
                    if (destroyBurst == true) aotContent.Add($"BurstCompileMethod.MakeDestroy<{systemType}>(default);");
                    if (drawGizmosBurst == true) aotContent.Add($"BurstCompileMethod.MakeDrawGizmos<{systemType}>(default);");
                }

                var components = UnityEditor.TypeCache.GetTypesWithAttribute<ComponentGroupAttribute>().OrderBy(x => x.FullName).ToArray();
                foreach (var component in components) {

                    var asm = component.Assembly.GetName().Name;
                    var info = asms.FirstOrDefault(x => x.name == asm);
                    if (editorAssembly == false && info.isEditor == true) continue;

                    var attr = (ComponentGroupAttribute)component.GetCustomAttribute(typeof(ComponentGroupAttribute));
                    var systemType = EditorUtils.GetTypeName(component);
                    var groupType = EditorUtils.GetTypeName(attr.groupType);
                    var str = $"StaticTypes<{systemType}>.ApplyGroup(typeof({groupType}));";
                    typesContent.Add(str);
                    componentTypes.Add(component);

                }

                {
                    var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponent>().OrderBy(x => x.FullName).ToArray();
                    foreach (var component in allComponents) {

                        if (component.IsValueType == false) continue;

                        var asm = component.Assembly.GetName().Name;
                        var info = asms.FirstOrDefault(x => x.name == asm);
                        if (editorAssembly == false && info.isEditor == true) continue;

                        var isTagType = IsTagType(component);
                        var isTag = isTagType.ToString().ToLower();
                        var type = EditorUtils.GetTypeName(component);
                        {
                            var str = $"StaticTypes<{type}>.Validate(isTag: {isTag});";
                            typesContent.Add(str);
                        }
                        componentTypes.Add(component);
                        if (isTagType == false) {
                            if (component.GetProperty("Default", BindingFlags.Static | BindingFlags.Public) != null) {
                                var str = $"StaticTypes<{type}>.SetDefaultValue({type}.Default);";
                                typesContent.Add(str);
                            }
                        }

                        aotContent.Add($"StaticTypes<{type}>.AOT();");

                    }
                }
                {
                    var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentDestroy>().OrderBy(x => x.FullName).ToArray();
                    foreach (var component in allComponents) {

                        if (component.IsValueType == false) continue;

                        var asm = component.Assembly.GetName().Name;
                        var info = asms.FirstOrDefault(x => x.name == asm);
                        if (editorAssembly == false && info.isEditor == true) continue;

                        var isTagType = IsTagType(component);
                        var isTag = isTagType.ToString().ToLower();
                        var type = EditorUtils.GetTypeName(component);
                        var str = $"StaticTypesDestroy<{type}>.RegisterAutoDestroy(isTag: {isTag});";
                        typesContent.Add(str);
                        componentTypes.Add(component);
                        aotContent.Add($"AutoDestroyRegistryStatic<{type}>.Destroy(null);");

                    }
                }
                {
                    var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentShared>().OrderBy(x => x.FullName).ToArray();
                    foreach (var component in allComponents) {

                        if (component.IsValueType == false) continue;

                        var asm = component.Assembly.GetName().Name;
                        var info = asms.FirstOrDefault(x => x.name == asm);
                        if (editorAssembly == false && info.isEditor == true) continue;

                        var isTag = IsTagType(component).ToString().ToLower();
                        var hasCustomHash = HasComponentCustomSharedHash(component);
                        var type = EditorUtils.GetTypeName(component);
                        var str = $"StaticTypes<{type}>.ValidateShared(isTag: {isTag}, hasCustomHash: {hasCustomHash.ToString().ToLower()});";
                        typesContent.Add(str);
                        componentTypes.Add(component);
                        aotContent.Add($"StaticTypesShared<{type}>.AOT();");

                    }
                }
                {
                    var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponentStatic>().OrderBy(x => x.FullName).ToArray();
                    foreach (var component in allComponents) {

                        if (component.IsValueType == false) continue;

                        var asm = component.Assembly.GetName().Name;
                        var info = asms.FirstOrDefault(x => x.name == asm);
                        if (editorAssembly == false && info.isEditor == true) continue;

                        var isTag = IsTagType(component).ToString().ToLower();
                        var type = EditorUtils.GetTypeName(component);
                        var str = $"StaticTypes<{type}>.ValidateStatic(isTag: {isTag});";
                        typesContent.Add(str);
                        componentTypes.Add(component);
                        aotContent.Add($"StaticTypesStatic<{type}>.AOT();");

                    }
                }
                {
                    var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigInitialize>().OrderBy(x => x.FullName).ToArray();
                    foreach (var component in allComponents) {

                        if (component.IsValueType == false) continue;

                        var asm = component.Assembly.GetName().Name;
                        var info = asms.FirstOrDefault(x => x.name == asm);
                        if (editorAssembly == false && info.isEditor == true) continue;

                        var isTag = IsTagType(component).ToString().ToLower();
                        var type = EditorUtils.GetTypeName(component);
                        var str = $"StaticTypes<{type}>.ValidateStatic(isTag: {isTag});";
                        typesContent.Add(str);
                        componentTypes.Add(component);
                        aotContent.Add($"ConfigInitializeTypes<{type}>.AOT();");

                    }
                }

                var methods = new scg::List<MethodDefinition>();
                var publicContent = new scg::List<string>();
                var filesContent = new scg::List<FileContent[]>();
                {
                    var cache = new Cache();
                    for (var index = 0; index < generators.Length; ++index) {
                        var customCodeGenerator = generators[index];
                        cache.Load(dir, $"Cache/{customCodeGenerator.GetType().Name}.cache");
                        customCodeGenerator.cache = cache;
                        customCodeGenerator.dir = dir;
                        customCodeGenerator.asms = asms;
                        customCodeGenerator.systems = types;
                        customCodeGenerator.editorAssembly = editorAssembly;
                        customCodeGenerator.burstedTypes = burstedTypes;
                        customCodeGenerator.burstDiscardedTypes = burstDiscardedTypes;
                        UnityEditor.EditorUtility.DisplayProgressBar(PROGRESS_BAR_CAPTION, customCodeGenerator.GetType().Name, index / (float)generators.Length);
                        cache.SetMethod("AddInitialization");
                        customCodeGenerator.AddInitialization(typesContent, componentTypes);
                        cache.SetMethod("AddPublicContent");
                        publicContent.Add(customCodeGenerator.AddPublicContent());
                        cache.SetMethod("AddFileContent");
                        var files = customCodeGenerator.AddFileContent(componentTypes);
                        if (files != null) filesContent.Add(files);
                        cache.SetMethod("AddMethods");
                        methods.AddRange(customCodeGenerator.AddMethods(componentTypes));
                        cache.Push();
                        componentTypes.Add(customCodeGenerator.GetType());
                    }
                }

                var methodRegistryContents = methods.Where(x => x.definition != null && x.type != null)
                                                    .Select(x => $"WorldStaticCallbacks.{x.registerMethodName}<{x.type}>({x.GetMethodParamsCall()});").ToArray();
                var methodContents = methods.Where(x => x.definition != null)
                                            .Select(
                                                x =>
                                                    $"{(x.burstCompile == true ? "[BURST]" : string.Empty)} {(string.IsNullOrEmpty(x.pInvoke) == false ? $"[AOT.MonoPInvokeCallbackAttribute(typeof({x.pInvoke}))]" : string.Empty)} public static unsafe void {x.methodName}({x.definition}) {{\n{x.content}\n}}")
                                            .ToArray();

                var newContent = template.Replace("{{CONTENT}}", string.Join("\n", aotContent));
                newContent = newContent.Replace("{{CUSTOM_METHOD_REGISTRY}}", string.Join("\n", methodRegistryContents));
                newContent = newContent.Replace("{{CUSTOM_METHODS}}", string.Join("\n", publicContent) + "\n" + string.Join("\n", methodContents));
                newContent = newContent.Replace("{{CONTENT_TYPES}}", string.Join("\n", typesContent));
                newContent = newContent.Replace("{{EDITOR}}", editorAssembly == true ? ".Editor" : string.Empty);
                {
                    var prevContent = System.IO.File.Exists(path) == true ? System.IO.File.ReadAllText(path) : string.Empty;
                    newContent = EditorUtils.ReFormatCode(newContent);
                    if (prevContent != newContent) {
                        System.IO.File.WriteAllText(path, newContent);
                        UnityEditor.AssetDatabase.ImportAsset(path);
                    }
                }

                if (filesContent.Count > 0) {
                    var hasAny = false;
                    foreach (var files in filesContent) {
                        foreach (var file in files) {
                            hasAny = true;
                            if (hasAny == true) break;
                        }
                        if (hasAny == true) break;
                    }

                    if (hasAny == true) {
                        System.IO.Directory.CreateDirectory(filesPath);
                    } else {
                        System.IO.Directory.Delete(filesPath, true);
                    }

                    foreach (var files in filesContent) {
                        foreach (var file in files) {
                            var filepath = $"{filesPath}/{file.filename}.cs";
                            var prevContent = System.IO.File.Exists(filepath) == true ? System.IO.File.ReadAllText(filepath) : string.Empty;
                            newContent = EditorUtils.ReFormatCode(fileTemplate.Replace("{{CONTENT}}", file.content));
                            if (prevContent != newContent) {
                                System.IO.File.WriteAllText(filepath, newContent);
                                UnityEditor.AssetDatabase.ImportAsset(filepath);
                            }
                        }
                    }

                } else {
                    // Clean up all files
                    System.IO.Directory.Delete(filesPath, true);
                }
            } catch (System.Exception ex) {
                UnityEngine.Debug.LogException(ex);
            } finally {
                UnityEditor.EditorUtility.ClearProgressBar();
            }
            {
                var csc = @$"{dir}/csc.rsp";
                var path = @$"{dir}/{ECS}.Gen.{postfix}.asmdef";
                var template = string.Empty;
                if (editorAssembly == true) {
                    template = @"{
                        ""name"": """ + ECS + @".Gen." + postfix + @""",
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
                        ""name"": """ + ECS + @".Gen." + postfix + @""",
                        ""references"": [
                            ""{{CONTENT}}""
                            ],
                        ""allowUnsafeCode"": true
                    }";
                }

                var content = new scg::HashSet<string>();
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

                var newContent = template.Replace("{{CONTENT}}", string.Join(@""",""", content.OrderBy(x => x).ToArray()));
                var prevContent = System.IO.File.Exists(path) == true ? System.IO.File.ReadAllText(path) : string.Empty;
                if (prevContent != newContent) {
                    var pathDummy = @$"{dir}/{ECS}.Dummy.cs";
                    System.IO.File.WriteAllText(pathDummy, "// Code generator dummy script");
                    System.IO.File.WriteAllText(csc, "@Assets/csc.rsp");
                    System.IO.File.WriteAllText(path, newContent);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }
            }

        }

        public static void PatchSystemsList(System.Collections.Generic.List<System.Type> types) {

            var genericTypes = new System.Collections.Generic.HashSet<System.Type>(types.Count);
            for (var index = 0; index < types.Count; ++index) {

                var type = types[index];
                if (type.IsValueType == false) continue;

                if (type.IsGenericType == true && genericTypes.Contains(type) == false) {
                    types.RemoveAt(index);
                    --index;
                    var typeGen = EditorUtils.GetFirstInterfaceConstraintType(type);
                    if (typeGen != null) {
                        var genTypes = UnityEditor.TypeCache.GetTypesDerivedFrom(typeGen).OrderBy(x => x.FullName).ToArray();
                        foreach (var genType in genTypes) {
                            if (genType.IsValueType == false) continue;
                            var gType = type.MakeGenericType(genType);
                            types.Add(gType);
                            genericTypes.Add(gType);
                        }
                    }
                }

            }

        }

    }

}