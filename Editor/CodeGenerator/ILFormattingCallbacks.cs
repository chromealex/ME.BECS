namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using ME.BECS.Mono.Reflection;

    // Positive callback inventory, not a whole-program dispatch/completeness proof.
    // Facts describe possible concrete values at a formatting site. Unknown objects
    // never turn into an enumeration of unrelated implementations or a proven leaf.
    internal sealed class ILFormattingCallbacks {
        private static readonly MethodInfo ObjectToString = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes);
        private sealed class Value {
            internal static readonly Value Empty = new Value();
            internal static readonly Value Unknown = new Value { unknown = true };
            internal Type[] types = Type.EmptyTypes;
            internal int[] arrays = Array.Empty<int>();
            internal int[] addresses = Array.Empty<int>();
            internal bool unknown;

            internal Value Union(Value other) {
                if (ReferenceEquals(this, other)) return this;
                var t = this.types.Union(other.types).ToArray();
                var a = this.arrays.Union(other.arrays).ToArray();
                var r = this.addresses.Union(other.addresses).ToArray();
                if (t.Length + a.Length + r.Length > 128) throw new FlowLimit();
                if (t.Length == this.types.Length && a.Length == this.arrays.Length && r.Length == this.addresses.Length &&
                    (!other.unknown || this.unknown)) return this;
                return new Value { types = t, arrays = a, addresses = r, unknown = this.unknown || other.unknown };
            }
        }

        private sealed class Frame {
            internal Value[] variables;
            internal List<Value> stack = new List<Value>();
            internal Frame Copy() => new Frame { variables = (Value[])this.variables.Clone(), stack = new List<Value>(this.stack) };
            internal Value Pop() {
                if (this.stack.Count == 0) throw new InvalidOperationException("Invalid formatting IL stack");
                var index = this.stack.Count - 1;
                var value = this.stack[index]; this.stack.RemoveAt(index); return value;
            }
            internal Value Read(Value value) {
                if (value.addresses.Length == 0) return value;
                var result = Value.Empty;
                foreach (var address in value.addresses) result = result.Union(this.variables[address]);
                return result;
            }
            internal void Write(Value address, Value value) {
                foreach (var index in address.addresses)
                    this.variables[index] = address.addresses.Length == 1 ? value : this.variables[index].Union(value);
            }
        }

        private sealed class FlowLimit : Exception { }
        private readonly MethodBase method;
        private readonly Instruction[] instructions;
        private readonly MethodBody body;
        private readonly Type[] variableTypes;
        private readonly int argumentCount;
        private readonly Frame[] frames;
        private readonly bool[] queued;
        private readonly Queue<int> pending = new Queue<int>();
        private readonly Dictionary<int, int> offsets;
        private readonly Dictionary<int, Value> arrayElements = new Dictionary<int, Value>();
        private readonly HashSet<MethodInfo> callbacks = new HashSet<MethodInfo>();
        private bool unresolved;
        private int work;

        private static bool Formatting(MethodInfo method) => method.DeclaringType == typeof(string) && method.IsStatic &&
            (method.Name == nameof(string.Format) || method.Name == nameof(string.Concat)) && method.ReturnType == typeof(string);

        internal static bool IsFormattingCall(MethodInfo method) => Formatting(method) || method == ObjectToString;

        internal static MethodInfo[] Collect(MethodBase method, Instruction[] instructions, out bool unresolved) {
            var cached = ILAnalysisSession.Get((typeof(ILFormattingCallbacks), method, instructions), () => {
                var callbacks = CollectCore(method, instructions, out var unknown);
                return (callbacks, unknown);
            });
            unresolved = cached.unknown;
            return cached.callbacks;
        }

        private static MethodInfo[] CollectCore(MethodBase method, Instruction[] instructions, out bool unresolved) {
            unresolved = false;
            if (!instructions.Any(instruction => instruction.Operand is MethodInfo target && IsFormattingCall(target))) return Array.Empty<MethodInfo>();
            var analysis = new ILFormattingCallbacks(method, instructions);
            try { analysis.Run(); }
            catch (FlowLimit) { throw new InvalidOperationException("Scheduled-job formatting IL traversal limit exceeded: " + method); }
            catch (InvalidOperationException) { analysis.unresolved = true; }
            unresolved = analysis.unresolved;
            return analysis.callbacks.OrderBy(callback => callback.DeclaringType.AssemblyQualifiedName, StringComparer.Ordinal)
                .ThenBy(callback => callback.ToString(), StringComparer.Ordinal).ToArray();
        }

        private ILFormattingCallbacks(MethodBase method, Instruction[] instructions) {
            this.method = method; this.instructions = instructions; this.body = method.GetMethodBody();
            var arguments = (method.IsStatic ? Type.EmptyTypes : new[] { method.DeclaringType })
                .Concat(method.GetParameters().Select(parameter => parameter.ParameterType)).ToArray();
            this.argumentCount = arguments.Length;
            this.variableTypes = arguments.Concat(this.body.LocalVariables.Select(local => local.LocalType)).ToArray();
            this.frames = new Frame[instructions.Length]; this.queued = new bool[instructions.Length];
            this.offsets = instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(item => item.Offset, item => item.index);
        }

        private static Value Typed(Type type, bool exact = false) {
            if (type == null) return Value.Unknown;
            if (type.IsByRef) type = type.GetElementType();
            if (type.ContainsGenericParameters || type.IsPointer || !exact && !type.IsSealed && !type.IsValueType) return Value.Unknown;
            return new Value { types = new[] { type } };
        }

        private void Queue(int index) { if (!this.queued[index]) { this.queued[index] = true; this.pending.Enqueue(index); } }
        private void Merge(int index, Frame value) {
            var frame = this.frames[index];
            if (frame == null) { this.frames[index] = value.Copy(); this.Queue(index); return; }
            if (frame.stack.Count != value.stack.Count) throw new InvalidOperationException("Inconsistent formatting IL stack");
            var changed = false;
            for (var slot = 0; slot < frame.variables.Length; ++slot) {
                var merged = frame.variables[slot].Union(value.variables[slot]);
                changed |= !ReferenceEquals(merged, frame.variables[slot]); frame.variables[slot] = merged;
            }
            for (var slot = 0; slot < frame.stack.Count; ++slot) {
                var merged = frame.stack[slot].Union(value.stack[slot]);
                changed |= !ReferenceEquals(merged, frame.stack[slot]); frame.stack[slot] = merged;
            }
            if (changed) this.Queue(index);
        }

        private void Run() {
            if (this.instructions.Length == 0) return;
            this.Merge(0, new Frame { variables = this.variableTypes.Select((type, index) =>
                index < this.argumentCount || type.IsValueType ? Typed(type) : Value.Empty).ToArray() });
            while (this.pending.Count > 0) {
                if ((this.work & 255) == 0) CodeGeneratorTimings.Work(this.method);
                if (++this.work > 200000) throw new FlowLimit();
                var index = this.pending.Dequeue(); this.queued[index] = false;
                var before = this.frames[index]; var next = before.Copy();
                var instruction = this.instructions[index];
                this.Step(index, next);
                foreach (var clause in this.body.ExceptionHandlingClauses) {
                    if (instruction.Offset >= clause.TryOffset && instruction.Offset < clause.TryOffset + clause.TryLength) {
                        var handler = before.Copy(); handler.stack.Clear();
                        if (clause.Flags == ExceptionHandlingClauseOptions.Clause || clause.Flags == ExceptionHandlingClauseOptions.Filter)
                            handler.stack.Add(Value.Unknown);
                        this.Merge(this.offsets[clause.Flags == ExceptionHandlingClauseOptions.Filter ? clause.FilterOffset : clause.HandlerOffset], handler);
                    }
                    if (clause.Flags == ExceptionHandlingClauseOptions.Filter && instruction.OpCode == OpCodes.Endfilter &&
                        instruction.Offset >= clause.FilterOffset && instruction.Offset < clause.HandlerOffset) {
                        var handler = next.Copy(); handler.stack.Clear(); handler.stack.Add(Value.Unknown);
                        this.Merge(this.offsets[clause.HandlerOffset], handler);
                    }
                }
                if (instruction.Operand is Instruction branch) this.Merge(this.offsets[branch.Offset], next);
                if (instruction.Operand is Instruction[] branches) foreach (var branchTarget in branches) this.Merge(this.offsets[branchTarget.Offset], next);
                if (instruction.OpCode == OpCodes.Endfinally) {
                    // Inventory may include several leave continuations, not claim
                    // that one particular exceptional path actually executes them.
                    foreach (var leave in this.instructions.Where(item => item.OpCode == OpCodes.Leave || item.OpCode == OpCodes.Leave_S))
                        this.Merge(this.offsets[((Instruction)leave.Operand).Offset], next);
                }
                if (index + 1 < this.instructions.Length && instruction.OpCode.FlowControl != FlowControl.Branch &&
                    instruction.OpCode.FlowControl != FlowControl.Return && instruction.OpCode.FlowControl != FlowControl.Throw &&
                    instruction.OpCode != OpCodes.Jmp) this.Merge(index + 1, next);
            }
        }

        private int Variable(Instruction instruction, bool argument) {
            var name = instruction.OpCode.Name;
            var last = name[name.Length - 1];
            var slot = last >= '0' && last <= '3' ? last - '0' : argument
                ? ((ParameterInfo)instruction.Operand).Position + (this.method.IsStatic ? 0 : 1)
                : ((LocalVariableInfo)instruction.Operand).LocalIndex;
            return argument ? slot : this.argumentCount + slot;
        }

        private Value Elements(Value array) {
            var result = Value.Empty;
            foreach (var site in array.arrays) if (this.arrayElements.TryGetValue(site, out var elements)) result = result.Union(elements);
            foreach (var type in array.types.Where(type => type.IsArray)) result = result.Union(Typed(type.GetElementType()));
            return array.unknown ? result.Union(Value.Unknown) : result;
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
                var value = frame.Pop(); frame.variables[slot] = this.variableTypes[slot].IsByRef ? value : frame.Read(value); return;
            }
            if (op == OpCodes.Dup) { var value = frame.Pop(); frame.stack.Add(value); frame.stack.Add(value); return; }
            if (op == OpCodes.Ldnull) { frame.stack.Add(Value.Empty); return; }
            if (op == OpCodes.Ldstr) { frame.stack.Add(Typed(typeof(string))); return; }
            if (op == OpCodes.Initobj) { var address = frame.Pop(); var type = (Type)instruction.Operand; frame.Write(address, type.IsValueType ? Typed(type) : Value.Empty); return; }
            if (op == OpCodes.Ldobj || name.StartsWith("ldind", StringComparison.Ordinal)) { frame.stack.Add(frame.Read(frame.Pop())); return; }
            if (op == OpCodes.Stobj || name.StartsWith("stind", StringComparison.Ordinal)) { var value = frame.Read(frame.Pop()); frame.Write(frame.Pop(), value); return; }
            if (op == OpCodes.Box) {
                frame.Pop(); var type = (Type)instruction.Operand;
                frame.stack.Add(Typed(Nullable.GetUnderlyingType(type) ?? type)); return;
            }
            if (op == OpCodes.Unbox || op == OpCodes.Unbox_Any || op == OpCodes.Castclass || op == OpCodes.Isinst) {
                var value = frame.Read(frame.Pop()); var declared = Typed((Type)instruction.Operand);
                frame.stack.Add(declared.unknown ? value : declared); return;
            }
            if (op == OpCodes.Ldfld || op == OpCodes.Ldflda || op == OpCodes.Ldsfld || op == OpCodes.Ldsflda) {
                if (op == OpCodes.Ldfld || op == OpCodes.Ldflda) frame.Pop();
                frame.stack.Add(Typed(((FieldInfo)instruction.Operand).FieldType)); return;
            }
            if (op == OpCodes.Newarr) {
                frame.Pop(); frame.stack.Add(new Value { arrays = new[] { index }, types = new[] { ((Type)instruction.Operand).MakeArrayType() } }); return;
            }
            if (name.StartsWith("stelem", StringComparison.Ordinal)) {
                var value = frame.Read(frame.Pop()); frame.Pop(); var array = frame.Read(frame.Pop());
                foreach (var site in array.arrays) {
                    if (!this.arrayElements.TryGetValue(site, out var previous)) previous = Value.Empty;
                    var merged = previous.Union(value);
                    if (ReferenceEquals(previous, merged)) continue;
                    this.arrayElements[site] = merged;
                    for (var node = 0; node < this.frames.Length; ++node) if (this.frames[node] != null) this.Queue(node);
                }
                return;
            }
            if (name.StartsWith("ldelem", StringComparison.Ordinal)) { frame.Pop(); frame.stack.Add(this.Elements(frame.Read(frame.Pop()))); return; }
            if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj) {
                if (!(instruction.Operand is MethodBase called)) throw new InvalidOperationException("Unresolved formatting IL call");
                var parameters = called.GetParameters(); var args = new Value[parameters.Length];
                for (var argument = args.Length - 1; argument >= 0; --argument) args[argument] = frame.Pop();
                var receiver = !called.IsStatic && op != OpCodes.Newobj ? frame.Pop() : Value.Empty;
                if (called is MethodInfo method) {
                    if (Formatting(method)) {
                        for (var argument = 0; argument < args.Length; ++argument) {
                            var type = parameters[argument].ParameterType; var value = frame.Read(args[argument]);
                            if (type == typeof(object[])) value = this.Elements(value);
                            else if (type != typeof(object)) continue;
                            this.Format(value, method.Name == nameof(string.Format));
                        }
                    } else if (op == OpCodes.Callvirt && method == ObjectToString) this.Format(frame.Read(receiver), false);
                }
                // Do not retain a concrete reference across an unknown ref/out write.
                for (var argument = 0; argument < args.Length; ++argument)
                    if (parameters[argument].ParameterType.IsByRef && !parameters[argument].IsIn) frame.Write(args[argument], Typed(parameters[argument].ParameterType));
                if (op == OpCodes.Newobj) frame.stack.Add(Typed(called.DeclaringType, exact: true));
                else if (called is MethodInfo result && result.ReturnType != typeof(void)) frame.stack.Add(Typed(result.ReturnType));
                return;
            }
            if (op == OpCodes.Leave || op == OpCodes.Leave_S) { frame.stack.Clear(); return; }
            if (op == OpCodes.Ret) { if (this.method is MethodInfo result && result.ReturnType != typeof(void)) frame.Pop(); return; }
            if (op.StackBehaviourPop == StackBehaviour.Varpop || op.StackBehaviourPush == StackBehaviour.Varpush) throw new InvalidOperationException("Unsupported formatting IL stack effect");
            var pop = op.StackBehaviourPop == StackBehaviour.Pop0 ? 0 : op.StackBehaviourPop.ToString().Split('_').Length;
            for (var count = 0; count < pop; ++count) frame.Pop();
            var push = op.StackBehaviourPush == StackBehaviour.Push0 ? 0 : op.StackBehaviourPush == StackBehaviour.Push1_push1 ? 2 : 1;
            for (var count = 0; count < push; ++count) frame.stack.Add(Value.Unknown);
        }

        private void Format(Value value, bool composite) {
            this.unresolved |= value.unknown;
            foreach (var type in value.types) {
                if (composite && typeof(IFormattable).IsAssignableFrom(type)) {
                    var map = type.GetInterfaceMap(typeof(IFormattable));
                    for (var index = 0; index < map.InterfaceMethods.Length; ++index)
                        if (map.InterfaceMethods[index].Name == nameof(ToString)) this.callbacks.Add(map.TargetMethods[index]);
                } else {
                    var target = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .SingleOrDefault(method => method.IsVirtual && method.GetBaseDefinition() == ObjectToString);
                    if (target != null) this.callbacks.Add(target);
                }
            }
        }
    }
}
