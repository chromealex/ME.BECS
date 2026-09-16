namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public static class SourceGeneratorScheduledJobs {
        private sealed class Entry {
            public string failure;
            public readonly Dictionary<MethodInfo, Type[]> roots = new Dictionary<MethodInfo, Type[]>();
        }
        private static readonly object gate = new object();
        private static readonly Dictionary<Type, Entry> cache = new Dictionary<Type, Entry>();
        private static readonly Dictionary<Assembly, string[][]> metadata = new Dictionary<Assembly, string[][]>();
        private static HashSet<Assembly> assemblySet = new HashSet<Assembly>();

        // Successful lookup never traverses method IL. Failed lookup does not partially mutate output.
        public static bool TryCollect(Type system, HashSet<Type> output, out string reason, Type lifecycle = null) {
            lock (gate) {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToArray();
                if (!assemblySet.SetEquals(assemblies)) {
                    cache.Clear();
                    metadata.Clear();
                    assemblySet = new HashSet<Assembly>(assemblies);
                }
                if (!cache.TryGetValue(system, out var entry)) {
                    try { entry = Read(system, assemblies); }
                    catch (Exception exception) { entry = new Entry { failure = exception.GetType().Name + ": " + exception.Message }; }
                    cache.Add(system, entry);
                }
                reason = entry.failure;
                if (reason != null) return false;
                var requested = SourceGeneratorScheduledJobsValidation.GetLifecycleMethods(system, lifecycle);
                if (requested.Any(root => !entry.roots.ContainsKey(root))) { reason = "Requested lifecycle is not covered"; return false; }
                foreach (var root in requested) output.UnionWith(entry.roots[root]);
                return true;
            }
        }

        public static void Collect(Type system, HashSet<Type> output, Type lifecycle = null) {
            if (TryCollect(system, output, out _, lifecycle)) return;
            foreach (var root in SourceGeneratorScheduledJobsValidation.GetLifecycleMethods(system, lifecycle))
                SourceGeneratorScheduledJobsValidation.Collect(root, output);
        }

        private static Entry Read(Type system, Assembly[] assemblies) {
            var entry = new Entry();
            if (system.ContainsGenericParameters) { entry.failure = "Open generic system"; return entry; }
            var expectedRoots = SourceGeneratorScheduledJobsValidation.GetLifecycleMethods(system);
            var identity = system.IsGenericType ? system.AssemblyQualifiedName : system.FullName;
            var rootIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in system.IsGenericType ? assemblies : new[] { system.Assembly }) {
                if (!metadata.TryGetValue(assembly, out var rows)) {
                    rows = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                        .Where(a => a.Key == "ME.BECS.SystemScheduledJobs.v1" && a.Value != null).Select(a => a.Value.Split('\n')).ToArray();
                    metadata.Add(assembly, rows);
                }
                foreach (var row in rows.Where(r => r.Length > 0 && r[0] == identity)) {
                    if (row.Length < 3 || row[2] != "0" || !rootIds.Add(row[1])) {
                        entry.failure = "Incomplete, malformed or duplicate lifecycle summary";
                        return entry;
                    }
                    var encodedJobs = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var line in row.Skip(3)) {
                        if (line.Length == 0) continue;
                        if (!line.StartsWith("J\t", StringComparison.Ordinal) || !encodedJobs.Add(line.Substring(2))) {
                            entry.failure = "Unexpected or duplicate summary operation";
                            return entry;
                        }
                    }
                    var holder = assembly.GetType("ME.BECS.SourceGenerated.ScheduledJobs_" +
                        ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(identity + "\n" + row[1]), false);
                    var flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
                    var getRoot = holder?.GetMethod("GetRoot", flags, null, Type.EmptyTypes, null);
                    var getJobs = holder?.GetMethod("GetJobs", flags, null, Type.EmptyTypes, null);
                    if (getRoot == null || getRoot.ReturnType != typeof(MethodInfo) || getRoot.ContainsGenericParameters ||
                        getJobs == null || getJobs.ReturnType != typeof(Type[]) || getJobs.ContainsGenericParameters) {
                        entry.failure = "Typed catalog unavailable";
                        return entry;
                    }
                    var root = getRoot.Invoke(null, null) as MethodInfo;
                    var jobs = getJobs.Invoke(null, null) as Type[];
                    if (root == null || !expectedRoots.Contains(root) || entry.roots.ContainsKey(root) || jobs == null ||
                        jobs.Any(t => t == null || t.ContainsGenericParameters || !t.IsValueType) || jobs.Length != encodedJobs.Count ||
                        !encodedJobs.SetEquals(jobs.Select(SourceGeneratorScheduledJobsValidation.Encode))) {
                        entry.failure = "Typed catalog does not match lifecycle/summary";
                        return entry;
                    }
                    entry.roots.Add(root, jobs);
                }
            }
            if (entry.roots.Count != expectedRoots.Count) entry.failure = "Missing lifecycle catalogs";
            return entry;
        }
    }
}
