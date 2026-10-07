namespace ME.BECS.Editor {

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    internal static class SourceGeneratorGraphValidation {

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Inspect Semantic Job Graphs")]
        private static void Inspect() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before inspecting job graphs.");
                return;
            }
            UnityEngine.Debug.Log("[ME.BECS] " + Describe(true));
        }

        internal static string Describe(bool details) {
            var roots = 0;
            var withGaps = 0;
            var missing = 0;
            var unresolved = 0;
            var malformed = 0;
            var safetyRoots = 0;
            var safetyWithGaps = 0;
            var safetyDependencies = 0;
            var content = new StringBuilder();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName, StringComparer.Ordinal)) {
                if (assembly.IsDynamic) continue;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var safetySeen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var attr in assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), true)) {
                    var attribute = (System.Reflection.AssemblyMetadataAttribute)attr;
                    if (attribute.Key == "ME.BECS.JobSafety.v1") {
                        var safetyRows = attribute.Value?.Split('\n');
                        if (safetyRows == null || safetyRows.Length < 3 || !safetyRows[1].StartsWith("M:", StringComparison.Ordinal) ||
                            !int.TryParse(safetyRows[2], NumberStyles.None, CultureInfo.InvariantCulture, out var safetyGapCount) || !safetySeen.Add(safetyRows[0] + "\n" + safetyRows[1])) {
                            ++malformed;
                            continue;
                        }
                        ++safetyRoots;
                        if (safetyGapCount != 0) ++safetyWithGaps;
                        safetyDependencies += safetyRows.Count(s => s.StartsWith("D\t", StringComparison.Ordinal));
                        continue;
                    }
                    if (attribute.Key != "ME.BECS.JobGraph.v2") continue;

                    var rows = attribute.Value?.Split('\n');
                    if (rows == null || rows.Length < 5 || !rows[0].StartsWith("M:", StringComparison.Ordinal) ||
                        !int.TryParse(rows[1], NumberStyles.None, CultureInfo.InvariantCulture, out var reachable) ||
                        !int.TryParse(rows[2], NumberStyles.None, CultureInfo.InvariantCulture, out var absent) ||
                        !int.TryParse(rows[3], NumberStyles.None, CultureInfo.InvariantCulture, out var gaps) ||
                        reachable < 1 || absent > reachable || !seen.Add(rows[0])) {
                        ++malformed;
                        if (details) content.AppendLine("Malformed/duplicate graph metadata: " + assembly.FullName);
                        continue;
                    }
                    ++roots;
                    if (absent != 0 || gaps != 0) ++withGaps;
                    missing += absent;
                    unresolved += gaps;
                    if (!details) continue;
                    content.AppendLine($"{rows[0]} [{assembly.GetName().Name}]: method instances={reachable}, missing={absent}, unresolved={gaps}");
                    for (var i = 4; i < rows.Length; ++i) if (rows[i].Length != 0) content.AppendLine("  " + rows[i]);
                }
            }
            return $"Semantic job graphs: roots={roots}, roots with gaps={withGaps}, missing={missing}, unresolved={unresolved}, malformed={malformed}\n" +
                   $"Source safety summaries: roots={safetyRoots}, roots with gaps={safetyWithGaps}, dependency records={safetyDependencies} (metadata availability, NOT IL parity)\n" +
                   "Instantiated reachability only; counts are summed per root. NOT entity counts or proof of safety coverage. IL analysis remains active.\n" + content;
        }
    }
}
