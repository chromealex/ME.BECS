using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Tracks the isReadonly VALUE in query structs, not a flag on the enclosing method.
// The finite lattice is a union of 0/1 and symbolic parameter values pN; ? is unknown.
// A join containing 0 must never narrow a job's argument accesses to read-only.
internal sealed class QueryScheduleModeFlow {
    internal const string Schema = "schedule-mode-schema=1";
    private readonly Compilation compilation;
    private readonly IMethodSymbol method;
    private readonly System.Threading.CancellationToken cancellation;
    private readonly Func<IOperation, bool> omitted;
    private readonly Dictionary<IInvocationOperation, string> calls = new Dictionary<IInvocationOperation, string>();
    private bool record;
    private Action<State>? exceptional;

    internal QueryScheduleModeFlow(Compilation compilation, IMethodSymbol method, System.Threading.CancellationToken cancellation, Func<IOperation, bool> omitted) {
        this.compilation = compilation;
        this.method = method;
        this.cancellation = cancellation;
        this.omitted = omitted;
    }

    internal static string Union(string left, string right) {
        if (left == right) return left;
        if (left == "?" || right == "?") return "?";
        return string.Join("|", left.Split('|').Concat(right.Split('|')).Distinct().OrderBy(value => value, StringComparer.Ordinal));
    }

    private bool Is(ITypeSymbol? type, string name) => type != null &&
        SymbolEqualityComparer.Default.Equals(type, this.compilation.GetTypeByMetadataName(name));
    private bool IsQuery(ITypeSymbol? type) => this.Is(type, "ME.BECS.QueryBuilder") || this.Is(type, "ME.BECS.QueryBuilderDisposable");
    private bool IsValue(ITypeSymbol? type) => this.IsQuery(type) || type?.SpecialType == SpecialType.System_Boolean;

    private State InitialState() {
        var state = new State();
        // Populate symbolic parameters before any join. A missing local means
        // unknown, but an unassigned parameter still has its incoming pN value.
        foreach (var parameter in this.method.Parameters)
            if (parameter.RefKind is RefKind.None or RefKind.In && this.IsValue(parameter.Type))
                state.Variables.Add(parameter, "p" + parameter.Ordinal.ToString(CultureInfo.InvariantCulture));
        return state;
    }

    private sealed class State {
        internal readonly Dictionary<ISymbol, string> Variables = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        internal readonly Dictionary<CaptureId, string> Captures = new Dictionary<CaptureId, string>();
        internal bool Escaped;
        internal State Copy() {
            var copy = new State { Escaped = this.Escaped };
            foreach (var entry in this.Variables) copy.Variables.Add(entry.Key, entry.Value);
            foreach (var entry in this.Captures) copy.Captures.Add(entry.Key, entry.Value);
            return copy;
        }
        internal void ForgetValues() {
            foreach (var key in this.Variables.Keys.ToArray()) this.Variables[key] = "?";
            this.ForgetCaptures();
        }
        internal void ForgetCaptures() {
            foreach (var key in this.Captures.Keys.ToArray()) this.Captures[key] = "?";
        }
        internal bool Merge(State other) {
            var changed = !this.Escaped && other.Escaped;
            this.Escaped |= other.Escaped;
            foreach (var key in this.Variables.Keys.Concat(other.Variables.Keys).Distinct(SymbolEqualityComparer.Default).ToArray()) {
                var merged = Union(this.Variables.TryGetValue(key, out var a) ? a : "?", other.Variables.TryGetValue(key, out var b) ? b : "?");
                if (!this.Variables.TryGetValue(key, out var previous) || merged != previous) { changed = true; this.Variables[key] = merged; }
            }
            foreach (var key in this.Captures.Keys.Concat(other.Captures.Keys).Distinct().ToArray()) {
                var merged = Union(this.Captures.TryGetValue(key, out var a) ? a : "?", other.Captures.TryGetValue(key, out var b) ? b : "?");
                if (!this.Captures.TryGetValue(key, out var previous) || merged != previous) { changed = true; this.Captures[key] = merged; }
            }
            return changed;
        }
    }

    private sealed class Cleanup {
        internal readonly ControlFlowRegion Region;
        internal readonly ControlFlowRegion[] Remaining;
        internal readonly int? Target;
        internal readonly Cleanup? Parent;
        internal readonly string Key;
        internal readonly int Depth;
        internal Cleanup(ControlFlowRegion[] regions, int? target, Cleanup? parent) {
            this.Region = regions[0]; this.Remaining = regions.Skip(1).ToArray();
            this.Target = target; this.Parent = parent; this.Depth = (parent?.Depth ?? 0) + 1;
            this.Key = string.Join(",", regions.Select(region => region.FirstBlockOrdinal.ToString(CultureInfo.InvariantCulture))) +
                ":" + (target?.ToString(CultureInfo.InvariantCulture) ?? "throw") + "/" + parent?.Key;
        }
    }

    private sealed class FilterSearch {
        internal readonly int Origin, Candidate;
        internal readonly Cleanup? Cleanup;
        internal readonly string Key;
        internal FilterSearch(int origin, int candidate, Cleanup? cleanup) {
            this.Origin = origin; this.Candidate = candidate; this.Cleanup = cleanup;
            this.Key = origin.ToString(CultureInfo.InvariantCulture) + ":" +
                candidate.ToString(CultureInfo.InvariantCulture) + "/" + cleanup?.Key;
        }
    }

    internal Dictionary<IInvocationOperation, string> Analyze(ControlFlowGraph graph) {
        this.calls.Clear(); this.record = false; this.exceptional = null;
        // Preserve first-pass filter search separately from second-pass cleanup.
        var regions = new Stack<ControlFlowRegion>();
        var hasHandlers = false;
        regions.Push(graph.Root);
        while (regions.Count > 0) {
            var region = regions.Pop();
            hasHandlers |= region.Kind is ControlFlowRegionKind.Finally or ControlFlowRegionKind.Catch or ControlFlowRegionKind.Filter;
            foreach (var child in region.NestedRegions) regions.Push(child);
        }
        if (!hasHandlers) return this.AnalyzeOrdinary(graph);
        var handlers = ExceptionalFlowRoutes.Create(graph, this.cancellation, includeFilters: true);
        if (handlers == null) return this.calls;
        // Do not join a normal leave with an exceptional unwind: only the former
        // can reach the code after try/finally. Nested cleanup has its own resume.
        var inputs = new Dictionary<(int Block, string Resume), (State State, Cleanup? Cleanup, FilterSearch? Filter)>();
        var pending = new Queue<(int Block, string Resume)>();
        var queued = new HashSet<(int Block, string Resume)>();
        var failed = false;
        var steps = 0;
        long keyBytes = 0;
        void Enqueue(int block, Cleanup? cleanup, State state, FilterSearch? filter = null) {
            if (failed) return;
            if (!graph.Blocks[block].IsReachable && graph.Blocks[block].Kind != BasicBlockKind.Exit) return;
            var key = (block, filter != null ? "F" + filter.Key : "C" + cleanup?.Key);
            var changed = !inputs.TryGetValue(key, out var previous);
            if (changed) {
                keyBytes += key.Item2.Length * 2L;
                if (inputs.Count >= 10000 || keyBytes > 16777216L) { failed = true; return; }
                inputs.Add(key, (state.Copy(), cleanup, filter));
            } else changed = previous.State.Merge(state);
            if (changed && queued.Add(key)) pending.Enqueue(key);
        }
        Cleanup? Enclosing(int block, Cleanup? resume) {
            while (resume != null && (block < resume.Region.FirstBlockOrdinal || block > resume.Region.LastBlockOrdinal))
                resume = resume.Parent;
            return resume;
        }
        void Follow(int? target, ControlFlowRegion[] cleanup, Cleanup? resume, State state) {
            if (cleanup.Length != 0) {
                if (cleanup.Length > 64 || resume?.Depth >= 64) { failed = true; return; }
                // Keep an enclosing cleanup alive UNTIL it is actually left. A
                // new exception in this unwind can be caught inside it and must
                // then recover its original pending return/leave continuation.
                var continuation = new Cleanup(cleanup, target, Enclosing(cleanup[0].FirstBlockOrdinal, resume));
                Enqueue(continuation.Region.FirstBlockOrdinal, continuation, state);
            } else if (target.HasValue) Enqueue(target.Value, Enclosing(target.Value, resume), state);
            // A null target denotes an exception escaping this method.
        }
        void Search(int origin, int start, Cleanup? resume, State state) {
            if (failed) return;
            // Type mismatches may skip any candidate. Running a filter is a
            // separate path; its rejection resumes here with its MODIFIED state.
            var candidates = handlers[origin];
            for (var index = start; index < candidates.Length; ++index) {
                this.cancellation.ThrowIfCancellationRequested();
                // Rejected filters can revisit the tail of a long handler list.
                // Bound actual search work, not only dequeued CFG blocks.
                if (++steps > 100000) { failed = true; return; }
                var route = candidates[index];
                if (route.Filter != null)
                    Enqueue(route.Filter.FirstBlockOrdinal, resume, state, new FilterSearch(origin, index, resume));
                else Follow(route.Target, route.Unwind, resume, state);
            }
        }
        Enqueue(0, null, this.InitialState());
        while (pending.Count > 0) {
            this.cancellation.ThrowIfCancellationRequested();
            if (failed || ++steps > 100000) { this.exceptional = null; return this.calls; }
            var key = pending.Dequeue();
            queued.Remove(key);
            var block = graph.Blocks[key.Block];
            var input = inputs[key];
            if (input.Filter == null && input.Cleanup != null && (key.Block < input.Cleanup.Region.FirstBlockOrdinal || key.Block > input.Cleanup.Region.LastBlockOrdinal)) {
                this.exceptional = null; return this.calls;
            }
            var filtering = input.Filter;
            if (filtering != null && (handlers[filtering.Origin][filtering.Candidate].Filter is not { } filterRegion ||
                key.Block < filterRegion.FirstBlockOrdinal || key.Block > filterRegion.LastBlockOrdinal)) {
                this.exceptional = null; return this.calls;
            }
            var state = input.State.Copy();
            // Select the active cleanup by each actual unwind/handler entry, not
            // by the final destination before its cleanup has executed.
            this.exceptional = candidate => {
                if (filtering != null) Search(filtering.Origin, filtering.Candidate + 1, filtering.Cleanup, candidate);
                else Search(key.Block, 0, input.Cleanup, candidate);
            };
            foreach (var operation in block.Operations) this.Evaluate(operation, state);
            if (block.BranchValue != null) this.Evaluate(block.BranchValue, state);
            if (block.Kind == BasicBlockKind.Exit) continue;
            foreach (var edge in new[] { block.FallThroughSuccessor, block.ConditionalSuccessor }) {
                if (edge == null) continue;
                if (edge.Semantics == ControlFlowBranchSemantics.ProgramTermination) continue;
                if (filtering != null) {
                    var route = handlers[filtering.Origin][filtering.Candidate];
                    if (edge.Semantics is ControlFlowBranchSemantics.Throw or ControlFlowBranchSemantics.Rethrow or
                        ControlFlowBranchSemantics.StructuredExceptionHandling) {
                        // CLR treats an exception escaping a filter as false.
                        // Its partial mutations survive; no finally runs yet.
                        Search(filtering.Origin, filtering.Candidate + 1, filtering.Cleanup, state);
                    } else if (edge.Semantics == ControlFlowBranchSemantics.Regular && edge.Destination != null && edge.FinallyRegions.Length == 0) {
                        var target = edge.Destination.Ordinal;
                        if (target >= route.Filter!.FirstBlockOrdinal && target <= route.Filter.LastBlockOrdinal)
                            Enqueue(target, filtering.Cleanup, state, filtering);
                        else if (target == route.Target) Follow(route.Target, route.Unwind, filtering.Cleanup, state);
                        else { failed = true; break; }
                    } else { failed = true; break; }
                    continue;
                }
                if (edge.Semantics is ControlFlowBranchSemantics.Throw or ControlFlowBranchSemantics.Rethrow) {
                    this.exceptional?.Invoke(state);
                } else if (edge.Semantics == ControlFlowBranchSemantics.StructuredExceptionHandling) {
                    var cleanup = input.Cleanup;
                    if (cleanup == null || edge.Destination != null) { failed = true; break; }
                    Follow(cleanup.Target, cleanup.Remaining, cleanup.Parent, state);
                } else if (edge.Semantics is ControlFlowBranchSemantics.Regular or ControlFlowBranchSemantics.Return) {
                    Follow(edge.Destination?.Ordinal, edge.FinallyRegions.ToArray(), input.Cleanup, state);
                } else { failed = true; break; }
            }
        }
        this.exceptional = null;
        if (failed) return this.calls;
        // Emit only after convergence: an early loop iteration is not a proof for later iterations.
        // Now union all continuations visiting the SAME call site; this join must
        // not feed back into normal successors or exceptional exits above.
        this.record = true;
        foreach (var group in inputs.GroupBy(entry => entry.Key.Block)) {
            var block = graph.Blocks[group.Key];
            var state = group.First().Value.State.Copy();
            foreach (var other in group.Skip(1)) state.Merge(other.Value.State);
            foreach (var operation in block.Operations) this.Evaluate(operation, state);
            if (block.BranchValue != null) this.Evaluate(block.BranchValue, state);
        }
        return this.calls;
    }

    private Dictionary<IInvocationOperation, string> AnalyzeOrdinary(ControlFlowGraph graph) {
        // Preserve the compact array-based fixed point for the common case;
        // graphs without finally need no handler tables or continuation keys.
        var inputs = new State?[graph.Blocks.Length];
        inputs[0] = this.InitialState();
        var pending = new Queue<int>();
        var queued = new bool[inputs.Length];
        pending.Enqueue(0); queued[0] = true;
        var steps = 0;
        while (pending.Count != 0) {
            this.cancellation.ThrowIfCancellationRequested();
            if (++steps > 100000) return this.calls;
            var index = pending.Dequeue(); queued[index] = false;
            var block = graph.Blocks[index];
            var state = inputs[index]!.Copy();
            foreach (var operation in block.Operations) this.Evaluate(operation, state);
            if (block.BranchValue != null) this.Evaluate(block.BranchValue, state);
            foreach (var successor in new[] { block.FallThroughSuccessor?.Destination, block.ConditionalSuccessor?.Destination }) {
                if (successor == null || !successor.IsReachable) continue;
                var changed = inputs[successor.Ordinal] == null;
                if (changed) inputs[successor.Ordinal] = state.Copy();
                else changed = inputs[successor.Ordinal]!.Merge(state);
                if (changed && !queued[successor.Ordinal]) { queued[successor.Ordinal] = true; pending.Enqueue(successor.Ordinal); }
            }
        }
        this.record = true;
        foreach (var block in graph.Blocks) {
            if (!block.IsReachable || inputs[block.Ordinal] == null) continue;
            var state = inputs[block.Ordinal]!.Copy();
            foreach (var operation in block.Operations) this.Evaluate(operation, state);
            if (block.BranchValue != null) this.Evaluate(block.BranchValue, state);
        }
        return this.calls;
    }

    private string Read(IOperation? operation, State state) {
        if (state.Escaped) return "?";
        switch (operation) {
            case ILocalReferenceOperation local:
                return SymbolEqualityComparer.Default.Equals(local.Local.ContainingSymbol, this.method) &&
                    local.Local.RefKind == RefKind.None && state.Variables.TryGetValue(local.Local, out var localValue) ? localValue : "?";
            case IParameterReferenceOperation parameter:
                if (parameter.Parameter.RefKind is RefKind.Ref or RefKind.Out ||
                    !SymbolEqualityComparer.Default.Equals(parameter.Parameter.ContainingSymbol, this.method)) return "?";
                return state.Variables.TryGetValue(parameter.Parameter, out var parameterValue) ? parameterValue :
                    "p" + parameter.Parameter.Ordinal.ToString(CultureInfo.InvariantCulture);
            case IFlowCaptureReferenceOperation capture:
                return state.Captures.TryGetValue(capture.Id, out var captureValue) ? captureValue : "?";
            default: return "?";
        }
    }

    private void Write(IOperation? target, string value, State state, bool receiverMutation = false) {
        switch (target) {
            case ILocalReferenceOperation local when local.Local.RefKind == RefKind.None:
                state.Variables[local.Local] = value; state.ForgetCaptures(); break;
            case IParameterReferenceOperation parameter when parameter.Parameter.RefKind == RefKind.None ||
                (!receiverMutation && parameter.Parameter.RefKind == RefKind.In):
                state.Variables[parameter.Parameter] = value; state.ForgetCaptures(); break;
            case IParameterReferenceOperation parameter when receiverMutation && parameter.Parameter.RefKind == RefKind.In:
                break; // A mutable struct method on an in parameter operates on a defensive copy.
            case IFieldReferenceOperation field when field.Field.Name == "isReadonly" && this.IsQuery(field.Field.ContainingType):
                this.Write(field.Instance, value, state); break;
            case IFlowCaptureReferenceOperation capture:
                // CFG captures can hold storage addresses as well as copied values. Invalidate
                // possible aliases instead of assuming a capture is always a value copy.
                state.ForgetValues();
                state.Captures[capture.Id] = value;
                break;
            case ILocalReferenceOperation:
            case IParameterReferenceOperation:
                state.Escaped = true; state.ForgetValues(); break;
        }
    }

    private string Evaluate(IOperation? operation, State state) {
        this.cancellation.ThrowIfCancellationRequested();
        if (operation == null || this.omitted(operation)) return "?";
        if (this.exceptional == null) return this.EvaluateCore(operation, state);
        // Operand/argument effects precede the operation's own possible throw.
        // Calls additionally observe pre-mutation state AFTER arguments below;
        // placing that snapshot before arguments would invent impossible modes.
        var canThrow = operation is IInvocationOperation or IObjectCreationOperation or IPropertyReferenceOperation or
            IBinaryOperation or IUnaryOperation or IFieldReferenceOperation or IArrayElementReferenceOperation or
            ICompoundAssignmentOperation or IIncrementOrDecrementOperation or IInterpolatedStringOperation ||
            operation is IConversionOperation conversion && (conversion.OperatorMethod != null || conversion.IsChecked ||
                !SymbolEqualityComparer.Default.Equals(conversion.Operand.Type, conversion.Type));
        if (operation is IObjectCreationOperation { Constructor.IsImplicitlyDeclared: true, Type.IsValueType: true }) canThrow = false;
        var value = this.EvaluateCore(operation, state);
        if (canThrow) this.exceptional?.Invoke(state);
        return value;
    }

    private string EvaluateCore(IOperation operation, State state) {
        switch (operation) {
            case IAnonymousFunctionOperation:
            case IFlowAnonymousFunctionOperation:
                state.Escaped = true; state.ForgetValues(); return "?";
            case ILocalFunctionOperation: return "?"; // A declaration is not an invocation.
            case ILocalReferenceOperation:
            case IParameterReferenceOperation:
            case IFlowCaptureReferenceOperation: return this.Read(operation, state);
            case IDefaultValueOperation: return this.IsValue(operation.Type) ? "0" : "?";
            case ILiteralOperation literal: return literal.ConstantValue.Value is bool flag ? (flag ? "1" : "0") : "?";
            case IConversionOperation conversion:
                var converted = this.Evaluate(conversion.Operand, state);
                return conversion.OperatorMethod == null && this.IsValue(conversion.Type) ? converted : "?";
            case IFlowCaptureOperation capture:
                var captured = this.Evaluate(capture.Value, state);
                if (this.IsValue(capture.Value.Type)) state.Captures[capture.Id] = captured;
                return captured;
            case ISimpleAssignmentOperation assignment:
                this.Evaluate(assignment.Target, state);
                var assigned = this.Evaluate(assignment.Value, state);
                if (assignment.IsRef && this.IsValue(assignment.Target.Type)) { state.Escaped = true; state.ForgetValues(); return "?"; }
                if (this.IsValue(assignment.Target.Type)) this.Write(assignment.Target, assigned, state);
                return assigned;
            case IVariableDeclaratorOperation variable:
                var initial = this.Evaluate(variable.Initializer?.Value, state);
                if (this.IsValue(variable.Symbol.Type)) {
                    if (variable.Symbol.RefKind != RefKind.None) { state.Escaped = true; state.ForgetValues(); }
                    else { state.Variables[variable.Symbol] = initial; state.ForgetCaptures(); }
                }
                return initial;
            case ICompoundAssignmentOperation compound:
                this.Evaluate(compound.Target, state);
                this.Evaluate(compound.Value, state);
                if (this.IsValue(compound.Target.Type)) this.Write(compound.Target, "?", state);
                return "?";
            case IDeconstructionAssignmentOperation deconstruction:
                this.Evaluate(deconstruction.Value, state);
                state.ForgetValues(); return "?";
            case IInvocationOperation invocation: return this.Invoke(invocation, state);
            case IObjectCreationOperation creation:
                var arguments = creation.Arguments.Select(argument => this.Evaluate(argument.Value, state)).ToArray();
                if (creation.Constructor?.IsImplicitlyDeclared != true || creation.Type?.IsValueType != true) this.exceptional?.Invoke(state);
                foreach (var argument in creation.Arguments) this.InvalidateRefArgument(argument, state);
                if (creation.Initializer != null) { this.Evaluate(creation.Initializer, state); return "?"; }
                if (this.IsQuery(creation.Type) && creation.Arguments.Length == 0 && creation.Constructor?.IsImplicitlyDeclared == true) return "0";
                if (this.Is(creation.Type, "ME.BECS.QueryBuilderDisposable") && creation.Arguments.Length == 1 &&
                    this.Is(creation.Arguments[0].Parameter?.Type, "ME.BECS.QueryBuilder")) return arguments[0];
                return "?";
            case IFieldReferenceOperation field:
                var instance = this.Evaluate(field.Instance, state);
                return field.Field.Name == "isReadonly" && this.IsQuery(field.Field.ContainingType) ? instance : "?";
            case IPropertyReferenceOperation property:
                this.Evaluate(property.Instance, state);
                foreach (var argument in property.Arguments) this.Evaluate(argument.Value, state);
                this.exceptional?.Invoke(state);
                if (this.IsQuery(property.Instance?.Type)) this.Write(property.Instance, "?", state, receiverMutation: true);
                return "?";
            case IAddressOfOperation address:
                this.Evaluate(address.Reference, state);
                if (this.IsValue(address.Reference.Type)) { state.Escaped = true; state.ForgetValues(); }
                return "?";
        }
        foreach (var child in operation.ChildOperations) this.Evaluate(child, state);
        // Unmodeled bool expressions and storage locations never imply read-only.
        return "?";
    }

    private string Invoke(IInvocationOperation operation, State state) {
        var method = operation.TargetMethod;
        var receiver = this.Evaluate(operation.Instance, state);
        var arguments = new Dictionary<int, string>();
        var reduced = method.ReducedFrom != null;
        if (reduced) arguments[0] = receiver;
        foreach (var argument in operation.Arguments) {
            var value = this.Evaluate(argument.Value, state);
            if (argument.Parameter != null) arguments[argument.Parameter.Ordinal + (reduced ? 1 : 0)] = value;
        }
        // For instance calls the struct receiver is storage, and argument evaluation may
        // have modified it. Extension methods taking a struct by value already copied it.
        if (operation.Instance is ILocalReferenceOperation or IParameterReferenceOperation or IFlowCaptureReferenceOperation)
            receiver = this.Read(operation.Instance, state);
        this.exceptional?.Invoke(state);
        var annotations = new StringBuilder();
        var definition = method.ReducedFrom ?? method;
        foreach (var parameter in definition.Parameters) {
            if (this.IsValue(parameter.Type)) annotations.Append("\t!schedule-value-").Append(parameter.Ordinal).Append('=')
                .Append(arguments.TryGetValue(parameter.Ordinal, out var argumentValue) ? argumentValue : "?");
        }
        if (MethodSummaryContracts.IsSchedulingMethod(method, this.compilation)) {
            var mode = "0";
            var queryParameter = definition.Parameters.FirstOrDefault(parameter => this.IsQuery(parameter.Type));
            var boolParameter = definition.Parameters.FirstOrDefault(parameter => parameter.Name == "isReadonly" && parameter.Type.SpecialType == SpecialType.System_Boolean);
            if (queryParameter != null) mode = queryParameter.RefKind == RefKind.None && arguments.TryGetValue(queryParameter.Ordinal, out var queryMode) ? queryMode : "?";
            else if (this.IsQuery(method.ContainingType)) mode = receiver;
            else if (boolParameter != null) mode = arguments.TryGetValue(boolParameter.Ordinal, out var boolMode) ? boolMode : "?";
            annotations.Append("\t!schedule-readonly=").Append(mode);
        }
        var ownBuilder = this.Is(method.ContainingType, "ME.BECS.QueryBuilder") && !method.IsStatic &&
            this.Is(method.ReturnType, "ME.BECS.QueryBuilder");
        var setsReadOnly = ownBuilder && method.Name == "AsReadonly" && method.Arity == 0 && method.Parameters.Length == 0;
        var preservesMode = ownBuilder && this.PreservesMode(method);
        var factory = method.IsStatic && method.Name == "Query" && this.Is(method.ReturnType, "ME.BECS.QueryBuilder") &&
            (this.Is(method.ContainingType, "ME.BECS.API") || this.Is(method.ContainingType, "ME.BECS.APIExt"));
        // These audited builder operations do not schedule jobs themselves. Their
        // receiver/argument expressions still have independent call-summary edges.
        if (setsReadOnly || preservesMode || factory) annotations.Append("\t!query-mode-leaf");
        if (setsReadOnly || preservesMode) annotations.Append("\t!query-mode-only");
        if (QuerySchedulingContracts.DoesNotSchedule(method, this.compilation, setsReadOnly || preservesMode))
            annotations.Append("\t!query-no-schedule");
        if (this.record && annotations.Length != 0) this.calls[operation] = annotations.ToString();
        if (setsReadOnly) {
            this.Write(operation.Instance, "1", state, receiverMutation: true);
            return "1";
        }
        if (preservesMode) return receiver;
        foreach (var argument in operation.Arguments) this.InvalidateRefArgument(argument, state);
        if (reduced && definition.Parameters.Length > 0 && definition.Parameters[0].RefKind != RefKind.None && this.IsValue(operation.Instance?.Type)) {
            this.Write(operation.Instance, "?", state);
            state.Escaped = true;
        }
        if (method.MethodKind is MethodKind.LocalFunction or MethodKind.DelegateInvoke) { state.Escaped = true; state.ForgetValues(); }
        if (this.IsQuery(operation.Instance?.Type)) this.Write(operation.Instance, "?", state, receiverMutation: true);
        if (factory) return "0";
        return "?";
    }

    private void InvalidateRefArgument(IArgumentOperation argument, State state) {
        if (this.IsValue(argument.Value.Type) && argument.Parameter != null && argument.Parameter.RefKind != RefKind.None) {
            this.Write(argument.Value, "?", state);
            // A user helper can retain a reference (e.g. through unsafe code).
            state.Escaped = true;
        }
    }

    private bool PreservesMode(IMethodSymbol method) {
        if (method.Parameters.Length == 0 && method.Arity > 0) {
            var filter = new StringBuilder();
            SystemQueryFilterContracts.Append(filter, method, this.compilation);
            return filter.Length != 0;
        }
        if (method.Arity != 0) return false;
        if (method.Parameters.Length == 0 && method.Name is "AsUnsafe" or "Sort" or "AsJob" or "WithBurst" or "WaitForAllJobs") return true;
        return (method.Name is "AsParallel" or "ParallelFor" && method.Parameters.Length == 1 ||
                method.Name == "Step" && method.Parameters.Length == 2) &&
            method.Parameters.All(parameter => parameter.RefKind == RefKind.None && parameter.Type.SpecialType == SpecialType.System_UInt32);
    }
}
