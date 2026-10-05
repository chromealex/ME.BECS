namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    // Source-count oracle for explicit comparison reports only. Production
    // reservations are selected from compiled IL by JobsEarlyInitCodeGenerator.
    internal static class SourceGeneratorJobEntityCounts {
        private sealed class InitializerPlan {
            internal Type initializer;
            internal string[] groupKeys;
        }

        internal static bool MatchesLegacy(string[] validatedRows, IReadOnlyDictionary<string, uint> groups, uint groupCount,
            Jobs.JobsEarlyInitCodeGenerator.NewEntInfo legacy) {
            if (legacy.loopGroups == null || legacy.loopGroups.Length != groupCount ||
                (legacy.count != null && legacy.count.Length != groupCount)) return false;
            var counts = new uint[groupCount];
            var loopGroups = new bool[groupCount];
            ulong loops = 0;
            foreach (var row in validatedRows.Skip(3)) {
                if (!row.StartsWith("C\t", StringComparison.Ordinal)) continue;
                var fields = row.Split('\t');
                var group = groups[fields[1] + "\t" + fields[2]];
                counts[group] = uint.Parse(fields[3], CultureInfo.InvariantCulture);
                var loop = uint.Parse(fields[4], CultureInfo.InvariantCulture);
                loopGroups[group] = loop > 0u;
                loops += loop;
            }
            if (legacy.brCount < 0 || loops != (ulong)legacy.brCount) return false;
            for (var group = 0; group < counts.Length; ++group)
                if (counts[group] != (legacy.count == null ? 0 : legacy.count[group]) || loopGroups[group] != legacy.loopGroups[group]) return false;
            return true;
        }

        // Validates metadata and closes the signature only. Never invokes initialization.
        internal static bool TryGetInitializer(Type job, string[] rows, IReadOnlyDictionary<string, uint> groups,
            uint groupCount, out string call, out string reason) {
            call = null;
            if (!TryGetPlan(job, rows, groups, groupCount, out var plan, out reason)) return false;
            // Diagnostic compatibility only; production exports IL reservations.
            call = "global::" + plan.initializer.FullName + ".Apply<" + EditorUtils.GetTypeName(job) + ">(" +
                string.Join(", ", new[] { "global::ME.BECS.SourceGenerated.EntityInputs.GroupCount" }.Concat(plan.groupKeys.Select(key =>
                    "global::ME.BECS.SourceGenerated.EntityInputs.Id_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(key)))) + ");";
            return true;
        }

        private static bool TryGetPlan(Type job, string[] rows, IReadOnlyDictionary<string, uint> groups,
            uint groupCount, out InitializerPlan plan, out string reason) {
            plan = null;
            reason = "entity-count summary is incomplete or belongs to a different job";
            if (job.ContainsGenericParameters || !job.IsVisible || rows == null || rows.Length < 3 ||
                rows[0] != (job.IsGenericType ? job.AssemblyQualifiedName : job.FullName) ||
                !rows[1].StartsWith("M:", StringComparison.Ordinal) || rows[2] != "0") return false;
            var maximum = job.GetCustomAttribute<EntitiesJobMaxCountAttribute>()?.count ?? 0u;
            var limitSeen = false;
            string[] initializer = null;
            var arguments = new List<uint>();
            var groupKeys = new List<string>();
            var seen = new HashSet<uint>();
            string previous = null;
            reason = "invalid count/limit/initializer records or unresolved entity group";
            foreach (var row in rows.Skip(3)) {
                var fields = row.Split('\t');
                if (fields[0] == "L") {
                    if (limitSeen || fields.Length != 2 || fields[1] != maximum.ToString(CultureInfo.InvariantCulture)) return false;
                    limitSeen = true;
                } else if (fields[0] == "C") {
                    if (fields.Length != 5 || !groups.TryGetValue(fields[1] + "\t" + fields[2], out var group) ||
                        group >= groupCount || !seen.Add(group) || !Number(fields[3], out var inline) || !Number(fields[4], out var loop)) return false;
                    var identity = fields[1] + "\n" + fields[2];
                    if (previous != null && StringComparer.Ordinal.Compare(previous, identity) >= 0) return false;
                    previous = identity;
                    if (inline > 0u || (maximum > 0u && loop > 0u)) {
                        arguments.Add(group);
                        groupKeys.Add(fields[1] + "\t" + fields[2]);
                    }
                } else if (fields[0] == "I") {
                    if (initializer != null || fields.Length != 5 || fields[3] != "Apply" || fields[4] != "v3") return false;
                    initializer = fields;
                } else return false; // G records and unknown extensions cannot establish completeness.
            }
            if (!limitSeen || initializer == null) return false;
            var expected = "ME.BECS.SourceGenerated.JobEntityCounts_" +
                ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(initializer[1] + "\n" + rows[0] + "\n" + rows[1]);
            if (initializer[2] != expected) return false;
            reason = "source entity-count initializer assembly/signature unavailable or ambiguous";
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.FullName == initializer[1]).ToArray();
            if (assemblies.Length != 1) return false;
            var type = assemblies[0].GetType(expected, false);
            if (type == null || !type.IsVisible) return false;
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(candidate => candidate.Name == "Apply").ToArray();
            if (methods.Length != 1) return false;
            var method = methods[0];
            if (!method.IsGenericMethodDefinition || method.GetGenericArguments().Length != 1 || method.ReturnType != typeof(void) ||
                method.GetParameters().Length != arguments.Count + 1 || method.GetParameters().Any(parameter => parameter.ParameterType != typeof(uint))) return false;
            try { method.MakeGenericMethod(job); }
            catch (ArgumentException) { return false; }
            plan = new InitializerPlan { initializer = type, groupKeys = groupKeys.ToArray() };
            reason = null;
            return true;
        }

        private static bool Number(string text, out uint value) =>
            uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && text == value.ToString(CultureInfo.InvariantCulture);
    }
}
