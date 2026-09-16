using System;
using System.Collections.Generic;
using System.Linq;

namespace ME.BECS.SourceGenerator;

internal static class JobSummaryDiagnostics {
    internal static IEnumerable<string> Describe(IEnumerable<string> gaps) {
        // Keep every category visible. A global Take(12) hid all useful missing-body/dispatch
        // diagnostics behind alphabetically earlier constructor diagnostics.
        foreach (var category in gaps.GroupBy(static g => g.Split(':')[0]).OrderBy(static g => g.Key, StringComparer.Ordinal)) {
            var ordered = category.OrderBy(static g => g, StringComparer.Ordinal).ToArray();
            yield return category.Key + " (" + ordered.Length + "): " + string.Join(" | ", ordered.Take(2));
        }
    }
}
