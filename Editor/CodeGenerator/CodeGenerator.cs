using System.Reflection;
using Newtonsoft.Json;

namespace ME.BECS.Editor {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using System.Linq;
    using scg = System.Collections.Generic;

    public class CodeGeneratorOrderAttribute : System.Attribute {

        public int order;

        public CodeGeneratorOrderAttribute(int order) {
            this.order = order;
        }

    }

    public struct MethodPointerData : System.IEquatable<MethodPointerData> {

        // Safety/weight diagnostics distinguish closed generic methods and overloads.
        // Entity reservations use the call-site graph, not this legacy comparer.
        public static readonly System.Collections.Generic.IEqualityComparer<MethodPointerData> ExactComparer = new ExactMethodComparer();

        private sealed class ExactMethodComparer : System.Collections.Generic.IEqualityComparer<MethodPointerData> {
            public bool Equals(MethodPointerData x, MethodPointerData y) =>
                object.Equals(x.originalMethodInfo, y.originalMethodInfo) && x.rootType == y.rootType;

            public int GetHashCode(MethodPointerData value) =>
                (value.originalMethodInfo?.GetHashCode() ?? 0) ^ (value.rootType?.GetHashCode() ?? 0);
        }

        private MethodInfo originalMethodInfo;
        private System.Type rootType;

        public MethodPointerData(MethodInfo originalMethodInfo, System.Type rootType = null) {
            this.originalMethodInfo = originalMethodInfo;
            this.rootType = rootType;
        }

        public bool Equals(MethodPointerData other) {
            if (this.originalMethodInfo.IsGenericMethod == true) {
                if (this.originalMethodInfo.Name == other.originalMethodInfo.Name &&
                    this.originalMethodInfo.ReturnType == other.originalMethodInfo.ReturnType &&
                    this.originalMethodInfo.MemberType == other.originalMethodInfo.MemberType &&
                    this.rootType == other.rootType &&
                    this.originalMethodInfo.GetGenericMethodDefinition() == other.originalMethodInfo.GetGenericMethodDefinition()) {
                    return true;
                }
            }
            return this.originalMethodInfo.Name == other.originalMethodInfo.Name &&
                   this.originalMethodInfo.ReturnType == other.originalMethodInfo.ReturnType &&
                   this.originalMethodInfo.DeclaringType == other.originalMethodInfo.DeclaringType &&
                   this.originalMethodInfo.ReflectedType == other.originalMethodInfo.ReflectedType &&
                   this.originalMethodInfo.MemberType == other.originalMethodInfo.MemberType &&
                   this.originalMethodInfo.IsGenericMethod == other.originalMethodInfo.IsGenericMethod &&
                   this.rootType == other.rootType;
        }

        public override bool Equals(object obj) {
            return obj is MethodPointerData other && this.Equals(other);
        }

        public override int GetHashCode() {
            if (this.originalMethodInfo.IsGenericMethod == true) {
                return this.originalMethodInfo.Name.GetHashCode() ^ this.originalMethodInfo.ReturnType.GetHashCode() ^ this.originalMethodInfo.GetGenericMethodDefinition().GetHashCode() ^ (this.rootType != null ? this.rootType.GetHashCode() : 0);
            }
            return (this.originalMethodInfo != null ? this.originalMethodInfo.GetHashCode() : 0) ^ (this.rootType != null ? this.rootType.GetHashCode() : 0);
        }

    }
    
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
                        if (System.Array.IndexOf(item.hashCodes, hashCode) == -1) {
                            System.Array.Resize(ref item.hashCodes, item.hashCodes.Length + 1);
                            item.hashCodes[^1] = hashCode;
                        }
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
                var scriptsPath = ScriptsImporter.FindScript(key);
                if (scriptsPath != null) {
                    cacheIsInvalid = false;
                    foreach (string scriptPath in scriptsPath) {
                        var monoScriptHashCode = scriptPath != null ? Md5(scriptPath) : null;
                        if (System.Array.IndexOf(cachedItem.hashCodes, monoScriptHashCode) == -1) {
                            cacheIsInvalid = true;
                            break;
                        }
                    }
                }
            }

            if (cacheIsInvalid == false) {
                if (cachedItem.data is T data) {
                    value = data;
                    return true;
                }
                try {
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
                } catch (System.Exception) {}
                
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
            System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(this.cacheData,  Formatting.Indented));
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
        public System.Collections.Generic.List<System.Type> jobTypes;
        public System.Collections.Generic.List<System.Type> entityTypes;
        public System.Collections.Generic.List<System.Type> aspects;

        public bool IsValidTypeForAssembly(System.Type type, bool runtimeInEditor = true) {

            return EditorUtils.IsValidTypeForAssembly(this.editorAssembly, type, this.asms, runtimeInEditor);

        }

        public virtual void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) { }

        // Addon input transport only. Implementations export data records, never C# bodies.
        public virtual void AppendSourceGeneratorInputs(System.Text.StringBuilder manifest) { }

        // Dependencies of source-emitted code, independent of legacy C# callbacks.
        public virtual void AddSourceGeneratorReferences(scg::List<System.Type> references) { }

        // Opt in only for feeders whose output depends exclusively on compiled
        // code and the selected type lists, never on asset values or graph topology.
        public virtual bool CacheCompiledInputs => false;
        internal System.Type[] preparedInputReferences;
        internal void AddPreparedInputReferences(scg::List<System.Type> references) {
            if (this.preparedInputReferences != null) references.AddRange(this.preparedInputReferences);
            else this.AddSourceGeneratorReferences(references);
        }

        // Declarative compiler-owned initialization; null is rejected at export preflight.
        public virtual string SourceInitializationKind => this.GetType().GetMethod(nameof(AddInitialization),
            new[] { typeof(scg::List<string>), typeof(scg::List<System.Type>) })?.DeclaringType == typeof(CustomCodeGenerator) ? "none" : null;

        public virtual string SourceRegistrationKind => this.GetType().GetMethod(nameof(AddMethods),
            new[] { typeof(scg::List<System.Type>) })?.DeclaringType == typeof(CustomCodeGenerator) ? "none" : null;

        public virtual scg::List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
        }

        public virtual string AddPublicContent() {
            return string.Empty;
        }

        public virtual FileContent[] AddFileContent(System.Collections.Generic.List<System.Type> references) {
            return null;
        }

        // Filenames without .cs only. The exporter replaces existing outputs with
        // a fixed comment; no feeder-supplied C# is accepted. Legacy hooks above
        // remain recognizable solely to produce an actionable migration error.
        public virtual scg::IEnumerable<string> GetRetiredSourceFiles() => System.Array.Empty<string>();

    }

    public static class CodeGenerator {
        
        public struct MethodDefinition {
            // Callback body and registration are owned by a source generator.
            public string generatedRegistration;

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

        [UnityEditor.Callbacks.DidReloadScripts]
        public static void OnScriptsReload() {

            // Skip code generation if project creation is in progress
            if (UnityEditor.EditorPrefs.HasKey("ME.BECS.Editor.AwaitPackageImportData") == true) return;

            // Asset inputs must refresh after imports settle, not synchronously
            // inside assembly reload. The graph input scheduler coalesces this with
            // its own startup check and proves freshness from compiled metadata.
            InputRefreshRequested?.Invoke();

        }

        public static void RegenerateBurstAOT(bool forced = false, bool cleanCache = false) {
            TryRegenerateBurstAOT(forced, cleanCache);
        }

        public static event System.Action<bool> ExportCompleted;
        public static event System.Action InputRefreshRequested;
        public static string LastExportedGraphSnapshot { get; private set; }
        private static bool exportingInputs;

        // Reports export completion only, not the result of Unity's later compilation.
        public static bool TryRegenerateBurstAOT(bool forced = false, bool cleanCache = false) {
            if (exportingInputs) return false;
            
            // Skip if project creation is in progress
            if (UnityEditor.EditorPrefs.HasKey("ME.BECS.Editor.AwaitPackageImportData") == true) return false;

            if (CodeGeneratorMenu.IsEnabledAuto == false && forced == false) return false;

            if (UnityEngine.Application.isBatchMode == true) {
                Logger.Editor.Warning($"[ ME.BECS ] CodeGen won't run in batchmode. Ensure it was properly generated (or stored in the repo) before the build");
                return false;
            }

            Logger.Editor.Log($"[ ME.BECS ] Regenerating assemblies {(forced == true ? "(forced)" : "")}");

            var exported = false;
            exportingInputs = true;
            try {
                using var publicationBridges = SourceGeneratorPublicationBridges.BeginPlanning();
                SourceGeneratorAnalysisReceipt.Invalidate();
                var codeFingerprint = SourceGeneratorGraphSnapshot.GetCodeFingerprint();
                using var analysis = new ILAnalysisSession(codeFingerprint, cleanCache);
                using var incremental = new ILPersistentAnalysis(cleanCache);
                var graphSnapshot = SourceGeneratorGraphSnapshot.GetCurrent();
                var compilerSnapshot = SourceGeneratorGraphSnapshot.GetCompilerSnapshot();
                var list = EditorUtils.GetAssembliesInfo();
                var runtimeExported = Build(list, $"Assets/{ECS}.Gen/Runtime", out var runtimeContent);
                if (!runtimeExported) return false;
                var editorExported = Build(list, $"Assets/{ECS}.Gen/Editor", out var editorContent, editorAssembly: true);
                if (runtimeExported && editorExported && (codeFingerprint != SourceGeneratorGraphSnapshot.GetCodeFingerprint() ||
                    graphSnapshot != SourceGeneratorGraphSnapshot.GetCurrent())) {
                    throw new System.InvalidOperationException("Input assets or loaded script assemblies changed between Runtime and Editor exports. Retry input generation.");
                }
                if (runtimeExported && editorExported) {
                    SourceGeneratorAnalysisReceipt.Commit(graphSnapshot, compilerSnapshot, runtimeContent, editorContent);
                    LastExportedGraphSnapshot = graphSnapshot;
                    exported = true;
                }
                return exported;
            } finally {
                exportingInputs = false;
                // Observers must not mask the original export error or prevent other
                // observers from updating their stale-input state.
                if (ExportCompleted != null) foreach (System.Action<bool> handler in ExportCompleted.GetInvocationList()) {
                    try { handler(exported); }
                    catch (System.Exception exception) { UnityEngine.Debug.LogException(exception); }
                }
            }
        }

        public const string PROGRESS_BAR_CAPTION = "[ ME.BECS ] CodeGenerator";

        private static bool Build(System.Collections.Generic.List<AssemblyInfo> asms, string dir, out string publishedContent, bool editorAssembly = false) {
            publishedContent = null;
            using var sourceGeneratorLookup = SourceGeneratorBridge.BeginLookupScope();
            using var timings = new CodeGeneratorTimings(editorAssembly);

            string postfix;
            if (editorAssembly == true) {
                postfix = "Editor";
            } else {
                postfix = "Runtime";
            }

            var generators = SourceGeneratorInputManifest.CreateFeeders();

            if (System.IO.Directory.Exists(dir) == false) {
                System.IO.Directory.CreateDirectory(dir);
            }

            UnityEditor.EditorUtility.DisplayProgressBar(PROGRESS_BAR_CAPTION, $"Build {dir}", 0f);
            var componentTypes = new System.Collections.Generic.List<System.Type>();
            var exportSucceeded = false;
            string inputResponse = null;
            try {
                var path = @$"{dir}/{ECS}.Gen.cs";
                var filesPath = @$"{dir}/{ECS}.Files";
                // One discovery snapshot supplies both compiler inputs and references.
                // Validate the retirement plan before publishing any new inputs.
                var inputManifest = SourceGeneratorInputManifest.PrepareActiveInputs($"{ECS}.Gen.{postfix}", editorAssembly, generators, out _, componentTypes);
                publishedContent = inputManifest;
                var retiredFiles = SourceGeneratorExportContract.GetRetiredFileNames(generators);
                componentTypes.AddRange(generators.Select(generator => generator.GetType()));
                CodeGeneratorTimings.Stage("Write inputs / retire legacy output", 0.95f);
                var inputPath = SourceGeneratorInputTransport.Publish(editorAssembly, inputManifest);
                inputResponse = SourceGeneratorInputTransport.CompilerResponse(inputPath, inputManifest);
                var newContent = SourceGeneratorExportContract.RetirementComment;
                {
                    var prevContent = System.IO.File.Exists(path) == true ? System.IO.File.ReadAllText(path) : string.Empty;
                    if (prevContent != newContent) {
                        System.IO.File.WriteAllText(path, newContent);
                        UnityEditor.AssetDatabase.ImportAsset(path);
                    }
                }

                // Retire only explicitly declared old outputs. Preserve their .meta
                // files and any unrelated content; fresh projects need no extra stubs.
                foreach (var filename in retiredFiles) {
                    var filepath = $"{filesPath}/{filename}.cs";
                    if (!System.IO.File.Exists(filepath) || System.IO.File.ReadAllText(filepath) == newContent) continue;
                    System.IO.File.WriteAllText(filepath, newContent);
                    UnityEditor.AssetDatabase.ImportAsset(filepath);
                }
                exportSucceeded = true;
            } catch (System.OperationCanceledException) {
                Logger.Editor.Log("[ ME.BECS ] Input export cancelled during analysis; no inputs were published for this target.");
            } catch (System.Exception ex) {
                UnityEngine.Debug.LogException(ex);
            } finally {
                UnityEditor.EditorUtility.ClearProgressBar();
            }
            // A failed analysis must not publish an asmdef built from a partial
            // reference set, nor create a consumer without its input manifest.
            // Preserve the last complete consumer and the original export error.
            if (!exportSucceeded) return false;
            CodeGeneratorTimings.Stage("Assembly references", 0.98f, cancellable: false);
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

                var references = SourceGeneratorInputManifest.GetAssemblyReferenceNames(asms,
                    UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ISystem)).Concat(componentTypes), editorAssembly);
                var newContent = template.Replace("{{CONTENT}}", string.Join(@""",""", references));
                var prevContent = System.IO.File.Exists(path) == true ? System.IO.File.ReadAllText(path) : string.Empty;
                if (prevContent != newContent) {
                    System.IO.File.WriteAllText(path, newContent);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }
                // Native project-owned additional files carry the compiler inputs.
                // Do not put project selection paths or hashes into response files.
                var response = inputResponse;
                if (!System.IO.File.Exists(csc) || System.IO.File.ReadAllText(csc) != response) {
                    System.IO.File.WriteAllText(csc, response);
                    UnityEditor.AssetDatabase.ImportAsset(csc);
                }
            }

            return exportSucceeded;
        }

        public static void PatchSystemsList(System.Collections.Generic.List<System.Type> types) {

            var genericTypes = new System.Collections.Generic.HashSet<System.Type>(types.Count);
            for (var index = 0; index < types.Count; ++index) {

                var type = types[index];
                if (type.IsValueType == false) continue;

                if (type.IsGenericType == true && type.ContainsGenericParameters && genericTypes.Contains(type) == false) {
                    types.RemoveAt(index);
                    --index;
                    var typeGen = EditorUtils.GetFirstInterfaceConstraintType(type);
                    if (typeGen != null) {
                        // Systems must use the same constraint/exclusion filter as graph allocation and execution.
                        // Jobs still use their existing expansion path.
                        var genTypes = typeof(ISystem).IsAssignableFrom(type)
                            ? EditorUtils.GetTypesDerivedFrom(typeGen, type).OrderBy(x => x.FullName, System.StringComparer.Ordinal)
                                .ThenBy(x => x.Assembly.FullName, System.StringComparer.Ordinal).ToArray()
                            : UnityEditor.TypeCache.GetTypesDerivedFrom(typeGen).OrderBy(x => x.FullName).ToArray();
                        foreach (var genType in genTypes) {
                            // TypeCache also returns generic component definitions. They
                            // cannot be arguments of a concrete job registration.
                            if (genType.IsValueType == false || genType.ContainsGenericParameters) continue;
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
