namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Provides scheduled jobs validation for BECS source-generator publication.
    /// </summary>
    public static class SourceGeneratorScheduledJobsValidation {
        /// <summary>
        /// Returns lifecycle method.
        /// </summary>
        public static MethodInfo GetLifecycleMethod(Type system, string name) {
            var contract = name == nameof(IAwake.OnAwake) ? typeof(IAwake) :
                name == nameof(IStart.OnStart) ? typeof(IStart) :
                name == nameof(IUpdate.OnUpdate) ? typeof(IUpdate) :
                name == nameof(IDestroy.OnDestroy) ? typeof(IDestroy) :
                name == nameof(IDrawGizmos.OnDrawGizmos) ? typeof(IDrawGizmos) :
                throw new ArgumentException("Unknown lifecycle method: " + name, nameof(name));
            return GetLifecycleMethods(system, contract).SingleOrDefault();
        }

        /// <summary>
        /// Tests whether the context is lifecycle burst allowed.
        /// </summary>
        public static bool IsLifecycleBurstAllowed(Type system, string name) {
            if (SourceGeneratorSystemLifecycle.TryGet(system, name, out var present, out _, out var discarded))
                return present && !discarded;
            var method = GetLifecycleMethod(system, name);
            return method != null && !Attribute.IsDefined(method, typeof(WithoutBurstAttribute));
        }

        /// <summary>
        /// Returns lifecycle methods.
        /// </summary>
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

        // Transitional IL inventory, shared with Features.Editor. This is a union
        // of statically visible call targets, not a proof for arbitrary virtual or
        // delegate dispatch. Keep source coverage until those gaps are addressed.
        /// <summary>
        /// Collects matching entries into the supplied results.
        /// </summary>
        public static void Collect(MethodInfo root, HashSet<Type> types) {
            if (root == null || root.GetMethodBody() == null) return;
            var found = ILAnalysisSession.Get((typeof(SourceGeneratorScheduledJobsValidation), root), () =>
                ILPersistentAnalysis.Get("scheduled-jobs", ILPersistentAnalysis.MethodIdentity(root), () => ILScheduledJobs.Collect(root),
                    ILSummaryData.EncodeTypes, ILSummaryData.DecodeTypes));
            types.UnionWith(found);
        }

        internal static bool IsUnityAddressIntrinsic(MethodBase candidate) {
            // These exact Unity APIs only reinterpret addresses; their injected
            // implementation tokens need not be resolvable by a managed IL reader.
            // Argument evaluation remains in the caller and is still traversed.
            if (!(candidate is MethodInfo method) || !method.IsStatic || !method.IsGenericMethod) return false;
            var definition = method.GetGenericMethodDefinition();
            var arguments = definition.GetGenericArguments();
            var parameters = definition.GetParameters();
            var pointer = typeof(void).MakePointerType();
            if (method.DeclaringType == typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtilityExtensions)) {
                // Collections forwards these in-T wrappers to injected ILSupport.
                return arguments.Length == 1 && parameters.Length == 1 && parameters[0].IsIn &&
                    parameters[0].ParameterType == arguments[0].MakeByRefType() &&
                    (definition.Name == "AddressOf" && definition.ReturnType == pointer ||
                     definition.Name == "AsRef" && definition.ReturnType == arguments[0].MakeByRefType());
            }
            if (method.DeclaringType != typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtility)) return false;
            if (arguments.Length == 2)
                return definition.Name == "As" && parameters.Length == 1 && parameters[0].ParameterType == arguments[0].MakeByRefType() &&
                    definition.ReturnType == arguments[1].MakeByRefType();
            if (arguments.Length != 1) return false;
            var byref = arguments[0].MakeByRefType();
            if (definition.Name == "AddressOf") return parameters.Length == 1 && parameters[0].ParameterType == byref && definition.ReturnType == pointer;
            if (definition.ReturnType != byref || parameters.Length == 0 || parameters[0].ParameterType != pointer) return false;
            return definition.Name == "AsRef" && parameters.Length == 1 ||
                definition.Name == "ArrayElementAsRef" && parameters.Length == 2 && parameters[1].ParameterType == typeof(int);
        }

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare Scheduled Jobs")]
        private static void Compare() {
            if (!SourceAnalysisDiagnostics.BeginComparison()) return;
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
                            report.AppendLine(system.FullName + ": SOURCE ORACLE DIFFERS from fresh production IL; inspect missing dispatch or conservative targets");
                        }
                    } else report.AppendLine(system.FullName + ": diagnostic source catalog unavailable — " + selectionReason);
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
            AppendDirectAccessReport(report, systems, assemblies);
            AppendScheduleModeReport(report, systems, assemblies);
            AppendDependencyPlanReport(report, systems);
            AppendSynchronizationReport(report, systems, assemblies);
            SourceGeneratorReport.Publish("ScheduledJobs", "Scheduled jobs: compared=" + compared + ", equal=" + equal + ", incomplete=" + incomplete +
                ", unavailable=" + unavailable + ", generic definitions expanded=" + genericDefinitions + ", unresolved open systems=" + generic +
                ", complete Type[] catalogs=" + catalogRoots + ", catalog unavailable/mismatch=" + catalogUnavailable +
                ", diagnostic source catalogs=" + sourceSelected + ", source vs production IL differences=" + selectionDifferences +
                ". Production uses fresh IL snapshots. Metadata/IL comparison only; Type[] getters invoked, no patches or registrations invoked.", report.ToString());
        }

        internal static bool IsSchedulingMethod(MethodInfo method) {
            if (!method.IsGenericMethod || method.ReturnType != typeof(Unity.Jobs.JobHandle) ||
                (method.Name != "Schedule" && method.Name != "ScheduleByRef" && method.Name != "ScheduleParallel" &&
                 method.Name != "ScheduleParallelByRef" && method.Name != "ScheduleBatch" && method.Name != "ScheduleBatchByRef" &&
                 method.Name != "ScheduleSingle" && method.Name != "ScheduleSingleByRef" &&
                 method.Name != "ScheduleParallelFor" && method.Name != "ScheduleParallelForBatch" &&
                 method.Name != "ScheduleSingleWithInject" && method.Name != "ScheduleSingleWithInjectByRef")) return false;
            var job = method.GetGenericArguments()[0];
            if (!job.GetInterfaces().Any(type => type.Name.StartsWith("IJob", StringComparison.Ordinal) &&
                (type.Namespace == "Unity.Jobs" || type.Namespace == "ME.BECS.Jobs"))) return false;
            var owner = method.DeclaringType;
            if (owner == null) return false;
            if (owner.Assembly == typeof(Ent).Assembly && (owner.Namespace == "ME.BECS" || owner.Namespace == "ME.BECS.Jobs")) return true;
            switch (owner.FullName) {
                // Bind the actual referenced API, not whichever similarly named
                // types happen to be loaded. Unity's compatibility assemblies can
                // expose the same names without being scheduling targets here.
                case "Unity.Jobs.IJobExtensions": return owner == typeof(Unity.Jobs.IJobExtensions);
                case "Unity.Jobs.IJobParallelForExtensions": return owner == typeof(Unity.Jobs.IJobParallelForExtensions);
                case "Unity.Jobs.IJobForExtensions": return owner == typeof(Unity.Jobs.IJobForExtensions);
                case "Unity.Jobs.IJobParallelForBatchExtensions": return owner == typeof(Unity.Jobs.IJobParallelForBatchExtensions);
                case "Unity.Jobs.IJobParallelForDeferExtensions": return owner == typeof(Unity.Jobs.IJobParallelForDeferExtensions);
                default: return false;
            }
        }

        private static void AppendDirectAccessReport(StringBuilder report, IEnumerable<Type> systems, Assembly[] assemblies) {
            report.AppendLine().AppendLine("System direct-access source coverage (NOT full system dependencies or Complete() control-flow validation):");
            var metadata = new Dictionary<Assembly, string[][]>();
            foreach (var assembly in assemblies.OrderBy(item => item.FullName, StringComparer.Ordinal)) {
                try {
                    metadata[assembly] = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                        .Where(item => item.Key == "ME.BECS.SystemDirectAccess.v1" && item.Value != null)
                        .Select(item => item.Value.Split('\n')).ToArray();
                } catch (Exception exception) {
                    report.AppendLine("  Metadata unavailable: " + assembly.FullName + " — " + exception.Message);
                }
            }
            var complete = 0; var incomplete = 0; var missing = 0; var invalid = 0; var unbound = 0;
            foreach (var system in systems.Distinct().Where(type => type.IsValueType && type.IsVisible && !type.ContainsGenericParameters)
                         .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)) {
                var expectedRoots = GetLifecycleMethods(system);
                var expectedCount = expectedRoots.Count;
                if (expectedCount == 0) continue;
                var identity = system.IsGenericType ? system.AssemblyQualifiedName : system.FullName;
                var selected = (system.IsGenericType ? metadata.Values.SelectMany(rows => rows) :
                    metadata.TryGetValue(system.Assembly, out var own) ? own.AsEnumerable() : Enumerable.Empty<string[]>())
                    .Where(rows => rows.Length >= 1 && rows[0] == identity).ToArray();
                // One metadata row per lifecycle root. Identical duplicates are still
                // ambiguous ownership and must not inflate the coverage totals.
                if (selected.Length != expectedCount || selected.Where(row => row.Length >= 2).Select(row => row[1]).Distinct(StringComparer.Ordinal).Count() != expectedCount) {
                    ++missing;
                    report.AppendLine("  Missing/duplicate direct lifecycle summaries: " + identity + " (source=" + selected.Length + ", expected=" + expectedCount + ")");
                    continue;
                }
                var boundRoots = new HashSet<MethodInfo>();
                foreach (var row in selected.OrderBy(row => row[1], StringComparer.Ordinal)) {
                    if (!ValidateDirectSummary(row, out var gaps)) {
                        ++invalid;
                        report.AppendLine("  Invalid direct summary: " + identity + " | " + row[1]);
                        continue;
                    }
                    MethodInfo boundRoot = null;
                    try {
                        var binding = row.Skip(3).SingleOrDefault(line => line.StartsWith("R\t", StringComparison.Ordinal))?.Split('\t');
                        var holders = binding == null ? Array.Empty<Type>() : assemblies.Where(assembly => assembly.FullName == binding[1])
                            .Select(assembly => assembly.GetType(binding[2], false)).Where(type => type != null).ToArray();
                        var getter = holders.Length == 1 ? holders[0].GetMethod("GetRoot", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null) : null;
                        if (getter != null && !getter.ContainsGenericParameters && getter.ReturnType == typeof(MethodInfo))
                            boundRoot = getter.Invoke(null, null) as MethodInfo;
                    } catch (Exception exception) { report.AppendLine("  Direct root lookup failed: " + identity + " — " + exception.GetBaseException().Message); }
                    if (boundRoot == null || !expectedRoots.Contains(boundRoot) || !boundRoots.Add(boundRoot)) {
                        ++unbound;
                        report.AppendLine("  Missing/ambiguous direct root binding: " + identity + " | " + row[1]);
                        continue;
                    }
                    if (gaps == 0) ++complete; else ++incomplete;
                    report.AppendLine("  " + identity + " | " + row[1] + ": accesses=" + row.Skip(3).Count(line => line.StartsWith("D\t", StringComparison.Ordinal)) +
                        ", query filters=" + row.Skip(3).Count(line => line.StartsWith("Q\t", StringComparison.Ordinal)) + ", gaps=" + gaps);
                    foreach (var dependency in row.Skip(3).Where(line => line.StartsWith("Y\t", StringComparison.Ordinal))) report.AppendLine("    system dependency: " + dependency.Substring(2));
                    foreach (var filter in row.Skip(3).Where(line => line.StartsWith("Q\t", StringComparison.Ordinal))) report.AppendLine("    query filter: " + filter.Substring(2));
                    foreach (var gap in row.Skip(3).Where(line => line.StartsWith("G\t", StringComparison.Ordinal))) report.AppendLine("    " + gap.Substring(2));
                }
            }
            report.AppendLine("Direct roots: complete and bound=" + complete + ", incomplete and bound=" + incomplete + ", invalid=" + invalid + ", unbound=" + unbound + ", systems with missing/duplicate roots=" + missing);
            report.AppendLine("Generated MethodInfo getters invoked; lifecycle methods NOT invoked. System-pointer dependencies and query filters are reported separately from direct component data accesses. Coverage does not prove scheduled-job access union, AsReadonly associations or Complete() ordering. No direct-access consumers were switched.");
        }

        internal static bool ValidateDirectSummary(string[] rows, out uint gaps) {
            gaps = 0;
            if (rows == null || rows.Length < 3 || string.IsNullOrEmpty(rows[0]) || !rows[1].StartsWith("M:", StringComparison.Ordinal) ||
                !uint.TryParse(rows[2], NumberStyles.None, CultureInfo.InvariantCulture, out gaps) || rows[2] != gaps.ToString(CultureInfo.InvariantCulture)) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var gapRows = 0;
            var bindingSeen = false;
            var systemDependencies = new HashSet<Type>();
            var queryFilters = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows.Skip(3)) {
                var fields = row.Split('\t');
                if (fields[0] == "Q") {
                    if (fields.Length != 3 || (fields[1] != "with" && fields[1] != "without" && fields[1] != "any" && fields[1] != "aspect")) return false;
                    Type component;
                    try { component = Type.GetType(fields[2], false); }
                    catch (Exception) { return false; }
                    if (component == null || !component.IsValueType || component.ContainsGenericParameters ||
                        !typeof(IComponentBase).IsAssignableFrom(component) || component.AssemblyQualifiedName != fields[2] || !queryFilters.Add(row)) return false;
                    continue;
                }
                if (fields[0] == "Y") {
                    if (fields.Length != 2) return false;
                    Type system;
                    try { system = Type.GetType(fields[1], false); }
                    catch (Exception) { return false; }
                    if (system == null || !system.IsValueType || system.ContainsGenericParameters || !typeof(ISystem).IsAssignableFrom(system) || !systemDependencies.Add(system)) return false;
                    continue;
                }
                if (fields[0] == "R") {
                    if (bindingSeen || fields.Length != 5 || fields[3] != "GetRoot" || fields[4] != "v1" || string.IsNullOrEmpty(fields[1]) ||
                        fields[2] != "ME.BECS.SourceGenerated.SystemDirectRoot_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(fields[1] + "\n" + rows[0] + "\n" + rows[1])) return false;
                    bindingSeen = true;
                    continue;
                }
                if (fields[0] == "G" && row.Length > 2) { ++gapRows; continue; }
                if (fields.Length != 5 || fields[0] != "D" || string.IsNullOrEmpty(fields[1]) || !fields[2].StartsWith("T:", StringComparison.Ordinal) ||
                    (fields[3] != "0" && fields[3] != "1" && fields[3] != "2") || (fields[4] != "0" && fields[4] != "1") ||
                    !seen.Add(fields[1] + "\t" + fields[2])) return false;
            }
            return gaps == 0 ? gapRows == 0 : gapRows > 0;
        }

        private static void AppendScheduleModeReport(StringBuilder report, IEnumerable<Type> systems, Assembly[] assemblies) {
            report.AppendLine().AppendLine("Schedule modes (source CFG and call-site arguments; NOT full dependency scheduling):");
            var metadata = new Dictionary<Assembly, string[][]>();
            foreach (var assembly in assemblies.OrderBy(item => item.FullName, StringComparer.Ordinal)) {
                try {
                    metadata[assembly] = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                        .Where(item => item.Key == "ME.BECS.SystemScheduleModes.v1" && item.Value != null)
                        .Select(item => item.Value.Split('\n')).ToArray();
                } catch (Exception exception) { report.AppendLine("  Schedule modes unavailable: " + assembly.FullName + " — " + exception.Message); }
            }
            var complete = 0; var incomplete = 0; var missing = 0; var invalid = 0;
            foreach (var system in systems.Distinct().Where(type => type.IsValueType && type.IsVisible && !type.ContainsGenericParameters)
                         .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)) {
                var count = GetLifecycleMethods(system).Count;
                if (count == 0) continue;
                var identity = system.IsGenericType ? system.AssemblyQualifiedName : system.FullName;
                var selected = (system.IsGenericType ? metadata.Values.SelectMany(rows => rows) :
                        metadata.TryGetValue(system.Assembly, out var own) ? own.AsEnumerable() : Enumerable.Empty<string[]>())
                    .Where(rows => rows.Length >= 1 && rows[0] == identity).ToArray();
                if (selected.Length != count || selected.Where(row => row.Length >= 2).Select(row => row[1]).Distinct(StringComparer.Ordinal).Count() != count) {
                    ++missing; report.AppendLine("  Missing/duplicate schedule mode roots: " + identity); continue;
                }
                foreach (var row in selected.OrderBy(row => row[1], StringComparer.Ordinal)) {
                    if (!ValidateScheduleModeSummary(row, out var gaps)) { ++invalid; report.AppendLine("  Invalid schedule modes: " + identity); continue; }
                    if (gaps == 0) ++complete; else ++incomplete;
                    report.AppendLine("  " + identity + " | " + row[1] + ": jobs=" + row.Count(line => line.StartsWith("S\t", StringComparison.Ordinal)) + ", gaps=" + gaps);
                    foreach (var line in row.Skip(3).Where(line => line.Length > 0)) report.AppendLine("    " + line);
                }
            }
            report.AppendLine("Schedule-mode metadata: complete=" + complete + ", incomplete=" + incomplete + ", invalid=" + invalid + ", systems missing roots=" + missing);
            report.AppendLine("Modes: 0=normal, 1=readonly, 0|1=both (must retain writes), ?=unresolved. No lifecycle methods invoked or dependency consumers switched.");
        }

        internal static bool ValidateScheduleModeSummary(string[] rows, out uint gaps) {
            gaps = 0;
            if (rows == null || rows.Length < 3 || string.IsNullOrEmpty(rows[0]) || !rows[1].StartsWith("M:", StringComparison.Ordinal) ||
                !uint.TryParse(rows[2], NumberStyles.None, CultureInfo.InvariantCulture, out gaps) || rows[2] != gaps.ToString(CultureInfo.InvariantCulture)) return false;
            var jobs = new HashSet<Type>();
            var gapRows = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows.Skip(3)) {
                if (row.Length == 0) continue;
                var fields = row.Split('\t');
                if (fields.Length == 2 && fields[0] == "G" && fields[1].Length > 0) { if (!gapRows.Add(row)) return false; continue; }
                if (fields.Length != 3 || fields[0] != "S" ||
                    (fields[1] != "0" && fields[1] != "1" && fields[1] != "0|1" && fields[1] != "?") ||
                    (fields[1] == "?" && gaps == 0)) return false;
                Type job;
                try { job = Type.GetType(fields[2], false); }
                catch (Exception) { return false; }
                if (job == null || !job.IsValueType || job.ContainsGenericParameters || job.AssemblyQualifiedName != fields[2] || !jobs.Add(job) ||
                    !job.GetInterfaces().Any(type => type.Name.StartsWith("IJob", StringComparison.Ordinal) &&
                        (type.Namespace == "Unity.Jobs" || type.Namespace == "ME.BECS.Jobs"))) return false;
            }
            return gapRows.Count == Math.Min(gaps, 12u);
        }

        private static void AppendSynchronizationReport(StringBuilder report, IEnumerable<Type> systems, Assembly[] assemblies) {
            report.AppendLine().AppendLine("Source synchronization (handle provenance and helper CFGs; complete plans selected by production):");
            var entries = new List<(Assembly Publisher, string[] Rows)>();
            foreach (var assembly in assemblies.OrderBy(item => item.FullName, StringComparer.Ordinal)) {
                try {
                    entries.AddRange(assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                        .Where(item => item.Key == "ME.BECS.SystemSynchronization.v3" && item.Value != null)
                        .Select(item => (assembly, item.Value.Split('\n'))));
                } catch (Exception exception) { report.AppendLine("  Synchronization catalog unavailable: " + assembly.FullName + " — " + exception.Message); }
            }
            var proven = 0; var unproven = 0; var incomplete = 0; var invalid = 0; var missing = 0;
            foreach (var system in systems.Distinct().Where(type => type.IsValueType && type.IsVisible && !type.ContainsGenericParameters)
                         .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)) {
                var roots = GetLifecycleMethods(system);
                var identity = system.IsGenericType ? system.AssemblyQualifiedName : system.FullName;
                var selected = entries.Where(entry => entry.Rows.Length > 0 && entry.Rows[0] == identity &&
                    (system.IsGenericType || entry.Publisher == system.Assembly)).ToArray();
                var seen = new HashSet<MethodInfo>();
                foreach (var entry in selected) {
                    try {
                        if (!ValidateSynchronizationSummary(entry.Rows, out var status) ||
                            !SourceGeneratorSystemDependencies.TryRoot(entry.Publisher, entry.Rows, out var root) ||
                            !roots.Contains(root) || !seen.Add(root)) {
                            ++invalid; report.AppendLine("  Invalid/duplicate synchronization root: " + identity); continue;
                        }
                        if (status == "proven") ++proven;
                        else if (status == "unproven") ++unproven;
                        else ++incomplete;
                        report.AppendLine("  " + identity + " | " + root.Name + ": " + status);
                        foreach (var row in entry.Rows.Skip(3).Where(row => row.Length > 0 && !row.StartsWith("R\t", StringComparison.Ordinal)))
                            report.AppendLine("    " + row);
                    } catch (Exception exception) { ++invalid; report.AppendLine("  Synchronization binding failed: " + identity + " — " + exception.GetBaseException().Message); }
                }
                foreach (var root in roots.Where(root => !seen.Contains(root))) { ++missing; report.AppendLine("  Missing synchronization root: " + identity + " | " + root.Name); }
            }
            report.AppendLine("Synchronization roots: proven=" + proven + ", unproven=" + unproven + ", incomplete=" + incomplete + ", invalid=" + invalid + ", missing=" + missing);
            report.AppendLine("Unproven means some modeled direct access can retain pending work; it is not a claim that this path executes at runtime. Incomplete coverage never proves safety and still requires legacy diagnostics. Typed root getters only; this report invokes no lifecycle methods or registrations.");
        }

        internal static bool ValidateSynchronizationSummary(string[] rows, out string status) {
            status = null;
            if (rows == null || rows.Length < 3 || string.IsNullOrEmpty(rows[0]) || !rows[1].StartsWith("M:", StringComparison.Ordinal) ||
                !uint.TryParse(rows[2], NumberStyles.None, CultureInfo.InvariantCulture, out var gaps) || rows[2] != gaps.ToString(CultureInfo.InvariantCulture)) return false;
            var counts = new Dictionary<string, uint>(StringComparer.Ordinal);
            var gapRows = new HashSet<string>(StringComparer.Ordinal);
            var unsafeRows = new HashSet<string>(StringComparer.Ordinal);
            var roots = 0;
            foreach (var row in rows.Skip(3)) {
                if (row.Length == 0) continue;
                var fields = row.Split('\t');
                if (fields[0] == "R") { if (fields.Length != 5 || ++roots != 1) return false; continue; }
                if (fields.Length != 2) return false;
                if (fields[0] == "S") {
                    if (status != null || (fields[1] != "proven" && fields[1] != "unproven" && fields[1] != "incomplete")) return false;
                    status = fields[1];
                } else if (fields[0] == "G" || fields[0] == "E") {
                    if (fields[1].Length == 0 || !(fields[0] == "G" ? gapRows : unsafeRows).Add(row)) return false;
                } else if (fields[0] == "A" || fields[0] == "C" || fields[0] == "U") {
                    if (counts.ContainsKey(fields[0]) || !uint.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
                        fields[1] != count.ToString(CultureInfo.InvariantCulture)) return false;
                    counts.Add(fields[0], count);
                } else return false;
            }
            if (counts.Count != 3 || gapRows.Count != Math.Min(gaps, 12u) ||
                counts["U"] > counts["A"] || unsafeRows.Count != Math.Min(counts["U"], 12u)) return false;
            return status == (gaps != 0 ? "incomplete" : counts["U"] != 0 ? "unproven" : "proven") && (gaps != 0 || roots == 1);
        }

        private static void AppendDependencyPlanReport(StringBuilder report, IEnumerable<Type> systems) {
            report.AppendLine().AppendLine("Diagnostic compiler dependency union vs production IL operation snapshots (synchronization selected independently):");
            var reader = new SourceGeneratorSystemDependencies();
            var current = new Systems.SystemDependenciesCodeGenerator();
            var complete = 0; var unavailable = 0; var equal = 0; var different = 0; var comparisonFailed = 0;
            var sourceOnly = 0; var synchronizationFallback = 0;
            var audited = 0; var unresolvedRoots = 0; var unresolvedSites = 0;
            string Record(Jobs.JobsEarlyInitCodeGenerator.TypeInfo info) => ((byte)info.op).ToString(CultureInfo.InvariantCulture) + "\t" + info.type.AssemblyQualifiedName;
            foreach (var system in systems.Distinct().Where(type => type.IsValueType && type.IsVisible && !type.ContainsGenericParameters)
                         .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)) {
                foreach (var root in GetLifecycleMethods(system)) {
                    var sourceSynchronization = reader.TryReadSynchronization(root, out _, out var synchronizationReason);
                    if (!sourceSynchronization) {
                        ++synchronizationFallback;
                        report.AppendLine("  Legacy synchronization required: " + system.AssemblyQualifiedName + " | " + root.Name + " — " + synchronizationReason);
                    }
                    var sourceAvailable = reader.TryRead(root, out var source, out var reason);
                    if (!sourceAvailable) {
                        ++unavailable; report.AppendLine("  Source plan unavailable: " + system.AssemblyQualifiedName + " | " + root.Name + " — " + reason);
                    } else {
                        ++complete;
                        if (sourceSynchronization) ++sourceOnly;
                    }
                    try {
                        var legacy = current.GetComparisonAnalysis(root, out var unresolved);
                        ++audited;
                        if (unresolved.Length != 0) {
                            ++unresolvedRoots;
                            unresolvedSites += unresolved.Length;
                            report.AppendLine("  IL dispatch unresolved: " + system.AssemblyQualifiedName + " | " + root.Name + " — sites=" + unresolved.Length);
                            foreach (var issue in unresolved.Take(12)) report.AppendLine("    " + issue);
                        }
                        if (!sourceAvailable) continue;
                        var expected = new HashSet<string>(legacy.ops.Select(Record), StringComparer.Ordinal);
                        var actual = new HashSet<string>(source.Select(Record), StringComparer.Ordinal);
                        if (actual.SetEquals(expected)) { ++equal; continue; }
                        ++different;
                        report.AppendLine("  Dependency union differs: " + system.AssemblyQualifiedName + " | " + root.Name);
                        foreach (var item in actual.Except(expected).OrderBy(value => value, StringComparer.Ordinal)) report.AppendLine("    source: " + item);
                        foreach (var item in expected.Except(actual).OrderBy(value => value, StringComparer.Ordinal)) report.AppendLine("    legacy IL: " + item);
                    } catch (Exception exception) { ++comparisonFailed; report.AppendLine("  Legacy IL comparison failed: " + system.AssemblyQualifiedName + " | " + root.Name + " — " + exception.GetBaseException().Message); }
                }
            }
            report.AppendLine("Dependency plans: complete and typed=" + complete + ", unavailable=" + unavailable + ", equal=" + equal + ", different=" + different + ", comparison failed=" + comparisonFailed);
            report.AppendLine("IL dispatch diagnostics: audited=" + audited + ", roots with unresolved sites=" + unresolvedRoots + ", sites=" + unresolvedSites +
                ". An empty diagnostic list is NOT proof of full IL dependency coverage or synchronization.");
            report.AppendLine("Source diagnostics: both domains complete=" + sourceOnly + ", operations unavailable=" + unavailable + ". Production operations use IL snapshots; synchronization compatibility IL fallback=" + synchronizationFallback);
            report.AppendLine("Differences are reported for semantic review, not as export gates. Typed getters invoked; lifecycle methods/registrations NOT invoked. Source operation catalogs do not replace or veto IL snapshots. Synchronization remains source-first independently; operation parity is not a handle-flow proof.");
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
