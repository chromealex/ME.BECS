using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Path-sensitive synchronization analysis. No IL and no execution of user methods.
// A proven result means all modeled direct accesses follow completion of all outstanding
// dependencies. It does not prove job scheduling/return-handle ownership or runtime safety.
internal sealed class SystemSynchronizationSummary {
    internal const string MetadataKey = "ME.BECS.SystemSynchronization.v3";
    private readonly Compilation compilation;
    private readonly CancellationToken cancellation;
    private readonly IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods;
    private readonly ISet<(string Assembly, string Id)> conflicts;
    private readonly Func<string, MethodSummaryType?> decode;
    private readonly Dictionary<(string Assembly, string Id), (MethodSynchronizationProgram? Program, string Reason)> programs = new();
    private readonly Dictionary<ITypeSymbol, string> roles = new(SymbolEqualityComparer.Default);
    private readonly SortedSet<string> gaps = new(StringComparer.Ordinal);
    private readonly SortedSet<string> accesses = new(StringComparer.Ordinal);
    private readonly SortedSet<string> unproven = new(StringComparer.Ordinal);
    private readonly HashSet<string> completions = new(StringComparer.Ordinal);
    private int nextCell, nextToken, steps, paths;
    private long stateKeyBytes;

    private sealed class Value {
        internal static readonly Value Empty = new(Array.Empty<int>());
        internal static readonly Value Unknown = new(Array.Empty<int>(), true);
        internal readonly int[] Tokens;
        internal readonly bool IsUnknown;
        internal Value(int[] tokens, bool unknown = false) { this.Tokens = tokens; this.IsUnknown = unknown; }
        internal static Value Union(IEnumerable<Value> values) {
            var array = values.ToArray();
            return new Value(array.SelectMany(value => value.Tokens).Distinct().OrderBy(token => token).ToArray(), array.Any(value => value.IsUnknown));
        }
    }
    private sealed class State {
        internal readonly Dictionary<int, Value> Cells = new();
        internal readonly HashSet<int> Pending = new();
        internal State Clone() {
            var result = new State();
            foreach (var pair in this.Cells) result.Cells.Add(pair.Key, pair.Value); // Values are immutable.
            result.Pending.UnionWith(this.Pending);
            return result;
        }
    }
    private sealed class Frame {
        internal string Id = "", Key = "", ReturnRole = "";
        internal int ReturnCell;
        internal MethodSynchronizationProgram Program = null!;
        internal readonly Dictionary<string, MethodSummaryType> Types = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> Roles = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, int> Cells = new(StringComparer.Ordinal);
        internal readonly List<int> OwnedCells = new();
    }
    private readonly struct Result {
        internal readonly State State;
        internal readonly Value Value;
        internal readonly bool Throws;
        internal Result(State state, Value value, bool throws = false) { this.State = state; this.Value = value; this.Throws = throws; }
    }
    private sealed class Unwind {
        internal readonly int Region;
        internal readonly int[] Remaining;
        internal readonly int? Target;
        internal readonly string Semantics, Key;
        internal readonly Unwind? Parent;
        internal readonly int Depth;
        internal Unwind(int region, int[] remaining, int? target, string semantics, Unwind? parent) {
            this.Region = region; this.Remaining = remaining; this.Target = target; this.Semantics = semantics; this.Parent = parent;
            this.Depth = (parent?.Depth ?? 0) + 1;
            this.Key = Number(region) + ":" + string.Join(",", remaining.Select(Number)) + ":" +
                (target.HasValue ? Number(target.Value) : "-") + ":" + semantics + "/" + parent?.Key;
        }
    }
    private readonly struct Binding {
        internal readonly string Mode;
        internal readonly int? Cell;
        internal Binding(string mode, int? cell) { this.Mode = mode; this.Cell = cell; }
    }

    internal SystemSynchronizationSummary(Compilation compilation, CancellationToken cancellation,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, Func<string, MethodSummaryType?> decode) {
        this.compilation = compilation;
        this.cancellation = cancellation;
        this.methods = methods;
        this.conflicts = conflicts;
        this.decode = decode;
    }

    internal string Analyze(string assembly, MethodSummaryGraph.Summary root, string? rootBinding) {
        this.gaps.Clear(); this.accesses.Clear(); this.unproven.Clear(); this.completions.Clear();
        this.nextCell = 0; this.nextToken = 1; this.steps = 0; this.paths = 0; this.stateKeyBytes = 0;
        var state = new State();
        var entry = this.NewCell(state, new Value(new[] { 1 }));
        state.Pending.Add(1);
        var frame = this.CreateFrame(assembly, root.Id, root.RootArguments ?? root.Environment, state,
            new Binding("value", null), new Dictionary<int, Binding> { [0] = new("ref", entry) });
        if (frame != null) {
            if (frame.Program.Slots.Values.Count(slot => slot.Kind == "P") != 1 ||
                !frame.Roles.TryGetValue("p0", out var role) || role != "C" || frame.Program.Slots["p0"].Passing != "ref")
                this.gaps.Add("UnsupportedLifecycleParameters: " + root.Id);
            else this.Execute(frame, state, new HashSet<string>(StringComparer.Ordinal));
        }
        if (rootBinding == null) this.gaps.Add("MissingLifecycleBinding");
        var system = root.Flags.Single(flag => flag.StartsWith("system-type=", StringComparison.Ordinal)).Substring("system-type=".Length);
        var status = this.gaps.Count != 0 ? "incomplete" : this.unproven.Count != 0 ? "unproven" : "proven";
        return system + "\n" + root.Id + "\n" + Number(this.gaps.Count) + "\nS\t" + status +
            "\nA\t" + Number(this.accesses.Count) + "\nC\t" + Number(this.completions.Count) + "\nU\t" + Number(this.unproven.Count) + "\n" +
            string.Join("\n", this.unproven.Take(12).Select(site => "E\t" + site)) + "\n" +
            string.Join("\n", this.gaps.Take(12).Select(gap => "G\t" + gap)) + (rootBinding == null ? "" : "\n" + rootBinding);
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private int NewCell(State state, Value value) { var cell = ++this.nextCell; state.Cells.Add(cell, value); return cell; }
    private string Role(ITypeSymbol? type) {
        if (type == null || type.TypeKind == TypeKind.TypeParameter) return "?";
        if (this.roles.TryGetValue(type, out var cached)) return cached;
        var role = this.Is(type, "Unity.Jobs.JobHandle") ? "H" : this.Is(type, "ME.BECS.SystemContext") ? "C" :
            this.Is(type, "ME.BECS.QueryBuilder") || this.Is(type, "ME.BECS.QueryBuilderDisposable") ? "Q" : "U";
        this.roles.Add(type, role);
        return role;
    }
    private bool Is(ITypeSymbol? type, string name) => type != null && SymbolEqualityComparer.Default.Equals(type, this.compilation.GetTypeByMetadataName(name));

    private MethodSynchronizationProgram? Program(string assembly, string id, out MethodSummaryGraph.Summary? summary) {
        summary = null;
        var key = (assembly, id);
        if (this.conflicts.Contains(key)) { this.gaps.Add("ConflictingSummary: " + id); return null; }
        if (!this.methods.TryGetValue(key, out summary)) { this.gaps.Add("MissingSummary: " + id); return null; }
        if (!summary.Flags.Contains(MethodSummaryContracts.SafetySchema) || !summary.Flags.Contains(MethodSummaryContracts.SchedulingSchema)) {
            this.gaps.Add("MissingSynchronizationContracts: " + id); return null;
        }
        if (!summary.Flags.Contains(DestroyDispatchContracts.Schema)) this.gaps.Add("MissingDestroyContracts: " + id);
        if (!summary.Flags.Contains(ImplicitFormattingContracts.Schema)) this.gaps.Add("MissingImplicitFormattingContracts: " + id);
        if (MethodSummaryContracts.IsConstructor(id) && !summary.Flags.Contains(MethodSummaryContracts.ConstructorSchema)) {
            this.gaps.Add("MissingConstructorContract: " + id); return null;
        }
        if (!this.programs.TryGetValue(key, out var cached)) {
            var valid = MethodSynchronizationProgram.TryParse(summary.Flags, out var program, out var reason);
            cached = (valid ? program : null, reason);
            this.programs.Add(key, cached);
        }
        if (cached.Program == null) { this.gaps.Add(cached.Reason + ": " + id); return null; }
        if (cached.Program.Gaps.Count != 0) {
            foreach (var gap in cached.Program.Gaps) this.gaps.Add(gap + ": " + id);
            return null;
        }
        return cached.Program;
    }

    private Frame? CreateFrame(string assembly, string id, MethodSummaryType[] arguments, State state, Binding receiver,
        Dictionary<int, Binding> bindings) {
        var program = this.Program(assembly, id, out var summary);
        if (program == null || summary == null) return null;
        if (arguments.Length != summary.Environment.Length || arguments.Any(type => type.IsOpen || type.IsUnsupported)) {
            this.gaps.Add("UnresolvedSynchronizationContext: " + id); return null;
        }
        var frame = new Frame { Id = id, Program = program, Key = assembly + "\n" + id + "\n" + string.Join(";", arguments.Select(type => type.Encode())) };
        for (var index = 0; index < arguments.Length; ++index) {
            if (frame.Types.ContainsKey(summary.Environment[index].Identity)) { this.gaps.Add("DuplicateGenericVariable: " + id); return null; }
            frame.Types.Add(summary.Environment[index].Identity, arguments[index]);
        }
        string ResolveRole(MethodSynchronizationProgram.Slot slot) {
            var expression = slot.Type.Substitute(frame.Types);
            var role = expression.IsOpen || expression.IsUnsupported ? "?" : this.Role(MethodSummaryTypeResolver.Resolve(expression, this.compilation));
            if (role == "?" || (slot.Role != "T" && slot.Role != role)) this.gaps.Add("InvalidSynchronizationType: " + id + " / " + slot.Name);
            return role;
        }
        frame.ReturnRole = ResolveRole(program.Return);
        // References to ordinary data have no handle-cell identity in this model;
        // every borrow/read/store is conservatively recorded as an A event. Resolve
        // generic T first, so ref T cannot silently become an unmodeled handle alias.
        if (program.Return.Passing != "value" && frame.ReturnRole != "U") { this.gaps.Add("RefReturn: " + id); return null; }
        foreach (var slot in program.Slots.Values) {
            var role = ResolveRole(slot);
            frame.Roles.Add(slot.Name, role);
            var binding = new Binding("value", null);
            if (slot.Kind == "P") {
                var ordinal = int.Parse(slot.Name.Substring(1), CultureInfo.InvariantCulture);
                if (!bindings.TryGetValue(ordinal, out binding) || binding.Mode != slot.Passing) {
                    this.gaps.Add("InvalidParameterBinding: " + id); return null;
                }
            } else if (slot.Kind == "I") binding = receiver;
            if (slot.Kind != "V" && role != "U" && !binding.Cell.HasValue) {
                this.gaps.Add("MissingTrackedArgument: " + id + " / " + slot.Name); return null;
            }
            if (slot.Kind != "V" && binding.Mode != "value" && binding.Cell.HasValue) frame.Cells.Add(slot.Name, binding.Cell.Value);
            else {
                var value = slot.Kind == "V" ? Value.Unknown : binding.Cell.HasValue ? state.Cells[binding.Cell.Value] : Value.Empty;
                var cell = this.NewCell(state, role == "U" ? Value.Empty : value);
                frame.Cells.Add(slot.Name, cell);
                frame.OwnedCells.Add(cell);
            }
        }
        if (bindings.Count != program.Slots.Values.Count(slot => slot.Kind == "P")) { this.gaps.Add("ParameterArityMismatch: " + id); return null; }
        frame.ReturnCell = this.NewCell(state, Value.Empty);
        frame.OwnedCells.Add(frame.ReturnCell);
        return frame;
    }

    private Value Read(Frame frame, State state, string slot) {
        if (slot == "-") return Value.Empty;
        var value = state.Cells[frame.Cells[slot]];
        if (value.IsUnknown && frame.Roles[slot] != "U") this.gaps.Add("UnknownHandleValue: " + frame.Id);
        return value;
    }
    private void Write(Frame frame, State state, string slot, Value value) {
        if (slot != "-") state.Cells[frame.Cells[slot]] = frame.Roles[slot] == "U" ? Value.Empty : value;
    }
    private Binding Bind(Frame frame, string mode, string slot) => new(mode, slot == "-" ? null : frame.Cells[slot]);

    private string Normalize(State state, ref Value? returned) {
        // Job identities are irrelevant to this proof; coverage by live storage is not.
        // Two pending jobs can be merged ONLY if every cell (including caller frames and
        // the return value) covers both or neither. Future copy/union/Complete operations
        // cannot distinguish such jobs. Keep an uncovered class for lost pending handles.
        // This finite quotient handles repeated scheduling without reusing a job identity
        // and accidentally letting a stale handle complete a newer invocation.
        var holders = state.Pending.ToDictionary(token => token, token => new List<int>());
        if (returned != null)
            foreach (var token in returned.Tokens) if (holders.TryGetValue(token, out var list)) list.Add(0);
        var cells = state.Cells.Keys.OrderBy(cell => cell).ToArray();
        foreach (var cell in cells) {
            this.cancellation.ThrowIfCancellationRequested();
            foreach (var token in state.Cells[cell].Tokens) if (holders.TryGetValue(token, out var list)) list.Add(cell);
        }
        var groups = holders.GroupBy(pair => string.Join(",", pair.Value.Select(Number)), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        var rename = new Dictionary<int, int>();
        for (var index = 0; index < groups.Length; ++index)
            foreach (var pair in groups[index]) rename.Add(pair.Key, index + 1);
        Value Rewrite(Value value) {
            var tokens = value.Tokens.Where(rename.ContainsKey).Select(token => rename[token]).Distinct().OrderBy(token => token).ToArray();
            return tokens.Length == 0 ? value.IsUnknown ? Value.Unknown : Value.Empty : new Value(tokens, value.IsUnknown);
        }
        state.Pending.Clear();
        for (var index = 1; index <= groups.Length; ++index) state.Pending.Add(index);
        var key = new StringBuilder("P").Append(Number(groups.Length)).Append(';');
        void Append(Value value) => key.Append(value.IsUnknown ? '?' : '=').Append(string.Join(",", value.Tokens.Select(Number))).Append(';');
        foreach (var cell in cells) {
            var value = Rewrite(state.Cells[cell]);
            state.Cells[cell] = value;
            key.Append(Number(cell));
            Append(value);
        }
        key.Append('R');
        if (returned == null) key.Append('-');
        else { returned = Rewrite(returned); Append(returned); }
        return key.ToString();
    }

    private List<Result> Execute(Frame frame, State initial, HashSet<string> ancestors, bool captureExceptions = false) {
        var result = new List<Result>();
        var exceptions = captureExceptions || frame.Program.FinallyRegions.Count != 0 || frame.Program.CatchRegions.Count != 0;
        if (exceptions && !frame.Program.SupportsExceptions) { this.gaps.Add("MissingSynchronizationExceptionContract: " + frame.Id); return result; }
        if (ancestors.Count >= 64 || !ancestors.Add(frame.Key)) { this.gaps.Add("RecursiveSynchronizationFlow: " + frame.Id); return result; }
        var pending = new Queue<(int Block, State State, Value? Return, Unwind? Resume)>();
        var visited = new HashSet<(int Block, string Resume, string State)>();
        pending.Enqueue((frame.Program.Entry, initial, null, null));
        Unwind? Enclosing(int block, Unwind? resume) {
            while (resume != null && (block < resume.Region || block > frame.Program.FinallyRegions[resume.Region])) resume = resume.Parent;
            return resume;
        }
        void Follow(int? target, string semantics, int[] unwind, State state, Value? returned, Unwind? resume) {
            if (unwind.Length != 0) {
                if (unwind.Length > 64 || resume?.Depth >= 64) { this.gaps.Add("FinallyNestingLimit: " + frame.Id); return; }
                var parent = Enclosing(unwind[0], resume);
                // Do not cancel a pending return before its cleanup is actually
                // left: a new throw while unwinding may be caught inside it.
                if (parent == null && semantics is "Throw" or "Rethrow" or "Catch") {
                    returned = null; state.Cells[frame.ReturnCell] = Value.Empty;
                }
                pending.Enqueue((unwind[0], state, returned, new Unwind(unwind[0], unwind.Skip(1).ToArray(), target, semantics, parent)));
            } else if (semantics is "Throw" or "Rethrow") {
                state.Cells[frame.ReturnCell] = Value.Empty;
                if (exceptions) pending.Enqueue((-1, state, null, null));
            } else if (semantics is "Regular" or "Return" or "Catch" && target.HasValue) {
                var parent = Enclosing(target.Value, resume);
                if (parent == null && semantics == "Catch") { returned = null; state.Cells[frame.ReturnCell] = Value.Empty; }
                pending.Enqueue((target.Value, state, returned, parent));
            } else this.gaps.Add("UnsupportedControlFlowEdge: " + frame.Id);
        }
        void Throw(MethodSynchronizationProgram.Block block, State state, Value? returned, Unwind? resume) {
            if (returned != null) returned = state.Cells[frame.ReturnCell];
            // Handler alternatives own independent states; ref/out changes and
            // pending jobs from a throwing callee must reach every alternative.
            foreach (var edge in block.Catches)
                Follow(edge.Target, "Catch", MethodSynchronizationProgram.RegionIds(edge.Unwind), state.Clone(), returned, resume);
            Follow(null, "Throw", block.Handlers, state, returned, resume);
        }
        while (pending.Count > 0) {
            this.cancellation.ThrowIfCancellationRequested();
            var work = pending.Dequeue();
            if (work.Return != null) work.Return = work.State.Cells[frame.ReturnCell];
            var normalized = this.Normalize(work.State, ref work.Return);
            if (!visited.Add((work.Block, work.Resume?.Key ?? "", normalized))) continue;
            this.stateKeyBytes += normalized.Length * 2L;
            if (++this.paths > 4096 || this.steps > 50000 || this.stateKeyBytes > 16777216L) {
                this.gaps.Add("SynchronizationTraversalLimit"); break;
            }
            if (work.Block == -1) { result.Add(new Result(work.State, Value.Empty, throws: true)); continue; }
            var block = frame.Program.Blocks[work.Block];
            if (work.Resume != null && (block.Id < work.Resume.Region || block.Id > frame.Program.FinallyRegions[work.Resume.Region])) {
                this.gaps.Add("InvalidFinallyContinuation: " + frame.Id); continue;
            }
            var states = new List<(State State, Value? Return)> { (work.State, work.Return) };
            for (var index = 0; index < block.Events.Count && states.Count != 0; ++index) {
                var operation = block.Events[index];
                var site = frame.Id + " @" + Number(block.Id) + ":" + Number(index);
                var next = new List<(State State, Value? Return)>();
                foreach (var current in states) {
                    this.cancellation.ThrowIfCancellationRequested();
                    if (++this.steps > 50000) { this.gaps.Add("SynchronizationTraversalLimit"); break; }
                    var state = current.State;
                    // Calls return their own exceptional states, including partial
                    // ref mutations and jobs scheduled inside a throwing callee.
                    // Before/after also cover native throws without managed bodies.
                    var canThrow = operation[0] is "C" or "T" or "X" or "A" or "F" or "O";
                    if (exceptions && canThrow) Throw(block, state.Clone(), current.Return, work.Resume);
                    if (operation[0] == "C") {
                        foreach (var after in this.Call(frame, state, operation, site, ancestors, captureExceptions: exceptions)) {
                            if (after.Throws) { if (exceptions) Throw(block, after.State, current.Return, work.Resume); continue; }
                            if (exceptions) Throw(block, after.State.Clone(), current.Return, work.Resume);
                            next.Add((after.State, current.Return == null ? null : after.State.Cells[frame.ReturnCell]));
                        }
                        continue;
                    }
                    if (operation[0] == "T") {
                        foreach (var after in this.Format(frame, state, operation, site, ancestors, exceptions)) {
                            if (after.Throws) { if (exceptions) Throw(block, after.State, current.Return, work.Resume); continue; }
                            if (exceptions) Throw(block, after.State.Clone(), current.Return, work.Resume);
                            next.Add((after.State, current.Return == null ? null : after.State.Cells[frame.ReturnCell]));
                        }
                        continue;
                    }
                    var returned = current.Return;
                    switch (operation[0]) {
                        case "=": this.Write(frame, state, operation[1], this.Read(frame, state, operation[2])); break;
                        case "Z": this.Write(frame, state, operation[1], Value.Empty); break;
                        case "?": this.gaps.Add("UnknownHandleEffect: " + site); this.Write(frame, state, operation[1], Value.Unknown); break;
                        case "R": returned = this.Read(frame, state, operation[1]); state.Cells[frame.ReturnCell] = returned; break;
                        case "A":
                            var expression = this.decode(operation[1])?.Substitute(frame.Types);
                            var component = expression == null || expression.IsOpen || expression.IsUnsupported ? null :
                                MethodSummaryTypeResolver.Resolve(expression, this.compilation);
                            if (component == null) this.gaps.Add("UnknownReferenceAccess: " + site);
                            else if (this.Role(component) == "U") this.Access(state, site);
                            break;
                        case "O":
                            var stored = this.decode(operation[1])?.Substitute(frame.Types);
                            var storedType = stored == null || stored.IsOpen || stored.IsUnsupported ? null :
                                MethodSummaryTypeResolver.Resolve(stored, this.compilation);
                            if (storedType == null) this.gaps.Add("UnknownFieldStorage: " + site);
                            else if (this.Role(storedType) != "U") this.gaps.Add("FieldStorage: " + site);
                            break;
                        case "F":
                            var field = frame.Program.Symbols[operation[1]];
                            if (MethodSummaryContracts.Value(field, "ref") is "0" or "1" or "2") this.Access(state, site);
                            else if (MethodSummaryContracts.Value(field, "ref") != null || MethodSummaryContracts.Has(field, "ref-unknown"))
                                this.gaps.Add("UnknownComponentReference: " + site);
                            break;
                    }
                    if (exceptions && canThrow && operation[0] != "X") Throw(block, state.Clone(), current.Return, work.Resume);
                    next.Add((state, returned));
                }
                if (operation[0] == "T" && next.Count > 1) {
                    // Null/non-null formatting can branch many times in one CFG
                    // block. Merge by the same live-handle coverage quotient used
                    // at block entries; otherwise n nullable operands create 2^n
                    // equivalent states before the traversal budget is checked.
                    var distinct = new List<(State State, Value? Return)>();
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var candidate in next) {
                        this.cancellation.ThrowIfCancellationRequested();
                        var returned = candidate.Return;
                        var key = this.Normalize(candidate.State, ref returned);
                        if (!keys.Add(key)) continue;
                        this.stateKeyBytes += key.Length * 2L;
                        if (keys.Count > 4096 || this.stateKeyBytes > 16777216L) { this.gaps.Add("SynchronizationTraversalLimit"); break; }
                        distinct.Add((candidate.State, returned));
                    }
                    states = distinct;
                } else states = next;
            }
            foreach (var current in states) {
                if (block.Kind == "Exit") {
                    if (frame.ReturnRole != "U" && current.Return == null) this.gaps.Add("MissingReturnValue: " + frame.Id);
                    result.Add(new Result(current.State, current.Return ?? Value.Empty));
                    continue;
                }
                var edges = block.Edges.Distinct().ToArray();
                foreach (var edge in edges) {
                    if (edge.Semantics == "ProgramTermination") continue;
                    var state = edges.Length == 1 ? current.State : current.State.Clone();
                    if (edge.Semantics is "Throw" or "Rethrow") { if (exceptions) Throw(block, state, current.Return, work.Resume); continue; }
                    if (edge.Semantics == "StructuredExceptionHandling") {
                        var resume = work.Resume;
                        if (resume == null || edge.Target.HasValue || edge.Unwind.Length != 0) { this.gaps.Add("InvalidFinallyContinuation: " + frame.Id); continue; }
                        Follow(resume.Target, resume.Semantics, resume.Remaining, state, current.Return, resume.Parent);
                        continue;
                    }
                    Follow(edge.Target, edge.Semantics, MethodSynchronizationProgram.RegionIds(edge.Unwind), state, current.Return, work.Resume);
                }
            }
        }
        ancestors.Remove(frame.Key);
        foreach (var value in result) foreach (var cell in frame.OwnedCells) value.State.Cells.Remove(cell);
        return result;
    }

    private void Access(State state, string site) {
        this.accesses.Add(site);
        if (state.Pending.Count != 0) this.unproven.Add(site);
    }
    private void Complete(State state, Value handle, string site) {
        this.completions.Add(site);
        state.Pending.ExceptWith(handle.Tokens);
    }

    private List<Result> Format(Frame frame, State state, string[] instruction, string site, HashSet<string> ancestors, bool captureExceptions) {
        var original = frame.Program.Symbols[instruction[1]];
        var expression = this.decode(original[4])?.Substitute(frame.Types);
        var type = expression == null || expression.IsOpen || expression.IsUnsupported ? null :
            MethodSummaryTypeResolver.Resolve(expression, this.compilation);
        var operation = JobConstrainedCall.Resolve(original, this.compilation, frame.Types, this.gaps, this.methods, this.conflicts);
        var arity = MethodSummaryContracts.Value(operation, "formatting-arity");
        if (type == null || arity is not ("0" or "2")) { this.gaps.Add("UnresolvedFormattingFlow: " + site); return new List<Result>(); }
        if (MethodSummaryContracts.Has(operation, "ecs-leaf")) return new List<Result> { new(state, Value.Empty) };
        var optional = type.IsReferenceType || type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var skipped = optional ? state.Clone() : null;
        var call = new List<string> { "C", "-", instruction[1], "value", instruction[2] };
        // Formatting string/provider parameters carry no tracked handle tokens.
        // The formatter receiver is a value snapshot (boxing/constrained temp),
        // never an alias that may clear the caller's generic T storage.
        if (arity == "2") call.AddRange(new[] { "0:value:-", "1:value:-" });
        var results = this.Call(frame, state, call.ToArray(), site, ancestors, operation, captureExceptions);
        if (skipped != null) results.Add(new Result(skipped, Value.Empty));
        return results;
    }

    private List<Result> Call(Frame frame, State state, string[] call, string site, HashSet<string> ancestors, string[]? resolved = null, bool captureExceptions = false) {
        var operation = resolved ?? JobConstrainedCall.Resolve(frame.Program.Symbols[call[2]], this.compilation, frame.Types, this.gaps, this.methods, this.conflicts);
        if (MethodSummaryContracts.Has(operation, "default-value-construction")) {
            this.Write(frame, state, call[1], Value.Empty);
            return new List<Result> { new(state, Value.Empty) };
        }
        var receiver = this.decode(operation[4])?.Substitute(frame.Types);
        if (receiver == null || receiver.IsOpen || receiver.IsUnsupported || receiver.Kind != 'n') {
            this.gaps.Add("UnresolvedCallReceiver: " + site); return new List<Result>();
        }
        var arguments = new List<MethodSummaryType>(receiver.Arguments);
        var methodArguments = new List<ITypeSymbol>();
        for (var index = 5; index < MethodSummaryContracts.ArgumentEnd(operation); ++index) {
            var type = this.decode(operation[index])?.Substitute(frame.Types);
            var symbol = type == null || type.IsOpen || type.IsUnsupported ? null : MethodSummaryTypeResolver.Resolve(type, this.compilation);
            if (symbol == null) { this.gaps.Add("UnresolvedCallTypeArgument: " + site); return new List<Result>(); }
            arguments.Add(type!); methodArguments.Add(symbol);
        }
        var owner = MethodSummaryTypeResolver.Resolve(receiver, this.compilation) as INamedTypeSymbol;
        var targetId = this.methods.TryGetValue((operation[2], operation[3]), out var targetBody) ? targetBody.AllocatorOrigin ?? operation[3] : operation[3];
        var target = owner == null ? null : MethodSummaryInterfaceMap.Members(owner).FirstOrDefault(method => MethodSummaryIdentity.Get(method) == targetId);
        if (target != null && target.Arity > 0) {
            if (methodArguments.Count != target.Arity) target = null;
            else target = target.Construct(methodArguments.ToArray());
        }
        var bindings = new Dictionary<int, Binding>();
        var operands = new Dictionary<int, string>();
        foreach (var encoded in call.Skip(5)) {
            var parts = encoded.Split(':');
            var ordinal = int.Parse(parts[0], CultureInfo.InvariantCulture);
            bindings.Add(ordinal, this.Bind(frame, parts[1], parts[2]));
            operands.Add(ordinal, parts[2]);
        }
        if (target != null && target.Parameters.Length != bindings.Count) { this.gaps.Add("CallArityMismatch: " + site); return new List<Result>(); }
        if (this.Primitive(frame, state, call, operation, target, operands, site, out var primitive)) {
            this.Write(frame, state, call[1], primitive);
            return new List<Result> { new(state, Value.Empty) };
        }
        if (MethodSummaryContracts.Has(operation, "native-memory-access")) this.Access(state, site);
        DestroyDispatchContracts.Component(operation, this.compilation, frame.Types, this.decode, this.gaps);
        if (MethodSummaryContracts.Value(operation, "safety") is "0" or "1" or "2" &&
            MethodSummaryContracts.Value(operation, "component") != null || MethodSummaryContracts.Value(operation, "system-access") != null) {
            this.Access(state, site);
            if (call[1] != "-" && frame.Roles[call[1]] != "U") this.gaps.Add("TrackedComponentAccessResult: " + site);
            this.Write(frame, state, call[1], Value.Unknown);
            return new List<Result> { new(state, Value.Empty) };
        }
        if (MethodSummaryContracts.Has(operation, "ecs-leaf") || MethodSummaryContracts.Has(operation, "scalar-comparison") ||
            MethodSummaryContracts.Value(operation, "query-filter") != null) {
            if (call[1] != "-" && frame.Roles[call[1]] != "U" || call[4] != "-" && frame.Roles[call[4]] != "U" ||
                operands.Values.Any(slot => slot != "-" && frame.Roles[slot] != "U")) this.gaps.Add("OpaqueTrackedEffect: " + site);
            this.Write(frame, state, call[1], Value.Unknown);
            return new List<Result> { new(state, Value.Empty) };
        }
        // CodeGeneratorIgnore suppresses legacy discovery, not necessarily execution or
        // handle mutation. Only audited leaf contracts above can bypass body analysis.
        var helper = this.CreateFrame(operation[2], operation[3], arguments.ToArray(), state, this.Bind(frame, call[3], call[4]), bindings);
        if (helper == null) return new List<Result>();
        var results = this.Execute(helper, state, ancestors, captureExceptions);
        foreach (var result in results) if (!result.Throws) this.Write(frame, result.State, call[1], result.Value);
        return results;
    }

    private bool Primitive(Frame frame, State state, string[] call, string[] operation, IMethodSymbol? method,
        Dictionary<int, string> operands, string site, out Value result) {
        result = Value.Empty;
        if (method == null) return false;
        Value Argument(IParameterSymbol parameter) => this.Read(frame, state, operands[parameter.Ordinal]);
        Value Parameters(params string[] roles) => Value.Union(method.Parameters.Where(parameter => roles.Contains(this.Role(parameter.Type))).Select(Argument));
        var receiver = this.Read(frame, state, call[4]);
        switch (JobControlContracts.Classify(method, this.compilation)) {
            case "complete":
                this.Complete(state, receiver, site);
                this.Write(frame, state, call[4], Value.Empty);
                return true;
            case "complete-all":
                // Read the entire union before clearing aliased ref arguments.
                this.Complete(state, Parameters("H"), site);
                foreach (var operand in operands.Values) this.Write(frame, state, operand, Value.Empty);
                return true;
            case "complete-array":
                this.gaps.Add("OpaqueCompletionInputs: " + site);
                return true;
            case "poll": case "flush":
                // IsCompleted=true is not a completion/ownership transfer. Do not
                // clear pending tokens, even on a branch testing the returned bool.
                return true;
        }
        if (this.Is(method.ContainingType, "Unity.Jobs.JobHandle")) {
            if (method.IsStatic && method.Name == "CombineDependencies" && method.Parameters.Length >= 2 &&
                method.Parameters.All(parameter => this.Role(parameter.Type) == "H") &&
                ExternalValueContracts.IsLeaf(method, this.compilation)) { result = Parameters("H"); return true; }
        }
        if (this.Is(method.ContainingType, "ME.BECS.SystemContext")) {
            if (!method.IsStatic && method.Name == "get_dependsOn" && method.Parameters.Length == 0) { result = receiver; return true; }
            if (!method.IsStatic && method.Name is "SetDependency" or "AddDependency" && method.Parameters.Length > 0 &&
                method.Parameters.All(parameter => this.Role(parameter.Type) == "H")) {
                var value = method.Name == "AddDependency" ? Value.Union(new[] { receiver, Parameters("H") }) : Parameters("H");
                this.Write(frame, state, call[4], value);
                return true;
            }
            if (method.IsStatic && method.Name == "Create" && this.Role(method.ReturnType) == "C" &&
                method.Parameters.Count(parameter => this.Role(parameter.Type) == "H") == 1) { result = Parameters("H"); return true; }
        }
        if (this.Is(method.ContainingType, "ME.BECS.SystemContextExt") && method.Name == "AddDependency" && method.IsStatic &&
            method.Parameters.Length == 2 && this.Role(method.Parameters[0].Type) == "H" && this.Role(method.Parameters[1].Type) == "C") {
            result = Parameters("H", "C");
            this.Write(frame, state, operands[1], result);
            return true;
        }
        if ((this.Is(method.ContainingType, "ME.BECS.API") || this.Is(method.ContainingType, "ME.BECS.APIExt")) &&
            method.IsStatic && method.Name == "Query" && this.Is(method.ReturnType, "ME.BECS.QueryBuilder")) {
            result = Parameters("C", "H");
            return true;
        }
        if (this.Is(method.ContainingType, "ME.BECS.QueryBuilder") && !method.IsStatic) {
            if (method.Name == "WaitForAllJobs" && method.Parameters.Length == 0 && this.Role(method.ReturnType) == "Q") {
                this.Complete(state, receiver, site);
                this.Write(frame, state, call[4], Value.Empty);
                result = Value.Empty;
                return true;
            }
            if (this.Role(method.ReturnType) == "Q" &&
                (MethodSummaryContracts.Value(operation, "query-filter") != null ||
                 method.Arity == 0 && (method.Parameters.Length == 0 && method.Name is "AsReadonly" or "AsUnsafe" or "Sort" or "AsJob" or "WithBurst" ||
                     (method.Name is "AsParallel" or "ParallelFor" && method.Parameters.Length == 1 || method.Name == "Step" && method.Parameters.Length == 2) &&
                     method.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_UInt32)))) {
                result = receiver; return true;
            }
        }
        if (MethodSummaryContracts.Value(operation, "scheduled-job") != null && MethodSummaryContracts.IsSchedulingMethod(method, this.compilation)) {
            var dependency = Parameters("Q", "H", "C");
            if (this.Role(method.ContainingType) == "Q") dependency = Value.Union(new[] { dependency, receiver });
            var token = ++this.nextToken;
            state.Pending.Add(token);
            result = Value.Union(new[] { dependency, new Value(new[] { token }) });
            return true;
        }
        return false;
    }
}
