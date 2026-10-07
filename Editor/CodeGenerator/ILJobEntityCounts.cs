using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ME.BECS.Mono.Reflection;

namespace ME.BECS.Editor.Jobs {
    using System.Collections.Generic;
    // Counts allocation sites, not loop iterations. Each invocation of a helper
    // contributes independently; a repeating site requires EntitiesJobMaxCount.
    // Unsupported paths never expose a partial result as a complete reservation.
    internal sealed class ILJobEntityCounts {
        internal struct Count {
            internal uint inline;
            internal uint loop;
        }

        private sealed class Summary {
            internal readonly Dictionary<Type, Count> counts = new Dictionary<Type, Count>();
            internal readonly HashSet<string> gaps = new HashSet<string>(StringComparer.Ordinal);

            internal void Add(Summary other, bool repeating) {
                this.gaps.UnionWith(other.gaps);
                foreach (var entry in other.counts) {
                    this.counts.TryGetValue(entry.Key, out var count);
                    try {
                        checked {
                            if (repeating) count.loop += entry.Value.inline + entry.Value.loop;
                            else { count.inline += entry.Value.inline; count.loop += entry.Value.loop; }
                        }
                        this.counts[entry.Key] = count;
                    } catch (OverflowException) { this.gaps.Add("EntityCountOverflow"); }
                }
            }
        }

        private sealed class Node {
            internal MethodBase method;
            internal readonly Summary local = new Summary();
            internal readonly List<(Node target, bool repeating)> calls = new List<(Node, bool)>();
            internal readonly HashSet<Node> initializers = new HashSet<Node>();
            internal readonly List<Node> callers = new List<Node>();
            internal readonly List<Node> effectConsumers = new List<Node>();
            internal bool mayCreate, unknown;
            internal int component, depth;
        }

        private readonly Dictionary<MethodBase, Node> methods = new Dictionary<MethodBase, Node>();
        private readonly Queue<Node> pending = new Queue<Node>();
        private readonly MethodInfo terminal = typeof(Ent).GetMethod(nameof(Ent.NewEnt_INTERNAL),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetGenericMethodDefinition();
        private int instructionCount;
        private bool includeDeclaredVirtualTargets;

        // A separate record kind makes IL authority explicit to the compiler.
        // Rows retain loop-only groups even when no reservation can be made.
        internal static bool TryGetPayload(Type job, out string payload, out string reason) {
            payload = null;
            if (!TryAnalyze(job, out var counts, out reason)) return false;
            return FormatPayload(job, counts, out payload, out reason);
        }

        // One production walk collects known targets AND coverage gaps. If it
        // has gaps, the compatibility projection reuses this same snapshot.
        // With no gaps it is identical to the strict walk (no virtual fallback
        // edge was needed). Explicit strict diagnostics remain independent.
        internal static bool TryGetExportPayload(Type job, out string payload, out string reason) {
            var counts = AnalyzeKnown(job, out reason);
            payload = null;
            return reason == null && FormatPayload(job, counts, out payload, out reason);
        }

        private static bool FormatPayload(Type job, Dictionary<Type, Count> counts, out string payload, out string reason) {
            payload = null;
            reason = null;
            var maximum = job.GetCustomAttribute<EntitiesJobMaxCountAttribute>()?.count ?? 0u;
            var format = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new System.Text.StringBuilder();
            ulong loops = 0;
            var allocate = false;
            foreach (var entry in counts.OrderBy(item => item.Key.Assembly.FullName, StringComparer.Ordinal)
                         .ThenBy(item => item.Key.FullName.Replace('+', '.'), StringComparer.Ordinal)) {
                var count = entry.Value;
                loops += count.loop;
                var reserved = maximum > 0u && count.loop > 0u ? maximum : count.inline;
                allocate |= reserved > 0u;
                rows.Append('\n').Append(entry.Key.Assembly.FullName).Append("\tT:").Append(entry.Key.FullName.Replace('+', '.'))
                    .Append('\t').Append(reserved.ToString(format)).Append('\t').Append(count.inline.ToString(format))
                    .Append('\t').Append(count.loop.ToString(format));
            }
            if (loops > uint.MaxValue) { reason = "EntityLoopCountOverflow: " + job; return false; }
            payload = "v1\n" + job.AssemblyQualifiedName + "\n" + maximum.ToString(format) + "\n" + loops.ToString(format) +
                "\n" + (allocate ? "1" : "0") + rows;
            return true;
        }

        internal static bool TryAnalyze(Type job, out Dictionary<Type, Count> counts, out string reason) {
            var result = Analyze(job, false, out _, out reason);
            counts = reason == null ? new Dictionary<Type, Count>(result.counts) : null;
            return reason == null;
        }

        // Transitional compatibility snapshot, NOT complete dispatch coverage.
        // Preserve the old nominal virtual target policy, but count known calls
        // with the same per-site/loop graph as the covered IL path. Never reuse
        // the old globally-deduplicated, instruction-mutating expansion.
        internal static Dictionary<Type, Count> AnalyzeKnown(Type job, out string reason) {
            var result = Analyze(job, true, out var bounded, out reason);
            if (!bounded || result.gaps.Contains("EntityCountOverflow"))
                throw new InvalidOperationException("Cannot construct a bounded compatibility entity reservation for " + job + ": " + reason);
            return new Dictionary<Type, Count>(result.counts);
        }

        private static Summary Analyze(Type job, bool includeDeclaredVirtualTargets, out bool bounded, out string reason) {
            var cached = ILAnalysisSession.Get((typeof(ILJobEntityCounts), job, includeDeclaredVirtualTargets), () =>
                ILPersistentAnalysis.Get("entity-counts", job.AssemblyQualifiedName + "\n" + includeDeclaredVirtualTargets, () => {
                    var summary = AnalyzeCore(job, includeDeclaredVirtualTargets, out var complete, out var failure);
                    return (summary, complete, failure);
                }, Encode, Decode));
            bounded = cached.complete;
            reason = cached.failure;
            return cached.summary;
        }

        [Serializable] private sealed class CountData {
            public string type;
            public uint inline, loop;
        }
        [Serializable] private sealed class CountsData {
            public CountData[] counts;
            public string[] gaps;
            public bool bounded;
            public string reason;
        }
        private static CountsData Encode((Summary summary, bool complete, string failure) value) => new CountsData {
            counts = value.summary.counts.OrderBy(pair => pair.Key.AssemblyQualifiedName, StringComparer.Ordinal)
                .Select(pair => new CountData { type = pair.Key.AssemblyQualifiedName, inline = pair.Value.inline, loop = pair.Value.loop }).ToArray(),
            gaps = value.summary.gaps.OrderBy(gap => gap, StringComparer.Ordinal).ToArray(), bounded = value.complete, reason = value.failure,
        };
        private static (Summary summary, bool complete, string failure) Decode(CountsData data) {
            if (data?.counts == null || data.gaps == null) throw new FormatException("Missing cached entity counts.");
            var summary = new Summary();
            foreach (var count in data.counts) summary.counts.Add(ILSummaryData.Resolve(count.type), new Count { inline = count.inline, loop = count.loop });
            summary.gaps.UnionWith(data.gaps);
            return (summary, data.bounded, string.IsNullOrEmpty(data.reason) ? null : data.reason);
        }

        private static Summary AnalyzeCore(Type job, bool includeDeclaredVirtualTargets, out bool bounded, out string reason) {
            var analyzer = new ILJobEntityCounts { includeDeclaredVirtualTargets = includeDeclaredVirtualTargets };
            var result = new Summary();
            bounded = true;
            try {
                var roots = JobsEarlyInitCodeGenerator.GetJobExecuteMethods(job).Select(method => analyzer.GetNode(method, 0)).ToArray();
                while (analyzer.pending.Count > 0) {
                    var node = analyzer.pending.Dequeue();
                    CodeGeneratorTimings.Work(node.method);
                    analyzer.ReadBody(node);
                }
                var summaries = analyzer.Summarize();
                foreach (var root in roots) result.Add(summaries[root.component], false);
            } catch (FlowLimitException) { bounded = false; result.gaps.Add("EntityCountFlowLimit: " + job); }
            catch (TraversalLimitException) { bounded = false; result.gaps.Add("EntityCountTraversalLimit: " + job); }
            reason = result.gaps.Count == 0 ? null : string.Join("; ", result.gaps.OrderBy(gap => gap, StringComparer.Ordinal));
            return result;
        }

        private sealed class TraversalLimitException : System.Exception { }

        private Node GetNode(MethodBase method, int depth) {
            if (this.methods.TryGetValue(method, out var node)) return node;
            // Recursion reuses an existing node. Growing generic instantiations
            // still need a depth bound before reflection constructs an unbounded
            // family of ever larger closed types.
            if (this.methods.Count >= 10000 || depth >= 128) throw new TraversalLimitException();
            node = new Node { method = method, depth = depth };
            this.methods.Add(method, node);
            this.pending.Enqueue(node);
            return node;
        }

        private void AddInitializer(Node owner, Type type) {
            var constructor = type?.TypeInitializer;
            if (constructor == null || constructor.Equals(owner.method)) return;
            var target = this.GetNode(constructor, owner.depth + 1);
            if (owner.initializers.Add(target)) target.effectConsumers.Add(owner);
        }

        private void AddCall(Node owner, MethodBase method, bool repeating) {
            var target = this.GetNode(method, owner.depth + 1);
            owner.calls.Add((target, repeating)); // Keep every call site, even the same target twice.
            target.callers.Add(owner);
            target.effectConsumers.Add(owner);
        }

        private void ReadBody(Node node) {
            var method = node.method;
            var result = node.local;
            if (method.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false)) return;
            if (ILInfrastructure.IsLeaf(method)) return;
            // Unresolved delegate/abstract dispatch can reach user code: report it as such
            // before the BCL boundary would classify System.Action.Invoke as a framework call.
            if (method.IsAbstract || method.DeclaringType != null && typeof(Delegate).IsAssignableFrom(method.DeclaringType) && method.Name == "Invoke") {
                result.gaps.Add("UnresolvedDispatch: " + method);
                return;
            }
            if (ILInfrastructure.IsOpaque(method)) {
                result.gaps.Add("OpaqueFrameworkCall: " + method.DeclaringType + "." + method.Name);
                return;
            }
            this.AddInitializer(node, method.DeclaringType);
            if (method is MethodInfo creation && creation.IsGenericMethod && creation.GetGenericMethodDefinition() == this.terminal) {
                var group = creation.GetGenericArguments()[0];
                if (group.IsGenericType || !group.IsValueType || !typeof(IEntityType).IsAssignableFrom(group))
                    result.gaps.Add("UnresolvedEntityType: " + method);
                else result.counts.Add(group, new Count { inline = 1u });
                return;
            }
            if (method.ContainsGenericParameters) { result.gaps.Add("OpenMethod: " + method); return; }
            if (IsBurstStorageLeaf(method)) return;
            if (method is MethodInfo factory && factory.DeclaringType == typeof(Activator) && factory.Name == "CreateInstance" &&
                factory.IsGenericMethod && factory.GetParameters().Length == 0) {
                var type = factory.GetGenericArguments()[0];
                var constructor = type.GetConstructor(Type.EmptyTypes);
                // `new T()` has no user body for a value type without an explicit constructor.
                if (constructor != null) { this.AddCall(node, constructor, false); return; }
                if (type.IsValueType) { this.AddInitializer(node, type); return; }
            }
            var body = method.GetMethodBody();
            // Native/extern methods without IL retain their existing framework contract.
            // Managed abstract/delegate dispatch is explicitly excluded above.
            if (body == null) return;
            var instructions = ILAnalysisSession.Instructions(method);
            this.instructionCount = checked(this.instructionCount + instructions.Length);
            if (this.instructionCount > 1000000) throw new TraversalLimitException();
            var flow = ILAnalysisSession.Get((typeof(Flow), method), () => new Flow(instructions, body));
            var delegates = ILDelegateTargets.Read(method, instructions);
            for (var index = 0; index < instructions.Length; ++index) {
                if (!flow.reachable[index]) continue;
                var instruction = instructions[index];
                if (instruction.Operand is FieldInfo field && field.IsStatic)
                    this.AddInitializer(node, field.DeclaringType);
                if (instruction.OpCode.OperandType == OperandType.InlineField && !(instruction.Operand is FieldInfo))
                    result.gaps.Add("MissingFieldTarget: " + method);
                if (instruction.OpCode == OpCodes.Calli) { result.gaps.Add("IndirectCall: " + method); continue; }
                // A method address (ldftn/ldvirtftn) is not an invocation.
                if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt &&
                    instruction.OpCode != OpCodes.Newobj && instruction.OpCode != OpCodes.Jmp) continue;
                if (!(instruction.Operand is MethodBase target)) { result.gaps.Add("MissingCallTarget: " + method); continue; }
                if (target.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false)) continue;
                if (ILDelegateTargets.IsInvoke(target) && delegates.At(instruction.Offset) is var invocation && invocation.complete) {
                    // Every invocation site contributes separately. Alternative
                    // single-cast targets form a conservative union, not a claim
                    // they execute sequentially. Multicast lists stay unresolved.
                    foreach (var callback in invocation.targets) this.AddCall(node, callback, flow.repeating[index]);
                    continue;
                }
                if (instruction.OpCode == OpCodes.Callvirt && target is MethodInfo virtualMethod) {
                    var receiver = ILCallTargets.ConstrainedReceiver(instructions, index);
                    if (ILCallTargets.IsExactReceiver(receiver)) {
                        target = ILCallTargets.ResolveConstrained(virtualMethod, receiver);
                        if (target == null) { result.gaps.Add("UnresolvedConstrainedCall: " + virtualMethod); continue; }
                    } else if (virtualMethod.IsVirtual && !virtualMethod.IsFinal && !virtualMethod.DeclaringType.IsSealed) {
                        result.gaps.Add("UnresolvedVirtualCall: " + virtualMethod);
                        if (this.includeDeclaredVirtualTargets) this.AddCall(node, target, flow.repeating[index]);
                        continue;
                    }
                }
                this.AddCall(node, target, flow.repeating[index]);
            }
        }

        private Summary[] Summarize() {
            var nodes = this.methods.Values.ToArray();
            // First solve presence/unknown effects, including initialization edges.
            // A CLR initializer is a proof obligation, never a per-Execute count.
            var effects = new Queue<Node>();
            foreach (var node in nodes) {
                node.mayCreate = node.local.counts.Count > 0;
                node.unknown = node.local.gaps.Count > 0;
                if (node.mayCreate || node.unknown) effects.Enqueue(node);
            }
            while (effects.Count > 0) {
                var effect = effects.Dequeue();
                foreach (var consumer in effect.effectConsumers) {
                    var changed = effect.mayCreate && !consumer.mayCreate || effect.unknown && !consumer.unknown;
                    consumer.mayCreate |= effect.mayCreate;
                    consumer.unknown |= effect.unknown;
                    if (changed) effects.Enqueue(consumer);
                }
            }
            foreach (var node in nodes)
                foreach (var initializer in node.initializers)
                    if (initializer.mayCreate || initializer.unknown)
                        node.local.gaps.Add("UnprovenTypeInitialization: " + initializer.method.DeclaringType);

            // Collapse the invocation graph only. Initialization edges must not
            // invent recursion between a .cctor and ordinary same-type helpers.
            var components = Components(nodes);
            var summaries = new Summary[components.Count];
            for (var index = components.Count - 1; index >= 0; --index) {
                var component = components[index];
                var recursive = component.Count > 1 || component[0].calls.Any(call => call.target == component[0]);
                var summary = summaries[index] = new Summary();
                foreach (var node in component) {
                    summary.Add(node.local, recursive);
                    foreach (var call in node.calls) {
                        if (call.target.component == index) continue;
                        // Every allocation reachable from a recursive component can
                        // repeat. A closed allocation-free cycle contributes zero.
                        summary.Add(summaries[call.target.component], recursive || call.repeating);
                    }
                }
            }
            return summaries;
        }

        private static List<List<Node>> Components(Node[] nodes) {
            var visited = new HashSet<Node>();
            var order = new List<Node>();
            var walk = new Stack<(Node node, bool exit)>();
            foreach (var root in nodes) {
                if (visited.Contains(root)) continue;
                walk.Push((root, false));
                while (walk.Count > 0) {
                    var item = walk.Pop();
                    if (item.exit) { order.Add(item.node); continue; }
                    if (!visited.Add(item.node)) continue;
                    walk.Push((item.node, true));
                    foreach (var call in item.node.calls) if (!visited.Contains(call.target)) walk.Push((call.target, false));
                }
            }
            visited.Clear();
            var pending = new Stack<Node>();
            var components = new List<List<Node>>();
            for (var index = order.Count - 1; index >= 0; --index) {
                if (!visited.Add(order[index])) continue;
                var component = new List<Node>();
                pending.Push(order[index]);
                while (pending.Count > 0) {
                    var node = pending.Pop();
                    node.component = components.Count;
                    component.Add(node);
                    foreach (var caller in node.callers) if (visited.Add(caller)) pending.Push(caller);
                }
                components.Add(component);
            }
            return components;
        }

        internal static bool IsBurstStorageLeaf(MethodBase method) {
            // Audited Unity.Burst SharedStatic implementation: these overloads use
            // type-identity hashes, unmanaged size and native storage, never user
            // constructors/callbacks. In particular typeof(TContext) does NOT run
            // TContext's initializer. Do not include the System.Type overloads:
            // their virtual AssemblyQualifiedName getter may be user code.
            if (method.DeclaringType?.IsGenericType != true ||
                method.DeclaringType.GetGenericTypeDefinition() != typeof(Unity.Burst.SharedStatic<>)) return false;
            var parameters = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            if (!method.IsStatic && (method.Name == "get_Data" || method.Name == "get_UnsafeDataPointer")) return parameters.Length == 0;
            if (!(method is MethodInfo factory) || !method.IsStatic || factory.ReturnType != method.DeclaringType) return false;
            if (method.Name == "GetOrCreate" && method.IsGenericMethod && (method.GetGenericArguments().Length == 1 || method.GetGenericArguments().Length == 2))
                return parameters.SequenceEqual(new[] { typeof(uint) });
            if (method.Name == "GetOrCreateUnsafe" && !method.IsGenericMethod)
                return parameters.SequenceEqual(new[] { typeof(uint), typeof(long), typeof(long) });
            if ((method.Name == "GetOrCreatePartiallyUnsafeWithHashCode" || method.Name == "GetOrCreatePartiallyUnsafeWithSubHashCode") &&
                method.IsGenericMethod && method.GetGenericArguments().Length == 1)
                return parameters.SequenceEqual(new[] { typeof(uint), typeof(long) });
            return false;
        }

        // Original per-method offsets only; never mutate cached Instruction.loopInfo.
        // SCCs capture real cycles (including switch/goto), not any backwards address.
        private sealed class FlowLimitException : System.Exception { }
        private sealed class Flow {
            internal readonly bool[] reachable;
            internal readonly bool[] repeating;
            private readonly List<int>[] edges;
            private readonly List<int>[] reverse;
            private readonly Dictionary<int, int> offsets;
            private int work;

            private void Step() {
                if ((++this.work & 1023) == 0) CodeGeneratorTimings.Work(null);
                if (this.work > 1000000) throw new FlowLimitException();
            }

            internal Flow(Instruction[] instructions, MethodBody body) {
                var length = instructions.Length;
                this.reachable = new bool[length]; this.repeating = new bool[length];
                this.edges = Enumerable.Range(0, length).Select(_ => new List<int>()).ToArray();
                this.reverse = Enumerable.Range(0, length).Select(_ => new List<int>()).ToArray();
                this.offsets = instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(item => item.Offset, item => item.index);
                for (var index = 0; index < length; ++index) {
                    var instruction = instructions[index];
                    if (instruction.Operand is Instruction branch) this.Edge(index, this.offsets[branch.Offset]);
                    if (instruction.Operand is Instruction[] branches) foreach (var target in branches) this.Edge(index, this.offsets[target.Offset]);
                    if (index + 1 < length && instruction.OpCode.FlowControl != FlowControl.Branch &&
                        instruction.OpCode.FlowControl != FlowControl.Return && instruction.OpCode.FlowControl != FlowControl.Throw &&
                        instruction.OpCode != OpCodes.Jmp) this.Edge(index, index + 1);
                }
                foreach (var clause in body.ExceptionHandlingClauses) {
                    var entry = clause.Flags == ExceptionHandlingClauseOptions.Filter ? clause.FilterOffset : clause.HandlerOffset;
                    for (var index = 0; index < length; ++index) {
                        this.Step();
                        var offset = instructions[index].Offset;
                        if (Inside(offset, clause.TryOffset, clause.TryLength)) this.Edge(index, this.offsets[entry]);
                        if (clause.Flags == ExceptionHandlingClauseOptions.Filter && offset >= clause.FilterOffset && offset < clause.HandlerOffset) {
                            // Exception filters can be revisited during exception search.
                            this.repeating[index] = true;
                            if (instructions[index].OpCode == OpCodes.Endfilter) this.Edge(index, this.offsets[clause.HandlerOffset]);
                        }
                        if (clause.Flags != ExceptionHandlingClauseOptions.Finally || !Inside(offset, clause.TryOffset, clause.TryLength) ||
                            instructions[index].OpCode != OpCodes.Leave && instructions[index].OpCode != OpCodes.Leave_S ||
                            !(instructions[index].Operand is Instruction target)) continue;
                        // Normal leave runs finally before its continuation. Include
                        // all continuations conservatively when finding repetitions.
                        this.Edge(index, this.offsets[clause.HandlerOffset]);
                        for (var end = 0; end < length; ++end) {
                            this.Step();
                            if (Inside(instructions[end].Offset, clause.HandlerOffset, clause.HandlerLength) && instructions[end].OpCode == OpCodes.Endfinally)
                                this.Edge(end, this.offsets[target.Offset]);
                        }
                    }
                }
                if (length == 0) return;
                var visited = new bool[length];
                var order = new List<int>();
                var stack = new Stack<(int Node, bool Exit)>();
                stack.Push((0, false));
                while (stack.Count > 0) {
                    var item = stack.Pop();
                    if (item.Exit) { order.Add(item.Node); continue; }
                    if (visited[item.Node]) continue;
                    visited[item.Node] = this.reachable[item.Node] = true;
                    stack.Push((item.Node, true));
                    foreach (var next in this.edges[item.Node]) if (!visited[next]) stack.Push((next, false));
                }
                Array.Clear(visited, 0, length);
                var pending = new Stack<int>();
                for (var index = order.Count - 1; index >= 0; --index) {
                    if (visited[order[index]]) continue;
                    var component = new List<int>();
                    pending.Push(order[index]); visited[order[index]] = true;
                    while (pending.Count > 0) {
                        var node = pending.Pop(); component.Add(node);
                        foreach (var previous in this.reverse[node]) if (this.reachable[previous] && !visited[previous]) {
                            visited[previous] = true; pending.Push(previous);
                        }
                    }
                    if (component.Count > 1 || this.edges[component[0]].Contains(component[0]))
                        foreach (var node in component) this.repeating[node] = true;
                }
            }

            private static bool Inside(int offset, int start, int length) => offset >= start && offset - start < length;
            private void Edge(int from, int to) { this.Step(); this.edges[from].Add(to); this.reverse[to].Add(from); }
        }
    }
}
