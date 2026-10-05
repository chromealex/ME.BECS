namespace ME.BECS.Editor {
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using scg = System.Collections.Generic;

    // One export snapshot. Reading constants and closing method signatures must
    // never execute Args_* getters, EarlyInit, or legacy method selection.
    internal sealed class SourceGeneratorJobEarlyInit {
        private readonly scg::Dictionary<Type, (bool success, scg::KeyValuePair<int, MethodInfo>[] methods, string reason)> cache =
            new scg::Dictionary<Type, (bool, scg::KeyValuePair<int, MethodInfo>[], string)>();

        internal bool TryGetMethods(Type job, out scg::KeyValuePair<int, MethodInfo>[] methods, out string reason) {
            if (!this.cache.TryGetValue(job, out var selected)) {
                var success = Read(job, out var result, out var error);
                selected = (success, result, error);
                this.cache.Add(job, selected);
            }
            methods = selected.methods == null ? null : (scg::KeyValuePair<int, MethodInfo>[])selected.methods.Clone();
            reason = selected.reason;
            return selected.success;
        }

        private static bool Read(Type job, out scg::KeyValuePair<int, MethodInfo>[] methods, out string reason) {
            methods = null;
            reason = "source EarlyInit requires a visible closed value-type job";
            if (!job.IsValueType || !job.IsVisible || job.ContainsGenericParameters) return false;
            var catalog = job.Assembly.GetType("ME.BECS.SourceGenerated.JobEarlyInit_" +
                ME.BECS.CodeGeneration.SourceGeneratorNames.Encode(job.Assembly.GetName().Name), false);
            reason = "source EarlyInit catalog unavailable; recompile the job assembly";
            if (catalog == null || !catalog.IsVisible || !catalog.IsAbstract || !catalog.IsSealed) return false;
            var definition = job.IsGenericType ? job.GetGenericTypeDefinition() : job;
            var arguments = job.IsGenericType ? job.GetGenericArguments() : Type.EmptyTypes;
            var selected = new scg::Dictionary<int, MethodInfo>();
            var candidates = catalog.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var field in catalog.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
                if (!field.Name.StartsWith("Selection_", StringComparison.Ordinal)) continue;
                var text = field.IsLiteral && field.FieldType == typeof(string) ? field.GetRawConstantValue() as string : null;
                var row = text?.Split('\t');
                reason = "invalid source EarlyInit selection schema (requires v2)";
                if (row == null || row.Length != 7 || row[0] != "v2") return false;
                if (row[1] != definition.FullName) continue;
                reason = "invalid source EarlyInit selection identity, phase or arity";
                if (field.Name != "Selection_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(text) ||
                    !int.TryParse(row[6], NumberStyles.None, CultureInfo.InvariantCulture, out var phase) ||
                    row[6] != phase.ToString(CultureInfo.InvariantCulture) || !MatchesPhase(row[2], phase) ||
                    row[5] != arguments.Length.ToString(CultureInfo.InvariantCulture)) return false;
                var prefix = arguments.Length == 0 ? "Init_" : "InitGeneric_";
                if (!row[3].StartsWith(prefix, StringComparison.Ordinal)) return false;
                var suffix = row[3].Substring(prefix.Length);
                if (suffix.Length != 64 || suffix.Any(character => !(character >= '0' && character <= '9' || character >= 'A' && character <= 'F')) ||
                    row[4] != (arguments.Length == 0 ? "Args_" : "ArgsGeneric_") + suffix ||
                    arguments.Length != 0 && suffix != ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(definition.FullName + "|" + row[2])) return false;
                var wrappers = candidates.Where(method => method.Name == row[3]).ToArray();
                var getters = candidates.Where(method => method.Name == row[4]).ToArray();
                reason = "source EarlyInit wrapper/metadata signature missing or ambiguous";
                if (wrappers.Length != 1 || getters.Length != 1) return false;
                var wrapper = wrappers[0];
                var getter = getters[0];
                if (wrapper.ReturnType != typeof(void) || getter.ReturnType != typeof(Type[]) ||
                    wrapper.GetParameters().Length != 0 || getter.GetParameters().Length != 0 ||
                    wrapper.GetGenericArguments().Length != arguments.Length || getter.GetGenericArguments().Length != arguments.Length) return false;
                try {
                    if (arguments.Length != 0) {
                        wrapper = wrapper.MakeGenericMethod(arguments);
                        getter = getter.MakeGenericMethod(arguments);
                    }
                } catch (ArgumentException exception) {
                    reason = "source EarlyInit generic constraints do not match: " + exception.Message;
                    return false;
                }
                if (wrapper.ContainsGenericParameters || getter.ContainsGenericParameters) return false;
                reason = "ambiguous source EarlyInit selection in phase " + phase.ToString(CultureInfo.InvariantCulture);
                if (selected.ContainsKey(phase)) return false;
                selected.Add(phase, wrapper);
            }
            reason = "source EarlyInit selection absent for job; recompile its catalog";
            if (selected.Count == 0) return false;
            methods = selected.OrderBy(entry => entry.Key).ToArray();
            reason = null;
            return true;
        }

        private static bool MatchesPhase(string method, int phase) {
            switch (phase) {
                case 0: case 2: return method == "DoComponents";
                case 1: return method == "DoParallelForComponents";
                case 3: return method == "DoParallelForAspect";
                case 4: return method == "DoAspect";
                case 5: return method.StartsWith("DoAspectsComponents", StringComparison.Ordinal);
                case 6: return method.StartsWith("DoParallelForAspectsComponents", StringComparison.Ordinal);
                default: return false;
            }
        }
    }
}
