namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    // A consumer rebuild also recompiles its dependants (including predefined
    // Assembly-CSharp assemblies). Their new MVID is not necessarily new input
    // code: including it in the snapshot creates an export -> compile -> export loop.
    internal static class SourceGeneratorCodeIdentity {
        private static readonly Dictionary<Assembly, string> contents = new Dictionary<Assembly, string>();
        private static readonly Dictionary<string, string> libraryContents = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<Assembly, bool> inputOwners = new Dictionary<Assembly, bool>();
        private static Type[] genericJobConstraints;

        // Referencing BECS does not make a UI assembly an analysis root. Follow
        // references FROM contract owners to their helpers, never in reverse.
        // Re-evaluate owners after every domain reload so adding the first job,
        // component, view or attributed callback to an assembly is observed.
        internal static HashSet<string> InputAssemblies(UnityEditor.Compilation.Assembly[] scripts, Assembly[] loaded) {
            var byName = loaded.Where(assembly => assembly.IsDynamic == false)
                .GroupBy(assembly => assembly.GetName().Name)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var roots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var script in scripts) {
                if (IsConsumer(script.name) == true) continue;
                if (byName.TryGetValue(script.name, out var assemblies) == false || assemblies.Length != 1 ||
                    IsInputOwner(assemblies[0]) == true) roots.Add(script.name);
            }
            // Generic job specialization searches the whole domain for marker
            // implementations, including assemblies which reference the job owner.
            genericJobConstraints ??= GenericJobConstraints(Jobs.JobsEarlyInitCodeGenerator.DiscoveryContracts
                .SelectMany(contract => UnityEditor.TypeCache.GetTypesDerivedFrom(contract).Cast<Type>()));
            foreach (var constraint in genericJobConstraints) {
                if (constraint.ContainsGenericParameters == true) return new HashSet<string>(scripts.Select(script => script.name), StringComparer.Ordinal);
                foreach (var candidate in UnityEditor.TypeCache.GetTypesDerivedFrom(constraint)) {
                    if (candidate.IsValueType == true && candidate.ContainsGenericParameters == false) roots.Add(candidate.Assembly.GetName().Name);
                }
            }
            return InputDependencyClosure(scripts.Select(script => new KeyValuePair<string, string[]>(script.name,
                script.assemblyReferences.Select(reference => reference.name).ToArray())), roots);
        }

        private static bool IsInputOwner(Assembly assembly) {
            if (inputOwners.TryGetValue(assembly, out var owner) == true) return owner;
            // Framework implementation, including discovery extensions, remains
            // conservative. This is an inclusion rule, never a UI-name exclusion.
            var name = assembly.GetName().Name;
            owner = name == "ME.BECS" || name.StartsWith("ME.BECS.", StringComparison.Ordinal);
            try {
                var types = assembly.GetTypes();
                if (owner == false) {
                    owner = assembly.GetCustomAttributesData().Any(attribute => IsInputAttribute(attribute.AttributeType)) ||
                            types.Any(IsInputType);
                }
            } catch (Exception) { owner = true; } // Incomplete metadata must invalidate, not silently exclude.
            inputOwners[assembly] = owner;
            return owner;
        }

        internal static Type[] GenericJobConstraints(IEnumerable<Type> types) => types
            .Where(type => type.IsGenericTypeDefinition == true)
            .Select(EditorUtils.GetFirstInterfaceConstraintType).Where(constraint => constraint != null).Distinct().ToArray();

        internal static bool IsInputType(Type type) {
            const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                          BindingFlags.Instance | BindingFlags.DeclaredOnly;
            for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
                if (IsInputContract(parent) == true) return true;
            if (type.GetInterfaces().Any(IsInputContract) == true) return true;
            if (HasInputAttribute(type) == true) return true;
            return type.GetMembers(declared).Any(HasInputAttribute);
        }

        private static bool HasInputAttribute(MemberInfo member) => member.GetCustomAttributesData()
            .Any(attribute => IsInputAttribute(attribute.AttributeType));

        private static bool IsInputAttribute(Type type) {
            // SubclassSelector is an Inspector-only field drawer, not a codegen
            // contract. UI assemblies use it without owning any ECS inputs.
            if (type == typeof(ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute)) {
                return false;
            }
            for (; type != null; type = type.BaseType)
                if (IsInputContract(type) == true) return true;
            return false;
        }

        private static bool IsInputContract(Type type) {
            var ns = type.Namespace ?? "";
            return ns == "ME.BECS" || ns.StartsWith("ME.BECS.", StringComparison.Ordinal) ||
                   ns == "Unity.Jobs" || ns.StartsWith("Unity.Jobs.", StringComparison.Ordinal);
        }

        internal static HashSet<string> InputDependencyClosure(IEnumerable<KeyValuePair<string, string[]>> references,
            IEnumerable<string> roots) {
            var graph = references.ToDictionary(pair => pair.Key, pair => pair.Value ?? Array.Empty<string>(), StringComparer.Ordinal);
            var selected = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(roots);
            while (queue.Count > 0) {
                var name = queue.Dequeue();
                if (graph.TryGetValue(name, out var dependencies) == false || selected.Add(name) == false) continue;
                foreach (var dependency in dependencies) queue.Enqueue(dependency);
            }
            return selected;
        }

        // AppDomain is not a compilation input inventory: e.g. System.Buffers
        // first loads during Play Mode. Use compiler references, including DLLs
        // which have not been loaded yet, without loading/executing those DLLs.
        internal static Assembly[] LoadedScripts(IEnumerable<Assembly> loaded, ISet<string> names) =>
            loaded.Where(assembly => !assembly.IsDynamic && names.Contains(assembly.GetName().Name) &&
                !IsConsumer(assembly.GetName().Name)).ToArray();

        internal static string[] LibraryPaths(IEnumerable<string> paths, ISet<string> scripts) => paths
            .Where(path => !scripts.Contains(System.IO.Path.GetFileNameWithoutExtension(path)) &&
                !IsConsumer(System.IO.Path.GetFileNameWithoutExtension(path)))
            .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();

        internal static IEnumerable<string> Libraries(UnityEditor.Compilation.Assembly[] scripts) {
            var names = new HashSet<string>(scripts.Select(script => script.name), StringComparer.Ordinal);
            return Libraries(scripts.SelectMany(script => script.compiledAssemblyReferences), names);
        }

        internal static IEnumerable<string> Libraries(IEnumerable<string> paths, ISet<string> names) {
            foreach (var path in LibraryPaths(paths, names)) {
                string hash;
                lock (libraryContents) libraryContents.TryGetValue(path, out hash);
                if (hash == null) {
                    using var stream = System.IO.File.OpenRead(path);
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    hash = Convert.ToBase64String(sha.ComputeHash(stream));
                    lock (libraryContents) libraryContents[path] = hash;
                }
                // Relocation of a project/package does not change its contents.
                yield return "reference:" + System.IO.Path.GetFileName(path) + "\t" + hash;
            }
        }

        internal static HashSet<string> ConsumerDependants(UnityEditor.Compilation.Assembly[] scripts) =>
            FindConsumerDependants(scripts.Select(script => new KeyValuePair<string, string[]>(script.name,
                script.assemblyReferences.Select(reference => reference.name).ToArray())));

        internal static HashSet<string> FindConsumerDependants(IEnumerable<KeyValuePair<string, string[]>> references) {
            var assemblies = references.ToArray();
            var selected = new HashSet<string>(assemblies.Where(pair => IsConsumer(pair.Key)).Select(pair => pair.Key), StringComparer.Ordinal);
            bool changed;
            do {
                changed = false;
                foreach (var assembly in assemblies) {
                    if (selected.Contains(assembly.Key)) continue;
                    if (!(assembly.Value ?? Array.Empty<string>()).Any(name => IsConsumer(name) || selected.Contains(name))) continue;
                    selected.Add(assembly.Key);
                    changed = true;
                }
            } while (changed);
            selected.RemoveWhere(IsConsumer);
            return selected;
        }

        internal static bool IsConsumer(string name) => name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal) ||
            SourceGeneratorPublicationBridges.IsBridge(name);

        // Freshness identity used by the source-input fingerprint. Consumer
        // dependants keep their source-aware Stamp; every other input assembly is
        // identified by its compiled content instead of its MVID, so a rebuild
        // which produced the same code (e.g. test assemblies recompiled because a
        // referenced assembly changed, or a comment-only edit) does not restart IL
        // analysis and input publication. The content is a pure function of the
        // binary, so it is memoized by MVID in Library across domain reloads.
        private const string ContentCacheFormat = "ME.BECS.CodeContentStamps.v3";
        private const string ContentCachePath = "Library/ME.BECS/CodeContentStamps.txt";
        private const int ContentCacheLimit = 1024;
        private static readonly Dictionary<Assembly, string> contentStamps = new Dictionary<Assembly, string>();
        private static Dictionary<string, string> contentCache;
        private static readonly List<string> contentCacheOrder = new List<string>();

        internal static string ContentStamp(Assembly assembly, UnityEditor.Compilation.Assembly script, ISet<string> dependants) {
            if (dependants.Contains(assembly.GetName().Name)) return Stamp(assembly, script, dependants);
            var mvid = assembly.ManifestModule.ModuleVersionId.ToString("D");
            lock (contentStamps) {
                if (contentStamps.TryGetValue(assembly, out var known)) return known;
                LoadContentCache();
                if (contentCache.TryGetValue(mvid, out known)) {
                    contentStamps[assembly] = known;
                    return known;
                }
            }
            string stamp;
            try {
                stamp = "content:" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(ContentCacheFormat + "\n" + ILContentFingerprint.LoadedContent(assembly));
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                // Unreadable metadata must invalidate conservatively, never be skipped.
                UnityEngine.Debug.LogWarning("[ME.BECS] Source input identity of " + assembly.GetName().Name + " falls back to its MVID: " + exception.Message);
                stamp = mvid;
                lock (contentStamps) contentStamps[assembly] = stamp;
                return stamp;
            }
            lock (contentStamps) {
                contentStamps[assembly] = stamp;
                StoreContentCache(mvid, stamp);
            }
            return stamp;
        }

        private static void LoadContentCache() {
            if (contentCache != null) return;
            contentCache = new Dictionary<string, string>(StringComparer.Ordinal);
            contentCacheOrder.Clear();
            try {
                if (!System.IO.File.Exists(ContentCachePath)) return;
                var lines = System.IO.File.ReadAllLines(ContentCachePath);
                if (lines.Length == 0 || lines[0] != ContentCacheFormat) return;
                var length = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(string.Empty).Length + "content:".Length;
                foreach (var line in lines.Skip(1)) {
                    var fields = line.Split('\t');
                    // A torn or foreign row is ignored; the assembly is simply re-hashed.
                    if (fields.Length != 2 || !Guid.TryParse(fields[0], out _) || fields[1].Length != length ||
                        !fields[1].StartsWith("content:", StringComparison.Ordinal) || contentCache.ContainsKey(fields[0])) continue;
                    contentCache.Add(fields[0], fields[1]);
                    contentCacheOrder.Add(fields[0]);
                }
            } catch (Exception) {
                contentCache.Clear();
                contentCacheOrder.Clear();
            }
        }

        private static void StoreContentCache(string mvid, string stamp) {
            if (contentCache.ContainsKey(mvid)) return;
            contentCache.Add(mvid, stamp);
            contentCacheOrder.Add(mvid);
            while (contentCacheOrder.Count > ContentCacheLimit) {
                contentCache.Remove(contentCacheOrder[0]);
                contentCacheOrder.RemoveAt(0);
            }
            try {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ContentCachePath));
                var temporary = ContentCachePath + ".tmp";
                System.IO.File.WriteAllLines(temporary, new[] { ContentCacheFormat }.Concat(contentCacheOrder.Select(key => key + "\t" + contentCache[key])));
                if (System.IO.File.Exists(ContentCachePath)) System.IO.File.Delete(ContentCachePath);
                System.IO.File.Move(temporary, ContentCachePath);
            } catch (Exception exception) {
                // The memo is an optimization only: losing it costs a re-hash.
                UnityEngine.Debug.LogWarning("[ME.BECS] Cannot store source input identity cache: " + exception.Message);
            }
        }

        internal static string Stamp(Assembly assembly, UnityEditor.Compilation.Assembly script, ISet<string> dependants) =>
            Stamp(assembly, script.sourceFiles, dependants);

        internal static string Stamp(Assembly assembly, IEnumerable<string> sources, ISet<string> dependants) {
            if (!dependants.Contains(assembly.GetName().Name)) return assembly.ManifestModule.ModuleVersionId.ToString("D");
            lock (contents) if (contents.TryGetValue(assembly, out var cached)) return cached;
            // Hash the owning compiled declarations and IL, not the bodies of its
            // generated dependencies. Changes to real code still invalidate input.
            var text = new StringBuilder(ILContentFingerprint.AssemblyContent(assembly));
            // Source content also covers RVA/static initializer data, which is not
            // exposed by MethodBody. This is a byte hash, not source analysis.
            foreach (var path in sources.OrderBy(path => path, StringComparer.Ordinal)) {
                var normalized = path.Replace('\\', '/');
                if (normalized.StartsWith("Assets/ME.BECS.Gen/", StringComparison.Ordinal) ||
                    normalized.IndexOf("/Assets/ME.BECS.Gen/", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("Generated consumer source cannot belong to input assembly " + assembly.GetName().Name);
                using var stream = System.IO.File.OpenRead(path);
                using var sha = System.Security.Cryptography.SHA256.Create();
                text.Append('\n').Append(normalized).Append('\t').Append(Convert.ToBase64String(sha.ComputeHash(stream)));
            }
            foreach (var reference in assembly.GetReferencedAssemblies().OrderBy(reference => reference.FullName, StringComparer.Ordinal))
                text.Append("\nreference:").Append(reference.FullName);
            var result = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(text.ToString());
            lock (contents) contents[assembly] = result;
            return result;
        }
    }
}
