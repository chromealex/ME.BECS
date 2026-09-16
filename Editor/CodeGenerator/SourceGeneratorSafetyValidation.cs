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
                foreach (var job in jobs.Distinct().Where(t => t.IsValueType && t.IsVisible && !t.ContainsGenericParameters)
                             .OrderBy(t => t.FullName, StringComparer.Ordinal).ThenBy(t => t.Assembly.FullName, StringComparer.Ordinal)) {
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
                report.AppendLine("Normalized RO/WO/RW and isArg compared. No cache writes, jobs or registrations; legacy IRefOp.Op getters were allowed. Matching incomplete summaries do NOT establish coverage. Runtime safety remains legacy.");
                SourceGeneratorReport.Publish("Safety",
                    $"Safety: compared={compared}, equal={matched}, different={compared - matched}, incomplete={incomplete}, unavailable={unavailable}", report.ToString());
            } catch (Exception exception) {
                UnityEngine.Debug.LogException(exception);
            }
        }
    }
}
