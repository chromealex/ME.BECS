namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Names = ME.BECS.CodeGeneration.SourceGeneratorNames;

    // Production-only, content-addressed summaries. Diagnostics/default sessions
    // always compute independently. No generated consumers or runtime IDs are cached.
    internal sealed class ILPersistentAnalysis : IDisposable {
        [Serializable] private sealed class Dependency {
            public ILContentFingerprint.MethodReference method;
            public string mvid;
            public string body;
            [NonSerialized] internal MethodBase resolved;
        }
        [Serializable] private sealed class Record {
            public string key;
            public string context;
            public string implementation;
            public Dependency[] dependencies;
            public string payload;
        }
        [Serializable] private sealed class AssemblyRecord {
            public string name;
            public string mvid;
            public string declarations;
            public string scheduled;
            public int declarationFormat;
        }
        [Serializable] private sealed class Database {
            public int version;
            public string context;
            public AssemblyRecord[] assemblies;
            public Dependency[] methods;
            public CompactRecord[] records;
        }
        [Serializable] private sealed class CompactRecord {
            public string key;
            // Optional in v2 files written before analyzer partitioning. A
            // missing stamp is always a cache miss, not a format/load failure.
            public string implementation;
            public int[] dependencies;
            public string payload;
        }
        [Serializable] private sealed class Envelope { public string data; public string checksum; }
        private const string CacheHeader = "ME.BECS.ILCache.v3";

        internal static string ReadCacheData(TextReader reader) {
            var first = reader.ReadLine();
            string data, checksum;
            if (first == CacheHeader) {
                checksum = reader.ReadLine();
                data = reader.ReadToEnd();
            } else {
                // Read previous installations without forcing a full IL rebuild.
                var envelope = UnityEngine.JsonUtility.FromJson<Envelope>((first ?? "") + "\n" + reader.ReadToEnd());
                data = envelope?.data;
                checksum = envelope?.checksum;
            }
            if (data == null || checksum != Names.Hash(data)) throw new FormatException("Invalid incremental IL cache checksum.");
            return data;
        }

        internal static void WriteCacheData(TextWriter writer, string data) {
            writer.WriteLine(CacheHeader);
            writer.WriteLine(Names.Hash(data));
            writer.Write(data);
        }

        [ThreadStatic] private static ILPersistentAnalysis current;
        private const int DeclarationFormat = 2;
        private readonly ILPersistentAnalysis previous;
        private readonly Dictionary<string, Record> records = new Dictionary<string, Record>(StringComparer.Ordinal);
        private readonly Dictionary<string, AssemblyRecord> assemblies = new Dictionary<string, AssemblyRecord>(StringComparer.Ordinal);
        private readonly Dictionary<MethodBase, string> bodies = new Dictionary<MethodBase, string>();
        private readonly Dictionary<string, MethodBase> resolved = new Dictionary<string, MethodBase>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> valid = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<(string method, string body), Dependency> dependencyVersions = new Dictionary<(string, string), Dependency>();
        private readonly Dictionary<MethodBase, Dependency> currentDependencies = new Dictionary<MethodBase, Dependency>();
        private readonly Dictionary<Dependency, bool> validDependencies = new Dictionary<Dependency, bool>();
        private readonly Dictionary<string, (int hits, int misses)> statistics = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> missReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly string path;
        private readonly bool rebuild;
        private readonly ILAnalysisEnvironment environment;
        private string context;
        private string previousContext;
        private string scheduledImplementation;
        private bool dirty;
        private bool loaded;
        internal static bool Active => current != null;

        internal ILPersistentAnalysis(bool rebuild) : this(rebuild, ILAnalysisEnvironment.Capture()) { }

        // Consumed only after the producing worker has completed (including Dispose).
        // No mutable cache is concurrently shared with the publishing Editor thread.
        internal sealed class Snapshot {
            private ILPersistentAnalysis source;
            internal Snapshot(ILPersistentAnalysis source) { this.source = source; }
            internal ILPersistentAnalysis Take() => System.Threading.Interlocked.Exchange(ref this.source, null)
                ?? throw new InvalidOperationException("Persistent IL snapshot has already been consumed.");
        }

        internal Snapshot CaptureSnapshot() => new Snapshot(this);

        internal ILPersistentAnalysis(bool rebuild, Snapshot snapshot) : this(rebuild, ILAnalysisEnvironment.Capture()) {
            try {
            var source = snapshot.Take();
            if (rebuild || source.path != this.path || source.environment.target != this.environment.target) return;
            foreach (var pair in source.records) this.records.Add(pair.Key, pair.Value);
            foreach (var pair in source.assemblies) this.assemblies.Add(pair.Key, pair.Value);
            foreach (var pair in source.dependencyVersions) this.dependencyVersions.Add(pair.Key, pair.Value);
            this.loaded = source.loaded;
            this.previousContext = source.context ?? source.previousContext;
            // Recompute context against the current captured environment. Record
            // dependency validation remains active; the handoff only replaces IO/JSON.
            } catch {
                current = this.previous;
                throw;
            }
        }

        internal ILPersistentAnalysis(bool rebuild, ILAnalysisEnvironment environment) {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.previous = current;
            this.rebuild = rebuild;
            this.path = environment.cachePath;
            current = this;
        }

        internal static T Get<T, TData>(string kind, string identity, Func<T> analyze, Func<T, TData> encode, Func<TData, T> decode) {
            var scope = current;
            if (scope == null || identity == null) return analyze();
            scope.EnsureLoaded();
            var key = kind + "\n" + identity;
            var context = scope.Context();
            var implementation = ImplementationStamp(kind, scope.scheduledImplementation);
            scope.records.TryGetValue(key, out var record);
            var miss = scope.rebuild ? "rebuild" : record == null ? "missing" : record.context != context ? "context" :
                record.implementation != implementation ? "implementation" : "dependencies";
            if (!scope.rebuild && record != null && record.context == context && record.implementation == implementation) {
                try {
                    if (scope.Validate(record)) {
                        var result = decode(UnityEngine.JsonUtility.FromJson<TData>(record.payload));
                        foreach (var dependency in record.dependencies) ILDependencyCapture.Observe(dependency.resolved);
                        scope.Count(kind, true);
                        return result;
                    }
                } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                    // Invalid data/type identities are cache misses, never partial results.
                    miss = "decode/resolve: " + exception.GetType().Name;
                }
            }
            scope.Count(kind, false);
            var reasonKey = kind + "/" + miss;
            scope.missReasons.TryGetValue(reasonKey, out var misses);
            scope.missReasons[reasonKey] = misses + 1;
            using var capture = new ILDependencyCapture();
            var computed = analyze(); // Exceptions/cancellation must not be cached or swallowed.
            try {
                var dependencies = capture.Methods.Select(scope.GetDependency).OrderBy(item => item.method.Key, StringComparer.Ordinal).ToArray();
                scope.records[key] = new Record { key = key, context = context, implementation = implementation, dependencies = dependencies,
                    payload = UnityEngine.JsonUtility.ToJson(encode(computed)) };
                scope.valid[key] = true;
                scope.dirty = true;
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                // Open/unsupported reflection identities remain an uncached analysis.
                if (scope.records.Remove(key)) scope.dirty = true;
                scope.valid.Remove(key);
            }
            return computed;
        }

        internal static string MethodIdentity(MethodBase method) => method?.DeclaringType?.AssemblyQualifiedName == null || method.ContainsGenericParameters
            ? null : ILContentFingerprint.MethodReference.From(method).Key;
        internal static MethodBase ResolveMethod(ILContentFingerprint.MethodReference reference) => current == null ? reference.Resolve() : current.Resolve(reference);

        private string Body(MethodBase method) {
            CodeGeneratorTimings.Work(method);
            if (!this.bodies.TryGetValue(method, out var fingerprint)) this.bodies.Add(method, fingerprint = ILContentFingerprint.Body(method));
            return fingerprint;
        }

        private MethodBase Resolve(ILContentFingerprint.MethodReference reference) {
            if (!this.resolved.TryGetValue(reference.Key, out var method)) this.resolved.Add(reference.Key, method = reference.Resolve());
            return method;
        }

        private Dependency GetDependency(MethodBase method) {
            if (this.currentDependencies.TryGetValue(method, out var dependency)) return dependency;
            var reference = ILContentFingerprint.MethodReference.From(method);
            var body = this.Body(method);
            var key = (reference.Key, body);
            if (!this.dependencyVersions.TryGetValue(key, out dependency)) {
                dependency = new Dependency { method = reference, body = body };
                this.dependencyVersions.Add(key, dependency);
            }
            // Intern versions, not just method identities: a newly analyzed body
            // must never "refresh" the old body expected by another cached root.
            dependency.mvid = method.Module.ModuleVersionId.ToString("D");
            dependency.resolved = method;
            this.validDependencies[dependency] = true;
            this.currentDependencies.Add(method, dependency);
            return dependency;
        }

        private bool ValidateDependency(Dependency dependency) {
            if (this.validDependencies.TryGetValue(dependency, out var valid)) return valid;
            var method = this.Resolve(dependency.method);
            valid = BodyIsCurrent(method, dependency.mvid, dependency.body, this.Body);
            if (valid) {
                dependency.resolved = method;
                var currentMvid = method.Module.ModuleVersionId.ToString("D");
                if (dependency.mvid != currentMvid) {
                    dependency.mvid = currentMvid;
                    this.dirty = true;
                }
            }
            this.validDependencies.Add(dependency, valid);
            return valid;
        }

        private bool Validate(Record record) {
            if (this.valid.TryGetValue(record.key, out var value)) return value;
            value = record.dependencies != null && record.payload != null;
            if (value) foreach (var dependency in record.dependencies) {
                if (!this.ValidateDependency(dependency)) {
                    value = false;
                    break;
                }
            }
            this.valid[record.key] = value;
            return value;
        }

        internal static bool BodyIsCurrent(MethodBase method, string mvid, string body, Func<MethodBase, string> fingerprint) =>
            method != null && (method.Module.ModuleVersionId.ToString("D") == mvid || fingerprint(method) == body);

        internal static string ImplementationStamp(string kind, string scheduled) {
            // These summaries do not call ILScheduledJobs. Shared contracts such
            // as IsSchedulingMethod/ILCallTargets remain in the common context.
            // New/unknown consumers conservatively depend on every partition.
            switch (kind) {
                case "discovery":
                case "entity-counts":
                case "safety":
                case "system-dependencies":
                case "weights": return "shared-v1";
                default: return "scheduled-v1:" + scheduled;
            }
        }

        private string Context() {
            if (this.context != null) return this.context;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var scriptAssemblies = this.environment.scripts;
            var scripts = new HashSet<string>(scriptAssemblies.Select(assembly => assembly.name), StringComparer.Ordinal);
            var dependants = SourceGeneratorCodeIdentity.FindConsumerDependants(scriptAssemblies
                .Select(assembly => new KeyValuePair<string, string[]>(assembly.name, assembly.references)));
            var parts = new List<string> { "IL-analysis-v4", this.environment.target };
            var changedDeclarations = new List<string>();
            parts.AddRange(SourceGeneratorCodeIdentity.Libraries(scriptAssemblies.SelectMany(assembly => assembly.libraries), scripts));
            foreach (var script in scriptAssemblies) parts.Add(script.name + ":" + string.Join(";", script.defines.OrderBy(value => value, StringComparer.Ordinal)));
            var loaded = SourceGeneratorCodeIdentity.LoadedScripts(this.environment.loaded, scripts)
                .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal).ToArray();
            var changed = loaded.Where(assembly => assembly != typeof(ILPersistentAnalysis).Assembly &&
                !dependants.Contains(assembly.GetName().Name) &&
                (!this.assemblies.TryGetValue(assembly.FullName, out var previous) ||
                 previous.mvid != assembly.ManifestModule.ModuleVersionId.ToString("D") || previous.declarationFormat != DeclarationFormat)).ToArray();
            var fingerprints = ILAnalysisSession.IndexDeclarations(changed);
            var declarations = changed.Select((assembly, index) => (assembly, index))
                .ToDictionary(item => item.assembly, item => fingerprints[item.index]);
            // Workers write only their result slot; mutate the persistent cache
            // here in the same ordinal assembly order as sequential indexing.
            foreach (var assembly in loaded) {
                var mvid = assembly.ManifestModule.ModuleVersionId.ToString("D");
                if (assembly == typeof(ILPersistentAnalysis).Assembly) {
                    if (!this.assemblies.TryGetValue(assembly.FullName, out var analyzer) || analyzer.mvid != mvid ||
                        analyzer.declarationFormat != DeclarationFormat || string.IsNullOrEmpty(analyzer.scheduled)) {
                        CodeGeneratorTimings.Subject("Index analyzer implementation: " + assembly.GetName().Name);
                        var fingerprint = ILContentFingerprint.AnalyzerContent(assembly);
                        analyzer = new AssemblyRecord { name = assembly.FullName, mvid = mvid,
                            declarations = fingerprint.shared, scheduled = fingerprint.scheduled, declarationFormat = DeclarationFormat };
                        this.assemblies[assembly.FullName] = analyzer;
                        this.dirty = true;
                    }
                    this.scheduledImplementation = analyzer.scheduled;
                    parts.Add(assembly.FullName + ":" + analyzer.declarations);
                    continue;
                }
                if (dependants.Contains(assembly.GetName().Name)) {
                    var script = scriptAssemblies.Single(item => item.name == assembly.GetName().Name);
                    parts.Add(assembly.FullName + ":" + SourceGeneratorCodeIdentity.Stamp(assembly, script.sources, dependants));
                    continue;
                }
                // Precompiled libraries have a conservative file-content boundary
                // above, independent of lazy runtime loading. Script bodies are
                // validated separately; analyzer bodies are partitioned above.
                if (!this.assemblies.TryGetValue(assembly.FullName, out var stamp) || stamp.mvid != mvid || stamp.declarationFormat != DeclarationFormat) {
                    CodeGeneratorTimings.Subject("Index declarations: " + assembly.GetName().Name);
                    var previousDeclarations = stamp?.declarations;
                    stamp = new AssemblyRecord { name = assembly.FullName, mvid = mvid,
                        declarations = declarations[assembly], declarationFormat = DeclarationFormat };
                    if (previousDeclarations != null && previousDeclarations != stamp.declarations) changedDeclarations.Add(assembly.GetName().Name);
                    this.assemblies[assembly.FullName] = stamp;
                    this.dirty = true;
                }
                parts.Add(assembly.FullName + ":" + stamp.declarations);
            }
            parts.Sort(StringComparer.Ordinal);
            this.context = Names.Hash(string.Join("\n", parts));
            if (this.previousContext != null && this.previousContext != this.context)
                UnityEngine.Debug.Log("[ME.BECS] Incremental IL context changed. Changed declaration assemblies: " +
                    (changedDeclarations.Count == 0 ? "none (analyzer, references, defines or inventory changed)" : string.Join(", ", changedDeclarations)) + ".");
            UnityEngine.Debug.Log("[ME.BECS] Incremental IL declaration index: " + watch.ElapsedMilliseconds + " ms. Method bodies are validated separately.");
            return this.context;
        }

        private void Count(string kind, bool hit) {
            this.statistics.TryGetValue(kind, out var count);
            this.statistics[kind] = (count.hits + (hit ? 1 : 0), count.misses + (hit ? 0 : 1));
        }

        private void EnsureLoaded() {
            if (this.loaded) return;
            this.loaded = true;
            if (this.rebuild) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try {
                if (!File.Exists(this.path)) return;
                using var reader = new StreamReader(this.path, Encoding.UTF8);
                var data = UnityEngine.JsonUtility.FromJson<Database>(ReadCacheData(reader));
                var restored = Unpack(data);
                this.previousContext = data.context;
                foreach (var record in restored) this.records.Add(record.key, record);
                foreach (var assembly in data.assemblies) this.assemblies.Add(assembly.name, assembly);
                foreach (var dependency in data.methods) this.dependencyVersions.Add((dependency.method.Key, dependency.body), dependency);
                UnityEngine.Debug.Log("[ME.BECS] Incremental IL cache loaded: " + restored.Length + " summaries, " + data.methods.Length +
                    " method versions, " + watch.ElapsedMilliseconds + " ms.");
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                this.records.Clear(); this.assemblies.Clear();
                this.dependencyVersions.Clear();
            }
        }

        private static Database Pack(IEnumerable<Record> records, IEnumerable<AssemblyRecord> assemblies, string context) {
            var methods = new List<Dependency>();
            var indices = new Dictionary<(string method, string body), int>();
            var packed = new List<CompactRecord>();
            foreach (var record in records.Where(record => record.context == context).OrderBy(record => record.key, StringComparer.Ordinal)) {
                var dependencies = new int[record.dependencies.Length];
                for (var i = 0; i < dependencies.Length; ++i) {
                    var dependency = record.dependencies[i];
                    var key = (dependency.method.Key, dependency.body);
                    if (!indices.TryGetValue(key, out var index)) {
                        index = methods.Count;
                        indices.Add(key, index);
                        methods.Add(dependency);
                    }
                    dependencies[i] = index;
                }
                packed.Add(new CompactRecord { key = record.key, implementation = record.implementation, payload = record.payload, dependencies = dependencies });
            }
            return new Database { version = 2, context = context, methods = methods.ToArray(), records = packed.ToArray(),
                assemblies = assemblies.OrderBy(assembly => assembly.name, StringComparer.Ordinal).ToArray() };
        }

        private static Record[] Unpack(Database data) {
            if (data?.version != 2 || string.IsNullOrEmpty(data.context) || data.methods == null || data.records == null || data.assemblies == null)
                throw new FormatException("Invalid incremental IL cache header.");
            foreach (var dependency in data.methods) {
                if (dependency?.method == null || string.IsNullOrEmpty(dependency.method.owner) || string.IsNullOrEmpty(dependency.method.signature) ||
                    string.IsNullOrEmpty(dependency.mvid) || string.IsNullOrEmpty(dependency.body)) throw new FormatException("Invalid cached IL method version.");
            }
            return data.records.Select(record => {
                if (string.IsNullOrEmpty(record?.key) || record.payload == null || record.dependencies == null)
                    throw new FormatException("Invalid cached IL summary.");
                var dependencies = new Dependency[record.dependencies.Length];
                for (var i = 0; i < dependencies.Length; ++i) {
                    var index = record.dependencies[i];
                    if ((uint)index >= (uint)data.methods.Length) throw new FormatException("Invalid cached IL method index.");
                    dependencies[i] = data.methods[index];
                }
                return new Record { key = record.key, context = data.context, implementation = record.implementation, payload = record.payload, dependencies = dependencies };
            }).ToArray();
        }

        public void Dispose() {
            current = this.previous;
            if (this.statistics.Count > 0) UnityEngine.Debug.Log("[ME.BECS] Incremental IL summaries:\n" + string.Join("\n", this.statistics
                .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => "  " + pair.Key + ": reused=" + pair.Value.hits + ", analyzed=" + pair.Value.misses)) +
                (this.missReasons.Count == 0 ? "" : "\n  Miss reasons: " + string.Join(", ", this.missReasons.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "=" + pair.Value))));
            // Cancellation while indexing declarations has no complete context.
            // Preserve the last valid cache instead of replacing it with a partial
            // database whose null context would be rejected on the next attempt.
            if (!this.dirty || string.IsNullOrEmpty(this.context)) return;
            var temporary = this.path + ".tmp";
            try {
                // Drop obsolete declaration universes; changed bodies replace their
                // own keys. No stale summary is combined with a new type inventory.
                var database = Pack(this.records.Values, this.assemblies.Values, this.context);
                var data = UnityEngine.JsonUtility.ToJson(database);
                Directory.CreateDirectory(Path.GetDirectoryName(this.path));
                using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false))) WriteCacheData(writer, data);
                if (File.Exists(this.path)) File.Replace(temporary, this.path, null); else File.Move(temporary, this.path);
                UnityEngine.Debug.Log("[ME.BECS] Incremental IL cache saved: " + database.records.Length + " summaries, " + database.methods.Length +
                    " method versions, " + new FileInfo(this.path).Length + " bytes.");
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Could not persist incremental IL cache: " + exception.Message);
            } finally {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
