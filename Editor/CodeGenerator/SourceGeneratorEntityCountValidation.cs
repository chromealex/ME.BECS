namespace ME.BECS.Editor {

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    internal static class SourceGeneratorEntityCountValidation {

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare Job Entity Counts and Weights")]
        private static void CompareMetadata() => Compare();

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare Job Entity Counts")]
        private static void Compare() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before comparing entity counts.");
                return;
            }
            try {
                using var lookup = SourceGeneratorBridge.BeginLookupScope();
                Systems.SystemDependenciesCodeGenerator.GetUsedObjects(true, out var used, useSourceCatalogs: false);
                var generator = new EntityTypeCodeGenerator { entityTypes = used.entityTypes, editorAssembly = true, asms = EditorUtils.GetAssembliesInfo() };
                var groups = EntityTypeCodeGenerator.GetAllTypes(generator, out var groupCount);
                var keys = groups.ToDictionary(g => g.Item1.Assembly.FullName + "\tT:" + g.Item1.FullName.Replace('+', '.'), g => g.Item2);
                var jobs = used.jobTypes.ToList();
                CodeGenerator.PatchSystemsList(jobs);
                var catalogs = new Dictionary<Assembly, Dictionary<string, string[]>>();
                var weightCatalogs = new Dictionary<Assembly, Dictionary<string, string[]>>();
                var closedJobs = new SourceGeneratorClosedJobCatalog();
                var weightConsumer = new SourceGeneratorJobWeights();
                var generatedWeightInitializers = 0;
                var sourceWeightValues = 0;
                var legacyWeightFallbacks = 0;
                var report = new StringBuilder("[ME.BECS] Job entity counts and weights: source summaries vs fresh legacy IL analysis\n");
                var weightsCompared = 0;
                var weightsMatched = 0;
                var weightsIncomplete = 0;
                var weightsUnavailable = 0;
                var compared = 0;
                var matched = 0;
                var unavailable = 0;
                var incomplete = 0;
                foreach (var job in jobs.Distinct().Where(t => t.IsValueType && t.IsVisible && !t.ContainsGenericParameters)
                             .OrderBy(t => t.FullName, StringComparer.Ordinal).ThenBy(t => t.Assembly.FullName, StringComparer.Ordinal)) {
                    if (weightConsumer.TryGetInitializer(job, out _)) ++generatedWeightInitializers;
                    else if (weightConsumer.TryGetComplete(job, out _)) ++sourceWeightValues;
                    else ++legacyWeightFallbacks;
                    if (!catalogs.TryGetValue(job.Assembly, out var catalog)) {
                        catalog = new Dictionary<string, string[]>(StringComparer.Ordinal);
                        var weightCatalog = new Dictionary<string, string[]>(StringComparer.Ordinal);
                        foreach (AssemblyMetadataAttribute attribute in job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)) {
                            if (attribute.Key != "ME.BECS.JobEntityCounts.v1" && attribute.Key != "ME.BECS.JobWeights.v1") continue;
                            var rows = attribute.Value?.Split('\n');
                            if (rows == null || rows.Length < 3 || rows[0].Length == 0) continue;
                            var destination = attribute.Key == "ME.BECS.JobWeights.v1" ? weightCatalog : catalog;
                            // Multiple Execute implementations need an explicit root selection.
                            if (destination.ContainsKey(rows[0])) destination[rows[0]] = null;
                            else destination.Add(rows[0], rows);
                        }
                        catalogs.Add(job.Assembly, catalog);
                        weightCatalogs.Add(job.Assembly, weightCatalog);
                    }
                    string[] weightRows;
                    if ((job.IsGenericType ? closedJobs.TryGet(job, "JobWeights", out weightRows) : weightCatalogs[job.Assembly].TryGetValue(job.FullName, out weightRows)) && weightRows != null && weightRows.Length >= 4 &&
                        int.TryParse(weightRows[2], NumberStyles.None, CultureInfo.InvariantCulture, out var weightGaps) &&
                        uint.TryParse(weightRows[3], NumberStyles.None, CultureInfo.InvariantCulture, out var sourceWeight)) {
                        try {
                            var legacyContributions = new Dictionary<string, uint>(StringComparer.Ordinal);
                            var legacyWeight = Jobs.JobsEarlyInitCodeGenerator.GetJobWeightsInfo(job, legacyContributions).weight;
                            ++weightsCompared;
                            if (sourceWeight == legacyWeight) ++weightsMatched;
                            if (weightGaps != 0) ++weightsIncomplete;
                            if (sourceWeight != legacyWeight || weightGaps != 0) {
                                report.AppendLine($"{job.FullName}: weight source/legacy={sourceWeight}/{legacyWeight}, analysis gaps={weightGaps}");
                                if (sourceWeight != legacyWeight) {
                                    var sourceContributions = new Dictionary<string, uint>(StringComparer.Ordinal);
                                    foreach (var row in weightRows.Skip(4).Where(s => s.StartsWith("W\t", StringComparison.Ordinal))) {
                                        var fields = row.Split('\t');
                                        if (fields.Length == 3 && uint.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                                            sourceContributions[fields[1]] = value;
                                    }
                                    foreach (var key in sourceContributions.Keys.Union(legacyContributions.Keys).OrderBy(s => s, StringComparer.Ordinal)) {
                                        sourceContributions.TryGetValue(key, out var sourceValue);
                                        legacyContributions.TryGetValue(key, out var legacyValue);
                                        if (sourceValue != legacyValue) report.AppendLine($"  Weight contribution {key}: source/legacy={sourceValue}/{legacyValue}");
                                    }
                                }
                                foreach (var gap in weightRows.Skip(4).Where(s => s.StartsWith("G\t", StringComparison.Ordinal))) report.AppendLine("  " + gap.Substring(2));
                            }
                        } catch (Exception weightException) {
                            ++weightsUnavailable;
                            report.AppendLine("Legacy weight analysis failed for " + job.FullName + ": " + weightException.GetBaseException().Message);
                        }
                    } else {
                        ++weightsUnavailable;
                    }
                    string[] summary;
                    if (!(job.IsGenericType ? closedJobs.TryGet(job, "JobEntityCounts", out summary) : catalog.TryGetValue(job.FullName, out summary)) || summary == null) { ++unavailable; continue; }
                    if (!int.TryParse(summary[2], NumberStyles.None, CultureInfo.InvariantCulture, out var gaps)) {
                        report.AppendLine("Malformed count summary: " + job.FullName);
                        ++unavailable;
                        continue;
                    }
                    var counts = new int[groupCount];
                    var loops = 0;
                    var invalid = false;
                    var seen = new HashSet<uint>();
                    foreach (var row in summary.Skip(3)) {
                        if (!row.StartsWith("C\t", StringComparison.Ordinal)) continue;
                        var fields = row.Split('\t');
                        if (fields.Length != 5 || !keys.TryGetValue(fields[1] + "\t" + fields[2], out var group) || !seen.Add(group) ||
                            !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var inline) ||
                            !int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out var loop)) { invalid = true; break; }
                        counts[group] = inline;
                        loops = checked(loops + loop);
                    }
                    if (invalid) { ++unavailable; report.AppendLine("Unresolved count/group mapping: " + job.FullName); continue; }
                    var legacy = Jobs.JobsEarlyInitCodeGenerator.GetJobEntInfo(job, generator);
                    var equal = loops == legacy.brCount && counts.Select((count, index) => count == (legacy.count == null ? 0 : legacy.count[index])).All(x => x);
                    ++compared;
                    if (gaps != 0) ++incomplete;
                    if (equal) ++matched;
                    if (!equal || gaps != 0) {
                        report.AppendLine($"{job.FullName}: {(equal ? "counts match" : "COUNTS DIFFER")}, analysis gaps={gaps}, loops source/legacy={loops}/{legacy.brCount}");
                        for (var i = 0; i < counts.Length; ++i) {
                            var oldCount = legacy.count == null ? 0 : legacy.count[i];
                            if (counts[i] != oldCount) report.AppendLine($"  group {i}: source={counts[i]}, legacy={oldCount}");
                        }
                        foreach (var gap in summary.Skip(3).Where(s => s.StartsWith("G\t", StringComparison.Ordinal))) report.AppendLine("  " + gap.Substring(2));
                    }
                }
                report.AppendLine($"Entity counts: compared={compared}, equal={matched}, different={compared - matched}, incomplete={incomplete}, unavailable={unavailable}");
                report.AppendLine($"Weights: compared={weightsCompared}, equal={weightsMatched}, different={weightsCompared - weightsMatched}, incomplete={weightsIncomplete}, unavailable={weightsUnavailable}");
                var selection = $"Weight consumer: generated initializer={generatedWeightInitializers}, source value={sourceWeightValues}, legacy fallback={legacyWeightFallbacks} (availability only; methods NOT invoked)";
                report.AppendLine(selection);
                report.AppendLine("Read-only: no cache files, registrations or jobs executed. Matching incomplete summaries do NOT establish coverage. Counts remain legacy; regenerated initialization uses source weights only for unambiguous zero-gap summaries. This comparison always computes independent legacy weights.");
                SourceGeneratorReport.Publish("EntityCountsAndWeights",
                    $"Entity counts: compared={compared}, equal={matched}, different={compared - matched}, incomplete={incomplete}, unavailable={unavailable}\n" +
                    $"Weights: compared={weightsCompared}, equal={weightsMatched}, different={weightsCompared - weightsMatched}, incomplete={weightsIncomplete}, unavailable={weightsUnavailable}\n" + selection, report.ToString());
            } catch (Exception exception) {
                UnityEngine.Debug.LogException(exception);
            }
        }
    }
}
