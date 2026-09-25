namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using TypeInfo = Jobs.JobsEarlyInitCodeGenerator.TypeInfo;

    // One export pass only. Callers mutate sets while combining system dependencies.
    internal sealed class SourceGeneratorJobSafety {
        private readonly Dictionary<Type, HashSet<TypeInfo>> selected = new Dictionary<Type, HashSet<TypeInfo>>();
        private readonly Dictionary<Assembly, Dictionary<string, string[]>> catalogs = new Dictionary<Assembly, Dictionary<string, string[]>>();
        private SourceGeneratorClosedJobCatalog closed;
        private readonly object selectionLock = new object();

        internal HashSet<TypeInfo> Select(Type job) {
            // SystemDependenciesCodeGenerator shares this run-local consumer across workers.
            // Protect the complete lazy-selection transaction, including the closed catalog.
            lock (this.selectionLock) return this.SelectLocked(job);
        }

        private HashSet<TypeInfo> SelectLocked(Type job) {
            if (this.selected.TryGetValue(job, out var cached)) return new HashSet<TypeInfo>(cached);
            var legacy = Jobs.JobsEarlyInitCodeGenerator.GetJobTypesInfo(job);
            var result = legacy;
            if (this.TryRead(job, out var rows)) {
                var status = Validate(job, rows, legacy, out var source);
                if (status == 0) throw Difference(job);
                if (status == 1) result = source;
            }
            this.selected.Add(job, new HashSet<TypeInfo>(result));
            return new HashSet<TypeInfo>(result);
        }

        // Shared by production and the read-only report: -1 unavailable, 0 differs, 1 selected.
        internal static int Validate(Type job, string[] rows, HashSet<TypeInfo> legacy, out HashSet<TypeInfo> source) {
            source = null;
            if (rows == null || rows.Length < 3 || rows[0] != (job.IsGenericType ? job.AssemblyQualifiedName : job.FullName) ||
                !rows[1].StartsWith("M:", StringComparison.Ordinal) || rows[2] != "0" || !TryGetTypes(rows, out var sourceTypes)) return -1;
            var normalized = new HashSet<TypeInfo>(legacy);
            Jobs.JobsEarlyInitCodeGenerator.UpdateDeps(normalized);
            var identities = sourceTypes
                .GroupBy(Identity).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var expected = new HashSet<string>(normalized.Select(Record), StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            source = new HashSet<TypeInfo>();
            var valid = true;
            var sizeSeen = false;
            foreach (var row in rows.Skip(3)) {
                var fields = row.Split('\t');
                if (fields[0] == "A") continue; // Fully validated by TryGetTypes.
                if (fields[0] == "S") {
                    // Size initializer is consumed/validated independently.
                    if (sizeSeen || fields.Length != 5 || fields[3] != "Apply" || fields[4] != "v1") valid = false;
                    sizeSeen = true;
                    continue;
                }
                if (fields.Length != 5 || fields[0] != "D" ||
                    !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var mode) ||
                    mode < 0 || mode > 2 || fields[3] != mode.ToString(CultureInfo.InvariantCulture) ||
                    (fields[4] != "0" && fields[4] != "1") || !actual.Add(row.Substring(2))) {
                    valid = false;
                    break;
                }
                if (!identities.TryGetValue(fields[1] + "\t" + fields[2], out var types) || types.Length != 1)
                    return -1;
                if (!source.Add(new TypeInfo { type = types[0], op = (RefOp)mode, isArg = fields[4] == "1" })) {
                    valid = false;
                    break;
                }
            }
            return !valid ? -1 : expected.SetEquals(actual) ? 1 : 0;
        }

        private static InvalidOperationException Difference(Type job) => new InvalidOperationException(
            "Complete source safety dependencies differ from legacy for " + job.AssemblyQualifiedName +
            ". Run Compare Job Safety before changing dependency scheduling.");

        private static string Identity(Type type) => type.Assembly.FullName + "\tT:" + type.FullName.Replace('+', '.');
        private static string Record(TypeInfo item) => Identity(item.type) + "\t" +
            ((int)item.op).ToString(CultureInfo.InvariantCulture) + "\t" + (item.isArg ? "1" : "0");

        // Only calls the generated typeof catalog, never a job or registration initializer.
        internal static bool TryGetTypes(string[] rows, out Type[] types) {
            types = null;
            if (rows == null || rows.Length < 3 || rows[2] != "0") return false;
            var records = rows.Skip(3).Where(row => row.StartsWith("A\t", StringComparison.Ordinal)).ToArray();
            if (records.Length != 1) return false;
            var fields = records[0].Split('\t');
            if (fields.Length != 5 || fields[3] != "GetTypes" || fields[4] != "v1") return false;
            var expected = "ME.BECS.SourceGenerated.JobSafetyTypes_" +
                ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(fields[1] + "\n" + rows[0] + "\n" + rows[1]);
            if (fields[2] != expected) return false;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.FullName == fields[1]).ToArray();
            if (assemblies.Length != 1) return false;
            var owner = assemblies[0].GetType(expected, false);
            if (owner == null || !owner.IsVisible) return false;
            var methods = owner.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.Name == "GetTypes").ToArray();
            if (methods.Length != 1 || methods[0].ContainsGenericParameters || methods[0].ReturnType != typeof(Type[]) ||
                methods[0].GetParameters().Length != 0) return false;
            try { types = (Type[])methods[0].Invoke(null, null); }
            catch (TargetInvocationException) { return false; }
            if (types == null || types.Any(type => type == null || !type.IsValueType || type.ContainsGenericParameters ||
                !typeof(IComponentBase).IsAssignableFrom(type)) || types.Distinct().Count() != types.Length) return false;
            var dependencies = rows.Skip(3).Where(row => row.StartsWith("D\t", StringComparison.Ordinal)).Select(row => row.Split('\t')).ToArray();
            if (dependencies.Length != types.Length) return false;
            for (var index = 0; index < types.Length; ++index)
                if (dependencies[index].Length != 5 || Identity(types[index]) != dependencies[index][1] + "\t" + dependencies[index][2]) return false;
            return true;
        }

        private bool TryRead(Type job, out string[] rows) {
            rows = null;
            if (job.ContainsGenericParameters) return false;
            if (job.IsGenericType) {
                this.closed ??= new SourceGeneratorClosedJobCatalog();
                if (!this.closed.TryGet(job, "JobSafety", out rows)) return false;
            } else {
                if (!this.catalogs.TryGetValue(job.Assembly, out var catalog)) {
                    catalog = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (AssemblyMetadataAttribute attribute in job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)) {
                        if (attribute.Key != "ME.BECS.JobSafety.v1") continue;
                        var entry = attribute.Value?.Split('\n');
                        if (entry == null || entry.Length < 3 || string.IsNullOrEmpty(entry[0])) continue;
                        if (catalog.ContainsKey(entry[0])) catalog[entry[0]] = null;
                        else catalog.Add(entry[0], entry);
                    }
                    this.catalogs.Add(job.Assembly, catalog);
                }
                if (!catalog.TryGetValue(job.FullName, out rows)) return false;
            }
            return rows != null && rows.Length >= 3 && rows[0] == (job.IsGenericType ? job.AssemblyQualifiedName : job.FullName) &&
                rows[1].StartsWith("M:", StringComparison.Ordinal) && rows[2] == "0" &&
                !rows.Skip(3).Any(row => row.StartsWith("G\t", StringComparison.Ordinal));
        }
    }
}
