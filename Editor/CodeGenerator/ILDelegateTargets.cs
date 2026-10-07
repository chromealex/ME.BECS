namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using ME.BECS.Mono.Reflection;

    // Per-body single-cast delegate target flow. No delegate/constructor is run.
    // A complete site proves only its possible target set, not invocation order,
    // receiver identity, callback arguments, or whole-program effect coverage.
    internal sealed class ILDelegateTargets {
        internal sealed class Invocation {
            internal readonly MethodInfo[] targets;
            internal readonly bool complete;
            internal Invocation(MethodInfo[] targets, bool complete) { this.targets = targets; this.complete = complete; }
        }

        private sealed class Value {
            internal static readonly Value Empty = new Value();
            internal static readonly Value Unknown = new Value { unknown = true };
            internal MethodInfo[] pointers = Array.Empty<MethodInfo>();
            internal MethodInfo[] delegates = Array.Empty<MethodInfo>();
            internal int[] addresses = Array.Empty<int>();
            internal bool unknown;

            internal Value Union(Value other) {
                if (ReferenceEquals(this, other)) return this;
                var p = this.pointers.Union(other.pointers).ToArray();
                var d = this.delegates.Union(other.delegates).ToArray();
                var a = this.addresses.Union(other.addresses).ToArray();
                if (p.Length + d.Length + a.Length > 128) throw new InvalidOperationException("Delegate value limit");
                if (p.Length == this.pointers.Length && d.Length == this.delegates.Length && a.Length == this.addresses.Length &&
                    (!other.unknown || this.unknown)) return this;
                return new Value { pointers = p, delegates = d, addresses = a, unknown = this.unknown || other.unknown };
            }
        }

        private sealed class Frame {
            internal Value[] variables;
            internal bool[] escaped;
            internal List<Value> stack = new List<Value>();
            internal Frame Copy() => new Frame { variables = (Value[])this.variables.Clone(), escaped = (bool[])this.escaped.Clone(), stack = new List<Value>(this.stack) };
            internal Value Pop() {
                if (this.stack.Count == 0) throw new InvalidOperationException("Invalid delegate IL stack");
                var index = this.stack.Count - 1; var value = this.stack[index]; this.stack.RemoveAt(index); return value;
            }
            internal Value Read(Value value) => this.Read(value, new HashSet<int>());
            private Value Read(Value value, HashSet<int> active) {
                if (value.addresses.Length == 0) return value;
                var result = value.unknown ? Value.Unknown : Value.Empty;
                foreach (var address in value.addresses) {
                    if (!active.Add(address)) return Value.Unknown;
                    result = result.Union(this.Read(this.variables[address], active));
                    active.Remove(address);
                }
                return result;
            }
            internal void Escape(Value address) {
                // Unknown/native aliases can retain a pointer beyond this call.
                // Once escaped, a subsequent assignment cannot restore precision.
                for (var slot = 0; slot < this.variables.Length; ++slot)
                    if (address.unknown || address.addresses.Length == 0 || Array.IndexOf(address.addresses, slot) >= 0) {
                        this.escaped[slot] = true; this.variables[slot] = Value.Unknown;
                    }
            }
            internal void Write(Value address, Value value) {
                if (address.unknown || address.addresses.Length == 0) { this.Escape(Value.Unknown); return; }
                foreach (var slot in address.addresses)
                    this.variables[slot] = this.escaped[slot] ? Value.Unknown : address.addresses.Length == 1 ? value : this.variables[slot].Union(value);
            }
        }

        private static readonly Invocation UnknownInvocation = new Invocation(Array.Empty<MethodInfo>(), false);
        private readonly Dictionary<int, Invocation> sites = new Dictionary<int, Invocation>();
        internal Invocation At(int offset) => this.sites.TryGetValue(offset, out var site) ? site : UnknownInvocation;

        internal static bool IsInvoke(MethodBase method) => method is MethodInfo && !method.IsStatic && method.Name == "Invoke" &&
            method.DeclaringType != null && typeof(MulticastDelegate).IsAssignableFrom(method.DeclaringType);

        internal static ILDelegateTargets Read(MethodBase method, Instruction[] instructions) {
            return ILAnalysisSession.Get((typeof(ILDelegateTargets), method, instructions), () => ReadCore(method, instructions));
        }

        private static ILDelegateTargets ReadCore(MethodBase method, Instruction[] instructions) {
            var result = new ILDelegateTargets();
            foreach (var instruction in instructions)
                if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                    instruction.Operand is MethodBase target && IsInvoke(target)) result.sites.Add(instruction.Offset, UnknownInvocation);
            if (result.sites.Count == 0) return result;
            try { new Flow(method, instructions, result).Run(); }
            catch (InvalidOperationException) {
                // Unsupported IL/limits invalidate the whole body, including sites
                // visited before failure. Never retain a partial target proof.
                foreach (var offset in result.sites.Keys.ToArray()) result.sites[offset] = UnknownInvocation;
            }
            return result;
        }

        private sealed class Flow {
            private readonly MethodBase method;
            private readonly Instruction[] instructions;
            private readonly MethodBody body;
            private readonly ILDelegateTargets result;
            private readonly Type[] variableTypes;
            private readonly int argumentCount;
            private readonly Dictionary<int, int> offsets;
            private readonly Frame[] frames;
            private readonly bool[] queued;
            private readonly Queue<int> pending = new Queue<int>();
            private readonly Dictionary<int, Value> calls = new Dictionary<int, Value>();
            private int work;

            internal Flow(MethodBase method, Instruction[] instructions, ILDelegateTargets result) {
                this.method = method; this.instructions = instructions; this.result = result;
                this.body = method.GetMethodBody() ?? throw new InvalidOperationException("Missing delegate IL body");
                var arguments = (method.IsStatic ? Type.EmptyTypes : new[] { method.DeclaringType })
                    .Concat(method.GetParameters().Select(parameter => parameter.ParameterType)).ToArray();
                this.argumentCount = arguments.Length;
                this.variableTypes = arguments.Concat(this.body.LocalVariables.Select(local => local.LocalType)).ToArray();
                if (this.variableTypes.Length > 65536) throw new InvalidOperationException("Delegate storage limit");
                this.offsets = instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(item => item.Offset, item => item.index);
                this.frames = new Frame[instructions.Length]; this.queued = new bool[instructions.Length];
            }

            private void Merge(int index, Frame next) {
                var frame = this.frames[index]; var changed = frame == null;
                if (frame == null) this.frames[index] = next.Copy();
                else {
                    if (frame.stack.Count != next.stack.Count) throw new InvalidOperationException("Inconsistent delegate IL stack");
                    for (var slot = 0; slot < frame.variables.Length; ++slot) {
                        var merged = frame.escaped[slot] || next.escaped[slot] ? Value.Unknown : frame.variables[slot].Union(next.variables[slot]);
                        changed |= !ReferenceEquals(merged, frame.variables[slot]) || next.escaped[slot] && !frame.escaped[slot];
                        frame.escaped[slot] |= next.escaped[slot];
                        frame.variables[slot] = merged;
                    }
                    for (var slot = 0; slot < frame.stack.Count; ++slot) {
                        var merged = frame.stack[slot].Union(next.stack[slot]);
                        changed |= !ReferenceEquals(merged, frame.stack[slot]); frame.stack[slot] = merged;
                    }
                }
                if (changed && !this.queued[index]) { this.queued[index] = true; this.pending.Enqueue(index); }
            }

            internal void Run() {
                if (this.instructions.Length == 0) return;
                this.Merge(0, new Frame {
                    variables = this.variableTypes.Select((type, index) => index >= this.argumentCount && this.body.InitLocals && !type.IsValueType && !type.IsByRef && !type.IsPointer ? Value.Empty : Value.Unknown).ToArray(),
                    escaped = new bool[this.variableTypes.Length],
                });
                while (this.pending.Count > 0) {
                    if ((this.work & 255) == 0) CodeGeneratorTimings.Work(this.method);
                    if (++this.work > 200000) throw new InvalidOperationException("Delegate flow limit");
                    var index = this.pending.Dequeue(); this.queued[index] = false;
                    var before = this.frames[index]; var next = before.Copy(); var instruction = this.instructions[index];
                    this.Step(index, next);
                    foreach (var clause in this.body.ExceptionHandlingClauses) {
                        if (instruction.Offset >= clause.TryOffset && instruction.Offset < clause.TryOffset + clause.TryLength) {
                            // A callee can mutate ref/out storage and then throw.
                            // Include both pre-call and post-call states in handlers.
                            foreach (var state in new[] { before, next }) {
                                var handler = state.Copy(); handler.stack.Clear();
                                if (clause.Flags == ExceptionHandlingClauseOptions.Clause || clause.Flags == ExceptionHandlingClauseOptions.Filter) handler.stack.Add(Value.Unknown);
                                this.Merge(this.offsets[clause.Flags == ExceptionHandlingClauseOptions.Filter ? clause.FilterOffset : clause.HandlerOffset], handler);
                            }
                        }
                        if (clause.Flags == ExceptionHandlingClauseOptions.Filter && instruction.OpCode == OpCodes.Endfilter &&
                            instruction.Offset >= clause.FilterOffset && instruction.Offset < clause.HandlerOffset) {
                            var handler = next.Copy(); handler.stack.Clear(); handler.stack.Add(Value.Unknown);
                            this.Merge(this.offsets[clause.HandlerOffset], handler);
                        }
                    }
                    if (instruction.Operand is Instruction branch) this.Merge(this.offsets[branch.Offset], next);
                    if (instruction.Operand is Instruction[] branches) foreach (var target in branches) this.Merge(this.offsets[target.Offset], next);
                    if (instruction.OpCode == OpCodes.Endfinally)
                        foreach (var leave in this.instructions.Where(item => item.OpCode == OpCodes.Leave || item.OpCode == OpCodes.Leave_S))
                            this.Merge(this.offsets[((Instruction)leave.Operand).Offset], next);
                    if (index + 1 < this.instructions.Length && instruction.OpCode.FlowControl != FlowControl.Branch &&
                        instruction.OpCode.FlowControl != FlowControl.Return && instruction.OpCode.FlowControl != FlowControl.Throw)
                        this.Merge(index + 1, next);
                }
                foreach (var call in this.calls) {
                    var value = call.Value;
                    this.result.sites[call.Key] = new Invocation(value.delegates.OrderBy(target => target.DeclaringType.AssemblyQualifiedName, StringComparer.Ordinal)
                        .ThenBy(target => target.ToString(), StringComparer.Ordinal).ToArray(), !value.unknown && value.pointers.Length == 0 && value.addresses.Length == 0);
                }
            }

            private int Variable(Instruction instruction, bool argument) {
                var name = instruction.OpCode.Name; var last = name[name.Length - 1];
                var slot = last >= '0' && last <= '3' ? last - '0' : argument
                    ? ((ParameterInfo)instruction.Operand).Position + (this.method.IsStatic ? 0 : 1) : ((LocalVariableInfo)instruction.Operand).LocalIndex;
                return argument ? slot : this.argumentCount + slot;
            }

            private void Step(int index, Frame frame) {
                var instruction = this.instructions[index]; var op = instruction.OpCode; var name = op.Name;
                if (name.StartsWith("ldloc", StringComparison.Ordinal) || name.StartsWith("ldarg", StringComparison.Ordinal)) {
                    var slot = this.Variable(instruction, name.StartsWith("ldarg", StringComparison.Ordinal));
                    frame.stack.Add(name.StartsWith("ldloca", StringComparison.Ordinal) || name.StartsWith("ldarga", StringComparison.Ordinal)
                        ? new Value { addresses = new[] { slot } } : frame.variables[slot]); return;
                }
                if (name.StartsWith("stloc", StringComparison.Ordinal) || name.StartsWith("starg", StringComparison.Ordinal)) {
                    var slot = this.Variable(instruction, name.StartsWith("starg", StringComparison.Ordinal));
                    var value = frame.Pop();
                    frame.variables[slot] = frame.escaped[slot] ? Value.Unknown : this.variableTypes[slot].IsByRef || this.variableTypes[slot].IsPointer ? value : frame.Read(value); return;
                }
                if (op == OpCodes.Dup) { var value = frame.Pop(); frame.stack.Add(value); frame.stack.Add(value); return; }
                if (op == OpCodes.Ldnull) { frame.stack.Add(Value.Empty); return; }
                if (op == OpCodes.Ldftn || op == OpCodes.Ldvirtftn) {
                    if (op == OpCodes.Ldvirtftn) frame.Pop();
                    var target = ILCallTargets.Resolve(this.instructions, index, out var exact) as MethodInfo;
                    frame.stack.Add(exact && target != null && !target.ContainsGenericParameters ? new Value { pointers = new[] { target } } : Value.Unknown); return;
                }
                if (op == OpCodes.Castclass || op == OpCodes.Isinst) return; // Successful casts retain the immutable delegate value; failure is null/throw.
                if (op == OpCodes.Ldobj || name.StartsWith("ldind", StringComparison.Ordinal)) { frame.stack.Add(frame.Read(frame.Pop())); return; }
                if (op == OpCodes.Stobj || name.StartsWith("stind", StringComparison.Ordinal)) { var value = frame.Read(frame.Pop()); frame.Write(frame.Pop(), value); return; }
                if (op == OpCodes.Initobj) { frame.Write(frame.Pop(), Value.Empty); return; }
                if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj) {
                    if (!(instruction.Operand is MethodBase called)) throw new InvalidOperationException("Unresolved delegate IL call");
                    var parameters = called.GetParameters(); var args = new Value[parameters.Length];
                    for (var argument = args.Length - 1; argument >= 0; --argument) args[argument] = frame.Pop();
                    var receiver = !called.IsStatic && op != OpCodes.Newobj ? frame.Pop() : Value.Unknown;
                    if (IsInvoke(called)) {
                        var value = frame.Read(receiver);
                        this.calls[instruction.Offset] = this.calls.TryGetValue(instruction.Offset, out var prior) ? prior.Union(value) : value;
                    }
                    var returns = Value.Unknown;
                    if (op == OpCodes.Newobj && typeof(MulticastDelegate).IsAssignableFrom(called.DeclaringType) &&
                        parameters.Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(object), typeof(IntPtr) })) {
                        var pointer = frame.Read(args[1]);
                        if (!pointer.unknown && pointer.pointers.Length > 0 && pointer.delegates.Length == 0 && !pointer.pointers.Any(IsInvoke))
                            returns = new Value { delegates = pointer.pointers };
                    } else {
                        foreach (var value in args.Where(value => value.addresses.Length > 0)) frame.Escape(value);
                        for (var argument = 0; argument < args.Length; ++argument)
                            // External managed ref parameters cannot alias a new
                            // local in this activation unless that local's address
                            // escaped (handled above). Native pointers have no such
                            // storage boundary and invalidate all local facts.
                            if (parameters[argument].ParameterType.IsPointer ||
                                parameters[argument].ParameterType == typeof(IntPtr) || parameters[argument].ParameterType == typeof(UIntPtr)) frame.Escape(args[argument]);
                        if (receiver.addresses.Length > 0) frame.Escape(receiver);
                    }
                    if (op == OpCodes.Newobj || called is MethodInfo method && method.ReturnType != typeof(void)) frame.stack.Add(returns);
                    return;
                }
                if (op == OpCodes.Leave || op == OpCodes.Leave_S) { frame.stack.Clear(); return; }
                if (op == OpCodes.Ret) { if (this.method is MethodInfo method && method.ReturnType != typeof(void)) frame.Pop(); return; }
                if (op.StackBehaviourPop == StackBehaviour.Varpop || op.StackBehaviourPush == StackBehaviour.Varpush)
                    throw new InvalidOperationException("Unsupported delegate IL stack effect");
                var pop = op.StackBehaviourPop == StackBehaviour.Pop0 ? 0 : op.StackBehaviourPop.ToString().Split('_').Length;
                for (var count = 0; count < pop; ++count) {
                    var value = frame.Pop();
                    if (value.addresses.Length > 0 && op != OpCodes.Pop) frame.Escape(value);
                }
                var push = op.StackBehaviourPush == StackBehaviour.Push0 ? 0 : op.StackBehaviourPush == StackBehaviour.Push1_push1 ? 2 : 1;
                for (var count = 0; count < push; ++count) frame.stack.Add(Value.Unknown);
            }
        }
    }
}
