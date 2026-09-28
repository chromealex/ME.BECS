namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using TypeInfo = Jobs.JobsEarlyInitCodeGenerator.TypeInfo;

    // Run-local read-only comparison. Does not invoke view callbacks or registration methods.
    public sealed class SourceGeneratorViewSafety {
        private readonly Dictionary<Assembly, Dictionary<string, List<string[]>>> catalogs = new Dictionary<Assembly, Dictionary<string, List<string[]>>>();

        public int Compare(Type owner, string phase, HashSet<TypeInfo> legacy, out string detail) {
            if (!this.catalogs.TryGetValue(owner.Assembly, out var catalog)) {
                catalog = new Dictionary<string, List<string[]>>(StringComparer.Ordinal);
                foreach (var attribute in owner.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()) {
                    if (attribute.Key != "ME.BECS.ViewSafety.v1" || attribute.Value == null) continue;
                    var rows = attribute.Value.Split('\n');
                    if (rows.Length < 4) continue;
                    var key = rows[0] + "\n" + rows[1];
                    if (!catalog.TryGetValue(key, out var entries)) catalog.Add(key, entries = new List<string[]>());
                    entries.Add(rows.Skip(1).ToArray());
                }
                this.catalogs.Add(owner.Assembly, catalog);
            }
            if (!catalog.TryGetValue(phase + "\n" + owner.FullName, out var candidates) || candidates.Count != 1) {
                detail = "source callback catalog absent or ambiguous";
                return -1;
            }
            var summary = candidates[0];
            if (summary[2] != "0") {
                detail = "gaps=" + summary[2] + "; " + string.Join("; ", summary.Skip(3).Where(row => row.StartsWith("G\t", StringComparison.Ordinal)).Take(4));
                return -1;
            }
            var status = SourceGeneratorJobSafety.Validate(owner, summary, legacy, out _);
            detail = status == 1 ? "complete: dependencies equal" : status == 0 ? "complete: dependencies differ" : "complete metadata but typed catalog unavailable/invalid";
            if (status == 0) {
                var normalized = new HashSet<TypeInfo>(legacy);
                Jobs.JobsEarlyInitCodeGenerator.UpdateDeps(normalized);
                detail += "\n    source: " + string.Join("; ", summary.Skip(3).Where(row => row.StartsWith("D\t", StringComparison.Ordinal))) +
                    "\n    legacy: " + string.Join("; ", normalized.OrderBy(item => item.type.AssemblyQualifiedName, StringComparer.Ordinal)
                        .Select(item => item.type.AssemblyQualifiedName + " / " + item.op + " / argument=" + item.isArg));
            }
            return status;
        }
    }
}
