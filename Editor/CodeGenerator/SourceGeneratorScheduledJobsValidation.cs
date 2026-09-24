namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using ME.BECS.Mono.Reflection;

    public static class SourceGeneratorScheduledJobsValidation {
        public static MethodInfo GetLifecycleMethod(Type system, string name) {
            var contract = name == nameof(IAwake.OnAwake) ? typeof(IAwake) :
                name == nameof(IStart.OnStart) ? typeof(IStart) :
                name == nameof(IUpdate.OnUpdate) ? typeof(IUpdate) :
                name == nameof(IDestroy.OnDestroy) ? typeof(IDestroy) :
                name == nameof(IDrawGizmos.OnDrawGizmos) ? typeof(IDrawGizmos) :
                throw new ArgumentException("Unknown lifecycle method: " + name, nameof(name));
            return GetLifecycleMethods(system, contract).SingleOrDefault();
        }

        public static bool IsLifecycleBurstAllowed(Type system, string name) {
            if (SourceGeneratorSystemLifecycle.TryGet(system, name, out var present, out _, out var discarded))
                return present && !discarded;
            var method = GetLifecycleMethod(system, name);
            return method != null && !Attribute.IsDefined(method, typeof(WithoutBurstAttribute));
        }

        public static List<MethodInfo> GetLifecycleMethods(Type system, Type lifecycle = null) {
            var methods = new List<MethodInfo>();
            var seen = new HashSet<MethodInfo>();
            var contracts = lifecycle == null
                ? new[] { typeof(IAwake), typeof(IStart), typeof(IUpdate), typeof(IDestroy), typeof(IDrawGizmos) }
                : new[] { lifecycle };
            foreach (var contract in contracts) {
                if (!contract.IsAssignableFrom(system)) continue;
                foreach (var method in system.GetInterfaceMap(contract).TargetMethods)
                    if (seen.Add(method)) methods.Add(method);
            }
            return methods;
        }

        // Transitional fresh IL oracle, shared with Features.Editor; never reads cached C#.
        public static void Collect(MethodInfo root, HashSet<Type> types) {
            if (root == null || root.GetMethodBody() == null) return;
            var pending = new Queue<MethodInfo>();
            var visited = new HashSet<MethodPointerData>(MethodPointerData.ExactComparer) { new MethodPointerData(root) };
            pending.Enqueue(root);
            while (pending.Count != 0) {
                foreach (var instruction in pending.Dequeue().GetInstructions()) {
                    if (!(instruction.Operand is MethodInfo method)) continue;
                    if (method.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() != null) continue;
                    if (method.IsGenericMethod && (method.Name == "Schedule" || method.Name == "ScheduleSingleWithInject" || method.Name == "ScheduleSingleWithInjectByRef"))
                        types.Add(method.GetGenericArguments()[0]);
                    if (visited.Add(new MethodPointerData(method))) {
                        // Exact generic identities can expose F<T> -> F<List<T>> expansion.
                        // Fail explicitly; a truncated job set must never drive graph safety.
                        if (visited.Count > 10000) throw new InvalidOperationException("Scheduled-job IL traversal exceeded 10000 method instances: " + root);
                        if (method.GetMethodBody() != null) pending.Enqueue(method);
                    }
                }
            }
        }

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare Scheduled Jobs")]
        private static void Compare() {
            if (UnityEditor.EditorApplication.isCompiling) { UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation."); return; }
            var report = new StringBuilder();
            var compared = 0; var equal = 0; var incomplete = 0; var unavailable = 0; var generic = 0;
            var catalogRoots = 0; var catalogUnavailable = 0;
            var sourceSelected = 0; var selectionDifferences = 0;
            var metadata = new Dictionary<Assembly, string[][]>();
            string[][] Read(Assembly assembly) {
                if (metadata.TryGetValue(assembly, out var cached)) return cached;
                var rows = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Where(a => a.Key == "ME.BECS.SystemScheduledJobs.v1" && a.Value != null).Select(a => a.Value.Split('\n')).ToArray();
                metadata.Add(assembly, rows);
                return rows;
            }
            var systems = UnityEditor.TypeCache.GetTypesDerivedFrom<ISystem>().Where(t => t.IsValueType && t.IsVisible).ToList();
            var genericDefinitions = systems.Count(t => t.ContainsGenericParameters);
            CodeGenerator.PatchSystemsList(systems);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToArray();
            foreach (var system in systems.Distinct().OrderBy(t => t.FullName, StringComparer.Ordinal).ThenBy(t => t.Assembly.FullName, StringComparer.Ordinal)) {
                if (!system.IsValueType || !system.IsVisible) continue;
                if (system.ContainsGenericParameters) { ++generic; continue; }
                var roots = GetLifecycleMethods(system);
                if (roots.Count == 0) continue;
                try {
                    // Specializations are exported by the component argument's assembly, not
                    // necessarily the assembly declaring the generic system definition.
                    var rows = system.IsGenericType ? assemblies.SelectMany(Read) : Read(system.Assembly).AsEnumerable();
                    var identity = system.IsGenericType ? system.AssemblyQualifiedName : system.FullName;
                    var selected = rows.Where(r => r.Length >= 3 && r[0] == identity).ToArray();
                    if (selected.Length != roots.Count || selected.Select(r => r[1]).Distinct(StringComparer.Ordinal).Count() != roots.Count) {
                        ++unavailable; report.AppendLine(system.FullName + ": missing/duplicate lifecycle summaries (source=" + selected.Length + ", expected=" + roots.Count + ")"); continue;
                    }
                    var source = new HashSet<string>(StringComparer.Ordinal);
                    var boundRoots = new HashSet<MethodInfo>();
                    var gaps = 0;
                    foreach (var row in selected) {
                        if (!int.TryParse(row[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count)) throw new FormatException("Invalid gap count");
                        gaps = checked(gaps + count);
                        if (count == 0) {
                            var key = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(row[0] + "\n" + row[1]);
                            var holders = (system.IsGenericType ? assemblies : new[] { system.Assembly })
                                .Select(a => a.GetType("ME.BECS.SourceGenerated.ScheduledJobs_" + key, false)).Where(t => t != null).ToArray();
                            var getter = holders.Length == 1 ? holders[0].GetMethod("GetJobs", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null) : null;
                            var rootGetter = holders.Length == 1 ? holders[0].GetMethod("GetRoot", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null) : null;
                            var boundRoot = rootGetter != null && rootGetter.ReturnType == typeof(MethodInfo) && !rootGetter.ContainsGenericParameters
                                ? rootGetter.Invoke(null, null) as MethodInfo : null;
                            var typed = getter != null && getter.ReturnType == typeof(Type[]) && !getter.ContainsGenericParameters
                                ? getter.Invoke(null, null) as Type[] : null;
                            var expected = new HashSet<string>(row.Where(line => line.StartsWith("J\t", StringComparison.Ordinal)).Select(line => line.Substring(2)), StringComparer.Ordinal);
                            if (boundRoot != null && roots.Contains(boundRoot) && boundRoots.Add(boundRoot) &&
                                typed != null && typed.All(t => t != null && !t.ContainsGenericParameters) &&
                                typed.Length == expected.Count && expected.SetEquals(typed.Select(Encode))) ++catalogRoots;
                            else { ++catalogUnavailable; report.AppendLine(system.FullName + ": scheduled Type[] catalog missing/incompatible for " + row[1]); }
                        }
                        foreach (var line in row.Skip(3)) {
                            if (line.StartsWith("J\t", StringComparison.Ordinal)) source.Add(line.Substring(2));
                            else if (line.StartsWith("G\t", StringComparison.Ordinal)) report.AppendLine(system.FullName + ": " + line.Substring(2));
                            else if (line.Length != 0) throw new FormatException("Unknown summary row");
                        }
                    }
                    var jobs = new HashSet<Type>();
                    foreach (var root in roots) Collect(root, jobs);
                    var selectedJobs = new HashSet<Type>();
                    if (SourceGeneratorScheduledJobs.TryCollect(system, selectedJobs, out var selectionReason)) {
                        ++sourceSelected;
                        if (!selectedJobs.SetEquals(jobs)) {
                            ++selectionDifferences;
                            report.AppendLine(system.FullName + ": SOURCE-SELECTION DIFFERS from fresh IL; inspect semantic differences before removing fallback");
                        }
                    } else report.AppendLine(system.FullName + ": production IL fallback — " + selectionReason);
                    var legacy = jobs.ToDictionary(Encode, t => t.AssemblyQualifiedName, StringComparer.Ordinal);
                    ++compared;
                    var same = source.SetEquals(legacy.Keys);
                    if (same) ++equal;
                    if (gaps > 0) ++incomplete;
                    report.AppendLine(system.FullName + ": source=" + source.Count + ", legacy=" + legacy.Count + ", equal=" + same + ", gaps=" + gaps);
                    foreach (var key in legacy.Keys.Except(source).OrderBy(k => k, StringComparer.Ordinal)) report.AppendLine("  legacy only: " + legacy[key]);
                    foreach (var key in source.Except(legacy.Keys).OrderBy(k => k, StringComparer.Ordinal)) report.AppendLine("  source only (structural type): " + key);
                } catch (Exception exception) { ++unavailable; report.AppendLine(system.FullName + ": " + exception.Message); }
            }
            SourceGeneratorReport.Publish("ScheduledJobs", "Scheduled jobs: compared=" + compared + ", equal=" + equal + ", incomplete=" + incomplete +
                ", unavailable=" + unavailable + ", generic definitions expanded=" + genericDefinitions + ", unresolved open systems=" + generic +
                ", complete Type[] catalogs=" + catalogRoots + ", catalog unavailable/mismatch=" + catalogUnavailable +
                ", production source-selected=" + sourceSelected + ", source-selection vs IL differences=" + selectionDifferences +
                ". Metadata/IL comparison only; Type[] getters invoked, no patches or registrations invoked.", report.ToString());
        }

        internal static string Encode(Type type) {
            if (type.ContainsGenericParameters || type.IsByRef) throw new NotSupportedException("Open/byref scheduled type: " + type);
            var kind = 'n';
            string identity;
            Type[] arguments;
            if (type.IsArray || type.IsPointer) {
                kind = type.IsArray ? 'a' : '*';
                identity = type.IsArray ? type.GetArrayRank().ToString(CultureInfo.InvariantCulture) : "";
                arguments = new[] { type.GetElementType() };
            } else {
                var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                identity = type.Assembly.FullName + "\nT:" + definition.FullName.Replace('+', '.');
                arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            }
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(identity));
            return kind + encoded.Length.ToString(CultureInfo.InvariantCulture) + ":" + encoded + arguments.Length.ToString(CultureInfo.InvariantCulture) + ":" + string.Concat(arguments.Select(Encode));
        }
    }
}
