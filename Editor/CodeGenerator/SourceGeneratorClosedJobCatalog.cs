namespace ME.BECS.Editor {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    // Run-local: no stale metadata across reloads, no reflection invocation of job code.
    internal sealed class SourceGeneratorClosedJobCatalog {
        private readonly Dictionary<string, string[]> rows = new Dictionary<string, string[]>(StringComparer.Ordinal);

        internal SourceGeneratorClosedJobCatalog() {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)) {
                foreach (AssemblyMetadataAttribute attribute in assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)) {
                    if (attribute.Key != "ME.BECS.JobEntityCounts.v1" && attribute.Key != "ME.BECS.JobSafety.v1" && attribute.Key != "ME.BECS.JobWeights.v1") continue;
                    var payload = attribute.Value?.Split('\n');
                    if (payload == null || payload.Length < 3) continue;
                    var key = attribute.Key + "\n" + payload[0];
                    // Never pick a winner based on assembly load order.
                    if (this.rows.TryGetValue(key, out var previous)) {
                        if (previous == null || !previous.SequenceEqual(payload)) this.rows[key] = null;
                    } else this.rows.Add(key, payload);
                }
            }
        }

        internal bool TryGet(Type job, string kind, out string[] summary) =>
            this.rows.TryGetValue("ME.BECS." + kind + ".v1\n" + job.AssemblyQualifiedName, out summary) && summary != null;
    }
}
