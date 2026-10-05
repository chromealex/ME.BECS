using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ME.BECS.Mono.Reflection;

namespace ME.BECS.Editor.Jobs {
    using System.Collections.Generic;

    // Static ECS call-site heuristic, NOT a bound on dynamic work or allocations.
    // Ordinary helper bodies contribute once per closed method; IgnoreVisited opts
    // a wrapper into per-call expansion. Runtime entity reservations use a separate
    // analyzer and must never be inferred from these scheduling weights.
    internal static class ILJobWeights {
        private static readonly Dictionary<MethodInfo, uint> costs = CreateCosts();

        private static Dictionary<MethodInfo, uint> CreateCosts() {
            var result = new Dictionary<MethodInfo, uint>();
            void Add(Type type, bool instance, uint cost, params string[] names) {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | (instance ? BindingFlags.Instance : BindingFlags.Static);
                foreach (var method in type.GetMethods(flags)) if (names.Contains(method.Name)) result.Add(method, cost);
            }
            Add(typeof(Ent), false, 10u, nameof(Ent.NewEnt_INTERNAL));
            Add(typeof(EntExt), false, 1u, nameof(EntExt.Read), nameof(EntExt.Has), nameof(EntExt.TryRead));
            Add(typeof(EntExt), false, 2u, nameof(EntExt.Get), nameof(EntExt.Set), nameof(EntExt.Remove), nameof(EntExt.SetTag));
            Add(typeof(EntityConfigEntExt), false, 4u, nameof(EntityConfigEntExt.ReadStatic), nameof(EntityConfigEntExt.HasStatic), nameof(EntityConfigEntExt.TryReadStatic));
            Add(typeof(UnsafeEntityConfig), true, 3u, nameof(UnsafeEntityConfig.ReadStatic));
            Add(typeof(Components), true, 2u, nameof(Components.GetUnknownType), nameof(Components.RemoveUnknownType));
            Add(typeof(Components), true, 1u, nameof(Components.ReadUnknownType), nameof(Components.HasUnknownType));
            return result;
        }

        internal static uint Analyze(Type job, Dictionary<string, uint> contributions = null) {
            if (contributions != null) return AnalyzeCore(job, contributions);
            return ILAnalysisSession.Get((typeof(ILJobWeights), job), () =>
                ILPersistentAnalysis.Get("weights", job.AssemblyQualifiedName, () => AnalyzeCore(job, null),
                    value => new ILSummaryData.Number { value = value }, data => data.value));
        }

        private static uint AnalyzeCore(Type job, Dictionary<string, uint> contributions) {
            contributions?.Clear();
            var roots = JobsEarlyInitCodeGenerator.GetJobExecuteMethods(job);
            var weight = checked((uint)job.GetInterfaces().Sum(contract => contract.GenericTypeArguments.Length));
            var visited = new HashSet<MethodBase>();
            var active = new HashSet<MethodBase>();
            var pending = new Stack<(MethodBase method, int depth, bool exit)>();
            for (var i = roots.Length - 1; i >= 0; --i) pending.Push((roots[i], 0, false));
            var bodies = 0;
            var instructionsRead = 0;
            while (pending.Count > 0) {
                var item = pending.Pop();
                var method = item.method;
                if (item.exit) { active.Remove(method); continue; }
                if (ILInfrastructure.SkipBody(method)) continue;
                CodeGeneratorTimings.Work(method);
                if (method.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) || active.Contains(method)) continue;
                if (!method.IsDefined(typeof(CodeGeneratorIgnoreVisitedAttribute), false) && !visited.Add(method)) continue;
                if (method.GetMethodBody() == null) continue;
                if (++bodies > 10000 || item.depth >= 128)
                    throw new InvalidOperationException("IL job weight traversal limit exceeded: " + job);
                var instructions = ILAnalysisSession.Instructions(method);
                instructionsRead = checked(instructionsRead + instructions.Length);
                if (instructionsRead > 1000000) throw new InvalidOperationException("IL job weight instruction limit exceeded: " + job);
                active.Add(method);
                pending.Push((method, item.depth, true));
                var calls = new List<MethodBase>();
                var delegates = ILDelegateTargets.Read(method, instructions);
                for (var index = 0; index < instructions.Length; ++index) {
                    var instruction = instructions[index];
                    if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt &&
                        instruction.OpCode != OpCodes.Newobj && instruction.OpCode != OpCodes.Jmp) continue;
                    var target = ILCallTargets.Resolve(instructions, index);
                    if (target == null) continue;
                    var targets = ILDelegateTargets.IsInvoke(target) && delegates.At(instruction.Offset) is var invocation && invocation.complete
                        ? invocation.targets : new[] { target };
                    foreach (var called in targets) {
                        if (called is MethodInfo priced) {
                            var definition = priced.IsGenericMethod ? priced.GetGenericMethodDefinition() : priced;
                            if (costs.TryGetValue(definition, out var cost)) {
                                weight = checked(weight + cost);
                                if (contributions != null) {
                                    var key = definition.DeclaringType.FullName + "." + definition.Name;
                                    contributions.TryGetValue(key, out var previous);
                                    contributions[key] = checked(previous + cost);
                                }
                            }
                        }
                        // The direct cost is charged even when a callee's body is
                        // ignored/visited. Taking its address (ldftn) is not a call.
                        calls.Add(called);
                    }
                }
                for (var index = calls.Count - 1; index >= 0; --index) pending.Push((calls[index], item.depth + 1, false));
            }
            return weight;
        }
    }
}
