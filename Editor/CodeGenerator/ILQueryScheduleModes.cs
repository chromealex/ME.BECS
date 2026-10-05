namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using ME.BECS.Mono.Reflection;

    // Query/boolean value flow over an independently collected IL body inventory.
    // Unknown helpers do not prove readonly. Not a synchronization/coverage proof.
    internal sealed class ILQueryScheduleModes {
        [Flags]
        internal enum Mode : byte { Normal = 1, Readonly = 2, Unknown = 4 }
        private static readonly HashSet<string> PreservesMode = new HashSet<string>(StringComparer.Ordinal) {
            "With", "Without", "WithAny", "WithAll", "WithAspect", "AsUnsafe", "AsParallel", "ParallelFor", "AsJob", "WithBurst", "Step", "Sort", "WaitForAllJobs",
        };

        private sealed class Value {
            internal static readonly Value Unknown = new Value { mode = Mode.Unknown };
            internal static readonly Value Normal = new Value { mode = Mode.Normal };
            internal static readonly Value Readonly = new Value { mode = Mode.Readonly };
            internal Mode mode;
            internal int[] addresses = Array.Empty<int>();

            internal Value Union(Value other) {
                if (ReferenceEquals(this, other)) return this;
                var mode = this.mode | other.mode;
                if (other.addresses.Length == 0 && mode == this.mode) return this;
                if (this.addresses.Length == 0 && mode == other.mode) return other;
                var addresses = this.addresses.Union(other.addresses).ToArray();
                if (addresses.Length > 128) throw new InvalidOperationException("Query address flow limit exceeded");
                return mode == this.mode && addresses.Length == this.addresses.Length ? this : new Value { mode = mode, addresses = addresses };
            }
        }

        private sealed class Frame {
            internal Value[] variables;
            internal int[] externalCells;
            internal List<Value> stack = new List<Value>();
            internal Frame Copy() => new Frame { variables = (Value[])this.variables.Clone(), externalCells = this.externalCells, stack = new List<Value>(this.stack) };
            internal Value Pop() {
                if (this.stack.Count == 0) throw new InvalidOperationException("Invalid query IL stack");
                var index = this.stack.Count - 1;
                var value = this.stack[index]; this.stack.RemoveAt(index); return value;
            }
            internal Value Read(Value value) => value.addresses.Length == 0 ? value : this.Read(value, new HashSet<int>());
            private Value Read(Value value, HashSet<int> active) {
                if (value.addresses.Length == 0) return value;
                var result = new Value { mode = value.mode };
                foreach (var address in value.addresses) {
                    if (!active.Add(address)) return Value.Unknown;
                    result = result.Union(this.Read(this.variables[address], active));
                    active.Remove(address);
                }
                return result;
            }
            internal void Write(Value address, Value value) {
                // Addresses from unknown ref returns/pointers may alias tracked
                // storage. Never retain a readonly fact across such a write.
                if (address.addresses.Length == 0 || address.mode != 0) {
                    for (var index = 0; index < this.variables.Length; ++index)
                        this.variables[index] = this.variables[index].Union(Value.Unknown);
                    return;
                }
                foreach (var index in address.addresses)
                    this.variables[index] = address.addresses.Length == 1 ? value : this.variables[index].Union(value);
                // Distinct ref parameters are not necessarily distinct storage.
                // Until caller aliases are bound, writes weakly affect the other
                // external query/bool cells as well (local copies stay independent).
                if (this.externalCells.Length != 0 && address.addresses.Any(index => Array.IndexOf(this.externalCells, index) >= 0))
                    foreach (var index in this.externalCells)
                        if (Array.IndexOf(address.addresses, index) < 0) this.variables[index] = this.variables[index].Union(value);
            }
        }

        private sealed class Program {
            internal readonly IReadOnlyDictionary<MethodBase, Instruction[]> bodies;
            internal readonly Dictionary<MethodBase, Dictionary<int, Mode>> schedules = new Dictionary<MethodBase, Dictionary<int, Mode>>();
            private readonly HashSet<MethodBase> active = new HashSet<MethodBase>();
            private readonly HashSet<MethodBase> unbound = new HashSet<MethodBase>();
            private readonly HashSet<MethodBase> unboundRead = new HashSet<MethodBase>();
            private int work, calls;

            internal Program(IReadOnlyDictionary<MethodBase, Instruction[]> bodies) {
                this.bodies = bodies;
                foreach (var instructions in bodies.Values)
                    foreach (var instruction in instructions)
                        if (instruction.Operand is MethodBase target && bodies.ContainsKey(target) &&
                            instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt && instruction.OpCode != OpCodes.Newobj)
                            // Taking a method address can expose it to another
                            // caller even if a direct invocation was already bound.
                            this.unbound.Add(target);
            }
            internal void Spend() {
                if ((++this.work & 255) == 0) CodeGeneratorTimings.Work(null);
                if (this.work > 200000) throw new InvalidOperationException("Query program flow limit exceeded");
            }

            internal ILQueryScheduleModes Invoke(MethodBase method, Frame caller = null, Value[] arguments = null) {
                if (++this.calls > 10000 || this.active.Count >= 128 || !this.active.Add(method))
                    throw new InvalidOperationException("Recursive or excessive query helper expansion");
                try {
                    var flow = new ILQueryScheduleModes(method, this.bodies[method], this, caller, arguments);
                    flow.Run();
                    if (!this.schedules.TryGetValue(method, out var modes))
                        this.schedules.Add(method, modes = new Dictionary<int, Mode>());
                    foreach (var site in flow.schedules) {
                        modes.TryGetValue(site.Key, out var previous);
                        modes[site.Key] = previous | site.Value;
                    }
                    return flow;
                } finally { this.active.Remove(method); }
            }

            internal bool CanInvoke(MethodBase method, bool exactDispatch) {
                if (!this.bodies.ContainsKey(method) || method.ContainsGenericParameters) return false;
                if (!exactDispatch) {
                    // This body's effects may still be collected by the legacy
                    // inventory, but the named virtual slot isn't a proven target.
                    this.unbound.Add(method);
                    return false;
                }
                return true;
            }

            internal void CompleteInventory() {
                // Include bodies reached only through conservative inventory
                // edges (e.g. method addresses), without inventing caller values.
                foreach (var method in this.bodies.Keys)
                    if (!this.schedules.ContainsKey(method)) this.unbound.Add(method);
                while (true) {
                    var method = this.unbound.FirstOrDefault(candidate => !this.unboundRead.Contains(candidate));
                    if (method == null) break;
                    this.unboundRead.Add(method);
                    var instructions = this.bodies[method];
                    if (Sites(instructions).Length == 0) {
                        // An unbound entry has no caller values to preserve. If
                        // it has no schedules, propagate unknown entry contexts
                        // to its callees without interpreting unrelated setup
                        // (e.g. static reflection caches). Revisit already-bound
                        // callees too: an additional entry must not keep only
                        // the first caller's readonly facts.
                        for (var index = 0; index < instructions.Length; ++index) {
                            var target = ILCallTargets.Resolve(instructions, index);
                            if (ILCallTargets.TryConstruction(target, out _, out var constructor)) target = constructor;
                            if (target != null && this.bodies.ContainsKey(target)) this.unbound.Add(target);
                        }
                        if (!this.schedules.ContainsKey(method)) this.schedules.Add(method, new Dictionary<int, Mode>());
                        continue;
                    }
                    this.Invoke(method);
                }
            }
        }

        private readonly MethodBase method;
        private readonly Instruction[] instructions;
        private readonly MethodBody body;
        private readonly ExceptionHandlingClause[] clauses;
        private readonly int[] leaveTargets;
        private readonly Type[] variableTypes;
        private readonly int argumentCount;
        private readonly int externalStart;
        private readonly int baseSlot;
        private readonly Program program;
        private readonly Frame caller;
        private readonly Value[] arguments;
        private readonly Dictionary<int, int> offsets;
        private readonly Frame[] frames;
        private readonly bool[] queued;
        private readonly Queue<int> pending = new Queue<int>();
        private readonly Dictionary<int, Mode> schedules = new Dictionary<int, Mode>();
        private int work;
        private Frame normalExit, allEffects, stepException;
        private Value returnValue;

        internal static Dictionary<MethodInfo, Dictionary<int, Mode>> ReadGraph(MethodInfo root, IReadOnlyDictionary<MethodInfo, Instruction[]> bodies) {
            return ReadBodyGraph(root, bodies.ToDictionary(body => (MethodBase)body.Key, body => body.Value))
                .ToDictionary(body => (MethodInfo)body.Key, body => body.Value);
        }

        internal static Dictionary<MethodBase, Dictionary<int, Mode>> ReadBodyGraph(MethodBase root, IReadOnlyDictionary<MethodBase, Instruction[]> bodies) {
            try {
                var program = new Program(bodies);
                program.Invoke(root);
                program.CompleteInventory();
                foreach (var body in bodies)
                    foreach (var site in Sites(body.Value))
                        if (!program.schedules[body.Key].ContainsKey(site)) program.schedules[body.Key].Add(site, Mode.Unknown);
                return program.schedules;
            } catch (InvalidOperationException) {
                // A failed context invalidates the entire program's precise
                // facts, including nested calls visited before the failure.
                return bodies.ToDictionary(body => body.Key, body => Sites(body.Value).ToDictionary(site => site, _ => Mode.Unknown));
            }
        }

        private static int[] Sites(Instruction[] instructions) => instructions.Where(instruction => instruction.Operand is MethodInfo call &&
            SourceGeneratorScheduledJobsValidation.IsSchedulingMethod(call)).Select(instruction => instruction.Offset).ToArray();

        internal static Dictionary<int, Mode> Read(MethodBase method, Instruction[] instructions) {
            var sites = Sites(instructions);
            if (sites.Length == 0) return new Dictionary<int, Mode>();
            try {
                var flow = new ILQueryScheduleModes(method, instructions);
                flow.Run();
                // Dead/unvisited IL must not accidentally obtain a readonly fact
                // when another consumer conservatively scans all instructions.
                foreach (var site in sites) if (!flow.schedules.ContainsKey(site)) flow.schedules.Add(site, Mode.Unknown);
                return flow.schedules;
            } catch (InvalidOperationException) {
                // No partial precise result after unsupported IL or exhausted work.
                return sites.ToDictionary(site => site, _ => Mode.Unknown);
            }
        }

        internal static RefOp Apply(Mode mode, RefOp access, bool argument) {
            if (!argument) return access;
            if (mode == Mode.Readonly) return RefOp.ReadOnly;
            // A WriteOnly argument on a possibly-readonly query has both kinds of
            // access, not merely a possible write. Preserve that union explicitly.
            if ((mode & (Mode.Readonly | Mode.Unknown)) != 0 && access == RefOp.WriteOnly) return RefOp.ReadWrite;
            return access;
        }

        private ILQueryScheduleModes(MethodBase method, Instruction[] instructions, Program program = null, Frame caller = null, Value[] arguments = null) {
            this.method = method; this.instructions = instructions;
            this.program = program; this.caller = caller; this.arguments = arguments;
            this.baseSlot = caller?.variables.Length ?? 0;
            this.body = method.GetMethodBody() ?? throw new InvalidOperationException("Missing query IL body");
            var parameters = (method.IsStatic ? Type.EmptyTypes : new[] { method.DeclaringType.IsValueType ? method.DeclaringType.MakeByRefType() : method.DeclaringType })
                .Concat(method.GetParameters().Select(parameter => parameter.ParameterType)).ToArray();
            this.argumentCount = parameters.Length;
            this.externalStart = this.argumentCount + this.body.LocalVariables.Count;
            this.variableTypes = parameters.Concat(this.body.LocalVariables.Select(local => local.LocalType))
                .Concat(caller == null ? parameters.Where(type => type.IsByRef).Select(type => type.GetElementType()) : Type.EmptyTypes).ToArray();
            if (this.baseSlot + this.variableTypes.Length > 65536) throw new InvalidOperationException("Query storage limit exceeded");
            this.frames = new Frame[instructions.Length]; this.queued = new bool[instructions.Length];
            this.offsets = instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(item => item.Offset, item => item.index);
            this.clauses = this.body.ExceptionHandlingClauses.Cast<ExceptionHandlingClause>().ToArray();
            this.leaveTargets = instructions.Where(instruction => instruction.OpCode == OpCodes.Leave || instruction.OpCode == OpCodes.Leave_S)
                .Select(instruction => this.offsets[((Instruction)instruction.Operand).Offset]).Distinct().ToArray();
        }

        private static bool Query(Type type) {
            if (type.IsByRef) type = type.GetElementType();
            return type == typeof(QueryBuilder) || type.Assembly == typeof(QueryBuilder).Assembly && type.FullName == "ME.BECS.QueryBuilderDisposable";
        }
        private static Value Default(Type type) => !type.IsByRef && (Query(type) || type == typeof(bool)) ? Value.Normal : Value.Unknown;
        private static void AccumulateStorage(ref Frame result, Frame frame) {
            if (result == null) { result = frame.Copy(); result.stack.Clear(); return; }
            for (var index = 0; index < result.variables.Length; ++index)
                result.variables[index] = result.variables[index].Union(frame.variables[index]);
        }
        private void Queue(int index) { if (!this.queued[index]) { this.queued[index] = true; this.pending.Enqueue(index); } }
        private void Merge(int index, Frame next) {
            var previous = this.frames[index];
            if (previous == null) { this.frames[index] = next.Copy(); this.Queue(index); return; }
            if (previous.stack.Count != next.stack.Count) throw new InvalidOperationException("Inconsistent query IL stack");
            var changed = false;
            for (var slot = 0; slot < previous.variables.Length; ++slot) {
                var merged = previous.variables[slot].Union(next.variables[slot]);
                changed |= !ReferenceEquals(merged, previous.variables[slot]); previous.variables[slot] = merged;
            }
            for (var slot = 0; slot < previous.stack.Count; ++slot) {
                var merged = previous.stack[slot].Union(next.stack[slot]);
                changed |= !ReferenceEquals(merged, previous.stack[slot]); previous.stack[slot] = merged;
            }
            if (changed) this.Queue(index);
        }

        private void Run() {
            if (this.instructions.Length == 0) return;
            var entry = new Frame { variables = this.variableTypes.Select((type, index) =>
                index < this.argumentCount || index >= this.externalStart || !this.body.InitLocals ? Value.Unknown : Default(type)).ToArray() };
            if (this.caller == null) {
                var cell = this.externalStart;
                for (var index = 0; index < this.argumentCount; ++index)
                    if (this.variableTypes[index].IsByRef) entry.variables[index] = new Value { addresses = new[] { cell++ } };
                entry.externalCells = Enumerable.Range(this.externalStart, this.variableTypes.Length - this.externalStart)
                    .Where(index => Query(this.variableTypes[index]) || this.variableTypes[index] == typeof(bool)).ToArray();
            } else {
                if (this.arguments.Length != this.argumentCount) throw new InvalidOperationException("Invalid query helper arguments");
                // Shared addresses preserve aliases, including two ref parameters
                // bound to one cell; by-value parameters receive independent copies.
                entry.variables = this.caller.variables.Concat(entry.variables).ToArray();
                entry.externalCells = this.caller.externalCells;
                for (var index = 0; index < this.argumentCount; ++index)
                    entry.variables[this.baseSlot + index] = this.variableTypes[index].IsByRef ? this.arguments[index] : this.caller.Read(this.arguments[index]);
            }
            this.Merge(0, entry);
            while (this.pending.Count > 0) {
                if (++this.work > 200000) throw new InvalidOperationException("Query IL flow limit exceeded");
                this.program?.Spend();
                var index = this.pending.Dequeue(); this.queued[index] = false;
                var before = this.frames[index]; var next = before.Copy(); var instruction = this.instructions[index];
                var branchChoice = BranchChoice(instruction, before);
                this.stepException = null;
                AccumulateStorage(ref this.allEffects, before);
                this.Step(instruction, next);
                AccumulateStorage(ref this.allEffects, next);
                if (this.stepException != null) AccumulateStorage(ref this.allEffects, this.stepException);
                // Conservatively include both pre- and post-call storage when a
                // helper can mutate ref arguments and then throw.
                foreach (var clause in this.clauses) {
                    if (instruction.Offset >= clause.TryOffset && instruction.Offset < clause.TryOffset + clause.TryLength) {
                        var handler = before.Copy(); handler.stack.Clear();
                        for (var slot = 0; slot < handler.variables.Length; ++slot)
                            handler.variables[slot] = handler.variables[slot].Union(next.variables[slot]).Union(this.stepException?.variables[slot] ?? next.variables[slot]);
                        if (clause.Flags == ExceptionHandlingClauseOptions.Clause || clause.Flags == ExceptionHandlingClauseOptions.Filter)
                            handler.stack.Add(Value.Unknown);
                        this.Merge(this.offsets[clause.Flags == ExceptionHandlingClauseOptions.Filter ? clause.FilterOffset : clause.HandlerOffset], handler);
                    }
                    if (clause.Flags == ExceptionHandlingClauseOptions.Filter && instruction.OpCode == OpCodes.Endfilter &&
                        instruction.Offset >= clause.FilterOffset && instruction.Offset < clause.HandlerOffset) {
                        var handler = next.Copy(); handler.stack.Clear(); handler.stack.Add(Value.Unknown);
                        this.Merge(this.offsets[clause.HandlerOffset], handler);
                    }
                    if (clause.Flags == ExceptionHandlingClauseOptions.Filter &&
                        instruction.Offset >= clause.FilterOffset && instruction.Offset < clause.HandlerOffset) {
                        // Filters run during first-pass search, before protected
                        // finally/fault cleanup. A false/throwing filter can mutate
                        // locals/ref arguments seen by the next handler search.
                        // Include pre/post-call effects: a callee may write then throw.
                        var effects = before.Copy(); effects.stack.Clear();
                        for (var slot = 0; slot < effects.variables.Length; ++slot)
                            effects.variables[slot] = effects.variables[slot].Union(next.variables[slot])
                                .Union(this.stepException?.variables[slot] ?? next.variables[slot]);
                        this.PropagateFilterEffects(clause, effects);
                    }
                }
                if (branchChoice >= 0 && instruction.Operand is Instruction branch) this.Merge(this.offsets[branch.Offset], next);
                if (instruction.Operand is Instruction[] branches) foreach (var target in branches) this.Merge(this.offsets[target.Offset], next);
                if (instruction.OpCode == OpCodes.Endfinally) {
                    foreach (var target in this.leaveTargets) this.Merge(target, next);
                }
                if (branchChoice != 1 && index + 1 < this.instructions.Length && instruction.OpCode.FlowControl != FlowControl.Branch &&
                    instruction.OpCode.FlowControl != FlowControl.Return && instruction.OpCode.FlowControl != FlowControl.Throw &&
                    instruction.OpCode != OpCodes.Jmp) this.Merge(index + 1, next);
            }
        }

        private void PropagateFilterEffects(ExceptionHandlingClause filter, Frame effects) {
            var afterFilter = false;
            foreach (var target in this.clauses) {
                if (ReferenceEquals(target, filter)) { afterFilter = true; continue; }
                var sameRegion = target.TryOffset == filter.TryOffset && target.TryLength == filter.TryLength;
                var outerRegion = !sameRegion && target.TryOffset <= filter.TryOffset &&
                    target.TryOffset + target.TryLength >= filter.TryOffset + filter.TryLength;
                var cleanup = target.Flags == ExceptionHandlingClauseOptions.Finally || target.Flags == ExceptionHandlingClauseOptions.Fault;
                // Any nested cleanup may unwind after this search. Sibling
                // catch/filter clauses are ordered; outer handlers are possible
                // only after search continues outside the current protected region.
                var nestedCleanup = cleanup && target.TryOffset >= filter.TryOffset &&
                    target.TryOffset + target.TryLength <= filter.TryOffset + filter.TryLength;
                if (!nestedCleanup && !(sameRegion && afterFilter || outerRegion)) continue;
                var entry = effects.Copy();
                if (!cleanup) entry.stack.Add(Value.Unknown);
                this.Merge(this.offsets[target.Flags == ExceptionHandlingClauseOptions.Filter ? target.FilterOffset : target.HandlerOffset], entry);
            }
        }

        // Modes also represent exact scalar 0/1. A managed address is not its
        // pointee's boolean value and must never be pruned by this projection.
        private static bool ScalarBoolean(Value value) => value.addresses.Length == 0 &&
            (value.mode == Mode.Normal || value.mode == Mode.Readonly);
        private static int BranchChoice(Instruction instruction, Frame frame) {
            var op = instruction.OpCode;
            if (op != OpCodes.Brtrue && op != OpCodes.Brtrue_S && op != OpCodes.Brfalse && op != OpCodes.Brfalse_S || frame.stack.Count == 0) return 0;
            var value = frame.stack[frame.stack.Count - 1];
            if (!ScalarBoolean(value)) return 0;
            var take = (value.mode == Mode.Readonly) == (op == OpCodes.Brtrue || op == OpCodes.Brtrue_S);
            return take ? 1 : -1;
        }

        private int Variable(Instruction instruction, bool argument) {
            var name = instruction.OpCode.Name; var suffix = name[name.Length - 1];
            var index = suffix >= '0' && suffix <= '3' ? suffix - '0' : argument
                ? ((ParameterInfo)instruction.Operand).Position + (this.method.IsStatic ? 0 : 1)
                : ((LocalVariableInfo)instruction.Operand).LocalIndex;
            return this.baseSlot + (argument ? index : this.argumentCount + index);
        }

        private void Step(Instruction instruction, Frame frame) {
            var op = instruction.OpCode; var name = op.Name;
            if (name.StartsWith("ldloc", StringComparison.Ordinal) || name.StartsWith("ldarg", StringComparison.Ordinal)) {
                var argument = name.StartsWith("ldarg", StringComparison.Ordinal);
                var index = this.Variable(instruction, argument);
                frame.stack.Add(name.StartsWith("ldloca", StringComparison.Ordinal) || name.StartsWith("ldarga", StringComparison.Ordinal)
                    ? new Value { addresses = new[] { index } } : frame.variables[index]); return;
            }
            if (name.StartsWith("stloc", StringComparison.Ordinal) || name.StartsWith("starg", StringComparison.Ordinal)) {
                var index = this.Variable(instruction, name.StartsWith("starg", StringComparison.Ordinal)); var value = frame.Pop();
                frame.variables[index] = this.variableTypes[index - this.baseSlot].IsByRef ? value : frame.Read(value); return;
            }
            if (op == OpCodes.Dup) { var value = frame.Pop(); frame.stack.Add(value); frame.stack.Add(value); return; }
            if (op == OpCodes.Ldc_I4_0) { frame.stack.Add(Value.Normal); return; }
            if (op == OpCodes.Ldc_I4_1) { frame.stack.Add(Value.Readonly); return; }
            if (op == OpCodes.Ceq) {
                var right = frame.Pop(); var left = frame.Pop();
                frame.stack.Add(ScalarBoolean(left) && ScalarBoolean(right) ?
                    left.mode == right.mode ? Value.Readonly : Value.Normal : Value.Unknown);
                return;
            }
            if (op == OpCodes.Initobj) { frame.Write(frame.Pop(), Default((Type)instruction.Operand)); return; }
            if (op == OpCodes.Ldobj || name.StartsWith("ldind", StringComparison.Ordinal)) { frame.stack.Add(frame.Read(frame.Pop())); return; }
            if (op == OpCodes.Stobj || name.StartsWith("stind", StringComparison.Ordinal)) { var value = frame.Read(frame.Pop()); frame.Write(frame.Pop(), value); return; }
            if (op == OpCodes.Cpobj) { var value = frame.Read(frame.Pop()); frame.Write(frame.Pop(), value); return; }
            if (op == OpCodes.Cpblk || op == OpCodes.Initblk) {
                frame.Pop(); frame.Pop(); frame.Pop(); frame.Write(Value.Unknown, Value.Unknown); return;
            }
            if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj) { this.Call(instruction, frame); return; }
            if (op == OpCodes.Leave || op == OpCodes.Leave_S) { frame.stack.Clear(); return; }
            if (op == OpCodes.Ret) {
                if (this.method is MethodInfo result && result.ReturnType != typeof(void)) {
                    var value = frame.Pop();
                    if (!result.ReturnType.IsByRef) value = frame.Read(value);
                    this.returnValue = this.returnValue == null ? value : this.returnValue.Union(value);
                }
                AccumulateStorage(ref this.normalExit, frame);
                return;
            }
            if (op == OpCodes.Stfld || op == OpCodes.Stsfld) {
                var value = frame.Read(frame.Pop());
                if (op == OpCodes.Stfld) {
                    var address = frame.Pop(); var field = (FieldInfo)instruction.Operand;
                    if (Query(field.DeclaringType) && field.Name == "isReadonly" && field.FieldType == typeof(bool)) frame.Write(address, value);
                }
                return;
            }
            if (op == OpCodes.Ldfld || op == OpCodes.Ldflda) {
                var receiver = frame.Pop(); var field = (FieldInfo)instruction.Operand;
                if (Query(field.DeclaringType) && field.Name == "isReadonly" && field.FieldType == typeof(bool))
                    frame.stack.Add(op == OpCodes.Ldflda ? receiver : frame.Read(receiver));
                else frame.stack.Add(Value.Unknown);
                return;
            }
            if (op.StackBehaviourPop == StackBehaviour.Varpop || op.StackBehaviourPush == StackBehaviour.Varpush)
                throw new InvalidOperationException("Unsupported query IL stack effect");
            var pop = op.StackBehaviourPop == StackBehaviour.Pop0 ? 0 : op.StackBehaviourPop.ToString().Split('_').Length;
            for (var count = 0; count < pop; ++count) frame.Pop();
            var push = op.StackBehaviourPush == StackBehaviour.Push0 ? 0 : op.StackBehaviourPush == StackBehaviour.Push1_push1 ? 2 : 1;
            for (var count = 0; count < push; ++count) frame.stack.Add(Value.Unknown);
        }

        private void Call(Instruction instruction, Frame frame) {
            if (!(instruction.Operand is MethodBase called)) throw new InvalidOperationException("Unresolved query IL call");
            var parameters = called.GetParameters(); var arguments = new Value[parameters.Length];
            for (var index = arguments.Length - 1; index >= 0; --index) arguments[index] = frame.Pop();
            var receiver = !called.IsStatic && instruction.OpCode != OpCodes.Newobj ? frame.Pop() : Value.Unknown;
            var returns = Value.Unknown;
            var handled = false;
            if (called is MethodInfo method) {
                if (SourceGeneratorScheduledJobsValidation.IsSchedulingMethod(method)) {
                    var mode = Mode.Normal;
                    var query = Array.FindIndex(parameters, parameter => Query(parameter.ParameterType));
                    var readOnly = Array.FindIndex(parameters, parameter => parameter.Name == "isReadonly" && parameter.ParameterType == typeof(bool));
                    if (query >= 0) mode = frame.Read(arguments[query]).mode;
                    else if (readOnly >= 0) mode = frame.Read(arguments[readOnly]).mode;
                    else if (!method.IsStatic && Query(method.DeclaringType)) mode = frame.Read(receiver).mode;
                    this.schedules.TryGetValue(instruction.Offset, out var previous);
                    this.schedules[instruction.Offset] = previous | (mode == 0 ? Mode.Unknown : mode);
                    handled = true;
                } else if (ILFormattingCallbacks.IsFormattingCall(method)) {
                    // Callback bodies are collected separately. Do not interpret
                    // BCL formatting internals as if they represented concrete
                    // callback invocation/aliasing. A callback may retain an
                    // unsafe alias to caller storage, so invalidate caller facts.
                    frame.Write(Value.Unknown, Value.Unknown);
                    handled = true;
                } else if (method.DeclaringType == typeof(QueryBuilder) && !method.IsStatic && method.ReturnType == typeof(QueryBuilder)) {
                    if (method.Name == nameof(QueryBuilder.AsReadonly) && parameters.Length == 0) {
                        frame.Write(receiver, Value.Readonly); returns = Value.Readonly; handled = true;
                    } else if (PreservesMode.Contains(method.Name)) {
                        returns = frame.Read(receiver); handled = true;
                    }
                } else if (method.IsStatic && method.Name == "Query" && method.ReturnType == typeof(QueryBuilder) &&
                    (method.DeclaringType == typeof(API) || method.DeclaringType == typeof(APIExt))) {
                    returns = Value.Normal; handled = true;
                }
            }
            if (!handled) {
                var helper = ILCallTargets.Resolve(this.instructions, this.offsets[instruction.Offset], out var exactDispatch);
                if (ILCallTargets.TryConstruction(helper, out _, out var constructor)) helper = constructor;
                if (helper != null && this.program != null && this.program.CanInvoke(helper, exactDispatch)) {
                    var values = helper.IsStatic ? arguments : new[] { receiver }.Concat(arguments).ToArray();
                    var flow = this.program.Invoke(helper, frame, values);
                    this.stepException = frame.Copy();
                    for (var index = 0; index < frame.variables.Length; ++index) {
                        this.stepException.variables[index] = flow.allEffects?.variables[index] ?? Value.Unknown;
                        frame.variables[index] = (flow.normalExit ?? flow.allEffects)?.variables[index] ?? Value.Unknown;
                    }
                    returns = flow.normalExit == null ? Value.Unknown : flow.returnValue ?? Value.Unknown;
                    // A returned ref may name caller storage but cannot export
                    // a callee-local address into a differently laid out frame.
                    if (returns.addresses.Any(address => address >= frame.variables.Length))
                        returns = new Value { mode = returns.mode | Mode.Unknown, addresses = returns.addresses.Where(address => address < frame.variables.Length).ToArray() };
                    handled = true;
                }
            }
            if (!handled) {
                for (var index = 0; index < parameters.Length; ++index)
                    if (parameters[index].ParameterType.IsByRef) frame.Write(arguments[index], Value.Unknown);
                    else if (parameters[index].ParameterType.IsPointer || parameters[index].ParameterType == typeof(IntPtr) ||
                        parameters[index].ParameterType == typeof(UIntPtr)) frame.Write(Value.Unknown, Value.Unknown);
                if (!called.IsStatic && instruction.OpCode != OpCodes.Newobj && Query(called.DeclaringType)) frame.Write(receiver, Value.Unknown);
            }
            if (instruction.OpCode == OpCodes.Newobj) frame.stack.Add(returns);
            else if (called is MethodInfo result && result.ReturnType != typeof(void)) frame.stack.Add(returns);
        }
    }
}
