namespace ME.BECS.Editor {
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    internal static class SourceGeneratorDestroyCallbackReport {
        [UnityEditor.MenuItem("ME.BECS/Source Generator/Export Destroy Callback Analysis")]
        private static void Export() {
            if (!SourceAnalysisDiagnostics.BeginComparison()) return;
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before exporting callback analysis.");
                return;
            }
            var report = new StringBuilder();
            var catalogs = 0;
            var safety = 0;
            var counts = 0;
            var registryAudits = 0;
            var registryAccesses = 0;
            var registryCoverage = 0;
            var registrationEffects = 0;
            var errors = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(item => item.FullName, StringComparer.Ordinal)) {
                if (assembly.IsDynamic) continue;
                try {
                    var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                        .Cast<AssemblyMetadataAttribute>()
                        .Where(attribute => attribute.Key == "ME.BECS.DestroyRegistryAccess.v1" || attribute.Key == "ME.BECS.DestroyRegistryCoverage.v1" ||
                            attribute.Key == "ME.BECS.DestroyRegistryAudit.v1" || attribute.Key == "ME.BECS.DestroyRegistrationEffects.v1" || attribute.Key == "ME.BECS.DestroyCallbackTargets.v1" ||
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
                        else if (record.Key == "ME.BECS.DestroyCallbackCounts.v1") ++counts;
                        else if (record.Key == "ME.BECS.DestroyRegistryAudit.v1") ++registryAudits;
                        else if (record.Key == "ME.BECS.DestroyRegistryAccess.v1") ++registryAccesses;
                        else if (record.Key == "ME.BECS.DestroyRegistryCoverage.v1") ++registryCoverage;
                        else if (record.Key == "ME.BECS.DestroyRegistrationEffects.v1") ++registrationEffects;
                        report.AppendLine("assembly\t" + assembly.FullName).AppendLine("metadata\t" + record.Key);
                        if (record.Key == "ME.BECS.DestroyRegistrationEffects.v1") {
                            if (!AppendRegistrationEffects(report, record.Value)) ++errors;
                        } else report.AppendLine(record.Value);
                        report.AppendLine();
                    }
                } catch (Exception exception) {
                    ++errors;
                    report.AppendLine("Metadata unavailable: " + assembly.FullName + " — " + exception.Message);
                }
            }
            SourceGeneratorReport.Publish("DestroyCallbacks",
                $"Destroy callback metadata: catalogs={catalogs}, safety records={safety}, count records={counts}, read errors={errors}. " +
                $"Registry audit catalogs={registryAudits}, typed accesses={registryAccesses}, covered assemblies={registryCoverage}, registration effect records={registrationEffects}. " +
                "Raw metadata export, NOT a coverage/parity check. Zero records does not prove coverage. " +
                "Potential source registrations and analyzed target effects do not prove runtime registry closure. " +
                "Callbacks, registrations and jobs NOT invoked; runtime registry call sites remain unresolved.", report.ToString());
        }

        private static bool AppendRegistrationEffects(StringBuilder report, string payload) {
            var rows = payload.Split('\n');
            if (rows.Length < 4 || rows[0] != "v1" || (rows[2] != "analyzed" && rows[2] != "unavailable")) {
                report.AppendLine("Invalid registration effects metadata.").AppendLine(payload);
                return false;
            }
            try {
                report.AppendLine("binding\t" + rows[2]).AppendLine("binding gaps\t" + rows[3])
                    .AppendLine("potential registration:").AppendLine(Encoding.UTF8.GetString(Convert.FromBase64String(rows[1])));
                foreach (var row in rows.Skip(4)) {
                    if (row.StartsWith("S\t", StringComparison.Ordinal) || row.StartsWith("C\t", StringComparison.Ordinal) || row.StartsWith("W\t", StringComparison.Ordinal)) {
                        report.AppendLine(row[0] == 'S' ? "target safety:" : row[0] == 'C' ? "target entity counts:" : "target weights:")
                            .AppendLine(Encoding.UTF8.GetString(Convert.FromBase64String(row.Substring(2))));
                    } else report.AppendLine(row);
                }
                return true;
            } catch (FormatException) {
                report.AppendLine("Invalid registration effects encoding.").AppendLine(payload);
                return false;
            }
        }
    }
}
