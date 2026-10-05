namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    // An explicit migration diagnostic, never an export/compilation prerequisite.
    internal static class SourceGeneratorILSynchronizationValidation {
        internal sealed class Report {
            public string totals, details;
        }

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare IL Synchronization")]
        private static void Compare() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation.");
                return;
            }
            var systems = UnityEditor.TypeCache.GetTypesDerivedFrom<ISystem>()
                .Where(type => type.IsValueType && type.IsVisible).ToList();
            CodeGenerator.PatchSystemsList(systems);
            try {
                var report = BuildReport(systems.Where(type => !type.ContainsGenericParameters)
                    .SelectMany(type => SourceGeneratorScheduledJobsValidation.GetLifecycleMethods(type)),
                    (root, index, total) => UnityEditor.EditorUtility.DisplayCancelableProgressBar("IL synchronization coverage",
                        root.DeclaringType + "." + root.Name, total == 0 ? 1f : (float)index / total));
                SourceGeneratorReport.Publish("ILSynchronization", report.totals, report.details);
            } finally {
                UnityEditor.EditorUtility.ClearProgressBar();
            }
        }

        internal static Report BuildReport(IEnumerable<MethodInfo> methods, Func<MethodInfo, int, int, bool> cancel = null) {
            var roots = methods.Distinct().OrderBy(root => root.DeclaringType.FullName, StringComparer.Ordinal)
                .ThenBy(root => root.DeclaringType.Assembly.FullName, StringComparer.Ordinal)
                .ThenBy(root => root.ToString(), StringComparer.Ordinal).ToArray();
            var source = new SourceGeneratorSystemDependencies();
            var gaps = new Dictionary<string, int>(StringComparer.Ordinal);
            var details = new StringBuilder();
            var visited = 0; var proven = 0; var unproven = 0; var incomplete = 0;
            var sourceComplete = 0; var compared = 0; var differences = 0; var ilOnlyProofs = 0;
            foreach (var root in roots) {
                if (cancel != null && cancel(root, visited, roots.Length)) break;
                var il = ILSynchronization.Analyze(root);
                var available = source.TryReadSynchronization(root, out var errors, out var reason);
                var expected = available ? errors.Count == 0 ? "proven" : "unproven" : "incomplete";
                ++visited;
                if (il.status == "proven") ++proven;
                else if (il.status == "unproven") ++unproven;
                else ++incomplete;
                if (available) ++sourceComplete;
                if (available && il.status != "incomplete") {
                    ++compared;
                    if (il.status != expected) ++differences;
                    if (il.status == "proven" && expected == "unproven") ++ilOnlyProofs;
                }
                details.AppendLine(root.DeclaringType.AssemblyQualifiedName + " :: " + root);
                details.AppendLine("  IL=" + il.status + "; source=" + expected + "; access sites=" + il.accesses + "; completion sites=" + il.completions);
                if (!available) details.AppendLine("  Source unavailable: " + reason);
                foreach (var gap in il.gaps) details.AppendLine("  Gap: " + gap);
                foreach (var site in il.unsafeSites) details.AppendLine("  Unproven access: " + site);
                foreach (var category in il.gaps.Select(gap => gap.Split(':')[0]).Distinct(StringComparer.Ordinal)) {
                    gaps.TryGetValue(category, out var count);
                    gaps[category] = count + 1;
                }
            }
            var totals = new StringBuilder("IL synchronization vs compiler summaries (diagnostic only)\n");
            totals.AppendLine("Roots: analyzed=" + visited + "; selected=" + roots.Length + "; cancelled=" + (visited != roots.Length));
            totals.AppendLine("IL: proven=" + proven + "; unproven=" + unproven + "; incomplete=" + incomplete);
            totals.AppendLine("Source: complete=" + sourceComplete + "; incomplete/unavailable=" + (visited - sourceComplete));
            totals.AppendLine("Both complete: compared=" + compared + "; different=" + differences + "; IL proven/source unproven=" + ilOnlyProofs);
            totals.AppendLine("Incomplete means no proof. Differences require semantic review; neither analyzer is an unquestionable oracle.");
            totals.AppendLine("Proof scope: ordering of modeled direct accesses, NOT dependency publication or job-to-job conflict validation.");
            totals.AppendLine("Production synchronization selection was NOT changed. Lifecycle/job/constructor bodies were NOT invoked.");
            foreach (var gap in gaps.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal))
                totals.AppendLine("Gap roots: " + gap.Key + "=" + gap.Value);
            return new Report { totals = totals.ToString(), details = details.ToString() };
        }
    }
}
