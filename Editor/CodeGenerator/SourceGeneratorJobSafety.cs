namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using TypeInfo = Jobs.JobsEarlyInitCodeGenerator.TypeInfo;

    // One export pass only. Callers mutate sets while combining system dependencies.
    internal sealed class SourceGeneratorJobSafety {
        internal enum SourceStatus { Missing, Incomplete, Complete, Invalid }

        private readonly Dictionary<Type, (HashSet<TypeInfo> dependencies, bool source)> selected = new Dictionary<Type, (HashSet<TypeInfo>, bool)>();
        private readonly Dictionary<Type, HashSet<TypeInfo>> exportedIL = new Dictionary<Type, HashSet<TypeInfo>>();
        private readonly Dictionary<Assembly, Dictionary<string, string[]>> catalogs = new Dictionary<Assembly, Dictionary<string, string[]>>();
        private readonly Func<Type, HashSet<TypeInfo>> legacy;
        private SourceGeneratorClosedJobCatalog closed;
        private readonly object selectionLock = new object();

        internal SourceGeneratorJobSafety() : this(job => Jobs.JobsEarlyInitCodeGenerator.GetJobTypesInfo(job)) { }

        internal SourceGeneratorJobSafety(Func<Type, HashSet<TypeInfo>> legacy) {
            this.legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        }

        internal HashSet<TypeInfo> Select(Type job) {
            // Source-first comparison/testing path only. Production callers use
            // SelectForExport and do not consult these catalogs.
            // Protect the complete lazy-selection transaction, including the closed
            // catalog, if a run-local consumer is shared by concurrent callers.
            lock (this.selectionLock) return this.SelectLocked(job, out _);
        }

        internal HashSet<TypeInfo> SelectForExport(Type job, out bool sourceSelected) {
            // Production uses the freshly compiled IL. Source summaries are an
            // independent diagnostic oracle, not a gate or fallback for export.
            lock (this.selectionLock) {
                sourceSelected = false;
                if (!this.exportedIL.TryGetValue(job, out var dependencies)) {
                    var analyzed = this.legacy(job) ?? throw new InvalidOperationException("IL safety analysis returned no result for " + job);
                    dependencies = new HashSet<TypeInfo>(analyzed);
                    Jobs.JobsEarlyInitCodeGenerator.UpdateDeps(dependencies);
                    this.exportedIL.Add(job, dependencies);
                }
                return new HashSet<TypeInfo>(dependencies);
            }
        }

        private HashSet<TypeInfo> SelectLocked(Type job, out bool sourceSelected) {
            if (this.selected.TryGetValue(job, out var cached)) {
                sourceSelected = cached.source;
                return new HashSet<TypeInfo>(cached.dependencies);
            }
            var status = this.ReadSourceLocked(job, out _, out var source, out var reason);
            if (status == SourceStatus.Invalid) throw new InvalidOperationException("Invalid source safety catalog for " + job.AssemblyQualifiedName +
                ": " + reason + ". Recompile its source catalog and export Compare Job Safety; refusing silent fallback for corrupt metadata.");
            // Retain the source-first oracle for migration/regression diagnostics.
            // It is deliberately isolated from the production IL export cache.
            sourceSelected = status == SourceStatus.Complete;
            var result = sourceSelected ? source : this.legacy(job);
            this.selected.Add(job, (new HashSet<TypeInfo>(result), sourceSelected));
            return new HashSet<TypeInfo>(result);
        }

        internal SourceStatus ReadSource(Type job, out string[] rows, out HashSet<TypeInfo> source, out string reason) {
            lock (this.selectionLock) {
                var status = this.ReadSourceLocked(job, out rows, out source, out reason);
                if (rows != null) rows = (string[])rows.Clone();
                return status;
            }
        }

        private SourceStatus ReadSourceLocked(Type job, out string[] rows, out HashSet<TypeInfo> source, out string reason) {
            rows = null;
            source = null;
            try {
                var status = this.ReadRows(job, out rows, out reason);
                if (status != SourceStatus.Complete) return status;
                if (TryParse(job, rows, out source)) return SourceStatus.Complete;
                reason = "Complete catalog has invalid typed dependencies, identity or records";
            } catch (Exception exception) {
                source = null;
                reason = "Source safety catalog inspection failed: " + exception.GetBaseException().Message;
            }
            return SourceStatus.Invalid;
        }

        // Parity oracle for explicit job/view reports only. Production
        // selection does not call this: -1 unavailable, 0 differs, 1 normalized parity.
        internal static int Validate(Type job, string[] rows, HashSet<TypeInfo> legacy, out HashSet<TypeInfo> source) {
            if (!TryParse(job, rows, out source)) return -1;
            var normalized = new HashSet<TypeInfo>(legacy);
            Jobs.JobsEarlyInitCodeGenerator.UpdateDeps(normalized);
            var expected = new HashSet<string>(normalized.Select(Record), StringComparer.Ordinal);
            return expected.SetEquals(source.Select(Record)) ? 1 : 0;
        }

        // Compiler catalog validation without running IL analysis or mutating runtime state.
        internal static bool TryParse(Type job, string[] rows, out HashSet<TypeInfo> source) {
            source = null;
            if (job == null || job.ContainsGenericParameters || rows == null || rows.Length < 3 || rows.Any(row => row == null) || rows[0] != (job.IsGenericType ? job.AssemblyQualifiedName : job.FullName) ||
                !rows[1].StartsWith("M:", StringComparison.Ordinal) || rows[2] != "0" || !TryGetTypes(rows, out var sourceTypes)) return false;
            var identities = sourceTypes
                .GroupBy(Identity).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
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
                if (!identities.TryGetValue(fields[1] + "\t" + fields[2], out var types) || types.Length != 1) {
                    source = null;
                    return false;
                }
                if (!source.Add(new TypeInfo { type = types[0], op = (RefOp)mode, isArg = fields[4] == "1" })) {
                    valid = false;
                    break;
                }
            }
            if (!valid) source = null;
            return valid;
        }

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

        private SourceStatus ReadRows(Type job, out string[] rows, out string reason) {
            rows = null;
            reason = null;
            if (job == null || !job.IsValueType || job.ContainsGenericParameters) {
                reason = "Safety selection requires a closed job value type";
                return SourceStatus.Invalid;
            }
            if (job.IsGenericType) {
                this.closed ??= new SourceGeneratorClosedJobCatalog();
                if (!this.closed.TryGet(job, "JobSafety", out rows)) {
                    var ambiguous = this.closed.IsAmbiguous(job, "JobSafety");
                    reason = ambiguous ? "Conflicting closed-job safety catalogs" : "Missing closed-job safety catalog";
                    return ambiguous ? SourceStatus.Invalid : SourceStatus.Missing;
                }
            } else {
                if (!this.catalogs.TryGetValue(job.Assembly, out var catalog)) {
                    catalog = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (AssemblyMetadataAttribute attribute in job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)) {
                        if (attribute.Key != "ME.BECS.JobSafety.v1") continue;
                        var entry = attribute.Value?.Split('\n');
                        if (entry == null || entry.Length == 0 || string.IsNullOrEmpty(entry[0])) continue;
                        if (catalog.ContainsKey(entry[0])) catalog[entry[0]] = null;
                        else catalog.Add(entry[0], entry);
                    }
                    this.catalogs.Add(job.Assembly, catalog);
                }
                if (!catalog.TryGetValue(job.FullName, out rows)) {
                    reason = "Missing job safety catalog";
                    return SourceStatus.Missing;
                }
            }
            return ClassifyRows(job, rows, out reason);
        }

        internal static SourceStatus ClassifyRows(Type job, string[] rows, out string reason) {
            reason = "Malformed or duplicate source safety header";
            if (job == null || rows == null || rows.Length < 3 || rows.Any(row => row == null) || rows[0] != (job.IsGenericType ? job.AssemblyQualifiedName : job.FullName) ||
                !rows[1].StartsWith("M:", StringComparison.Ordinal) ||
                !uint.TryParse(rows[2], NumberStyles.None, CultureInfo.InvariantCulture, out var gaps) ||
                rows[2] != gaps.ToString(CultureInfo.InvariantCulture)) return SourceStatus.Invalid;
            var gapRows = rows.Skip(3).Where(row => row.StartsWith("G\t", StringComparison.Ordinal)).ToArray();
            if ((gaps == 0) != (gapRows.Length == 0) || gapRows.Any(row => row.Length == 2)) {
                reason = "Contradictory source safety coverage";
                return SourceStatus.Invalid;
            }
            if (gaps != 0) {
                reason = "Incomplete source safety coverage (gaps=" + gaps + "): " +
                    string.Join("; ", gapRows.Take(4).Select(row => row.Substring(2)));
                return SourceStatus.Incomplete;
            }
            reason = null;
            return SourceStatus.Complete;
        }
    }
}
