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
