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
        }
        [Serializable] private sealed class Record {
            public string key;
            public string context;
            public string implementation;
            public Dependency[] dependencies;
            public string payload;
            // Declaration scope: assemblies the analysis observed (its bodies, key
            // types, TypeCache results). Their reference closure is hashed into scope.
            public string[] roots;
            public string scope;
            // TypeCache.GetTypesDerivedFrom queries: "constraint\tresult hash".
            public string[] queries;
            public string[] types;
            public string typeScope;
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
            public string[] types;
            public CompactRecord[] records;
            public ILTypeDeclarationCache.Entry[] typeDeclarations;
        }
        [Serializable] private sealed class CompactRecord {
            public string key;
            // Optional in v2 files written before analyzer partitioning. A
            // missing stamp is always a cache miss, not a format/load failure.
            public string implementation;
            public int[] dependencies;
            public string payload;
            public string[] roots;
            public string scope;
            public string[] queries;
            public int[] types;
            public bool hasTypeCapture;
            public string typeScope;
        }
        [Serializable] private sealed class Envelope { public string data; public string checksum; }
        // v4: integrity checksum is FNV-1a/64 over UTF-16 code units. SHA-256 of the
        // ~70 MB cache (managed implementation under Mono) cost seconds on every
        // load and save; the cache needs corruption detection, not a secure digest.
        private const string CacheHeader = "ME.BECS.ILCache.v4";
        private const string Sha256CacheHeader = "ME.BECS.ILCache.v3";

        internal static string FastChecksum(string data) {
            var hash = 14695981039346656037UL;
            for (var i = 0; i < data.Length; ++i) {
                hash ^= data[i];
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture) + ":" +
                data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static string ReadCacheData(TextReader reader) {
            var first = reader.ReadLine();
            string data, checksum;
            if (first == CacheHeader) {
                checksum = reader.ReadLine();
                data = reader.ReadToEnd();
                if (checksum != FastChecksum(data)) throw new FormatException("Invalid incremental IL cache checksum.");
                return data;
            }
            if (first == Sha256CacheHeader) {
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
            writer.WriteLine(FastChecksum(data));
            writer.Write(data);
        }

        [ThreadStatic] private static ILPersistentAnalysis current;
        // v3 audits empty user attribute constructors independently of assembly MVID.
        // v4 skips compiler artifacts and stamps executable metadata by reachable code.
        // Reindex unchanged DLLs too; otherwise old stamps persist until their next rebuild.
        private const int DeclarationFormat = 5;
        // v3 records carry their own declaration scope and type queries.
        private const int DatabaseVersion = 3;
        private readonly ILPersistentAnalysis previous;
        private readonly Dictionary<string, Record> records = new Dictionary<string, Record>(StringComparer.Ordinal);
        private readonly Dictionary<string, AssemblyRecord> assemblies = new Dictionary<string, AssemblyRecord>(StringComparer.Ordinal);
        private readonly Dictionary<MethodBase, string> bodies = new Dictionary<MethodBase, string>();
        private readonly Dictionary<string, MethodBase> resolved = new Dictionary<string, MethodBase>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> valid = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<(string method, string body), Dependency> dependencyVersions = new Dictionary<(string, string), Dependency>();
        private readonly Dictionary<MethodBase, Dependency> currentDependencies = new Dictionary<MethodBase, Dependency>();
        private readonly Dictionary<Dependency, bool> validDependencies = new Dictionary<Dependency, bool>();
        // Validation belongs to the session, never to shared serialized records.
        private readonly Dictionary<Dependency, MethodBase> validatedMethods = new Dictionary<Dependency, MethodBase>();
        private readonly Dictionary<string, (int hits, int misses)> statistics = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> missReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly string path;
        private readonly bool rebuild;
        private readonly ILAnalysisEnvironment environment;
        private string context;
        private string previousContext;
        // Built with the context: per-assembly semantic declarations and the script
        // reference graph (generated consumers excluded). Read-only once built.
        private Dictionary<string, string> declarationsByName;
        private Dictionary<string, string[]> referencesByName;
        private readonly Dictionary<string, string> scopeHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly ILTypeDeclarationCache typeDeclarations = new ILTypeDeclarationCache();
        private readonly Dictionary<string, bool> currentQueries = new Dictionary<string, bool>(StringComparer.Ordinal);
        private string scheduledImplementation;
        private bool dirty;
        private bool loaded;
        private bool disposed;
        private bool persist = true;
        // Coordinator result handed to the Editor publication: its consumer saves
        // once (merged). Saved here only if nobody consumes it (cancel/supersede).
        private bool deferred;
        private bool inherited;
        private readonly HashSet<string> changedRecords = new HashSet<string>(StringComparer.Ordinal);
        internal static bool Active => current != null;

        internal ILPersistentAnalysis(bool rebuild) : this(rebuild, ILAnalysisEnvironment.Capture()) { }

        // Consumed only after the producing worker has completed (including Dispose).
        // No mutable cache is concurrently shared with the publishing Editor thread.
        internal sealed class Snapshot {
            private ILPersistentAnalysis source;
            internal Snapshot(ILPersistentAnalysis source) { this.source = source; }
            internal ILPersistentAnalysis Take() {
                var owner = System.Threading.Volatile.Read(ref this.source)
                    ?? throw new InvalidOperationException("Persistent IL snapshot has already been consumed.");
                if (!System.Threading.Volatile.Read(ref owner.disposed))
                    throw new InvalidOperationException("Persistent IL snapshot producer has not finished disposal.");
                return System.Threading.Interlocked.Exchange(ref this.source, null)
                    ?? throw new InvalidOperationException("Persistent IL snapshot has already been consumed.");
            }

            // A deferred coordinator result that no publication consumed still
            // carries newly analyzed summaries; keep them for the next export.
            internal void PersistIfUnconsumed() {
                var owner = System.Threading.Volatile.Read(ref this.source);
                if (owner == null || !System.Threading.Volatile.Read(ref owner.disposed)) return;
                owner = System.Threading.Interlocked.Exchange(ref this.source, null);
                owner?.Save();
            }
        }

        internal void DeferPersistence() {
            if (!this.persist) return;
            this.persist = false;
            this.deferred = true;
        }

        internal Snapshot CaptureSnapshot() => new Snapshot(this);

        internal Func<ILPersistentAnalysis> PrepareWorker() {
            this.EnsureLoaded();
            var capturedContext = this.Context();
            var declarationsByName = this.declarationsByName;
            var referencesByName = this.referencesByName;
            var records = this.records.ToArray();
            var assemblies = this.assemblies.ToArray();
            var versions = this.dependencyVersions.ToArray();
            var typeDeclarations = this.typeDeclarations.Export();
            var environment = this.environment;
            var rebuild = this.rebuild;
            var scheduled = this.scheduledImplementation;
            return () => {
                var worker = new ILPersistentAnalysis(rebuild, environment) {
                    persist = false, loaded = true, context = capturedContext,
                    previousContext = capturedContext, scheduledImplementation = scheduled,
                    declarationsByName = declarationsByName, referencesByName = referencesByName,
                };
                foreach (var pair in records) worker.records.Add(pair.Key, pair.Value);
                foreach (var pair in assemblies) worker.assemblies.Add(pair.Key, pair.Value);
                foreach (var pair in versions) worker.dependencyVersions.Add(pair.Key, pair.Value);
                worker.typeDeclarations.Import(typeDeclarations);
                return worker;
            };
        }

        internal void MergeWorker(Snapshot snapshot) {
            if (current != this || this.disposed)
                throw new InvalidOperationException("Worker cache requires an active receiving IL session.");
            var worker = snapshot.Take();
            if (worker.persist || worker.context != this.context || worker.environment != this.environment)
                throw new InvalidOperationException("Cannot merge a worker from another IL snapshot.");
            foreach (var key in worker.changedRecords.OrderBy(key => key, StringComparer.Ordinal)) {
                // Match the memo merge: coordinator results take precedence over
                // duplicate work, including an uncacheable result in the child.
                if (this.changedRecords.Contains(key)) continue;
                if (worker.records.TryGetValue(key, out var record)) this.records[key] = record;
                else this.records.Remove(key);
                this.valid.Remove(key);
                this.changedRecords.Add(key);
            }
            foreach (var pair in worker.dependencyVersions)
                if (!this.dependencyVersions.ContainsKey(pair.Key)) this.dependencyVersions.Add(pair.Key, pair.Value);
            foreach (var pair in worker.validatedMethods) this.validatedMethods[pair.Key] = pair.Value;
            this.typeDeclarations.Import(worker.typeDeclarations.Export());
            foreach (var pair in worker.statistics) {
                this.statistics.TryGetValue(pair.Key, out var count);
                this.statistics[pair.Key] = (count.hits + pair.Value.hits, count.misses + pair.Value.misses);
            }
            foreach (var pair in worker.missReasons) {
                this.missReasons.TryGetValue(pair.Key, out var count);
                this.missReasons[pair.Key] = count + pair.Value;
            }
            this.dirty |= worker.dirty;
        }

        internal ILPersistentAnalysis(bool rebuild, Snapshot snapshot) : this(rebuild, ILAnalysisEnvironment.Capture()) {
            try {
                var source = snapshot.Take();
                if (rebuild || source.path != this.path || source.environment.target != this.environment.target) {
                    if (source.deferred) source.Save();
                    return;
                }
                foreach (var pair in source.records) this.records.Add(pair.Key, pair.Value);
                foreach (var pair in source.assemblies) this.assemblies.Add(pair.Key, pair.Value);
                foreach (var pair in source.dependencyVersions) this.dependencyVersions.Add(pair.Key, pair.Value);
                this.typeDeclarations.Import(source.typeDeclarations.Export());
                this.loaded = source.loaded;
                this.previousContext = source.context ?? source.previousContext;
                // Same AppDomain and (checked by the caller) the same compiled code:
                // reuse resolved methods, body fingerprints and dependency validation
                // instead of re-fingerprinting every method of recompiled assemblies.
                // These facts depend on method bodies only, not on the context.
                foreach (var pair in source.bodies) this.bodies[pair.Key] = pair.Value;
                foreach (var pair in source.resolved) this.resolved[pair.Key] = pair.Value;
                foreach (var pair in source.validDependencies) this.validDependencies[pair.Key] = pair.Value;
                foreach (var pair in source.validatedMethods) this.validatedMethods[pair.Key] = pair.Value;
                foreach (var pair in source.currentDependencies) this.currentDependencies[pair.Key] = pair.Value;
                foreach (var pair in source.valid) this.valid[pair.Key] = pair.Value;
                foreach (var key in source.changedRecords) this.changedRecords.Add(key);
                // The coordinator deferred its save to this consumer.
                this.dirty |= source.deferred && source.dirty;
                this.inherited = true;
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
                record.implementation != implementation ? "implementation" : !scope.ScopeIsCurrent(record) ? "declarations" :
                !scope.QueriesAreCurrent(record.queries) ? "type-queries" : record.types == null ? "type-capture" : "dependencies";
            if (miss == "dependencies") {
                try {
                    if (scope.Validate(record)) {
                        var result = decode(UnityEngine.JsonUtility.FromJson<TData>(record.payload));
                        foreach (var dependency in record.dependencies) ILDependencyCapture.Observe(scope.validatedMethods[dependency]);
                        // An enclosing summary inherits this scope exactly as if it had analyzed it.
                        ILDependencyCapture.ObserveRecorded(record.queries, record.roots, record.types.Select(ILSummaryData.Resolve));
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
            var rootName = identity.Split('\n')[0];
            var rootType = Type.GetType(rootName, false);
            ILDependencyCapture.ObserveType(rootType);
            var computed = analyze(); // Exceptions/cancellation must not be cached or swallowed.
            try {
                string typeScope = null;
                var types = ILDependencyCapture.PersistentTypes(capture.Types);
                if (rootType != null) {
                    try {
                        types = ILTypeDependencyGraph.Closure(capture.Types).Select(type => type.AssemblyQualifiedName).ToArray();
                        typeScope = scope.TypeScope(types);
                    } catch (NotSupportedException) {
                        // An unresolved executable metadata dispatch needs the
                        // existing assembly boundary. Never save a partial graph.
                        types = ILDependencyCapture.PersistentTypes(capture.Types);
                    }
                }
                // Graph construction can discover metadata getter/helper bodies.
                // Capture them only after the declaration closure is complete.
                var methods = capture.Methods;
                var dependencies = methods.Select(scope.GetDependency).OrderBy(item => item.method.Key, StringComparer.Ordinal).ToArray();
                var queries = capture.Queries;
                if (queries.Any(query => query.StartsWith(ILDependencyCapture.OpenQuery, StringComparison.Ordinal)))
                    throw new NotSupportedException("Open TypeCache constraint has no persistent identity.");
                var roots = Roots(key, methods, capture.Assemblies);
                scope.records[key] = new Record { key = key, context = context, implementation = implementation, dependencies = dependencies,
                    payload = UnityEngine.JsonUtility.ToJson(encode(computed)), roots = roots, scope = scope.ScopeHash(roots), queries = queries,
                    types = types, typeScope = typeScope };
                scope.valid[key] = true;
                scope.changedRecords.Add(key);
                scope.dirty = true;
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                // Open/unsupported reflection identities remain an uncached analysis.
                if (scope.records.Remove(key)) { scope.dirty = true; scope.changedRecords.Add(key); }
                scope.valid.Remove(key);
            }
            return computed;
        }

        private static readonly System.Text.RegularExpressions.Regex IdentityAssembly =
            new System.Text.RegularExpressions.Regex(@",\s*([^,\[\]\s]+),\s*Version=", System.Text.RegularExpressions.RegexOptions.Compiled);

        // Assemblies named by a summary key (root types, closed generic arguments).
        internal static IEnumerable<string> IdentityAssemblies(string identity) {
            if (string.IsNullOrEmpty(identity)) yield break;
            foreach (System.Text.RegularExpressions.Match match in IdentityAssembly.Matches(identity)) yield return match.Groups[1].Value;
        }

        private static string[] Roots(string key, IEnumerable<MethodBase> methods, IEnumerable<string> assemblies) {
            var roots = new HashSet<string>(assemblies, StringComparer.Ordinal);
            foreach (var method in methods) roots.Add(method.Module.Assembly.GetName().Name);
            roots.UnionWith(IdentityAssemblies(key));
            var result = roots.ToArray();
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        // Declarations visible to an analysis: its root assemblies and everything
        // they reference (compile-time visibility). Downstream assemblies are only
        // visible through recorded TypeCache queries, which add their own roots.
        // Libraries, the analyzer, defines and generated-consumer dependants are
        // part of the shared context. Assemblies without declarations are skipped.
        internal static string[] Closure(IEnumerable<string> roots, IReadOnlyDictionary<string, string[]> references, ICollection<string> declared) {
            var selected = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(roots);
            while (queue.Count > 0) {
                var name = queue.Dequeue();
                if (!selected.Add(name)) continue;
                if (references.TryGetValue(name, out var next)) foreach (var reference in next) queue.Enqueue(reference);
            }
            var result = selected.Where(declared.Contains).ToArray();
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private string ScopeHash(string[] roots) {
            if (roots == null) return null;
            var key = string.Join("\n", roots);
            if (this.scopeHashes.TryGetValue(key, out var hash)) return hash;
            var closure = Closure(roots, this.referencesByName, this.declarationsByName.Keys);
            hash = Names.Hash("scope-v1\n" + string.Join("\n", closure.Select(name => name + ":" + this.declarationsByName[name])));
            this.scopeHashes.Add(key, hash);
            return hash;
        }

        private bool ScopeIsCurrent(Record record) {
            if (string.IsNullOrEmpty(record.typeScope) == false) {
                try { return record.types != null && record.typeScope == this.TypeScope(record.types); }
                catch (Exception exception) when (exception is OperationCanceledException == false) { return false; }
            }
            return record.roots != null && string.IsNullOrEmpty(record.scope) == false && record.scope == this.ScopeHash(record.roots);
        }

        private string TypeScope(string[] types) {
            var text = new StringBuilder("type-scope-v1\n");
            foreach (var name in types) {
                var type = ILAnalysisSession.Get((typeof(ILTypeDeclarationCache), name), () => ILSummaryData.Resolve(name));
                text.Append(name).Append('\t').Append(this.typeDeclarations.Get(type)).Append('\n');
            }
            return Names.Hash(text.ToString());
        }

        // Reverse provenance index for loaded cache consumers. This is not a
        // validity check: missing maps/old records must never justify reuse.
        internal Dictionary<string, string[]> ConsumersBySourceFile() {
            this.EnsureLoaded();
            var consumers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var record in this.records.Values) {
                foreach (var identity in record.types ?? Array.Empty<string>()) {
                    var type = Type.GetType(identity, false);
                    if (type == null) { continue; }
                    foreach (var path in ILSourceTypeMap.Files(type)) {
                        if (consumers.TryGetValue(path, out var keys) == false) {
                            keys = new HashSet<string>(StringComparer.Ordinal);
                            consumers.Add(path, keys);
                        }
                        keys.Add(record.key);
                    }
                }
            }
            return consumers.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key,
                pair => pair.Value.OrderBy(key => key, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        }

        private bool QueriesAreCurrent(string[] queries) {
            if (queries == null) return true;
            foreach (var query in queries) {
                if (!this.currentQueries.TryGetValue(query, out var unchanged)) {
                    unchanged = false;
                    var separator = query.LastIndexOf('\t');
                    if (separator > 0 && !query.StartsWith(ILDependencyCapture.OpenQuery, StringComparison.Ordinal)) {
                        var constraint = Type.GetType(query.Substring(0, separator), false);
                        if (constraint != null) {
                            var types = ILAnalysisSession.ReadMetadata(() => UnityEditor.TypeCache.GetTypesDerivedFrom(constraint).ToArray());
                            unchanged = ILDependencyCapture.QueryResult(types) == query.Substring(separator + 1);
                        }
                    }
                    this.currentQueries.Add(query, unchanged);
                }
                if (!unchanged) return false;
            }
            return true;
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
                dependency = new Dependency { method = reference, body = body, mvid = method.Module.ModuleVersionId.ToString("D") };
                this.dependencyVersions.Add(key, dependency);
            }
            // Intern versions, not just method identities: a newly analyzed body
            // must never "refresh" the old body expected by another cached root.
            this.validatedMethods[dependency] = method;
            this.validDependencies[dependency] = true;
            this.currentDependencies.Add(method, dependency);
            return dependency;
        }

        private bool ValidateDependency(Dependency dependency) {
            if (this.validDependencies.TryGetValue(dependency, out var valid)) return valid;
            var method = this.Resolve(dependency.method);
            valid = BodyIsCurrent(method, dependency.mvid, dependency.body, this.Body);
            if (valid) {
                this.validatedMethods[dependency] = method;
                var currentMvid = method.Module.ModuleVersionId.ToString("D");
                if (dependency.mvid != currentMvid) {
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
            // Shared context: everything a summary cannot attribute to its own scope.
            // Script declarations are NOT part of it; each summary hashes only the
            // declarations of the assemblies it can see (see Closure/ScopeHash).
            // Generated consumers (ME.BECS.Gen.*, publication bridges) are produced
            // from the analysis and must not invalidate it by being regenerated.
            var parts = new List<string> { "IL-analysis-v5", this.environment.target };
            var changedDeclarations = new List<string>();
            var librariesStarted = watch.ElapsedMilliseconds;
            parts.AddRange(SourceGeneratorCodeIdentity.Libraries(scriptAssemblies.SelectMany(assembly => assembly.libraries), scripts));
            var librariesMs = watch.ElapsedMilliseconds - librariesStarted;
            var references = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var script in scriptAssemblies) {
                if (SourceGeneratorCodeIdentity.IsConsumer(script.name)) continue;
                var visible = (script.references ?? Array.Empty<string>()).Where(name => !SourceGeneratorCodeIdentity.IsConsumer(name))
                    .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                references[script.name] = visible;
                parts.Add(script.name + ":" + string.Join(";", script.defines.OrderBy(value => value, StringComparer.Ordinal)));
                parts.Add("references:" + script.name + ":" + string.Join(";", visible));
            }
            var declarationsByName = new Dictionary<string, string>(StringComparer.Ordinal);
            var loaded = SourceGeneratorCodeIdentity.LoadedScripts(this.environment.loaded, scripts)
                .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal).ToArray();
            var changed = loaded.Where(assembly => assembly != typeof(ILPersistentAnalysis).Assembly &&
                !dependants.Contains(assembly.GetName().Name) &&
                (!this.assemblies.TryGetValue(assembly.FullName, out var previous) ||
                 previous.mvid != assembly.ManifestModule.ModuleVersionId.ToString("D") || previous.declarationFormat != DeclarationFormat)).ToArray();
            var declarationsStarted = watch.ElapsedMilliseconds;
            var fingerprints = ILAnalysisSession.IndexDeclarations(changed);
            var declarationsMs = watch.ElapsedMilliseconds - declarationsStarted;
            long analyzerMs = 0, consumersMs = 0;
            var analyzerRebuilt = false;
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
                        var analyzerStarted = watch.ElapsedMilliseconds;
                        var fingerprint = ILContentFingerprint.AnalyzerContent(assembly);
                        analyzerMs += watch.ElapsedMilliseconds - analyzerStarted;
                        analyzerRebuilt = true;
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
                    var consumerStarted = watch.ElapsedMilliseconds;
                    var script = scriptAssemblies.Single(item => item.name == assembly.GetName().Name);
                    parts.Add(assembly.FullName + ":" + SourceGeneratorCodeIdentity.Stamp(assembly, script.sources, dependants));
                    consumersMs += watch.ElapsedMilliseconds - consumerStarted;
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
                // Inventory only; the declarations belong to the summaries' scopes.
                parts.Add("assembly:" + assembly.FullName);
                declarationsByName[assembly.GetName().Name] = stamp.declarations;
            }
            parts.Sort(StringComparer.Ordinal);
            this.declarationsByName = declarationsByName;
            this.referencesByName = references;
            this.scopeHashes.Clear();
            this.context = Names.Hash(string.Join("\n", parts));
            if (this.previousContext != null && this.previousContext != this.context)
                UnityEngine.Debug.Log("[ME.BECS] Incremental IL shared context changed (analyzer, libraries, references, defines or assembly inventory); all summaries are re-analyzed.");
            if (changedDeclarations.Count != 0)
                UnityEngine.Debug.Log("[ME.BECS] Incremental IL declarations changed: " + string.Join(", ", changedDeclarations) +
                    ". Only summaries that can see these assemblies are re-analyzed.");
            UnityEngine.Debug.Log("[ME.BECS] Incremental IL declaration index: " + watch.ElapsedMilliseconds + " ms. Method bodies are validated separately.");
            UnityEngine.Debug.Log("[ME.BECS] Incremental IL index breakdown: libraries=" + librariesMs +
                " ms; declarations=" + declarationsMs + " ms (reindexed=" + changed.Length + "/" + loaded.Length +
                " loaded assemblies); analyzer=" + analyzerMs + " ms (rebuilt=" + analyzerRebuilt +
                "); consumer stamps=" + consumersMs + " ms. Parallel declaration time is wall-clock, not summed worker time.");
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
            WaitForPendingSave();
            try {
                if (!File.Exists(this.path)) return;
                using var reader = new StreamReader(this.path, Encoding.UTF8);
                var data = UnityEngine.JsonUtility.FromJson<Database>(ReadCacheData(reader));
                var restored = Unpack(data);
                this.previousContext = data.context;
                foreach (var record in restored) this.records.Add(record.key, record);
                foreach (var assembly in data.assemblies) this.assemblies.Add(assembly.name, assembly);
                this.typeDeclarations.Import(data.typeDeclarations);
                foreach (var dependency in data.methods) this.dependencyVersions.Add((dependency.method.Key, dependency.body), dependency);
                UnityEngine.Debug.Log("[ME.BECS] Incremental IL cache loaded: " + restored.Length + " summaries, " + data.methods.Length +
                    " method versions, " + watch.ElapsedMilliseconds + " ms.");
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                this.records.Clear(); this.assemblies.Clear();
                this.dependencyVersions.Clear();
            }
        }

        private static Database Pack(IEnumerable<Record> records, IEnumerable<AssemblyRecord> assemblies, string context) {
            return PackWithVersions(records, assemblies, context, null);
        }

        private static Database PackWithVersions(IEnumerable<Record> records, IEnumerable<AssemblyRecord> assemblies, string context,
            Dictionary<Dependency, MethodBase> validatedMethods) {
            var methods = new List<Dependency>();
            var indices = new Dictionary<(string method, string body), int>();
            var packed = new List<CompactRecord>();
            var types = new List<string>();
            var typeIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var record in records.Where(record => record.context == context).OrderBy(record => record.key, StringComparer.Ordinal)) {
                var dependencies = new int[record.dependencies.Length];
                for (var i = 0; i < dependencies.Length; ++i) {
                    var dependency = record.dependencies[i];
                    var key = (dependency.method.Key, dependency.body);
                    if (!indices.TryGetValue(key, out var index)) {
                        index = methods.Count;
                        indices.Add(key, index);
                        // Refresh only the serialized copy after successful body validation.
                        // An older body version must retain its original MVID.
                        methods.Add(validatedMethods != null && validatedMethods.TryGetValue(dependency, out var validated)
                            ? new Dependency { method = dependency.method, body = dependency.body, mvid = validated.Module.ModuleVersionId.ToString("D") }
                            : dependency);
                    }
                    dependencies[i] = index;
                }
                int[] capturedTypes = null;
                if (record.types != null) {
                    capturedTypes = new int[record.types.Length];
                    for (var i = 0; i < capturedTypes.Length; ++i) {
                        var name = record.types[i];
                        if (typeIndices.TryGetValue(name, out var index) == false) {
                            index = types.Count;
                            types.Add(name);
                            typeIndices.Add(name, index);
                        }
                        capturedTypes[i] = index;
                    }
                }
                packed.Add(new CompactRecord { key = record.key, implementation = record.implementation, payload = record.payload, dependencies = dependencies,
                    roots = record.roots, scope = record.scope, queries = record.queries, types = capturedTypes, hasTypeCapture = record.types != null, typeScope = record.typeScope });
            }
            return new Database { version = DatabaseVersion, context = context, methods = methods.ToArray(), records = packed.ToArray(),
                assemblies = assemblies.OrderBy(assembly => assembly.name, StringComparer.Ordinal).ToArray(), types = types.ToArray() };
        }

        private static Record[] Unpack(Database data) {
            if (data?.version != DatabaseVersion || string.IsNullOrEmpty(data.context) || data.methods == null || data.records == null || data.assemblies == null)
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
                // Missing scope data is a cache miss ("declarations"), never a load failure.
                string[] capturedTypes = null;
                if (record.hasTypeCapture == true) {
                    if (record.types == null || data.types == null) {
                        throw new FormatException("Missing cached IL type table.");
                    }
                    capturedTypes = new string[record.types.Length];
                    for (var i = 0; i < capturedTypes.Length; ++i) {
                        var index = record.types[i];
                        if ((uint)index >= (uint)data.types.Length || string.IsNullOrEmpty(data.types[index]) == true) {
                            throw new FormatException("Invalid cached IL type index.");
                        }
                        capturedTypes[i] = data.types[index];
                    }
                }
                return new Record { key = record.key, context = data.context, implementation = record.implementation, payload = record.payload, dependencies = dependencies,
                    roots = record.roots, scope = record.scope, queries = record.queries, types = capturedTypes, typeScope = record.typeScope };
            }).ToArray();
        }

        public void Dispose() {
            try { this.DisposeCore(); }
            finally { System.Threading.Volatile.Write(ref this.disposed, true); }
        }

        private void DisposeCore() {
            current = this.previous;
            if (!this.persist && !this.deferred) return; // Workers: the coordinator merges results and writes once.
            if (this.statistics.Count > 0) UnityEngine.Debug.Log("[ME.BECS] Incremental IL summaries:\n" + string.Join("\n", this.statistics
                .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => "  " + pair.Key + ": reused=" + pair.Value.hits + ", analyzed=" + pair.Value.misses)) +
                (this.missReasons.Count == 0 ? "" : "\n  Miss reasons: " + string.Join(", ", this.missReasons.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "=" + pair.Value))));
            if (this.persist) this.Save();
        }

        private static readonly object saveGate = new object();
        private static System.Threading.Tasks.Task pendingSave = System.Threading.Tasks.Task.CompletedTask;

        static ILPersistentAnalysis() {
            // A domain reload must not abort a cache write in progress.
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += WaitForPendingSave;
        }

        internal static void WaitForPendingSave() {
            System.Threading.Tasks.Task task;
            lock (saveGate) task = pendingSave;
            try { task.Wait(); } catch (AggregateException) { }
        }

        // Capture on the calling thread; pack, serialize and write on a worker so
        // the Editor thread does not wait for a ~70 MB cache write.
        private void Save() {
            // Cancellation while indexing declarations has no complete context.
            // Preserve the last valid cache instead of replacing it with a partial
            // database whose null context would be rejected on the next attempt.
            if (this.dirty && this.context == null && this.inherited) {
                // A consumer that never queried the cache still owns the deferred
                // coordinator results; establish its context so they are kept.
                try { this.Context(); }
                catch (Exception exception) when (!(exception is OperationCanceledException)) {
                    UnityEngine.Debug.LogWarning("[ME.BECS] Could not index IL context for the cache save: " + exception.Message);
                }
            }
            if (!this.dirty || string.IsNullOrEmpty(this.context)) return;
            var records = this.records.Values.ToArray();
            var assemblies = this.assemblies.Values.ToArray();
            var validated = new Dictionary<Dependency, MethodBase>(this.validatedMethods);
            var typeDeclarations = this.typeDeclarations.Export();
            var context = this.context;
            var path = this.path;
            lock (saveGate) pendingSave = pendingSave.ContinueWith(_ => Write(path, records, assemblies, context, validated, typeDeclarations),
                System.Threading.CancellationToken.None, System.Threading.Tasks.TaskContinuationOptions.None,
                System.Threading.Tasks.TaskScheduler.Default);
        }

        private static void Write(string path, Record[] records, AssemblyRecord[] assemblies, string context, Dictionary<Dependency, MethodBase> validated,
            ILTypeDeclarationCache.Entry[] typeDeclarations) {
            var temporary = path + ".tmp";
            var saveWatch = System.Diagnostics.Stopwatch.StartNew();
            try {
                // Drop obsolete declaration universes; changed bodies replace their
                // own keys. No stale summary is combined with a new type inventory.
                var database = PackWithVersions(records, assemblies, context, validated);
                var usedTypes = new HashSet<string>(database.types, StringComparer.Ordinal);
                database.typeDeclarations = typeDeclarations.Where(entry => usedTypes.Contains(entry.name)).ToArray();
                var packedAt = saveWatch.ElapsedMilliseconds;
                var data = UnityEngine.JsonUtility.ToJson(database);
                var serializedAt = saveWatch.ElapsedMilliseconds;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false))) WriteCacheData(writer, data);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                UnityEngine.Debug.Log("[ME.BECS] Incremental IL cache saved (background): " + database.records.Length + " summaries, " + database.methods.Length +
                    " method versions, " + new FileInfo(path).Length + " bytes. Save timings: pack=" + packedAt +
                    " ms; JSON=" + (serializedAt - packedAt) + " ms; checksum/write/replace=" +
                    (saveWatch.ElapsedMilliseconds - serializedAt) + " ms; total=" + saveWatch.ElapsedMilliseconds +
                    " ms; managed heap=" + (GC.GetTotalMemory(false) / (1024L * 1024L)) + " MiB (no forced GC).");
            } catch (Exception exception) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Could not persist incremental IL cache: " + exception.Message);
            } finally {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
