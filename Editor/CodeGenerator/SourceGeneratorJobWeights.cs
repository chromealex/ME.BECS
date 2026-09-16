namespace ME.BECS.Editor {

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;

    // Instance lifetime is one generator run. Do not keep assembly metadata across reloads.
    internal sealed class SourceGeneratorJobWeights {
        private SourceGeneratorClosedJobCatalog closed;
        private readonly Dictionary<Type, string> initializers = new Dictionary<Type, string>();

        internal bool TryGetInitializer(Type job, out string call) {
            call = null;
            return this.TryGetComplete(job, out _) && this.initializers.TryGetValue(job, out call);
        }
        private readonly Dictionary<Assembly, Dictionary<string, string[]>> ordinary = new Dictionary<Assembly, Dictionary<string, string[]>>();

        internal bool TryGetComplete(Type job, out uint weight) {
            weight = 0;
            string[] rows = null;
            if (job.ContainsGenericParameters) return false;
            if (job.IsGenericType) {
                this.closed ??= new SourceGeneratorClosedJobCatalog();
                if (!this.closed.TryGet(job, "JobWeights", out rows)) return false;
            } else {
                if (!this.ordinary.TryGetValue(job.Assembly, out var catalog)) {
                    catalog = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (AssemblyMetadataAttribute attribute in job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)) {
                        if (attribute.Key != "ME.BECS.JobWeights.v1") continue;
                        var candidate = attribute.Value?.Split('\n');
                        if (candidate == null || candidate.Length < 4 || string.IsNullOrEmpty(candidate[0])) continue;
                        // Preserve ambiguity rather than allowing enumeration order to select a root.
                        if (catalog.ContainsKey(candidate[0])) catalog[candidate[0]] = null;
                        else catalog.Add(candidate[0], candidate);
                    }
                    this.ordinary.Add(job.Assembly, catalog);
                }
                if (!catalog.TryGetValue(job.FullName, out rows)) return false;
            }
            if (rows == null || rows.Length < 4 || !rows[1].StartsWith("M:", StringComparison.Ordinal) ||
                !uint.TryParse(rows[2], NumberStyles.None, CultureInfo.InvariantCulture, out var gaps) || gaps != 0) return false;
            foreach (var row in rows) if (row.StartsWith("G\t", StringComparison.Ordinal)) return false;
            if (!uint.TryParse(rows[3], NumberStyles.None, CultureInfo.InvariantCulture, out weight)) return false;
            string initializer = null;
            foreach (var row in rows) {
                if (!row.StartsWith("I\t", StringComparison.Ordinal)) continue;
                if (initializer != null) return false;
                var fields = row.Split('\t');
                if (fields.Length != 4 || fields[3] != "Apply") return false;
                var expectedType = "ME.BECS.SourceGenerated.JobWeight_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(fields[1] + "\n" + rows[0] + "\n" + rows[1]);
                if (fields[2] != expectedType) return false;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    if (assembly.IsDynamic || assembly.FullName != fields[1]) continue;
                    var method = assembly.GetType(fields[2], false)?.GetMethod(fields[3], BindingFlags.Public | BindingFlags.Static);
                    if (method == null || !method.IsGenericMethodDefinition || method.GetGenericArguments().Length != 1 ||
                        method.GetParameters().Length != 0 || method.ReturnType != typeof(void) || !method.DeclaringType.IsVisible) return false;
                    try { method.MakeGenericMethod(job); }
                    catch (ArgumentException) { return false; }
                    initializer = "global::" + fields[2] + ".Apply<" + EditorUtils.GetTypeName(job) + ">();";
                    break;
                }
                if (initializer == null) return false;
            }
            if (initializer != null) this.initializers[job] = initializer;
            return true;
        }
    }
}
