namespace ME.BECS.Editor {
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    internal static class SourceGeneratorDestroyCallbackReport {
        [UnityEditor.MenuItem("ME.BECS/Source Generator/Export Destroy Callback Analysis")]
        private static void Export() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before exporting callback analysis.");
                return;
            }
            var report = new StringBuilder();
            var catalogs = 0;
            var safety = 0;
            var counts = 0;
            var errors = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(item => item.FullName, StringComparer.Ordinal)) {
                if (assembly.IsDynamic) continue;
                try {
                    var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                        .Cast<AssemblyMetadataAttribute>()
                        .Where(attribute => attribute.Key == "ME.BECS.DestroyCallbackTargets.v1" ||
                            attribute.Key == "ME.BECS.DestroyCallbackSafety.v1" || attribute.Key == "ME.BECS.DestroyCallbackCounts.v1")
                        .OrderBy(attribute => attribute.Key, StringComparer.Ordinal)
                        .ThenBy(attribute => attribute.Value, StringComparer.Ordinal);
                    foreach (var record in records) {
                        if (string.IsNullOrEmpty(record.Value)) {
                            ++errors;
                            report.AppendLine("Empty metadata: " + assembly.FullName + " :: " + record.Key);
                            continue;
                        }
                        if (record.Key == "ME.BECS.DestroyCallbackTargets.v1") ++catalogs;
                        else if (record.Key == "ME.BECS.DestroyCallbackSafety.v1") ++safety;
                        else ++counts;
                        report.AppendLine("assembly\t" + assembly.FullName)
                            .AppendLine("metadata\t" + record.Key).AppendLine(record.Value).AppendLine();
                    }
                } catch (Exception exception) {
                    ++errors;
                    report.AppendLine("Metadata unavailable: " + assembly.FullName + " — " + exception.Message);
                }
            }
            SourceGeneratorReport.Publish("DestroyCallbacks",
                $"Destroy callback metadata: catalogs={catalogs}, safety records={safety}, count records={counts}, read errors={errors}. " +
                "Raw metadata export, NOT a coverage/parity check. Zero records does not prove coverage. " +
                "Callbacks, registrations and jobs NOT invoked; runtime registry call sites remain unresolved.", report.ToString());
        }
    }
}
