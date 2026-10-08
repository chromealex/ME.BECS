using System.Reflection;
using Newtonsoft.Json;

namespace ME.BECS.Editor {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using System.Linq;
    using scg = System.Collections.Generic;

    /// <summary>
    /// Supplies code generator order metadata to annotated declarations.
    /// </summary>
    public class CodeGeneratorOrderAttribute : System.Attribute {

        /// <summary>
        /// Order used by <c>CodeGeneratorOrderAttribute</c>.
        /// </summary>
        public int order;

        /// <summary>
        /// Initializes <c>CodeGeneratorOrderAttribute</c> from the supplied order.
        /// </summary>
        public CodeGeneratorOrderAttribute(int order) {
            this.order = order;
        }

    }

    /// <summary>
    /// Stores method pointer data for the associated editor API.
    /// </summary>
    public struct MethodPointerData : System.IEquatable<MethodPointerData> {

        // Safety/weight diagnostics distinguish closed generic methods and overloads.
        // Entity reservations use the call-site graph, not this legacy comparer.
        /// <summary>
        /// Exact comparer used by <c>MethodPointerData</c>.
        /// </summary>
        public static readonly System.Collections.Generic.IEqualityComparer<MethodPointerData> ExactComparer = new ExactMethodComparer();

        private sealed class ExactMethodComparer : System.Collections.Generic.IEqualityComparer<MethodPointerData> {
            public bool Equals(MethodPointerData x, MethodPointerData y) =>
                object.Equals(x.originalMethodInfo, y.originalMethodInfo) && x.rootType == y.rootType;

            public int GetHashCode(MethodPointerData value) =>
                (value.originalMethodInfo?.GetHashCode() ?? 0) ^ (value.rootType?.GetHashCode() ?? 0);
        }

        private MethodInfo originalMethodInfo;
        private System.Type rootType;

        /// <summary>
        /// Initializes <c>MethodPointerData</c> from the supplied original method info, root type.
        /// </summary>
        public MethodPointerData(MethodInfo originalMethodInfo, System.Type rootType = null) {
            this.originalMethodInfo = originalMethodInfo;
            this.rootType = rootType;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
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

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public override bool Equals(object obj) {
            return obj is MethodPointerData other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() {
            if (this.originalMethodInfo.IsGenericMethod == true) {
                return this.originalMethodInfo.Name.GetHashCode() ^ this.originalMethodInfo.ReturnType.GetHashCode() ^ this.originalMethodInfo.GetGenericMethodDefinition().GetHashCode() ^ (this.rootType != null ? this.rootType.GetHashCode() : 0);
            }
            return (this.originalMethodInfo != null ? this.originalMethodInfo.GetHashCode() : 0) ^ (this.rootType != null ? this.rootType.GetHashCode() : 0);
        }

    }
    
    /// <summary>
    /// Defines file content state and operations.
    /// </summary>
    public struct FileContent {

        /// <summary>
        /// Filename used by <c>FileContent</c>.
        /// </summary>
        public string filename;
        /// <summary>
        /// Content used by <c>FileContent</c>.
        /// </summary>
        public string content;

    }

    /// <summary>
    /// Caches  data for reuse.
    /// </summary>
    public class Cache {

        /// <summary>
        /// Stores cached item for <c>Cache</c>.
        /// </summary>
        [System.Serializable]
        public struct CachedItem {

            /// <summary>
            /// Indicates hash codes.
            /// </summary>
            public string[] hashCodes;
            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            [UnityEngine.SerializeReference]
            public object data;

        }

        /// <summary>
        /// Stores a key record used by <c>Cache</c>.
        /// </summary>
        public readonly struct Key {

            private readonly System.Type type;
            private readonly string method;
            private readonly string key;

            /// <summary>
            /// Initializes <c>Key</c> from the supplied type, method, key.
            /// </summary>
            public Key(System.Type type, string method, string key) {
                this.type = type;
                this.method = method;
                this.key = key;
            }

            /// <summary>
            /// Formats this value for display or diagnostics.
            /// </summary>
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

        /// <summary>
        /// Adds the supplied entry to cache.
        /// </summary>
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

        /// <summary>
        /// Attempts to get value and reports whether the operation succeeded.
        /// </summary>
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

        /// <summary>
        /// Sets key.
        /// </summary>
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

    /// <summary>
    /// Exports custom registration data for generated code.
    /// </summary>
    public abstract class CustomCodeGenerator {

        /// <summary>
        /// Cached data reused by the associated operation.
        /// </summary>
        public Cache cache;

        /// <summary>
        /// Dir used by <c>CustomCodeGenerator</c>.
        /// </summary>
        public string dir;
        /// <summary>
        /// Asms used by <c>CustomCodeGenerator</c>.
        /// </summary>
        public System.Collections.Generic.List<AssemblyInfo> asms;
        /// <summary>
        /// Whether editor assembly behavior or state is selected.
        /// </summary>
        public bool editorAssembly;
        /// <summary>
        /// Systems used by <c>CustomCodeGenerator</c>.
        /// </summary>
        public System.Collections.Generic.List<System.Type> systems;
        /// <summary>
        /// Job types used by <c>CustomCodeGenerator</c>.
        /// </summary>
        public System.Collections.Generic.List<System.Type> jobTypes;
        /// <summary>
        /// Entity types used by <c>CustomCodeGenerator</c>.
        /// </summary>
        public System.Collections.Generic.List<System.Type> entityTypes;
        /// <summary>
        /// Aspect descriptors used by this operation.
        /// </summary>
        public System.Collections.Generic.List<System.Type> aspects;

        /// <summary>
        /// Tests whether the context is valid type for assembly.
        /// </summary>
        public bool IsValidTypeForAssembly(System.Type type, bool runtimeInEditor = true) {

            return EditorUtils.IsValidTypeForAssembly(this.editorAssembly, type, this.asms, runtimeInEditor);

        }

        /// <summary>
        /// Provides the <c>AddInitialization</c> callback; this implementation performs no work.
        /// </summary>
        public virtual void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) { }

        // Addon input transport only. Implementations export data records, never C# bodies.
        /// <summary>
        /// Provides the <c>AppendSourceGeneratorInputs</c> callback; this implementation performs no work.
        /// </summary>
        public virtual void AppendSourceGeneratorInputs(System.Text.StringBuilder manifest) { }

        // Resumable form used by the sliced background export: a long feeder may
        // yield between independent parts. Must produce exactly the same text.
        /// <summary>
        /// Produces incremental steps for exporting this feature's source-generator inputs.
        /// </summary>
        public virtual System.Collections.IEnumerator AppendSourceGeneratorInputsSteps(System.Text.StringBuilder manifest) {
            this.AppendSourceGeneratorInputs(manifest);
            yield break;
        }

        // Dependencies of source-emitted code, independent of legacy C# callbacks.
        /// <summary>
        /// Provides the <c>AddSourceGeneratorReferences</c> callback; this implementation performs no work.
        /// </summary>
        public virtual void AddSourceGeneratorReferences(scg::List<System.Type> references) { }

        // Opt in only for feeders whose output depends exclusively on compiled
        // code and the selected type lists, never on asset values or graph topology.
        /// <summary>
        /// Gets cache compiled inputs; this implementation returns <c>false</c>.
        /// </summary>
        public virtual bool CacheCompiledInputs => false;
        internal System.Type[] preparedInputReferences;
        internal void AddPreparedInputReferences(scg::List<System.Type> references) {
            if (this.preparedInputReferences != null) references.AddRange(this.preparedInputReferences);
            else this.AddSourceGeneratorReferences(references);
        }

        // Declarative compiler-owned initialization; null is rejected at export preflight.
        /// <summary>
        /// Source initialization kind used by <c>CustomCodeGenerator</c>.
        /// </summary>
        public virtual string SourceInitializationKind => this.GetType().GetMethod(nameof(AddInitialization),
            new[] { typeof(scg::List<string>), typeof(scg::List<System.Type>) })?.DeclaringType == typeof(CustomCodeGenerator) ? "none" : null;

        /// <summary>
        /// Source registration kind used by <c>CustomCodeGenerator</c>.
        /// </summary>
        public virtual string SourceRegistrationKind => this.GetType().GetMethod(nameof(AddMethods),
            new[] { typeof(scg::List<System.Type>) })?.DeclaringType == typeof(CustomCodeGenerator) ? "none" : null;

        /// <summary>
        /// Adds methods.
        /// </summary>
        public virtual scg::List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
        }

        /// <summary>
        /// Adds public content.
        /// </summary>
        public virtual string AddPublicContent() {
            return string.Empty;
        }

        /// <summary>
        /// Adds file content.
        /// </summary>
        public virtual FileContent[] AddFileContent(System.Collections.Generic.List<System.Type> references) {
            return null;
        }

        // Filenames without .cs only. The exporter replaces existing outputs with
        // a fixed comment; no feeder-supplied C# is accepted. Legacy hooks above
        // remain recognizable solely to produce an actionable migration error.
        /// <summary>
        /// Returns retired source files.
        /// </summary>
        public virtual scg::IEnumerable<string> GetRetiredSourceFiles() => System.Array.Empty<string>();

    }

    /// <summary>
    /// Exports  registration data for generated code.
    /// </summary>
    public static class CodeGenerator {
        
        /// <summary>
        /// Defines method definition state and operations for <c>CodeGenerator</c>.
        /// </summary>
        public struct MethodDefinition {
            // Callback body and registration are owned by a source generator.
            /// <summary>
            /// Generated registration used by <c>CodeGenerator.MethodDefinition</c>.
            /// </summary>
            public string generatedRegistration;

            /// <summary>
            /// Method name used by <c>CodeGenerator.MethodDefinition</c>.
            /// </summary>
            public string methodName;
            /// <summary>
            /// Custom method params call used by <c>CodeGenerator.MethodDefinition</c>.
            /// </summary>
            public string customMethodParamsCall;
            /// <summary>
            /// Type descriptor used by the associated operation.
            /// </summary>
            public string type;
            /// <summary>
            /// Register method name used by <c>CodeGenerator.MethodDefinition</c>.
            /// </summary>
            public string registerMethodName;
            /// <summary>
            /// Definition used by <c>CodeGenerator.MethodDefinition</c>.
            /// </summary>
            public string definition;
            /// <summary>
            /// Content used by <c>CodeGenerator.MethodDefinition</c>.
            /// </summary>
            public string content;
            /// <summary>
            /// Whether burst compile behavior or state is selected.
            /// </summary>
            public bool burstCompile;
            /// <summary>
            /// P invoke used by <c>CodeGenerator.MethodDefinition</c>.
            /// </summary>
            public string pInvoke;

            /// <summary>
            /// Returns method params call.
            /// </summary>
            public string GetMethodParamsCall() {
                if (this.customMethodParamsCall != null) return this.customMethodParamsCall;
                return this.methodName;
            }

        }

        /// <summary>
        /// Ecs constant used by <c>CodeGenerator</c>.
        /// </summary>
        public const string ECS = "ME.BECS";
        /// <summary>
        /// Awake method constant used by <c>CodeGenerator</c>.
        /// </summary>
        public const string AWAKE_METHOD = "BurstCompileOnAwake";
        /// <summary>
        /// Start method constant used by <c>CodeGenerator</c>.
        /// </summary>
        public const string START_METHOD = "BurstCompileOnStart";
        /// <summary>
        /// Update method constant used by <c>CodeGenerator</c>.
        /// </summary>
        public const string UPDATE_METHOD = "BurstCompileOnUpdate";
        /// <summary>
        /// Destroy method constant used by <c>CodeGenerator</c>.
        /// </summary>
        public const string DESTROY_METHOD = "BurstCompileOnDestroy";
        /// <summary>
        /// Drawgizmos method constant used by <c>CodeGenerator</c>.
        /// </summary>
        public const string DRAWGIZMOS_METHOD = "BurstCompileOnDrawGizmos";

        /// <summary>
        /// Handles the scripts reload callback.
        /// </summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        public static void OnScriptsReload() {

            // Skip code generation if project creation is in progress
            if (UnityEditor.EditorPrefs.HasKey("ME.BECS.Editor.AwaitPackageImportData") == true) return;

            // Asset inputs must refresh after imports settle, not synchronously
            // inside assembly reload. The graph input scheduler coalesces this with
            // its own startup check and proves freshness from compiled metadata.
            InputRefreshRequested?.Invoke();

        }

        /// <summary>
        /// Requests publication of the inputs consumed by the BECS source generators.
        /// </summary>
        public static void RegenerateBurstAOT(bool forced = false, bool cleanCache = false) {
            TryRegenerateBurstAOT(forced, cleanCache);
        }

        /// <summary>
        /// Raised when export completed is reported by this API.
        /// </summary>
        public static event System.Action<bool> ExportCompleted;
        /// <summary>
        /// Raised when input refresh requested is reported by this API.
        /// </summary>
        public static event System.Action InputRefreshRequested;
        /// <summary>
        /// Last exported graph snapshot used by <c>CodeGenerator</c>.
        /// </summary>
        public static string LastExportedGraphSnapshot { get; private set; }
        private static bool exportingInputs;

        // Reports export completion only, not the result of Unity's later compilation.
        /// <summary>
        /// Attempts to publish source-generator inputs; returns false when publication cannot start or fails.
        /// </summary>
        public static bool TryRegenerateBurstAOT(bool forced = false, bool cleanCache = false) {
            if (SourceGeneratorInputRefresh.IsAnalyzing) return false;
            return TryRegenerateInputs(forced, cleanCache, null);
        }

        internal static bool PublishPreparedInputs(SourceGeneratorInputAnalysis.Result prepared) =>
            TryRegenerateInputs(true, prepared.rebuild, prepared);

        internal sealed class PendingPublication {
            internal scg.KeyValuePair<string, string>[] files;
            internal string codeFingerprint, graphSnapshot, compilerSnapshot, runtimeContent, editorContent;

            internal void Complete(bool written) {
                var success = false;
                try {
                    if (!written) return;
                    if (this.codeFingerprint != SourceGeneratorGraphSnapshot.GetCodeFingerprint() ||
                        this.graphSnapshot != SourceGeneratorGraphSnapshot.GetCurrent())
                        throw new System.InvalidOperationException("Inputs changed during background publication. Retry input generation.");
                    SourceGeneratorAnalysisReceipt.Commit(this.graphSnapshot, this.compilerSnapshot, this.runtimeContent, this.editorContent);
                    LastExportedGraphSnapshot = this.graphSnapshot;
                    success = true;
                } finally { FinishExport(success); }
            }
        }

        internal static PendingPublication PrepareBackgroundPublication(SourceGeneratorInputAnalysis.Result prepared) {
            PendingPublication pending = null;
            return TryRegenerateInputs(true, prepared.rebuild, prepared, value => pending = value) ? pending : null;
        }

        // Background export only: the Editor-thread publication as a resumable
        // sequence (see SourceGeneratorInputRefresh.PollBackground). Same checks,
        // same order and same single import batch as TryRegenerateInputs.
        internal static System.Collections.IEnumerator PrepareBackgroundPublicationSteps(SourceGeneratorInputAnalysis.Result prepared,
            System.Action<PendingPublication> accept) {
            if (exportingInputs) yield break;
            if (UnityEditor.EditorPrefs.HasKey("ME.BECS.Editor.AwaitPackageImportData") == true) yield break;
            if (UnityEngine.Application.isBatchMode == true) yield break;
            Logger.Editor.Log("[ ME.BECS ] Publishing source generator inputs (forced)");
            var exported = false;
            var deferred = false;
            exportingInputs = true;
            try {
                using var publicationBridges = SourceGeneratorPublicationBridges.BeginPlanning();
                var codeFingerprint = SourceGeneratorGraphSnapshot.GetCodeFingerprint();
                var graphSnapshot = SourceGeneratorGraphSnapshot.GetCurrent();
                if (prepared.codeFingerprint != codeFingerprint || prepared.fingerprint != graphSnapshot)
                    throw new System.InvalidOperationException("Background IL analysis is stale; no inputs were published. Retry export.");
                SourceGeneratorAnalysisReceipt.Invalidate();
                using var analysis = new ILAnalysisSession(codeFingerprint, prepared.rebuild, prepared.memo);
                using var incremental = new ILPersistentAnalysis(false, prepared.persistent);
                var compilerSnapshot = SourceGeneratorGraphSnapshot.GetCompilerSnapshot();
                var files = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                var contents = new string[2];
                foreach (var editor in new[] { false, true }) {
                    var built = new SourceGeneratorInputManifest.StepResult<string>();
                    var build = BuildSteps(built, editor, editor ? prepared.editor : prepared.runtime, files);
                    try { while (build.MoveNext()) yield return null; }
                    finally { (build as System.IDisposable)?.Dispose(); }
                    if (built.value == null) yield break;
                    contents[editor ? 1 : 0] = built.value;
                }
                if (codeFingerprint != SourceGeneratorGraphSnapshot.GetCodeFingerprint() ||
                    graphSnapshot != SourceGeneratorGraphSnapshot.GetCurrent()) {
                    throw new System.InvalidOperationException("Input assets or loaded script assemblies changed while preparing publication; no planned inputs were written. Retry input generation.");
                }
                accept(new PendingPublication { files = files.ToArray(), codeFingerprint = codeFingerprint,
                    graphSnapshot = graphSnapshot, compilerSnapshot = compilerSnapshot,
                    runtimeContent = contents[0], editorContent = contents[1] });
                deferred = true;
            } finally {
                // An exception or an abandoned sequence never leaves the export flag set.
                if (!deferred) FinishExport(exported);
            }
        }

        // Build(...) as steps; a failure is logged and leaves output.value null.
        private static System.Collections.IEnumerator BuildSteps(SourceGeneratorInputManifest.StepResult<string> output, bool editorAssembly,
            Systems.SystemDependenciesCodeGenerator.UsedObjects prepared, System.Collections.Generic.Dictionary<string, string> files) {
            using var sourceGeneratorLookup = SourceGeneratorBridge.BeginLookupScope();
            using var timings = new CodeGeneratorTimings(editorAssembly);
            var postfix = editorAssembly ? "Editor" : "Runtime";
            var generators = SourceGeneratorInputManifest.CreateFeeders();
            System.Collections.IEnumerator current = null;
            var stage = 0;
            var manifest = new SourceGeneratorInputManifest.StepResult<string>();
            var prepared2 = new SourceGeneratorInputManifest.StepResult<scg::KeyValuePair<string, string>[]>();
            var finished = false;
            try {
            while (!finished) {
                var more = false;
                try {
                    if (current == null) {
                        if (stage == 0) {
                            CodeGeneratorTimings.Stage("Prepare input export", 0f);
                            current = SourceGeneratorInputManifest.PrepareActiveInputsSteps(manifest, $"{ECS}.Gen.{postfix}", editorAssembly, generators, prepared);
                        } else if (stage == 1) {
                            CodeGeneratorTimings.Stage("Publish owner inputs", 0.97f, cancellable: false);
                            current = SourceGeneratorInputTransport.PrepareSteps(prepared2, editorAssembly, manifest.value);
                        } else {
                            foreach (var file in prepared2.value) files[file.Key] = file.Value;
                            output.value = manifest.value;
                            timings.Complete();
                            finished = true;
                        }
                    }
                    if (!finished) {
                        more = current.MoveNext();
                        if (!more) { (current as System.IDisposable)?.Dispose(); current = null; ++stage; }
                    }
                } catch (System.OperationCanceledException) {
                    timings.Cancelled();
                    Logger.Editor.Log("[ ME.BECS ] Input export cancelled during analysis; no inputs were published for this target.");
                    finished = true;
                } catch (System.Exception ex) {
                    UnityEngine.Debug.LogException(ex);
                    finished = true;
                }
                if (more) yield return null;
            }
            } finally {
                // Failed or abandoned: release the inner sequence's scopes (its finally blocks).
                (current as System.IDisposable)?.Dispose();
            }
        }

        private static bool TryRegenerateInputs(bool forced, bool cleanCache, SourceGeneratorInputAnalysis.Result prepared,
            System.Action<PendingPublication> accept = null) {
            if (exportingInputs) return false;
            
            // Skip if project creation is in progress
            if (UnityEditor.EditorPrefs.HasKey("ME.BECS.Editor.AwaitPackageImportData") == true) return false;

            if (CodeGeneratorMenu.IsEnabledAuto == false && forced == false) return false;

            if (UnityEngine.Application.isBatchMode == true) {
                Logger.Editor.Warning($"[ ME.BECS ] CodeGen won't run in batchmode. Ensure it was properly generated (or stored in the repo) before the build");
                return false;
            }

            Logger.Editor.Log($"[ ME.BECS ] Publishing source generator inputs {(forced == true ? "(forced)" : "")}");

            var exported = false;
            var deferred = false;
            exportingInputs = true;
            try {
                using var publicationBridges = SourceGeneratorPublicationBridges.BeginPlanning();
                var codeFingerprint = SourceGeneratorGraphSnapshot.GetCodeFingerprint();
                var graphSnapshot = SourceGeneratorGraphSnapshot.GetCurrent();
                if (prepared != null && (prepared.codeFingerprint != codeFingerprint || prepared.fingerprint != graphSnapshot))
                    throw new System.InvalidOperationException("Background IL analysis is stale; no inputs were published. Retry export.");
                SourceGeneratorAnalysisReceipt.Invalidate();
                using var analysis = prepared == null ? new ILAnalysisSession(codeFingerprint, cleanCache) :
                    new ILAnalysisSession(codeFingerprint, cleanCache, prepared.memo);
                using var incremental = prepared == null ? new ILPersistentAnalysis(cleanCache) :
                    new ILPersistentAnalysis(false, prepared.persistent);
                var compilerSnapshot = SourceGeneratorGraphSnapshot.GetCompilerSnapshot();
                // Keep both profiles in the same import batch. Inner profile
                // batches must not release Runtime inputs before Editor is ready.
                string runtimeContent, editorContent;
                var files = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                void PreparePublication(bool editor, string content) {
                    foreach (var file in SourceGeneratorInputTransport.Prepare(editor, content, out _)) files[file.Key] = file.Value;
                }
                if (!Build(out runtimeContent, prepared: prepared?.runtime, publication: PreparePublication)) return false;
                if (!Build(out editorContent, editorAssembly: true, prepared: prepared?.editor, publication: PreparePublication)) return false;
                if (codeFingerprint != SourceGeneratorGraphSnapshot.GetCodeFingerprint() ||
                    graphSnapshot != SourceGeneratorGraphSnapshot.GetCurrent()) {
                    throw new System.InvalidOperationException("Input assets or loaded script assemblies changed while preparing publication; no planned inputs were written. Retry input generation.");
                }
                if (accept != null) {
                    accept(new PendingPublication { files = files.ToArray(), codeFingerprint = codeFingerprint,
                        graphSnapshot = graphSnapshot, compilerSnapshot = compilerSnapshot,
                        runtimeContent = runtimeContent, editorContent = editorContent });
                    deferred = true;
                    return true;
                }
                UnityEditor.AssetDatabase.StartAssetEditing();
                try {
                    SourceGeneratorSystemFragments.ApplyPublication(files.ToArray());
                } finally {
                    UnityEditor.AssetDatabase.StopAssetEditing();
                }
                if (codeFingerprint != SourceGeneratorGraphSnapshot.GetCodeFingerprint() ||
                    graphSnapshot != SourceGeneratorGraphSnapshot.GetCurrent()) {
                    throw new System.InvalidOperationException("Input assets or loaded script assemblies changed during Runtime/Editor publication. Retry input generation.");
                }
                SourceGeneratorAnalysisReceipt.Commit(graphSnapshot, compilerSnapshot, runtimeContent, editorContent);
                LastExportedGraphSnapshot = graphSnapshot;
                exported = true;
                return true;
            } catch {
                deferred = false;
                throw;
            } finally {
                if (!deferred) FinishExport(exported);
            }
        }

        private static void FinishExport(bool exported) {
                exportingInputs = false;
                // Observers must not mask the original export error or prevent other
                // observers from updating their stale-input state.
                if (ExportCompleted != null) foreach (System.Action<bool> handler in ExportCompleted.GetInvocationList()) {
                    try { handler(exported); }
                    catch (System.Exception exception) { UnityEngine.Debug.LogException(exception); }
                }
        }

        /// <summary>
        /// Progress bar caption constant used by <c>CodeGenerator</c>.
        /// </summary>
        public const string PROGRESS_BAR_CAPTION = "[ ME.BECS ] CodeGenerator";

        private static bool Build(out string publishedContent, bool editorAssembly = false,
            Systems.SystemDependenciesCodeGenerator.UsedObjects? prepared = null, System.Action<bool, string> publication = null) {
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

            var exportSucceeded = false;
            try {
                CodeGeneratorTimings.Stage("Prepare input export", 0f);
                // The header retains its transport identity for existing snapshots.
                // Executable output belongs to independent owner fragments, not an
                // aggregate host. Never create/read/retire files in the former host.
                var inputManifest = SourceGeneratorInputManifest.PrepareActiveInputs($"{ECS}.Gen.{postfix}", editorAssembly, generators, out _, prepared: prepared);
                publishedContent = inputManifest;
                // Cancellation is safe during analysis, not between publishing
                // fragments, their compilation hosts and the completed receipt.
                CodeGeneratorTimings.Stage("Publish owner inputs", 0.97f, cancellable: false);
                if (publication == null) SourceGeneratorInputTransport.Publish(editorAssembly, inputManifest);
                else publication(editorAssembly, inputManifest);
                exportSucceeded = true;
            } catch (System.OperationCanceledException) {
                timings.Cancelled();
                Logger.Editor.Log("[ ME.BECS ] Input export cancelled during analysis; no inputs were published for this target.");
            } catch (System.Exception ex) {
                UnityEngine.Debug.LogException(ex);
            }
            if (!exportSucceeded) return false;

            timings.Complete();
            return exportSucceeded;
        }

        /// <summary>
        /// Patches systems list.
        /// </summary>
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
