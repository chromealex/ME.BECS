namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;

    internal sealed class SourceGeneratorJobSizes {
        private readonly SourceGeneratorClosedJobCatalog closed = new SourceGeneratorClosedJobCatalog();
        // One export pass only: never persist selection across recompilation or domain reload.
        private readonly Dictionary<Assembly, Dictionary<string, string[]>> catalogs = new Dictionary<Assembly, Dictionary<string, string[]>>();

        internal bool TrySelect(Type job, Type[] legacyComponents, out string initializer) {
            initializer = null;
            string[] rows;
            if (job.IsGenericType) {
                if (!this.closed.TryGet(job, "JobSafety", out rows)) return false;
            } else {
                if (!this.catalogs.TryGetValue(job.Assembly, out var catalog)) {
                    catalog = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (AssemblyMetadataAttribute attribute in job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)) {
                        if (attribute.Key != "ME.BECS.JobSafety.v1" || attribute.Value == null) continue;
                        var summary = attribute.Value.Split('\n');
                        if (summary.Length < 3 || summary[0].Length == 0) continue;
                        // Keep ambiguity explicit; never select whichever record came first.
                        if (catalog.ContainsKey(summary[0])) catalog[summary[0]] = null;
                        else catalog.Add(summary[0], summary);
                    }
                    this.catalogs.Add(job.Assembly, catalog);
                }
                if (!catalog.TryGetValue(job.FullName, out rows) || rows == null) return false;
            }
            var report = new StringBuilder();
            var status = SourceGeneratorSafetyValidation.ValidateSizeInitializer(job, rows, legacyComponents, report, out initializer);
            if (status == 0) throw new InvalidOperationException("[ ME.BECS ] Source maxStructSize differs from legacy. " +
                "Run Compare Job Safety before switching this job.\n" + report);
            return status == 1;
        }
    }
}
