namespace ME.BECS.Editor {

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    internal static class SourceGeneratorSafetyValidation {

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare Job Safety")]
        private static void Compare() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before comparing job safety.");
                return;
            }
            if (!UnityEditor.EditorUtility.DisplayDialog("Compare job safety",
                "The legacy IL analyzer invokes IRefOp.Op getters to determine access modes. Jobs and registration methods are not invoked. Continue?", "Compare", "Cancel")) return;
            try {
                using var lookup = SourceGeneratorBridge.BeginLookupScope();
                Systems.SystemDependenciesCodeGenerator.GetUsedObjects(true, out var used, useSourceCatalogs: false);
                var jobs = used.jobTypes.ToList();
                CodeGenerator.PatchSystemsList(jobs);
                var catalogs = new Dictionary<Assembly, Dictionary<string, string[]>>();
                var closedJobs = new SourceGeneratorClosedJobCatalog();
                var report = new StringBuilder("[ME.BECS] Job safety: source summaries vs fresh legacy IL analysis\n");
                var compared = 0;
                var matched = 0;
                var incomplete = 0;
                var unavailable = 0;
                var sizeCompared = 0;
                var sizeEqual = 0;
                var sizeUnavailable = 0;
                var typedCatalogs = 0;
                var selectedSafety = 0;
                var differentSafety = 0;
                var safetyCandidates = 0;
                foreach (var job in jobs.Distinct().Where(t => t.IsValueType && t.IsVisible && !t.ContainsGenericParameters)
                             .OrderBy(t => t.FullName, StringComparer.Ordinal).ThenBy(t => t.Assembly.FullName, StringComparer.Ordinal)) {
                    ++safetyCandidates;
                    if (!catalogs.TryGetValue(job.Assembly, out var catalog)) {
                        catalog = new Dictionary<string, string[]>(StringComparer.Ordinal);
                        foreach (AssemblyMetadataAttribute attribute in job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)) {
                            if (attribute.Key != "ME.BECS.JobSafety.v1") continue;
                            var rows = attribute.Value?.Split('\n');
                            if (rows == null || rows.Length < 3 || rows[0].Length == 0) continue;
                            if (catalog.ContainsKey(rows[0])) catalog[rows[0]] = null;
                            else catalog.Add(rows[0], rows);
                        }
                        catalogs.Add(job.Assembly, catalog);
                    }
                    string[] summary;
                    if (!(job.IsGenericType ? closedJobs.TryGet(job, "JobSafety", out summary) : catalog.TryGetValue(job.FullName, out summary)) || summary == null) { ++unavailable; continue; }
                    if (!int.TryParse(summary[2], NumberStyles.None, CultureInfo.InvariantCulture, out var gaps)) { ++unavailable; continue; }
                    var source = new HashSet<string>(StringComparer.Ordinal);
                    var invalid = false;
                    foreach (var row in summary.Skip(3)) {
                        if (!row.StartsWith("D\t", StringComparison.Ordinal)) continue;
                        var fields = row.Split('\t');
                        if (fields.Length != 5 || !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var mode) ||
                            mode > 2 || (fields[4] != "0" && fields[4] != "1") || !source.Add(row.Substring(2))) { invalid = true; break; }
                    }
                    if (invalid) { ++unavailable; report.AppendLine("Malformed safety summary: " + job.FullName); continue; }
                    HashSet<string> baseline;
                    try {
                        var legacy = Jobs.JobsEarlyInitCodeGenerator.GetJobTypesInfo(job);
                        var selection = SourceGeneratorJobSafety.Validate(job, summary, legacy, out _);
                        if (selection >= 0) ++typedCatalogs;
                        if (selection == 1) ++selectedSafety;
                        if (selection == 0) ++differentSafety;
                        if (selection < 0 && gaps == 0)
                            report.AppendLine("Source safety unavailable for complete summary (catalog/identity/records): " + job.AssemblyQualifiedName);
                        var sizeStatus = CompareSizes(job, summary, legacy.Select(entry => entry.type).Where(type => typeof(IComponent).IsAssignableFrom(type)).Distinct().ToArray(), report);
                        if (sizeStatus < 0) ++sizeUnavailable;
                        else { ++sizeCompared; if (sizeStatus == 1) ++sizeEqual; }
                        Jobs.JobsEarlyInitCodeGenerator.UpdateDeps(legacy);
                        baseline = new HashSet<string>(legacy.Select(d => d.type.Assembly.FullName + "\tT:" + d.type.FullName.Replace('+', '.') + "\t" +
                            ((int)d.op).ToString(CultureInfo.InvariantCulture) + "\t" + (d.isArg ? "1" : "0")), StringComparer.Ordinal);
                    } catch (Exception exception) {
                        ++unavailable;
                        report.AppendLine("Legacy analysis failed for " + job.FullName + ": " + exception.GetBaseException().Message);
                        continue;
                    }
                    var equal = source.SetEquals(baseline);
                    ++compared;
                    if (equal) ++matched;
                    if (gaps != 0) ++incomplete;
                    if (equal && gaps == 0) continue;
                    report.AppendLine($"{job.FullName}: {(equal ? "dependencies match" : "DEPENDENCIES DIFFER")}, analysis gaps={gaps}");
                    foreach (var row in source.Except(baseline).OrderBy(s => s, StringComparer.Ordinal)) report.AppendLine("  Source only: " + row.Replace('\t', ' '));
                    foreach (var row in baseline.Except(source).OrderBy(s => s, StringComparer.Ordinal)) report.AppendLine("  Legacy only: " + row.Replace('\t', ' '));
                    foreach (var gap in summary.Skip(3).Where(s => s.StartsWith("G\t", StringComparison.Ordinal))) report.AppendLine("  " + gap.Substring(2));
                }
                report.AppendLine($"Compared={compared}, equal={matched}, different={compared - matched}, incomplete={incomplete}, unavailable={unavailable}");
                var sizeSummary = $"MaxStructSize: compared={sizeCompared}, equal={sizeEqual}, different={sizeCompared - sizeEqual}, unavailable/incomplete={sizeUnavailable} (among jobs with readable safety summaries and successful legacy analysis; source metadata getters only; initializer NOT invoked; production selects source only after component-set and maximum-size parity, otherwise incomplete coverage falls back and complete differences stop export)";
                report.AppendLine(sizeSummary);
                report.AppendLine("Typed source safety catalogs=" + typedCatalogs + " (generated typeof getters read; jobs and initializers NOT invoked)");
                var safetySelection = $"Safety consumer: source={selectedSafety}, complete differences={differentSafety}, unavailable/fallback candidates={safetyCandidates - selectedSafety - differentSafety} (same validator as production; no jobs or registrations invoked)";
                report.AppendLine(safetySelection);
                report.AppendLine("Normalized RO/WO/RW and isArg compared. No cache writes, jobs or registrations; legacy IRefOp.Op getters were allowed. Matching incomplete summaries do NOT establish coverage. Production job safety consumers select zero-gap source dependencies after exact normalized parity; incomplete coverage remains legacy and complete differences stop export. Legacy analysis remains the transitional parity oracle, not removed.");
                SourceGeneratorReport.Publish("Safety",
                    $"Safety: compared={compared}, equal={matched}, different={compared - matched}, incomplete={incomplete}, unavailable={unavailable}\n" + sizeSummary + "\n" + safetySelection, report.ToString());
            } catch (Exception exception) {
                UnityEngine.Debug.LogException(exception);
            }
        }
        private static int CompareSizes(Type job, string[] rows, Type[] legacy, StringBuilder report) {
            return ValidateSizeInitializer(job, rows, legacy, report, out _);
        }

        internal static int ValidateSizeInitializer(Type job, string[] rows, Type[] legacy, StringBuilder report, out string initializer) {
            initializer = null;
            try {
                if (rows == null || rows.Length < 3 || rows[0] != (job.IsGenericType ? job.AssemblyQualifiedName : job.FullName) ||
                    !rows[1].StartsWith("M:", StringComparison.Ordinal)) return -1;
                if (rows[2] != "0" || rows.Skip(3).Any(row => row.StartsWith("G\t", StringComparison.Ordinal))) return -1;
                var records = rows.Skip(3).Where(row => row.StartsWith("S\t", StringComparison.Ordinal)).ToArray();
                if (records.Length != 1) throw new InvalidOperationException("missing/ambiguous size initializer metadata");
                var fields = records[0].Split('\t');
                if (fields.Length != 5 || fields[3] != "Apply" || fields[4] != "v1") throw new InvalidOperationException("invalid size schema");
                var expected = "ME.BECS.SourceGenerated.JobMaxStructSize_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(fields[1] + "\n" + rows[0] + "\n" + rows[1]);
                if (fields[2] != expected) throw new InvalidOperationException("size initializer identity mismatch");
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.FullName == fields[1]).ToArray();
                if (assemblies.Length != 1) throw new InvalidOperationException("size initializer assembly unavailable/ambiguous");
                var type = assemblies[0].GetType(expected, false);
                if (type == null || !type.IsVisible) throw new InvalidOperationException("size initializer type unavailable");
                var apply = type.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);
                var getComponents = type.GetMethod("GetComponents", BindingFlags.Public | BindingFlags.Static);
                var getSizes = type.GetMethod("GetSizes", BindingFlags.Public | BindingFlags.Static);
                if (apply == null || !apply.IsGenericMethodDefinition || apply.GetGenericArguments().Length != 1 || apply.GetParameters().Length != 0 || apply.ReturnType != typeof(void) ||
                    getComponents == null || getComponents.ContainsGenericParameters || getComponents.GetParameters().Length != 0 || getComponents.ReturnType != typeof(Type[]) ||
                    getSizes == null || getSizes.ContainsGenericParameters || getSizes.GetParameters().Length != 0 || getSizes.ReturnType != typeof(uint[]))
                    throw new InvalidOperationException("size initializer/getter signature mismatch");
                apply.MakeGenericMethod(job); // Check constraints, never invoke.
                var components = (Type[])getComponents.Invoke(null, null);
                var sizes = (uint[])getSizes.Invoke(null, null);
                if (components == null || sizes == null || components.Length != sizes.Length || components.Distinct().Count() != components.Length ||
                    components.Any(component => component == null || !component.IsValueType || component.ContainsGenericParameters || !typeof(IComponent).IsAssignableFrom(component)) || sizes.Any(size => size == 0u))
                    throw new InvalidOperationException("invalid size metadata values");
                var sameTypes = new HashSet<Type>(components).SetEquals(legacy);
                foreach (var component in components.Except(legacy).OrderBy(component => component.AssemblyQualifiedName, StringComparer.Ordinal))
                    report.AppendLine("Size source-only component: " + job.FullName + " / " + component.AssemblyQualifiedName);
                foreach (var component in legacy.Except(components).OrderBy(component => component.AssemblyQualifiedName, StringComparer.Ordinal))
                    report.AppendLine("Size legacy-only component: " + job.FullName + " / " + component.AssemblyQualifiedName);
                var legacySizes = legacy.ToDictionary(component => component, component => (uint)System.Runtime.InteropServices.Marshal.SizeOf(component));
                var sourceMaximum = sizes.Length == 0 ? 0u : sizes.Max();
                var legacyMaximum = legacySizes.Count == 0 ? 0u : legacySizes.Values.Max();
                var equal = sameTypes && sourceMaximum == legacyMaximum;
                for (var index = 0; index < components.Length; ++index) {
                    if (!legacySizes.TryGetValue(components[index], out var oldSize) || sizes[index] == oldSize) continue;
                    report.AppendLine("Size layout differs: " + job.FullName + " / " + components[index].FullName + ": UnsafeUtility=" + sizes[index] + ", Marshal=" + oldSize);
                }
                if (!equal) report.AppendLine("MaxStructSize differs: " + job.FullName + ": same component set=" + sameTypes + ", source=" + sourceMaximum + ", legacy=" + legacyMaximum);
                if (equal) initializer = "global::" + expected + ".Apply<" + EditorUtils.GetTypeName(job) + ">();";
                return equal ? 1 : 0;
            } catch (Exception exception) {
                report.AppendLine("MaxStructSize unavailable: " + job.FullName + " — " + exception.GetBaseException().Message);
                return -1;
            }
        }
    }
}
