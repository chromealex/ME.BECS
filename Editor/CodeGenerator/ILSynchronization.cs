namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using Unity.Jobs;
    using ME.BECS.Mono.Reflection;

    // Independent IL handle-provenance analysis. Diagnostic-only until coverage
    // includes opaque storage and all scheduling contracts.
    // Proof concerns ordering of direct accesses, not publication of returned
    // dependencies or the absence of conflicts between scheduled jobs.
    // Never run a lifecycle, job, constructor or native API to obtain a fact.
    internal sealed class ILSynchronization {
        internal sealed class Result {
            public string status;
            public string[] gaps, unsafeSites;
            public int accesses, completions;
        }

        // Values are immutable. Coverage is a MUST set; addresses are a MAY set.
        private sealed class Value {
            internal Type type, storageType;
            internal int? constant;
            internal bool nonComponentStorage, opaqueAddress;
            // Type identity / known storage are MUST facts. Possible component
            // storage is a MAY fact: a mixed ref branch must not erase its access.
            internal bool runtimeType, runtimeTypeHandle, knownComponentStorage, componentStorage;
            internal int[] tokens = Array.Empty<int>(), addresses = Array.Empty<int>();
            internal static Value Plain(Type type = null) => new Value { type = type, opaqueAddress = type?.IsByRef == true || type?.IsPointer == true };
            internal static Value Address(Type type, int cell) => new Value { type = type, storageType = type, addresses = new[] { cell } };
            internal static Value Constant(int value) => new Value { type = typeof(int), constant = value };
            internal Value Merge(Value other, ISet<int> leftPending = null, ISet<int> rightPending = null) {
                // A handle need only cover a token on paths where that work is
                // outstanding. This keeps zero-iteration/conditional scheduling
                // joins from erasing coverage of work created on the other path.
                var tokens = this.tokens.Union(other.tokens).Where(id =>
                    (Array.IndexOf(this.tokens, id) >= 0 || leftPending != null && !leftPending.Contains(id)) &&
                    (Array.IndexOf(other.tokens, id) >= 0 || rightPending != null && !rightPending.Contains(id))).OrderBy(id => id).ToArray();
                // A mix of known/opaque addresses cannot prove the known target.
                var addresses = this.addresses.Length == 0 || other.addresses.Length == 0 ? Array.Empty<int>() :
                    this.addresses.Union(other.addresses).OrderBy(id => id).ToArray();
                var type = this.type == other.type ? this.type : null;
                var storageType = this.storageType == other.storageType ? this.storageType : null;
                var constant = this.constant == other.constant ? this.constant : null;
                var nonComponentStorage = this.nonComponentStorage && other.nonComponentStorage;
                var opaqueAddress = this.opaqueAddress || other.opaqueAddress;
                var runtimeType = this.runtimeType && other.runtimeType;
                var runtimeTypeHandle = this.runtimeTypeHandle && other.runtimeTypeHandle;
                var componentStorage = this.componentStorage || other.componentStorage;
                var knownComponentStorage = this.knownComponentStorage && other.knownComponentStorage;
                return type == this.type && storageType == this.storageType && constant == this.constant && this.nonComponentStorage == nonComponentStorage && this.opaqueAddress == opaqueAddress &&
                    this.runtimeType == runtimeType && this.runtimeTypeHandle == runtimeTypeHandle && this.componentStorage == componentStorage && this.knownComponentStorage == knownComponentStorage &&
                    this.tokens.SequenceEqual(tokens) && this.addresses.SequenceEqual(addresses) ? this :
                    new Value { type = type, storageType = storageType, constant = constant, tokens = tokens, addresses = addresses, nonComponentStorage = nonComponentStorage, opaqueAddress = opaqueAddress,
                        runtimeType = runtimeType, runtimeTypeHandle = runtimeTypeHandle, componentStorage = componentStorage, knownComponentStorage = knownComponentStorage };
            }
            internal Value Retire(int current, int previous, bool currentPending, bool previousPending) {
                var tokens = this.tokens.Where(id => id != current && id != previous).ToList();
                // The summary token represents ALL earlier generations at this
                // site. A handle that covers only one of them cannot complete it.
                if ((currentPending || previousPending) && (!currentPending || Array.IndexOf(this.tokens, current) >= 0) &&
                    (!previousPending || Array.IndexOf(this.tokens, previous) >= 0)) tokens.Add(previous);
                return new Value { type = this.type, storageType = this.storageType, constant = this.constant, addresses = this.addresses, nonComponentStorage = this.nonComponentStorage, opaqueAddress = this.opaqueAddress,
                    runtimeType = this.runtimeType, runtimeTypeHandle = this.runtimeTypeHandle, componentStorage = this.componentStorage, knownComponentStorage = this.knownComponentStorage, tokens = tokens.OrderBy(id => id).ToArray() };
            }
        }

        private sealed class State {
            internal readonly Dictionary<int, Value> cells = new Dictionary<int, Value>();
            internal readonly HashSet<int> pending = new HashSet<int>();
            internal List<Value> stack = new List<Value>();
            internal State Copy() {
                var copy = new State { stack = new List<Value>(this.stack) };
                foreach (var cell in this.cells) copy.cells.Add(cell.Key, cell.Value);
                copy.pending.UnionWith(this.pending);
                return copy;
            }
            internal Value Pop() {
                if (this.stack.Count == 0) throw new InvalidOperationException("Invalid IL synchronization stack");
                var index = this.stack.Count - 1;
                var value = this.stack[index]; this.stack.RemoveAt(index); return value;
            }
            internal Value Cell(int index) => this.cells.TryGetValue(index, out var value) ? value : Value.Plain();
            internal Value Read(Value value) {
                var visited = new HashSet<int>();
                Value ReadAddress(Value item) {
                    if (item.addresses.Length == 0) return item;
                    Value result = null;
                    foreach (var address in item.addresses) {
                        if (!visited.Add(address)) throw new InvalidOperationException("Cyclic IL synchronization address");
                        var next = ReadAddress(this.Cell(address));
                        visited.Remove(address);
                        result = result == null ? next : result.Merge(next);
                    }
                    return result ?? Value.Plain();
                }
                return ReadAddress(value);
            }
            internal bool Merge(State other) {
                if (this.stack.Count != other.stack.Count) throw new InvalidOperationException("Inconsistent IL synchronization stack heights");
                var changed = false;
                foreach (var cell in this.cells.Keys.Union(other.cells.Keys).ToArray()) {
                    var before = this.Cell(cell);
                    var after = before.Merge(other.Cell(cell), this.pending, other.pending);
                    if (!this.cells.ContainsKey(cell) || !ReferenceEquals(before, after)) { this.cells[cell] = after; changed = true; }
                }
                for (var index = 0; index < this.stack.Count; ++index) {
                    var before = this.stack[index]; var after = before.Merge(other.stack[index], this.pending, other.pending);
                    if (!ReferenceEquals(before, after)) { this.stack[index] = after; changed = true; }
                }
                foreach (var token in other.pending) changed |= this.pending.Add(token);
                return changed;
            }
            internal void Retire(int current, int previous) {
                var currentPending = this.pending.Contains(current); var previousPending = this.pending.Contains(previous);
                foreach (var cell in this.cells.Keys.ToArray()) this.cells[cell] = this.cells[cell].Retire(current, previous, currentPending, previousPending);
                for (var index = 0; index < this.stack.Count; ++index)
                    this.stack[index] = this.stack[index].Retire(current, previous, currentPending, previousPending);
                this.pending.Remove(current);
                if (currentPending) this.pending.Add(previous);
            }
        }

        private sealed class Unwind {
            internal ExceptionHandlingClause handler;
            internal int next, target, resume, parent;
            internal Target destination;
        }

        private sealed class Target {
            internal object frame;
            internal int offset = -1;
            internal Type caughtType;
        }

        // A first-pass choice carries filter mutations BEFORE cleanup. Once
        // chosen, the destination travels through second-pass failures without
        // repeating the caller's filters. A newly thrown exception searches anew.
        private sealed class Failure {
            internal State state;
            internal Target target;
        }

        private readonly Target escaped = new Target();

        private readonly Dictionary<string, int> cells = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<int> externalCells = new HashSet<int>();
        private readonly Dictionary<string, int> tokens = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<MethodBase> active = new HashSet<MethodBase>();
        private readonly Dictionary<Type, bool> inertInitializers = new Dictionary<Type, bool>();
        private readonly HashSet<string> gaps = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> accesses = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> completions = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> unsafeSites = new HashSet<string>(StringComparer.Ordinal);
        private int work, calls, filterDepth;
        private long flowCost;
        private const int Incoming = 0;

        internal static Result Analyze(MethodInfo root) {
            var analysis = new ILSynchronization();
            try {
                if (root == null || root.ContainsGenericParameters) throw new InvalidOperationException("Missing/open synchronization root");
                var state = new State(); state.pending.Add(Incoming);
                var parameters = root.GetParameters();
                var args = new List<Value>();
                if (!root.IsStatic) args.Add(analysis.EntryArgument(state, root.DeclaringType.MakeByRefType(), "this", false));
                for (var index = 0; index < parameters.Length; ++index)
                    args.Add(analysis.EntryArgument(state, parameters[index].ParameterType, "p" + index, true));
                analysis.Invoke(root, state, args.ToArray(), "root", null, out _, out _);
            } catch (Exception exception) {
                analysis.gaps.Add("AnalysisFailure: " + exception.GetBaseException().Message);
            }
            return new Result {
                status = analysis.gaps.Count != 0 ? "incomplete" : analysis.unsafeSites.Count != 0 ? "unproven" : "proven",
                gaps = analysis.gaps.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                unsafeSites = analysis.unsafeSites.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                accesses = analysis.accesses.Count, completions = analysis.completions.Count,
            };
        }

        private static Type Element(Type type) => type?.IsByRef == true ? type.GetElementType() : type;
        private static bool Tracked(Type type) => Element(type) == typeof(JobHandle) || Element(type) == typeof(SystemContext) || Element(type) == typeof(QueryBuilder);
        private int Cell(string name) {
            if (!this.cells.TryGetValue(name, out var cell)) {
                if (this.cells.Count >= 8192) throw new InvalidOperationException("IL synchronization storage limit");
                this.cells.Add(name, cell = this.cells.Count);
            }
            return cell;
        }
        private Value EntryArgument(State state, Type type, string name, bool context) {
            var value = Value.Plain(Element(type));
            if (context && Element(type) == typeof(SystemContext)) value.tokens = new[] { Incoming };
            if (!type.IsByRef) return value;
            var cell = this.Cell("entry/" + name); state.cells[cell] = value;
            if (Element(type) != typeof(SystemContext)) this.externalCells.Add(cell);
            var address = Value.Address(Element(type), cell);
            address.componentStorage = typeof(IComponentBase).IsAssignableFrom(Element(type));
            return address;
        }

        private bool LocalStorage(State state, Value address) {
            var visited = new HashSet<int>();
            bool Local(Value value) {
                if (value.addresses.Length == 0 || value.opaqueAddress) return false;
                foreach (var cell in value.addresses) {
                    if (this.externalCells.Contains(cell) || !visited.Add(cell)) return false;
                    var stored = state.Cell(cell);
                    if (stored.opaqueAddress) return false;
                    if (stored.addresses.Length != 0 && !Local(stored)) return false;
                    visited.Remove(cell);
                }
                return true;
            }
            return Local(address);
        }
        private void Gap(string reason, MethodBase method) => this.gaps.Add(reason + ": " + method.DeclaringType + "." + method.Name);
        private void Charge(State state) {
            // Include first-pass search/copies as well as executed instructions.
            // A long rejected-handler chain must not bypass the work budget.
            this.flowCost += 1L + state.cells.Count + state.stack.Count;
            if (this.flowCost > 2000000) throw new InvalidOperationException("IL synchronization flow-state limit");
        }
        private void Access(State state, string site) {
            this.accesses.Add(site);
            if (state.pending.Count != 0) this.unsafeSites.Add(site);
        }
        private void Complete(State state, Value value, string site) {
            this.completions.Add(site);
            state.pending.ExceptWith(state.Read(value).tokens);
        }
        private void Store(State state, Value address, Value value, MethodBase method, string site) {
            if (address.componentStorage) this.Access(state, site);
            if (address.knownComponentStorage) return;
            if (address.addresses.Length == 0) {
                if (!address.nonComponentStorage) this.Gap("OpaqueStorageWrite", method);
                return;
            }
            foreach (var cell in address.addresses)
                state.cells[cell] = address.addresses.Length == 1 ? value : state.Cell(cell).Merge(value);
        }
        private static Value Combined(Type type, IEnumerable<Value> values) => new Value {
            type = type, tokens = values.SelectMany(value => value.tokens).Distinct().OrderBy(token => token).ToArray(),
        };

        private bool HasOpaqueInitializer(Type type) {
            var initializer = type?.TypeInitializer;
            if (initializer == null) return false;
            if (!this.inertInitializers.TryGetValue(type, out var inert)) {
                // Constant/static delegate storage does not invoke the referenced
                // method. Do not treat an arbitrary .cctor or constructor as inert.
                inert = ILAnalysisSession.Instructions(initializer).All(instruction => {
                    var op = instruction.OpCode;
                    if (op == OpCodes.Nop || op == OpCodes.Ret || op == OpCodes.Ldnull || op == OpCodes.Ldstr || op == OpCodes.Ldftn ||
                        op.Name.StartsWith("ldc.", StringComparison.Ordinal)) return true;
                    if (op == OpCodes.Stsfld && instruction.Operand is FieldInfo field)
                        return field.DeclaringType == type && !Tracked(field.FieldType) && !typeof(IComponentBase).IsAssignableFrom(field.FieldType);
                    if (op == OpCodes.Newobj && instruction.Operand is ConstructorInfo constructor &&
                        typeof(MulticastDelegate).IsAssignableFrom(constructor.DeclaringType))
                        return constructor.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(object), typeof(IntPtr) });
                    return false;
                });
                this.inertInitializers.Add(type, inert);
            }
            return !inert;
        }

        private State Invoke(MethodBase method, State caller, Value[] inputs, string path,
            Func<State, List<Failure>> searchOuter, out Value returned, out List<Failure> exceptional) {
            returned = Value.Plain((method as MethodInfo)?.ReturnType);
            exceptional = null;
            if (++this.calls > 10000 || this.active.Count >= 64 || !this.active.Add(method)) {
                this.Gap("RecursiveOrExcessiveExpansion", method); return caller;
            }
            try {
                var body = method.GetMethodBody();
                if (body == null) { this.Gap("OpaqueCall", method); return caller; }
                var regions = body.ExceptionHandlingClauses.Cast<ExceptionHandlingClause>().ToArray();
                if (this.HasOpaqueInitializer(method.DeclaringType) && method != method.DeclaringType.TypeInitializer)
                    this.Gap("TypeInitialization", method);
                var instructions = ILAnalysisSession.Instructions(method);
                if (instructions.Length == 0) return caller;
                var indices = instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(item => item.Offset, item => item.index);
                var start = caller.Copy();
                var saved = start.stack.Select((value, index) => {
                    var cell = this.Cell(path + "/saved" + index); start.cells[cell] = value; return cell;
                }).ToArray();
                start.stack.Clear();
                var arguments = inputs.Select((value, index) => {
                    var cell = this.Cell(path + "/a" + index); start.cells[cell] = value; return cell;
                }).ToArray();
                var locals = body.LocalVariables.Select(local => {
                    var cell = this.Cell(path + "/l" + local.LocalIndex); start.cells[cell] = Value.Plain(local.LocalType); return cell;
                }).ToArray();
                State Run(State input, int entry, int limit, bool filter, Func<State, List<Failure>> outer,
                    out Value returnValue, out List<Failure> failures, out State accepted, out State rejected) {
                    if (filter && (++this.calls > 10000 || this.filterDepth >= 32)) throw new InvalidOperationException("IL synchronization filter search limit");
                    if (filter) ++this.filterDepth;
                    try {
                        var frame = new object();
                        var targets = new Dictionary<int, Target>();
                        var boundary = filter ? new Target { frame = frame, offset = -2 } : this.escaped;
                        var cleanups = regions.Where(clause => clause.TryOffset >= entry && clause.TryOffset + clause.TryLength <= limit &&
                                (clause.Flags == ExceptionHandlingClauseOptions.Finally || clause.Flags == ExceptionHandlingClauseOptions.Fault))
                            .OrderBy(clause => clause.TryLength).ThenByDescending(clause => clause.TryOffset).ToArray();
                        var catches = regions.Where(clause => clause.TryOffset >= entry && clause.TryOffset + clause.TryLength <= limit &&
                                (clause.Flags == ExceptionHandlingClauseOptions.Clause || clause.Flags == ExceptionHandlingClauseOptions.Filter))
                            .OrderBy(clause => clause.TryLength).ThenByDescending(clause => clause.TryOffset).ToArray();
                        if (cleanups.GroupBy(clause => (clause.TryOffset, clause.TryLength)).Any(group => group.Count() != 1))
                            this.Gap("AmbiguousCleanupRegion", method);
                        var states = new Dictionary<(int Index, int Unwind, string Facts), State>(); var queue = new Queue<(int Index, int Unwind, string Facts)>();
                        var stackHeights = new Dictionary<int, int>();
                        var unwinds = new List<Unwind> { null };
                        var unwindIds = new Dictionary<(int Handler, int Next, int Target, int Resume, int Parent, Target Destination), int>();
                        var thrown = new List<Failure>();
                        State filterAccepted = null, filterRejected = null;
                        void Enqueue(int offset, State value, int unwind) {
                            if (offset < entry || offset >= limit) throw new InvalidOperationException("IL synchronization flow escaped its execution region");
                            if (!indices.TryGetValue(offset, out var index)) throw new InvalidOperationException("Invalid synchronization branch target");
                            if (stackHeights.TryGetValue(index, out var height) && height != value.stack.Count)
                                throw new InvalidOperationException("Inconsistent IL synchronization stack heights");
                            stackHeights[index] = value.stack.Count;
                            // A false type-test branch must not merge pending work into
                            // a true filter result that completed it. Keep scalar filter
                            // outcomes separate across compiler join temporaries too.
                            var facts = filter ? string.Join(",", arguments.Concat(locals).Select(cell => value.Cell(cell))
                                .Concat(value.stack).Select(item => item.constant?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?")) : "";
                            var key = (index, unwind, facts);
                            if (!states.TryGetValue(key, out var previous)) { states.Add(key, value.Copy()); queue.Enqueue(key); }
                            else if (previous.Merge(value)) queue.Enqueue(key);
                        }
                        void ThrowExit(State state, Target target) {
                            state = state.Copy();
                            state.stack = saved.Select(state.Cell).ToList();
                            var previous = thrown.FirstOrDefault(failure => failure.target == target);
                            if (previous == null) thrown.Add(new Failure { state = state, target = target });
                            else previous.state.Merge(state);
                        }
                        void FinishFilter(State state, bool accept) {
                            state = state.Copy(); state.stack.Clear();
                            if (accept) {
                                if (filterAccepted == null) filterAccepted = state;
                                else filterAccepted.Merge(state);
                            } else {
                                if (filterRejected == null) filterRejected = state;
                                else filterRejected.Merge(state);
                            }
                        }
                        void EnterCleanup(State state, int unwind) {
                            state = state.Copy(); state.stack.Clear();
                            Enqueue(unwinds[unwind].handler.HandlerOffset, state, unwind);
                        }
                        bool InTry(ExceptionHandlingClause clause, int offset) => offset >= clause.TryOffset && offset < clause.TryOffset + clause.TryLength;
                        bool InHandler(ExceptionHandlingClause clause, int offset) => offset >= clause.HandlerOffset && offset < clause.HandlerOffset + clause.HandlerLength;
                        int RetainedUnwind(int unwind, int target) {
                            // Only active enclosing handlers survive a locally caught
                            // exception. `next` handlers have not run yet; following that
                            // chain here would revive a canceled return/exception route.
                            while (unwind != 0) {
                                if (InHandler(unwinds[unwind].handler, target)) return unwind;
                                unwind = unwinds[unwind].parent;
                            }
                            return 0;
                        }
                        void Resume(State state, int target, int resume, Target destination) {
                            if (target == -2 && filter) { FinishFilter(state, false); return; }
                            if (target < 0) { ThrowExit(state, destination); return; }
                            state = state.Copy(); state.stack.Clear();
                            if (destination != null) state.stack.Add(Value.Plain(destination.caughtType));
                            Enqueue(target, state, resume);
                        }
                        void Transfer(State state, int source, int target, int activeUnwind, Target destination = null) {
                            var next = 0;
                            var resume = RetainedUnwind(activeUnwind, target);
                            var exceptionalTransfer = destination != null;
                            foreach (var clause in cleanups.Where(clause => InTry(clause, source) &&
                                         !InTry(clause, target) && (exceptionalTransfer || clause.Flags == ExceptionHandlingClauseOptions.Finally)).Reverse()) {
                                // The final target can escape an active outer finally,
                                // but a newly throwing inner cleanup may still be caught
                                // inside it. Retain that physical parent until it is left.
                                var parent = RetainedUnwind(activeUnwind, clause.HandlerOffset);
                                var key = (clause.HandlerOffset, next, target, resume, parent, destination);
                                if (!unwindIds.TryGetValue(key, out var id)) {
                                    if (unwinds.Count >= 4096) throw new InvalidOperationException("IL synchronization unwind limit");
                                    id = unwinds.Count;
                                    unwindIds.Add(key, id);
                                    unwinds.Add(new Unwind { handler = clause, next = next, target = target, resume = resume, parent = parent, destination = destination });
                                }
                                next = id;
                            }
                            if (next != 0) EnterCleanup(state, next);
                            else Resume(state, target, resume, destination);
                        }
                        List<Failure> Search(State state, int source) {
                            this.Charge(state);
                            // Exception types are conservative: visit every possible
                            // first matching handler, never let one sibling's completion
                            // affect another. Stable EH-table order resolves same-try
                            // catches; an inner catch shadows matching outer handlers.
                            var handled = new List<Type>(); var choices = new List<Failure>();
                            foreach (var clause in catches.Where(clause => InTry(clause, source))) {
                                this.Charge(state);
                                var isFilter = clause.Flags == ExceptionHandlingClauseOptions.Filter;
                                var caught = isFilter ? typeof(object) : clause.CatchType;
                                if (caught == null) { this.Gap("MissingCatchType", method); continue; }
                                if (!isFilter && handled.Any(type => type.IsAssignableFrom(caught))) continue;
                                if (!targets.TryGetValue(clause.HandlerOffset, out var destination))
                                    targets.Add(clause.HandlerOffset, destination = new Target { frame = frame, offset = clause.HandlerOffset, caughtType = caught });
                                if (isFilter) {
                                    var incoming = state.Copy(); incoming.stack.Clear(); incoming.stack.Add(Value.Plain(typeof(object)));
                                    Run(incoming, clause.FilterOffset, clause.HandlerOffset, true, null, out _, out var filterFailures, out var yes, out var no);
                                    if (filterFailures.Count != 0) throw new InvalidOperationException("Filter failure escaped rejection boundary");
                                    if (yes != null) choices.Add(new Failure { state = yes, target = destination });
                                    if (no == null) return choices;
                                    state = no; // False/throwing filters preserve all preceding effects for the next search step.
                                    continue;
                                }
                                choices.Add(new Failure { state = state.Copy(), target = destination });
                                handled.Add(caught);
                                if (caught == typeof(object)) return choices;
                            }
                            // Without an object catch, retain a conservative escaping
                            // alternative (including non-Exception CLI throws). This can
                            // refuse a proof, but cannot hide an unsafe outer cleanup.
                            if (outer != null) choices.AddRange(outer(state));
                            else choices.Add(new Failure { state = state.Copy(), target = boundary });
                            return choices;
                        }
                        void UnwindFailure(Failure failure, int source, int unwind) => Transfer(failure.state, source,
                            failure.target.frame == frame ? failure.target.offset : -1, unwind, failure.target);
                        void Fail(State state, int source, int unwind) {
                            foreach (var choice in Search(state, source)) UnwindFailure(choice, source, unwind);
                        }
                        Enqueue(entry, input, 0);
                        State exit = null; Value result = null;
                        while (queue.Count != 0) {
                            if (++this.work > 200000) throw new InvalidOperationException("IL synchronization work limit");
                            var key = queue.Dequeue(); var index = key.Index; var unwind = key.Unwind;
                            // A step may copy/merge thousands of cells. Bound that work,
                            // not just the number of instructions, using deterministic
                            // units so host speed cannot affect diagnostic coverage.
                            this.Charge(states[key]);
                            var state = states[key].Copy(); var instruction = instructions[index]; var op = instruction.OpCode;
                            var site = path + "/" + method.DeclaringType + "." + method.Name + "@" + instruction.Offset;
                            var fallthrough = true;
                            if (op == OpCodes.Ret) {
                                if (filter) { this.Gap("ReturnInsideFilter", method); continue; }
                                if (unwind != 0 || regions.Any(clause => InTry(clause, instruction.Offset) || InHandler(clause, instruction.Offset)))
                                    this.Gap("ReturnInsideProtectedRegion", method);
                                var value = method is MethodInfo info && info.ReturnType != typeof(void) ? state.Pop() : Value.Plain();
                                if (value.addresses.Length != 0 || method is MethodInfo refMethod && refMethod.ReturnType.IsByRef && !value.knownComponentStorage)
                                    this.Gap("RefReturn", method);
                                value = state.Read(value);
                                state.stack = saved.Select(state.Cell).ToList();
                                if (exit == null) { exit = state; result = value; } else { result = result.Merge(value, exit.pending, state.pending); exit.Merge(state); }
                                continue;
                            }
                            if (op == OpCodes.Throw || op == OpCodes.Rethrow) {
                                if (op == OpCodes.Rethrow && !catches.Any(clause => InHandler(clause, instruction.Offset)))
                                    this.Gap("UnboundRethrow", method);
                                Fail(state, instruction.Offset, unwind); continue;
                            }
                            if (op == OpCodes.Endfinally) {
                                if (unwind == 0) { this.Gap("UnboundFinally", method); continue; }
                                var continuation = unwinds[unwind];
                                if (instruction.Offset < continuation.handler.HandlerOffset ||
                                    instruction.Offset >= continuation.handler.HandlerOffset + continuation.handler.HandlerLength)
                                    throw new InvalidOperationException("Invalid synchronization finally continuation");
                                if (continuation.next != 0) EnterCleanup(state, continuation.next);
                                else Resume(state, continuation.target, continuation.resume, continuation.destination);
                                continue;
                            }
                            if (op == OpCodes.Endfilter) {
                                if (!filter) { this.Gap("UnboundFilter", method); continue; }
                                var decision = state.Read(state.Pop()).constant;
                                if (decision != 0) FinishFilter(state, true);
                                if (decision == null || decision == 0) FinishFilter(state, false);
                                continue;
                            }
                            if (op.FlowControl == FlowControl.Branch || op.FlowControl == FlowControl.Cond_Branch) {
                                if (op == OpCodes.Leave || op == OpCodes.Leave_S) {
                                    if (!(instruction.Operand is Instruction leave)) throw new InvalidOperationException("Invalid synchronization leave target");
                                    Transfer(state, instruction.Offset, leave.Offset, unwind);
                                    continue;
                                }
                                bool? taken = null;
                                if (op == OpCodes.Brtrue || op == OpCodes.Brtrue_S || op == OpCodes.Brfalse || op == OpCodes.Brfalse_S) {
                                    var condition = state.Read(state.stack[state.stack.Count - 1]).constant;
                                    if (condition != null) taken = (condition != 0) == (op == OpCodes.Brtrue || op == OpCodes.Brtrue_S);
                                }
                                PopFixed(state, op);
                                if (instruction.Operand is Instruction destination) { if (taken != false) Enqueue(destination.Offset, state, unwind); }
                                else if (instruction.Operand is Instruction[] choices) foreach (var choice in choices) Enqueue(choice.Offset, state, unwind);
                                else { this.Gap("UnsupportedBranch", method); }
                                fallthrough = op.FlowControl == FlowControl.Cond_Branch && taken != true;
                            } else if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj) {
                                // A call may fail before its body/effects. Expanded bodies
                                // also return exceptional states after any ref mutations.
                                Fail(state, instruction.Offset, unwind);
                                var target = ILCallTargets.Resolve(instructions, index, out var exact);
                                if (target == null) throw new InvalidOperationException("Unbound synchronization call");
                                var parameters = target.GetParameters(); var args = new Value[parameters.Length + (target.IsStatic || op == OpCodes.Newobj ? 0 : 1)];
                                for (var arg = args.Length - 1; arg >= 0; --arg) args[arg] = state.Pop();
                                var returnType = op == OpCodes.Newobj ? target.DeclaringType : (target as MethodInfo)?.ReturnType ?? typeof(void);
                                Value value; List<Failure> failure = null;
                                List<Failure> SearchCaller(State failed) => Search(failed, instruction.Offset);
                                if (op == OpCodes.Newobj) {
                                    state = this.Construct((ConstructorInfo)target, target.DeclaringType, state, args, path + "/new" + instruction.Offset, SearchCaller, out value, out failure);
                                } else if (ILCallTargets.TryConstruction(target, out var constructedType, out var constructor)) {
                                    state = this.Construct(constructor, constructedType, state, args, path + "/new" + instruction.Offset, SearchCaller, out value, out failure);
                                } else if (!this.Call(target, args, state, site, out value)) {
                                    if (!exact) { this.Gap("UnknownDispatch", target); value = Value.Plain(returnType); }
                                    else {
                                        state = this.Invoke(target, state, args, path + "/call" + instruction.Offset, SearchCaller, out value, out failure);
                                    }
                                } else Fail(state, instruction.Offset, unwind); // An opaque/native terminal may fail after its effects, before returning a handle.
                                if (failure != null) foreach (var failed in failure) UnwindFailure(failed, instruction.Offset, unwind);
                                if (state == null) continue;
                                if (returnType != typeof(void)) state.stack.Add(value);
                            } else if (op == OpCodes.Nop || op.OpCodeType == OpCodeType.Prefix) {
                            } else if (TryIndex(op, instruction.Operand, true, method, out var argument)) {
                                if (op == OpCodes.Starg || op == OpCodes.Starg_S) state.cells[arguments[argument]] = state.Pop();
                                else {
                                    var type = argument == 0 && !method.IsStatic ? method.DeclaringType.MakeByRefType() :
                                        method.GetParameters()[argument - (method.IsStatic ? 0 : 1)].ParameterType;
                                    state.stack.Add(op == OpCodes.Ldarga || op == OpCodes.Ldarga_S ? Value.Address(type, arguments[argument]) : state.Cell(arguments[argument]));
                                }
                            } else if (TryIndex(op, instruction.Operand, false, method, out var local)) {
                                if (op.Name.StartsWith("stloc", StringComparison.Ordinal)) state.cells[locals[local]] = state.Pop();
                                else state.stack.Add(op == OpCodes.Ldloca || op == OpCodes.Ldloca_S ? Value.Address(body.LocalVariables[local].LocalType, locals[local]) : state.Cell(locals[local]));
                            } else if (op == OpCodes.Dup) { var value = state.Pop(); state.stack.Add(value); state.stack.Add(value);
                            } else if (op == OpCodes.Pop) state.Pop();
                            else if (op == OpCodes.Isinst || op == OpCodes.Castclass) {
                                if (op == OpCodes.Castclass) Fail(state, instruction.Offset, unwind);
                                var value = state.Pop(); var type = (Type)instruction.Operand;
                                if (value.tokens.Length != 0 || value.addresses.Length != 0 || Tracked(type) || Tracked(value.type))
                                    this.Gap("OpaqueBoxedStorage", method);
                                // Type tests themselves do not run user code. Keep the
                                // match unknown rather than assuming the exception type.
                                state.stack.Add(new Value { type = type, runtimeType = value.runtimeType &&
                                    (op == OpCodes.Castclass || type == typeof(Type) || type == typeof(object)) });
                            }
                            else if (op == OpCodes.Ldtoken && instruction.Operand is Type) {
                                // ldtoken/GetTypeFromHandle is a runtime type identity,
                                // not an instance or a trigger for the named .cctor.
                                state.stack.Add(new Value { type = typeof(RuntimeTypeHandle), runtimeTypeHandle = true });
                            }
                            else if (op.Name.StartsWith("ldc.i4", StringComparison.Ordinal)) {
                                var number = op == OpCodes.Ldc_I4_M1 ? -1 : instruction.Operand != null ? Convert.ToInt32(instruction.Operand) : op.Name[op.Name.Length - 1] - '0';
                                state.stack.Add(Value.Constant(number));
                            }
                            else if (op == OpCodes.Ceq || op == OpCodes.Cgt || op == OpCodes.Cgt_Un || op == OpCodes.Clt || op == OpCodes.Clt_Un) {
                                var right = state.Pop().constant; var left = state.Pop().constant;
                                if (left == null || right == null) state.stack.Add(Value.Plain(typeof(int)));
                                else {
                                    var test = op == OpCodes.Ceq ? left == right : op == OpCodes.Cgt ? left > right : op == OpCodes.Clt ? left < right :
                                        op == OpCodes.Cgt_Un ? unchecked((uint)left.Value) > unchecked((uint)right.Value) : unchecked((uint)left.Value) < unchecked((uint)right.Value);
                                    state.stack.Add(Value.Constant(test ? 1 : 0));
                                }
                            }
                            else if (op == OpCodes.Initobj) {
                                var address = state.Pop();
                                if (address.addresses.Length == 0) Fail(state, instruction.Offset, unwind);
                                this.CheckStorageType(address, (Type)instruction.Operand, method);
                                this.Store(state, address, Value.Plain((Type)instruction.Operand), method, site);
                            }
                            else if (op == OpCodes.Ldobj || op.Name.StartsWith("ldind.", StringComparison.Ordinal)) {
                                var address = state.Pop();
                                if (address.addresses.Length == 0) Fail(state, instruction.Offset, unwind);
                                this.CheckIndirectType(address, op, instruction.Operand as Type, method);
                                var value = this.ReadStorage(state, address, method, site);
                                // Numeric indirection can truncate/sign-extend bits.
                                // It cannot forward a handle, type identity or an
                                // unconverted scalar constant from the storage cell.
                                state.stack.Add(op == OpCodes.Ldobj || op == OpCodes.Ldind_Ref ? value : Value.Plain());
                            }
                            else if (op == OpCodes.Stobj || op.Name.StartsWith("stind.", StringComparison.Ordinal)) {
                                var value = state.Pop(); var address = state.Pop();
                                if (address.addresses.Length == 0) Fail(state, instruction.Offset, unwind);
                                this.CheckIndirectType(address, op, instruction.Operand as Type, method);
                                if (op != OpCodes.Stobj && op != OpCodes.Stind_Ref) value = Value.Plain(Element(address.type));
                                this.Store(state, address, value, method, site);
                            }
                            else if (op == OpCodes.Cpobj) {
                                var source = state.Pop(); var destination = state.Pop();
                                if (source.addresses.Length == 0 || destination.addresses.Length == 0) Fail(state, instruction.Offset, unwind);
                                this.CheckStorageType(source, (Type)instruction.Operand, method);
                                this.CheckStorageType(destination, (Type)instruction.Operand, method);
                                var value = this.ReadStorage(state, source, method, site);
                                this.Store(state, destination, value, method, site);
                            }
                            else if (instruction.Operand is FieldInfo field &&
                                     (op == OpCodes.Ldfld || op == OpCodes.Ldflda || op == OpCodes.Ldsfld || op == OpCodes.Ldsflda || op == OpCodes.Stfld || op == OpCodes.Stsfld)) {
                                Fail(state, instruction.Offset, unwind);
                                if (field.IsStatic && this.HasOpaqueInitializer(field.DeclaringType)) this.Gap("TypeInitialization", method);
                                if (Tracked(field.FieldType)) this.Gap("OpaqueHandleStorage", method);
                                if (op == OpCodes.Stfld || op == OpCodes.Stsfld) state.Pop();
                                var receiver = !field.IsStatic ? state.Pop() : null;
                                if (receiver?.componentStorage == true || typeof(IComponentBase).IsAssignableFrom(field.DeclaringType) ||
                                    typeof(IRefOp).IsAssignableFrom(field.FieldType)) this.Access(state, site);
                                if (op != OpCodes.Stfld && op != OpCodes.Stsfld) {
                                    var value = Value.Plain(field.FieldType);
                                    value.opaqueAddress |= op == OpCodes.Ldflda || op == OpCodes.Ldsflda;
                                    if (op == OpCodes.Ldflda || op == OpCodes.Ldsflda) value.storageType = field.FieldType;
                                    value.componentStorage = op == OpCodes.Ldflda && receiver?.componentStorage == true;
                                    value.knownComponentStorage = op == OpCodes.Ldflda && receiver?.knownComponentStorage == true;
                                    // A primitive field of an ordinary managed object
                                    // cannot carry a JobHandle or alias an unmanaged ECS
                                    // component. Do not extend this to value-type fields:
                                    // their receiver may itself be a borrowed component ref.
                                    value.nonComponentStorage = (op == OpCodes.Ldflda || op == OpCodes.Ldsflda) &&
                                        field.DeclaringType.IsClass && !typeof(IComponentBase).IsAssignableFrom(field.DeclaringType) &&
                                        (field.FieldType.IsPrimitive || field.FieldType.IsEnum);
                                    state.stack.Add(value);
                                }
                            } else {
                                if (op == OpCodes.Div || op == OpCodes.Div_Un || op == OpCodes.Rem || op == OpCodes.Rem_Un ||
                                    op.Name.Contains("ovf")) Fail(state, instruction.Offset, unwind);
                                // Scalars cannot carry coverage. Unsupported opcodes still
                                // invalidate the proof, even when their stack shape is known.
                                var scalar = op.Name.StartsWith("ldc.", StringComparison.Ordinal) || op == OpCodes.Ldnull || op == OpCodes.Ldstr || op == OpCodes.Sizeof ||
                                    op.Name.StartsWith("conv.", StringComparison.Ordinal) || op == OpCodes.Add || op == OpCodes.Sub || op == OpCodes.Mul ||
                                    op == OpCodes.Div || op == OpCodes.Rem || op == OpCodes.And || op == OpCodes.Or || op == OpCodes.Xor || op == OpCodes.Not || op == OpCodes.Neg ||
                                    op == OpCodes.Ceq || op == OpCodes.Cgt || op == OpCodes.Cgt_Un || op == OpCodes.Clt || op == OpCodes.Clt_Un;
                                if (!scalar) this.Gap("UnsupportedOpcode " + op.Name, method);
                                PopFixed(state, op);
                                var pushes = PushCount(op);
                                for (var push = 0; push < pushes; ++push) state.stack.Add(Value.Plain());
                            }
                            if (fallthrough && index + 1 < instructions.Length) Enqueue(instructions[index + 1].Offset, state, unwind);
                        }
                        returnValue = result ?? Value.Plain((method as MethodInfo)?.ReturnType);
                        failures = thrown;
                        accepted = filterAccepted; rejected = filterRejected;
                        return exit;
                    } finally { if (filter) --this.filterDepth; }
                }
                return Run(start, instructions[0].Offset, body.GetILAsByteArray().Length, false, searchOuter,
                    out returned, out exceptional, out _, out _);
            } finally { this.active.Remove(method); }
        }

        private State Construct(ConstructorInfo constructor, Type type, State state, Value[] args, string path,
            Func<State, List<Failure>> searchOuter, out Value value, out List<Failure> exceptional) {
            value = Value.Plain(type);
            exceptional = null;
            if (constructor == null) {
                // Activator.CreateInstance<T>() for an ordinary default value
                // type has no constructor body. User-defined parameterless
                // constructors are resolved by ILCallTargets.TryConstruction.
                if (this.HasOpaqueInitializer(type)) this.gaps.Add("TypeInitialization: " + type);
                return state;
            }
            var receiver = this.Cell(path + "/object");
            state.cells[receiver] = value;
            var inputs = new[] { Value.Address(type, receiver) }.Concat(args).ToArray();
            // IL already encodes field initializers, argument evaluation and
            // base/this chaining in execution order. Never instantiate the type.
            state = this.Invoke(constructor, state, inputs, path, searchOuter, out _, out exceptional);
            if (state != null) value = state.Cell(receiver);
            return state;
        }

        private void CheckStorageType(Value address, Type type, MethodBase method) {
            if (address.storageType != null ? address.storageType != type : address.addresses.Length != 0 || address.knownComponentStorage)
                this.Gap("ReinterpretedStorage", method);
        }

        private void CheckIndirectType(Value address, OpCode op, Type type, MethodBase method) {
            if (type != null) { this.CheckStorageType(address, type, method); return; }
            var storage = address.storageType;
            if (storage == null) { if (address.addresses.Length != 0) this.Gap("ReinterpretedStorage", method); return; }
            if (storage.IsEnum) storage = Enum.GetUnderlyingType(storage);
            var suffix = op.Name.Substring(op.Name.IndexOf('.') + 1);
            var valid = suffix == "ref" ? !storage.IsValueType && !storage.IsPointer && !storage.IsByRef :
                suffix == "i1" || suffix == "u1" ? storage == typeof(byte) || storage == typeof(sbyte) || storage == typeof(bool) :
                suffix == "i2" || suffix == "u2" ? storage == typeof(short) || storage == typeof(ushort) || storage == typeof(char) :
                suffix == "i4" || suffix == "u4" ? storage == typeof(int) || storage == typeof(uint) :
                suffix == "i8" ? storage == typeof(long) || storage == typeof(ulong) :
                suffix == "r4" ? storage == typeof(float) : suffix == "r8" ? storage == typeof(double) :
                suffix == "i" && (storage == typeof(IntPtr) || storage == typeof(UIntPtr) || storage.IsPointer || storage.IsByRef);
            if (!valid) this.Gap("ReinterpretedStorage", method);
        }

        private Value ReadStorage(State state, Value address, MethodBase method, string site) {
            // A pointer/ref returned by an opaque producer is not a local value.
            // Dropping an ldobj result still performs its memory access.
            if (address.componentStorage) this.Access(state, site);
            if (address.knownComponentStorage) return Value.Plain(Element(address.type));
            if (address.addresses.Length == 0 && !address.nonComponentStorage) this.Gap("OpaqueStorageRead", method);
            return address.nonComponentStorage ? Value.Plain(address.type) : state.Read(address);
        }

        private bool Call(MethodBase candidate, Value[] args, State state, string site, out Value returned) {
            var method = candidate as MethodInfo;
            returned = Value.Plain(method?.ReturnType);
            // Only the actual CLR object constructor is an empty base contract.
            // A user constructor, even with the same name/signature, is expanded.
            if (candidate is ConstructorInfo constructor && constructor.DeclaringType == typeof(object) &&
                !constructor.IsStatic && constructor.GetParameters().Length == 0) return true;
            if (method == null) return false;
            var parameters = method.GetParameters(); var owner = method.DeclaringType;
            if (owner == typeof(Type) && method.IsStatic && method.Name == "GetTypeFromHandle" && method.ReturnType == typeof(Type) &&
                parameters.Length == 1 && parameters[0].ParameterType == typeof(RuntimeTypeHandle)) {
                returned = new Value { type = typeof(Type), runtimeType = state.Read(args[0]).runtimeTypeHandle };
                return true;
            }
            if (this.BurstStorage(method, args, state, site, out returned)) return true;
            if (this.NativeMemory(method, args, state, site, out returned)) return true;
            if (ILDelegateTargets.IsInvoke(method)) { this.Gap("DelegateInvoke", method); return true; }
            if (owner == typeof(JobHandle) && method.IsPublic && !method.IsGenericMethod) {
                if (!method.IsStatic && method.Name == "Complete" && parameters.Length == 0 && method.ReturnType == typeof(void)) {
                    this.Complete(state, args[0], site); return true;
                }
                if (!method.IsStatic && method.Name == "get_IsCompleted" && parameters.Length == 0 && method.ReturnType == typeof(bool) ||
                    method.IsStatic && method.Name == "ScheduleBatchedJobs" && parameters.Length == 0 && method.ReturnType == typeof(void)) return true;
                if (method.IsStatic && method.Name == "CombineDependencies" && method.ReturnType == typeof(JobHandle) && parameters.Length >= 2 &&
                    parameters.All(parameter => parameter.ParameterType == typeof(JobHandle))) {
                    returned = Combined(typeof(JobHandle), args.Select(state.Read)); return true;
                }
                if (method.IsStatic && method.Name == "CompleteAll" && method.ReturnType == typeof(void) && parameters.Length >= 2 &&
                    parameters.All(parameter => parameter.ParameterType == typeof(JobHandle).MakeByRefType())) {
                    foreach (var value in args) this.Complete(state, value, site); return true;
                }
            }
            if (owner == typeof(SystemContext) && !method.IsStatic) {
                if (method.Name == "get_dependsOn" && parameters.Length == 0 && method.ReturnType == typeof(JobHandle)) {
                    returned = Combined(typeof(JobHandle), new[] { state.Read(args[0]) }); return true;
                }
                if ((method.Name == "SetDependency" || method.Name == "AddDependency") && parameters.Length > 0 && method.ReturnType == typeof(void) &&
                    parameters.All(parameter => Element(parameter.ParameterType) == typeof(JobHandle))) {
                    var values = args.Skip(1).Select(state.Read);
                    if (method.Name == "AddDependency") values = values.Concat(new[] { state.Read(args[0]) });
                    this.Store(state, args[0], Combined(typeof(SystemContext), values), method, site); return true;
                }
            }
            if ((owner == typeof(API) || owner == typeof(APIExt)) && method.IsStatic && method.Name == "Query" && method.ReturnType == typeof(QueryBuilder) &&
                parameters.Any(parameter => Element(parameter.ParameterType) == typeof(SystemContext)) &&
                parameters.All(parameter => Element(parameter.ParameterType) == typeof(SystemContext) || parameter.ParameterType == typeof(JobHandle) || parameter.ParameterType == typeof(bool) ||
                    method.IsGenericMethod && typeof(ISystem).IsAssignableFrom(Element(parameter.ParameterType)))) {
                returned = Combined(typeof(QueryBuilder), args.Select(state.Read)); return true;
            }
            if (owner == typeof(QueryBuilder) && !method.IsStatic && method.Name == "WaitForAllJobs" && parameters.Length == 0 && method.ReturnType == typeof(QueryBuilder)) {
                returned = state.Read(args[0]); this.Complete(state, args[0], site); return true;
            }
            if (SourceGeneratorScheduledJobsValidation.IsSchedulingMethod(method)) {
                // ScheduleByRef copies the job NOW, not when its dependency
                // completes. A borrowed job may reside inside a component;
                // a proven local job passed through ref helpers does not.
                var job = method.GetGenericArguments()[0];
                for (var index = 0; index < parameters.Length; ++index)
                    if (parameters[index].ParameterType.IsByRef && Element(parameters[index].ParameterType) == job &&
                        !this.LocalStorage(state, args[index + (method.IsStatic ? 0 : 1)])) this.Access(state, site + "/job-input");
                var dependencies = Combined(typeof(JobHandle), args.Select(state.Read));
                if (!this.tokens.TryGetValue(site, out var token)) this.tokens.Add(site, token = this.tokens.Count * 2 + 1);
                // One static site can execute repeatedly. Do not let a saved
                // previous-generation handle complete its new submission.
                dependencies = dependencies.Retire(token, token + 1, state.pending.Contains(token), state.pending.Contains(token + 1));
                state.Retire(token, token + 1);
                state.pending.Add(token);
                returned = Combined(typeof(JobHandle), new[] { dependencies, new Value { tokens = new[] { token } } });
                return true;
            }
            if (method.IsGenericMethod && method.IsDefined(typeof(SafetyCheckAttribute), false) &&
                method.GetGenericArguments().Any(type => typeof(IComponentBase).IsAssignableFrom(type))) {
                this.Access(state, site);
                if (owner == typeof(EntExt) && (method.Name == "Set" || method.Name == "Remove" || method.Name == "SetOneShot" || method.Name == "SetTag") &&
                    method.GetGenericArguments().Any(type => typeof(IComponentDestroy).IsAssignableFrom(type))) {
                    // Set/Remove may invoke a replaceable registry callback; a
                    // prior Complete does not cover work scheduled by that callback.
                    // SetOneShot(CurrentTick) and SetTag forward to these APIs.
                    this.Gap("UnclosedDestroyRegistry", method);
                }
                // User annotations describe access, not absence of scheduling or
                // other effects. Continue through user bodies as ordinary calls.
                return method.DeclaringType.Assembly == typeof(Ent).Assembly;
            }
            return false;
        }

        private static bool ExactStorage(Value address, Type type) => address.storageType == type &&
            (address.addresses.Length != 0 && !address.opaqueAddress || address.knownComponentStorage || address.nonComponentStorage);

        private static Value NativeReference(Value address, Type type, bool sameLocation) {
            var exact = sameLocation && ExactStorage(address, type);
            return new Value {
                type = type.MakeByRefType(), storageType = exact ? type : null,
                addresses = exact ? address.addresses : Array.Empty<int>(),
                opaqueAddress = !exact || address.opaqueAddress,
                componentStorage = address.componentStorage || typeof(IComponentBase).IsAssignableFrom(type) && !exact,
                knownComponentStorage = exact && address.knownComponentStorage,
                nonComponentStorage = exact && address.nonComponentStorage,
            };
        }

        private Value NativeRead(State state, Value address, Type type, MethodInfo method, string site) {
            if (ExactStorage(address, type)) return this.ReadStorage(state, address, method, site);
            // An untyped pointer can point into a component even for scalar T.
            // Reading it cannot grant a handle/type-identity certificate.
            this.Access(state, site);
            if (Tracked(type)) this.Gap("OpaqueHandleStorage", method);
            return Value.Plain(type);
        }

        private void NativeWrite(State state, Value address, Value value, Type type, MethodInfo method, string site) {
            if (ExactStorage(address, type)) this.Store(state, address, value, method, site);
            else {
                this.Access(state, site);
                // The destination might alias a handle/context that we still
                // track. Do not keep certifying that handle after unknown writes.
                this.Gap("OpaqueNativeWrite", method);
            }
        }

        private bool NativeMemory(MethodInfo method, Value[] args, State state, string site, out Value returned) {
            returned = Value.Plain(method.ReturnType);
            if (method.DeclaringType != typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtility)) return false;
            if (method.IsPublic && method.IsStatic && method.IsGenericMethod && !method.ContainsGenericParameters &&
                method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 0 && method.ReturnType == typeof(int) &&
                (method.Name == "SizeOf" || method.Name == "AlignOf")) return true;
            if (!ILNativeComponentAccess.Memory(method, out _)) return false;
            var types = method.GetGenericArguments(); var type = types[0];
            switch (method.Name) {
                case "AddressOf":
                case "As":
                case "AsRef":
                case "ArrayElementAsRef":
                    var target = method.Name == "As" ? types[1] : type;
                    returned = NativeReference(args[0], target, type == target &&
                        (method.Name != "ArrayElementAsRef" || state.Read(args[1]).constant == 0));
                    if (Tracked(target) && !ExactStorage(returned, target)) this.Gap("OpaqueHandleStorage", method);
                    return true;
                case "ReadArrayElement":
                case "ReadArrayElementWithStride":
                    returned = this.NativeRead(state, NativeReference(args[0], type, state.Read(args[1]).constant == 0), type, method, site);
                    return true;
                case "WriteArrayElement":
                case "WriteArrayElementWithStride":
                    this.NativeWrite(state, NativeReference(args[0], type, state.Read(args[1]).constant == 0), args[args.Length - 1], type, method, site);
                    return true;
                case "CopyPtrToStructure":
                case "CopyStructureToPtr":
                    this.NativeWrite(state, args[1], this.NativeRead(state, args[0], type, method, site), type, method, site);
                    return true;
                default: return false;
            }
        }

        private bool BurstStorage(MethodInfo method, Value[] args, State state, string site, out Value returned) {
            returned = Value.Plain(method.ReturnType);
            var owner = method.DeclaringType;
            var shared = owner?.IsGenericType == true && owner.GetGenericTypeDefinition() == typeof(Unity.Burst.SharedStatic<>);
            if (!shared && owner != typeof(Unity.Burst.BurstRuntime)) return false;
            if (!method.IsPublic || method.IsAbstract || method.IsVirtual || method.ContainsGenericParameters ||
                (method.CallingConvention & CallingConventions.VarArgs) != 0) return false;
            var parameters = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var arity = method.GetGenericArguments().Length;
            if (shared && !method.IsStatic && arity == 0 && parameters.Length == 0) {
                var element = owner.GetGenericArguments()[0];
                if (method.Name == "get_UnsafeDataPointer" && method.ReturnType == typeof(void).MakePointerType()) return true;
                if (method.Name != "get_Data" || method.ReturnType != element.MakeByRefType()) return false;
                returned.storageType = element;
                if (Tracked(element)) this.Gap("OpaqueHandleStorage", method);
                if (typeof(IComponentBase).IsAssignableFrom(element)) {
                    // Retain the borrowed address until its actual use. Counting
                    // only this getter misses a read after newly scheduled work.
                    returned.componentStorage = returned.knownComponentStorage = true;
                    this.Access(state, site);
                }
                return true;
            }
            if (!method.IsStatic) return false;
            var typeArguments = 0;
            if (shared && method.ReturnType == owner) {
                if (method.Name == "GetOrCreate" && (arity == 1 || arity == 2) && parameters.SequenceEqual(new[] { typeof(uint) }) ||
                    method.Name == "GetOrCreateUnsafe" && arity == 0 && parameters.SequenceEqual(new[] { typeof(uint), typeof(long), typeof(long) }) ||
                    (method.Name == "GetOrCreatePartiallyUnsafeWithHashCode" || method.Name == "GetOrCreatePartiallyUnsafeWithSubHashCode") && arity == 1 &&
                    parameters.SequenceEqual(new[] { typeof(uint), typeof(long) })) return true;
                if (method.Name == "GetOrCreate" && arity == 0 && (parameters.Length == 2 || parameters.Length == 3) &&
                    parameters.Last() == typeof(uint) && parameters.Take(parameters.Length - 1).All(type => type == typeof(Type)))
                    typeArguments = parameters.Length - 1;
            } else if (owner == typeof(Unity.Burst.BurstRuntime) &&
                       (method.Name == "GetHashCode32" && method.ReturnType == typeof(int) || method.Name == "GetHashCode64" && method.ReturnType == typeof(long))) {
                if (arity == 1 && parameters.Length == 0) return true;
                if (arity == 0 && parameters.SequenceEqual(new[] { typeof(Type) })) typeArguments = 1;
            }
            if (typeArguments == 0) return false;
            // A Type parameter may be a user subclass with virtual metadata
            // callbacks. Only proven ldtoken-derived instances have a leaf body.
            if (!args.Take(typeArguments).All(value => state.Read(value).runtimeType)) this.Gap("UnknownBurstTypeIdentity", method);
            return true;
        }

        private static bool TryIndex(OpCode op, object operand, bool arguments, MethodBase method, out int index) {
            index = -1;
            var name = op.Name;
            if (!name.StartsWith(arguments ? "ldarg" : "ldloc", StringComparison.Ordinal) && !name.StartsWith(arguments ? "starg" : "stloc", StringComparison.Ordinal)) return false;
            if (operand is ParameterInfo parameter) index = parameter.Position + (method.IsStatic ? 0 : 1);
            else if (operand is LocalVariableInfo local) index = local.LocalIndex;
            else if (operand != null) index = Convert.ToInt32(operand);
            else if (name.Length > 0 && char.IsDigit(name[name.Length - 1])) index = name[name.Length - 1] - '0';
            if (index < 0) throw new InvalidOperationException("Unbound IL argument/local index");
            return true;
        }
        private static void PopFixed(State state, OpCode op) {
            int count;
            switch (op.StackBehaviourPop) {
                case StackBehaviour.Pop0: count = 0; break;
                case StackBehaviour.Pop1: case StackBehaviour.Popi: case StackBehaviour.Popref: count = 1; break;
                case StackBehaviour.Pop1_pop1: case StackBehaviour.Popi_pop1: case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8: case StackBehaviour.Popi_popr4: case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1: case StackBehaviour.Popref_popi: count = 2; break;
                case StackBehaviour.Popi_popi_popi: case StackBehaviour.Popref_popi_pop1: case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8: case StackBehaviour.Popref_popi_popr4: case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref: count = 3; break;
                default: throw new InvalidOperationException("Unsupported IL stack pop: " + op);
            }
            for (var index = 0; index < count; ++index) state.Pop();
        }
        private static int PushCount(OpCode op) {
            switch (op.StackBehaviourPush) {
                case StackBehaviour.Push0: return 0;
                case StackBehaviour.Push1: case StackBehaviour.Pushi: case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4: case StackBehaviour.Pushr8: case StackBehaviour.Pushref: return 1;
                default: throw new InvalidOperationException("Unsupported IL stack push: " + op);
            }
        }
    }
}
