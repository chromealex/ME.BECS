using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Ordered, intraprocedural input for the synchronization proof, including across asmdefs.
// This is NOT a proof by itself. A consumer must resolve every call and reject every G row.
// Slots hold values. Call operands distinguish snapshots (value) from storage (ref/in/out).
internal sealed class MethodSynchronizationFlow {
    internal const string Schema = "sync-flow-schema=3";
    internal const string ExceptionSchema = "sync-exception-schema=1";
    internal const string CatchSchema = "sync-catch-schema=1";
    internal const string FlagPrefix = "sync-flow=";
    private readonly Compilation compilation;
    private readonly IMethodSymbol method;
    private readonly CancellationToken cancellation;
    private Func<IOperation, bool> omitted;
    private readonly Dictionary<INamedTypeSymbol, int?> refModes;
    private readonly Dictionary<ISymbol, string> locals = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<CaptureId, string> captures = new();
    private MethodSynchronizationCaptures captureKinds = null!;
    private LocalContainerAllocatorProof? localContainers;
    private readonly Dictionary<ITypeSymbol, string> roles = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<string, string> contracts = new(StringComparer.Ordinal);
    private readonly HashSet<string> gaps = new(StringComparer.Ordinal);
    private readonly StringBuilder declarations = new();
    private readonly StringBuilder events = new();
    private int nextSlot;

    internal MethodSynchronizationFlow(Compilation compilation, IMethodSymbol method, CancellationToken cancellation,
        Func<IOperation, bool> omitted, Dictionary<INamedTypeSymbol, int?> refModes) {
        this.compilation = compilation;
        this.method = method;
        this.cancellation = cancellation;
        this.omitted = omitted;
        this.refModes = refModes;
        foreach (var pair in new[] { ("Unity.Jobs.JobHandle", "H"), ("ME.BECS.SystemContext", "C"),
                     ("ME.BECS.QueryBuilder", "Q"), ("ME.BECS.QueryBuilderDisposable", "Q") }) {
            var type = compilation.GetTypeByMetadataName(pair.Item1);
            if (type != null) this.roles[type] = pair.Item2;
        }
    }

    internal static string Unavailable(string reason) => Schema + "," + ExceptionSchema + "," + CatchSchema + "," + FlagPrefix + Encode("v3\nG\t" + reason + "\n");
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private void DeclareMethod() {
        this.declarations.Append("M\t").Append(this.Role(this.method.ReturnType)).Append('\t')
            .Append(this.method.ReturnsByRef ? "ref" : this.method.ReturnsByRefReadonly ? "in" : "value")
            .Append('\t').Append(MethodSummaryType.From(this.method.ReturnType).Encode()).Append('\n');
        if (!this.method.IsStatic)
            this.declarations.Append("I\tthis\t").Append(this.Role(this.method.ContainingType)).Append('\t')
                .Append(this.method.IsReadOnly || this.method.ContainingType.IsReadOnly ? "in" : "ref")
                .Append('\t').Append(MethodSummaryType.From(this.method.ContainingType).Encode()).Append('\n');
        foreach (var parameter in this.method.Parameters)
            this.declarations.Append("P\tp").Append(Number(parameter.Ordinal)).Append('\t').Append(this.Role(parameter.Type))
                .Append('\t').Append(Passing(parameter.RefKind)).Append('\t').Append(MethodSummaryType.From(parameter.Type).Encode()).Append('\n');
    }

    internal string BuildAutoAccessor(IFieldSymbol field) {
        this.DeclareMethod();
        var contract = this.Contract("field", field);
        var body = new StringBuilder("B\t0\tEntry\tNone\t1:Regular\t-\nB\t1\tBlock\tNone\t2:Return\t-\n");
        if (!this.method.IsStatic && MethodSummaryContracts.IsComponentType(this.method.ContainingType, this.compilation))
            body.Append("A\t").Append(MethodSummaryType.From(this.method.ContainingType).Encode()).Append('\n');
        body.Append("F\t").Append(contract).Append('\n')
            // Ordinary backing storage has no handle-token identity. A closed T
            // that is a handle/context/query must remain opaque, not read as zero.
            .Append("O\t").Append(MethodSummaryType.From(field.Type).Encode()).Append('\n')
            .Append("R\t-\nB\t2\tExit\tNone\t-\t-\n");
        return Schema + "," + ExceptionSchema + "," + CatchSchema + "," + FlagPrefix + Encode("v3\n" + this.declarations + body);
    }

    internal string Build(ControlFlowGraph graph) {
        this.DeclareMethod();
        this.AppendGraph(graph, 0, true, true);
        return this.Finish();
    }

    internal string BuildConstructor(IEnumerable<(ControlFlowGraph Graph, Func<IOperation, bool> Omitted)> initializers,
        ControlFlowGraph? body, IMethodSymbol? implicitBase = null) {
        var prefixes = initializers.ToArray();
        if (prefixes.Sum(static prefix => (long)prefix.Graph.Blocks.Length) + (body?.Blocks.Length ?? 2) > 20000)
            return Unavailable("ConstructorFlowSizeLimit");
        // Creating an instance can also run a type initializer. Its once-only,
        // runtime-dependent state is not part of the instance CFG composition.
        if (this.method.ContainingType.StaticConstructors.Length != 0) this.gaps.Add("ConstructorTypeInitializer");
        this.DeclareMethod();
        var bodyOmitted = this.omitted;
        var offset = 0;
        foreach (var prefix in prefixes) {
            this.cancellation.ThrowIfCancellationRequested();
            this.omitted = prefix.Omitted;
            this.AppendGraph(prefix.Graph, offset, offset == 0, false);
            offset += prefix.Graph.Blocks.Length;
        }
        this.omitted = bodyOmitted;
        if (body != null) this.AppendGraph(body, offset, offset == 0, true);
        else {
            // An implicit class constructor has only initializers and a base call.
            // No synthetic syntax/semantic model and no user code execution.
            this.events.Append("B\t").Append(Number(offset)).Append(offset == 0 ? "\tEntry" : "\tBlock")
                .Append("\tNone\t").Append(Number(offset + 1)).Append(":Regular\t-\n");
            if (implicitBase != null) this.Call(implicitBase, ("ref", "this"), Array.Empty<(int, string, string)>());
            this.events.Append("B\t").Append(Number(offset + 1)).Append("\tExit\tNone\t-\t-\n");
        }
        return this.Finish();
    }

    private string Finish() => Schema + "," + ExceptionSchema + "," + CatchSchema + "," + FlagPrefix + Encode("v3\n" + this.declarations +
        string.Concat(this.gaps.OrderBy(static gap => gap, StringComparer.Ordinal).Select(static gap => "G\t" + gap + "\n")) + this.events);

    private void AppendGraph(ControlFlowGraph graph, int offset, bool first, bool last) {
        this.localContainers = new LocalContainerAllocatorProof(graph.OriginalOperation, this.compilation, this.cancellation, this.omitted);
        // Capture IDs and locals are scoped to each CFG, not to the constructor's
        // combined program. Keep fresh slot IDs and shared method-parameter slots.
        this.captures.Clear();
        this.locals.Clear();
        this.captureKinds = new MethodSynchronizationCaptures(graph, this.method, this.omitted, type => this.Role(type) == "U", this.cancellation);
        var regions = new Stack<ControlFlowRegion>();
        var hasCatch = false;
        regions.Push(graph.Root);
        while (regions.Count != 0) {
            var region = regions.Pop();
            hasCatch |= region.Kind == ControlFlowRegionKind.Catch;
            if (region.Kind == ControlFlowRegionKind.Filter)
                this.gaps.Add("ExceptionControlFlow");
            if (region.Kind == ControlFlowRegionKind.Finally)
                this.declarations.Append("Y\t").Append(Number(offset + region.FirstBlockOrdinal)).Append('\t')
                    .Append(Number(offset + region.LastBlockOrdinal)).Append('\n');
            if (region.Kind == ControlFlowRegionKind.Catch)
                this.declarations.Append("K\t").Append(Number(offset + region.FirstBlockOrdinal)).Append('\t')
                    .Append(Number(offset + region.LastBlockOrdinal)).Append('\n');
            foreach (var child in region.NestedRegions) regions.Push(child);
        }
        var routes = hasCatch ? ExceptionalFlowRoutes.Create(graph, this.cancellation) : null;
        if (hasCatch && routes == null) this.gaps.Add("ExceptionControlFlow");
        foreach (var block in graph.Blocks) {
            this.cancellation.ThrowIfCancellationRequested();
            // Keep ordinal anchors for unreachable region endpoints and branch
            // targets, but never export their operations. A reachable leave can
            // target an unreachable block because its finally always throws:
            // dropping that edge would also drop the required cleanup effects.
            if (!block.IsReachable && block.Kind != BasicBlockKind.Exit) {
                this.events.Append("B\t").Append(Number(offset + block.Ordinal))
                    .Append("\tBlock\tNone\t-:ProgramTermination\t-\n");
                continue;
            }
            if (block.Kind == BasicBlockKind.Exit && !last) {
                this.events.Append("B\t").Append(Number(offset + block.Ordinal)).Append("\tBlock\tNone\t")
                    .Append(Number(offset + graph.Blocks.Length)).Append(":Regular\t-\n");
                continue;
            }
            var kind = block.Kind == BasicBlockKind.Entry && !first ? BasicBlockKind.Block : block.Kind;
            this.events.Append("B\t").Append(Number(offset + block.Ordinal)).Append('\t').Append(kind).Append('\t')
                .Append(block.ConditionKind).Append('\t').Append(Edge(block.FallThroughSuccessor, offset)).Append('\t')
                .Append(Edge(block.ConditionalSuccessor, offset)).Append('\n');
            var handlers = new List<int>();
            // Nested function CFGs retain lexical parent regions from a different
            // ordinal space. Their handlers are entered by the caller on throw.
            for (var region = block.EnclosingRegion; region != null && region != graph.Root; region = region.EnclosingRegion) {
                if (region.Kind != ControlFlowRegionKind.Try || region.EnclosingRegion?.Kind != ControlFlowRegionKind.TryAndFinally) continue;
                var final = region.EnclosingRegion.NestedRegions.Single(item => item.Kind == ControlFlowRegionKind.Finally);
                handlers.Add(offset + final.FirstBlockOrdinal);
            }
            if (handlers.Count != 0) this.events.Append("H\t").Append(string.Join(",", handlers.Select(Number))).Append('\n');
            if (routes != null) foreach (var route in routes[block.Ordinal]) {
                if (!route.Target.HasValue) continue; // H retains the escaping unwind.
                this.events.Append("J\t").Append(Number(offset + route.Target.Value)).Append('\t')
                    .Append(route.Unwind.Length == 0 ? "-" : string.Join(",", route.Unwind.Select(region => Number(offset + region.FirstBlockOrdinal))))
                    .Append('\n');
            }
            foreach (var operation in block.Operations) this.Value(operation);
            var branchValue = this.Value(block.BranchValue);
            if (block.FallThroughSuccessor?.Semantics == ControlFlowBranchSemantics.Return) {
                if (!last) this.gaps.Add("InitializerReturnFlow");
                if ((this.method.ReturnsByRef || this.method.ReturnsByRefReadonly) && this.Role(this.method.ReturnType) is not ("U" or "T"))
                    this.gaps.Add("RefReturn");
                this.events.Append("R\t").Append(branchValue).Append('\n');
            }
        }
    }

    private static string Edge(ControlFlowBranch? edge, int offset) => edge == null ? "-" :
        (edge.Destination == null ? "-" : Number(offset + edge.Destination.Ordinal)) + ":" + edge.Semantics +
        (edge.FinallyRegions.Length == 0 ? "" : ":" + string.Join(",", edge.FinallyRegions.Select(region => Number(offset + region.FirstBlockOrdinal))));
    private static string Passing(RefKind kind) => kind switch {
        RefKind.None => "value", RefKind.Ref => "ref", RefKind.In => "in", RefKind.Out => "out", _ => "unknown",
    };

    private string Role(ITypeSymbol? type) {
        // A generic helper can pass a JobHandle through T. Retain its storage and
        // portable type expression; the caller must substitute T before interpreting it.
        if (type is ITypeParameterSymbol) return "T";
        return type != null && this.roles.TryGetValue(type, out var role) ? role : "U";
    }

    private string Slot(ITypeSymbol? type) {
        var role = this.Role(type);
        if (role == "U") return "-";
        var slot = "v" + Number(this.nextSlot++);
        this.declarations.Append("V\t").Append(slot).Append('\t').Append(role).Append('\t')
            .Append(MethodSummaryType.From(type!).Encode()).Append('\n');
        return slot;
    }

    private string Unknown(ITypeSymbol? type, string reason) {
        this.gaps.Add(reason);
        var slot = this.Slot(type);
        if (slot != "-") this.events.Append("?\t").Append(slot).Append('\n');
        return slot;
    }

    private string Copy(string source, ITypeSymbol? type) {
        var slot = this.Slot(type);
        if (slot != "-") this.events.Append("=\t").Append(slot).Append('\t').Append(source).Append('\n');
        return slot;
    }

    // This function does not evaluate arbitrary expressions. Callers explicitly evaluate
    // non-storage receivers/arguments once, in source evaluation order.
    private string Storage(IOperation? operation, out bool readOnly) {
        readOnly = false;
        if (operation is IFlowCaptureReferenceOperation reference) {
            var capture = this.captureKinds.Get(reference.Id);
            if (capture?.Kind == MethodSynchronizationCaptures.Kind.Location)
                return this.Storage(capture.Location, out readOnly);
            if (capture?.Kind == MethodSynchronizationCaptures.Kind.Borrowed) return "-";
            if (capture?.Kind != MethodSynchronizationCaptures.Kind.Value) this.gaps.Add("FlowCaptureStorage");
            if (!this.captures.TryGetValue(reference.Id, out var captured))
                this.captures.Add(reference.Id, captured = this.Slot(reference.Type));
            return captured;
        }
        if (operation == null || this.Role(operation.Type) == "U") return "-";
        switch (operation) {
            case IDeclarationExpressionOperation declaration:
                return this.Storage(declaration.Expression, out readOnly);
            case ILocalReferenceOperation local:
                if (!SymbolEqualityComparer.Default.Equals(local.Local.ContainingSymbol, this.method)) {
                    this.gaps.Add("CapturedStorage"); return "-";
                }
                if (local.Local.RefKind != RefKind.None) { this.gaps.Add("RefLocalStorage"); return "-"; }
                if (!this.locals.TryGetValue(local.Local, out var slot)) this.locals.Add(local.Local, slot = this.Slot(local.Type));
                return slot;
            case IParameterReferenceOperation parameter:
                if (!SymbolEqualityComparer.Default.Equals(parameter.Parameter.ContainingSymbol, this.method)) {
                    this.gaps.Add("CapturedStorage"); return "-";
                }
                readOnly = parameter.Parameter.RefKind == RefKind.In;
                return "p" + Number(parameter.Parameter.Ordinal);
            case IInstanceReferenceOperation instance when instance.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance:
                readOnly = this.method.IsReadOnly || this.method.ContainingType.IsReadOnly;
                return "this";
            default: return "-";
        }
    }

    private (string Mode, string Slot) Receiver(IOperation? operation) {
        // Roslyn represents constrained Dispose in a generic using as an implicit
        // conversion to IDisposable. It is not an escaping boxed handle. Preserve
        // the actual receiver storage/type for concrete interface dispatch.
        if (operation is IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion &&
            !conversion.Conversion.IsUserDefined && conversion.Type?.TypeKind == TypeKind.Interface && conversion.Operand.Type?.IsValueType == true)
            operation = conversion.Operand;
        var storage = this.Storage(operation, out var readOnly);
        if (storage != "-") {
            this.ReferenceAccess(operation);
            return readOnly ? ("value", this.Copy(storage, operation!.Type)) : ("ref", storage);
        }
        return ("value", this.Value(operation));
    }

    private (string Mode, string Slot) Argument(IArgumentOperation argument) {
        var passing = Passing(argument.Parameter?.RefKind ?? RefKind.None);
        return this.Operand(argument.Value, passing);
    }

    private (string Mode, string Slot) Operand(IOperation? operation, string passing) {
        // An out discard still needs a temporary for the callee's effects, but
        // has no caller-owned location or value expression to read. In particular,
        // TryGetValue(..., out _) must not become an unsupported Discard operation.
        if (operation is IDiscardOperation && passing == "out") return (passing, this.Slot(operation.Type));
        if (passing == "value") return (passing, this.Value(operation));
        var storage = this.Storage(operation, out _);
        if (storage != "-") { this.ReferenceAccess(operation); return (passing, storage); }
        var value = this.Value(operation);
        if (passing is "ref" or "out" && this.Role(operation?.Type) != "U") this.gaps.Add("RefArgumentStorage");
        return (passing, value);
    }

    private string Contract(string kind, ISymbol symbol, ITypeSymbol? constrained = null, bool typeOfMetadata = false, string extraContract = "") {
        var contract = MethodSummaryContracts.Operation(kind, 0, symbol, this.compilation, this.refModes, constrained);
        if (contract == null) { this.gaps.Add("MemberWithoutId"); return "-"; }
        if (typeOfMetadata) contract += "\t!ecs-leaf\t!typeof-metadata";
        contract += extraContract;
        return this.Contract(contract);
    }

    private string Contract(string contract) {
        if (!this.contracts.TryGetValue(contract, out var key)) {
            key = "s" + Number(this.contracts.Count);
            this.contracts.Add(contract, key);
            this.declarations.Append("S\t").Append(key).Append('\t').Append(Encode(contract)).Append('\n');
        }
        return key;
    }

    private string Call(IMethodSymbol? target, (string Mode, string Slot) receiver,
        IEnumerable<(int Ordinal, string Mode, string Slot)> arguments, string kind = "call", ITypeSymbol? constrained = null, bool typeOfMetadata = false, string extraContract = "") {
        if (target == null) return this.Unknown(null, "MissingAccessor");
        if (target.MethodKind == MethodKind.DelegateInvoke) this.gaps.Add("DelegateInvoke");
        if (target.ReturnsByRef || target.ReturnsByRefReadonly) {
            if (this.Role(target.ReturnType) is not ("U" or "T")) this.gaps.Add("RefReturnStorage");
        }
        var result = this.Slot(target.ReturnType);
        this.events.Append("C\t").Append(result).Append('\t').Append(this.Contract(kind, target, constrained, typeOfMetadata, extraContract))
            .Append('\t').Append(receiver.Mode).Append('\t').Append(receiver.Slot);
        foreach (var argument in arguments.OrderBy(static argument => argument.Ordinal))
            this.events.Append('\t').Append(Number(argument.Ordinal)).Append(':').Append(argument.Mode).Append(':').Append(argument.Slot);
        this.events.Append('\n');
        // Reading the result of a ref-returning helper is a data access too. The
        // helper may have scheduled work before returning the address. Open T is
        // classified after substitution; tracked ref returns are rejected by the caller.
        if ((target.ReturnsByRef || target.ReturnsByRefReadonly) && this.Role(target.ReturnType) is "U" or "T")
            this.events.Append("A\t").Append(MethodSummaryType.From(target.ReturnType).Encode()).Append('\n');
        return result;
    }

    private List<(int Ordinal, string Mode, string Slot)> Arguments(IEnumerable<IArgumentOperation> arguments, int offset = 0) {
        var result = new List<(int, string, string)>();
        foreach (var argument in arguments) {
            var value = this.Argument(argument); // Evaluate in source order, bind by parameter ordinal.
            if (argument.Parameter == null) this.gaps.Add("ArgumentWithoutParameter");
            else result.Add((argument.Parameter.Ordinal + offset, value.Mode, value.Slot));
        }
        return result;
    }

    private void Format(IOperation operand, string value, bool interpolation) {
        if (ImplicitFormattingContracts.IsText(operand)) return;
        var contract = ImplicitFormattingContracts.Operation(operand, interpolation, 0);
        if (contract == null) { this.gaps.Add("ImplicitFormattingReceiver"); return; }
        // T has its own versioned symbol contract: dispatch determines whether
        // there are zero or two ordinary parameters and whether null skips it.
        // Older program readers reject this event rather than losing the call.
        this.events.Append("T\t").Append(this.Contract(contract)).Append('\t').Append(value).Append('\n');
    }

    private void FormatOperand(IOperation operand) {
        var value = this.Value(ImplicitFormattingContracts.ReceiverValue(operand));
        this.Format(operand, value, false);
    }

    private string Interpolate(IInterpolatedStringOperation operation) {
        var mode = ImplicitFormattingContracts.InterpolationMode(operation, this.compilation);
        if (mode == "unknown") this.gaps.Add("ImplicitFormattingLowering");
        var values = new List<(IOperation Operand, string Slot)>();
        // String.Format evaluates ALL argument expressions before invoking the
        // first formatter. Individual VisitInterpolation calls would interleave
        // evaluation and formatting and can falsely prove completion ordering.
        foreach (var part in operation.Parts) {
            if (part is IInterpolatedStringTextOperation) continue;
            if (part is not IInterpolationOperation interpolation) {
                this.Value(part); this.gaps.Add("ImplicitFormattingPart"); continue;
            }
            var value = this.Value(ImplicitFormattingContracts.ReceiverValue(interpolation.Expression));
            this.Value(interpolation.Alignment);
            this.Value(interpolation.FormatString);
            values.Add((interpolation.Expression, value));
        }
        if (mode == "format")
            foreach (var value in values) this.Format(value.Operand, value.Slot, true);
        return "-";
    }

    private string CompoundString(ICompoundAssignmentOperation operation) {
        if (operation.Target is IPropertyReferenceOperation property && !property.Property.ReturnsByRef && !property.Property.ReturnsByRefReadonly) {
            var receiver = this.Receiver(property.Instance);
            var indices = this.Arguments(property.Arguments);
            var dispatch = this.Dispatch(property.Property.GetMethod, property.Instance,
                property.Property.IsVirtual || property.Property.IsAbstract || property.Property.IsOverride);
            this.Call(property.Property.GetMethod, receiver, indices, constrained: dispatch);
            this.FormatOperand(operation.Value);
            indices.Add((property.Property.Parameters.Length, "value", "-"));
            this.Call(property.Property.SetMethod, receiver, indices,
                constrained: this.Dispatch(property.Property.SetMethod, property.Instance,
                    property.Property.IsVirtual || property.Property.IsAbstract || property.Property.IsOverride));
        } else {
            this.Value(operation.Target);
            this.FormatOperand(operation.Value);
            this.ReferenceWrite(operation.Target);
        }
        return "-";
    }

    private ITypeSymbol? Dispatch(IMethodSymbol? target, IOperation? instance, bool isVirtual) {
        while (instance is IConversionOperation conversion && conversion.OperatorMethod == null) instance = conversion.Operand;
        if (target?.ContainingType.TypeKind == TypeKind.Interface && instance?.Type?.IsValueType == true) return instance.Type;
        if (isVirtual && target != null && !target.IsSealed && !target.ContainingType.IsSealed && !target.ContainingType.IsValueType)
            this.gaps.Add("VirtualDispatch");
        return null;
    }

    private void ReferenceAccess(IOperation? operation) {
        if (operation is IFlowCaptureReferenceOperation reference) {
            var capture = this.captureKinds.Get(reference.Id);
            if (capture?.Kind == MethodSynchronizationCaptures.Kind.Location) this.ReferenceAccess(capture.Location);
            else if (capture?.Kind == MethodSynchronizationCaptures.Kind.Borrowed && reference.Type != null)
                this.events.Append("A\t").Append(MethodSummaryType.From(reference.Type).Encode()).Append('\n');
            else if (capture?.Kind != MethodSynchronizationCaptures.Kind.Value) this.gaps.Add("FlowCaptureStorage");
            return;
        }
        var byRef = operation is IParameterReferenceOperation parameter && parameter.Parameter.RefKind != RefKind.None ||
            operation is ILocalReferenceOperation local && local.Local.RefKind != RefKind.None ||
            operation is IInstanceReferenceOperation instance && instance.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                MethodSummaryContracts.IsComponentType(this.method.ContainingType, this.compilation);
        // A ref int/Ent/etc. may point into a component too. Do not require the
        // parameter's type itself to implement IComponentBase. Handle/context/query
        // storage has its separate transfer model; open T is classified by the caller.
        if (byRef && operation!.Type != null && this.Role(operation.Type) is "U" or "T")
            this.events.Append("A\t").Append(MethodSummaryType.From(operation.Type).Encode()).Append('\n');
    }

    private void ReferenceWrite(IOperation? target) {
        // Evaluating an lvalue and storing through it are separate effects. The RHS
        // may schedule new work after Get<T>()/a borrowed reference was evaluated.
        // Record the store AFTER the RHS without evaluating any receiver twice.
        switch (target) {
            case IFieldReferenceOperation field:
                this.ReferenceWrite(field.Instance);
                break;
            case IConversionOperation conversion when conversion.OperatorMethod == null:
                this.ReferenceWrite(conversion.Operand);
                break;
            case IInvocationOperation invocation when invocation.TargetMethod.ReturnsByRef || invocation.TargetMethod.ReturnsByRefReadonly:
                this.events.Append("A\t").Append(MethodSummaryType.From(invocation.TargetMethod.ReturnType).Encode()).Append('\n');
                break;
            case IPropertyReferenceOperation property when property.Property.ReturnsByRef || property.Property.ReturnsByRefReadonly:
                this.events.Append("A\t").Append(MethodSummaryType.From(property.Property.Type).Encode()).Append('\n');
                break;
            default:
                this.ReferenceAccess(target);
                break;
        }
    }

    private static bool ScalarLocal(IOperation operation) => operation is ILocalReferenceOperation or IParameterReferenceOperation &&
        ScalarType(operation.Type);
    private static bool ScalarType(ITypeSymbol? type) =>
        type?.TypeKind == TypeKind.Enum || type?.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Char or SpecialType.System_Single or SpecialType.System_Double;

    private string Value(IOperation? operation) {
        this.cancellation.ThrowIfCancellationRequested();
        if (operation == null || this.omitted(operation)) return "-";
        // Calls have their own before/after exceptional transfer in the interpreter.
        // These built-in operations can throw without a C row (overflow, cast,
        // allocation, null/array checks). Preserve those points across assemblies.
        var mayThrow = operation is IConversionOperation or IBinaryOperation or IUnaryOperation or
            IFieldReferenceOperation or IObjectCreationOperation or IInterpolatedStringOperation or
            ICompoundAssignmentOperation or IIncrementOrDecrementOperation or IArrayElementReferenceOperation;
        if (mayThrow) this.events.Append("X\n");
        var result = this.ValueCore(operation);
        if (mayThrow) this.events.Append("X\n");
        return result;
    }

    private string ValueCore(IOperation operation) {
        this.ReferenceAccess(operation);
        switch (operation) {
            case ILocalReferenceOperation:
            case IParameterReferenceOperation:
            case IInstanceReferenceOperation:
            case IFlowCaptureReferenceOperation:
                var storage = this.Storage(operation, out _);
                return storage != "-" ? this.Copy(storage, operation.Type) : this.Role(operation.Type) == "U" ? "-" :
                    this.Unknown(operation.Type, "UnknownStorage");
            case IDefaultValueOperation:
                var zero = this.Slot(operation.Type);
                if (zero != "-") this.events.Append("Z\t").Append(zero).Append('\n');
                return zero;
            case ILiteralOperation:
            case INameOfOperation:
            case ITypeOfOperation:
            case ISizeOfOperation:
            case ILocalFunctionOperation: return "-";
            case ICaughtExceptionOperation: return "-"; // Binding an exception object invokes no user code or handle conversion.
            case IExpressionStatementOperation expression: return this.Value(expression.Operation);
            case IArgumentOperation argument: return this.Argument(argument).Slot;
            case IConversionOperation conversion:
                var operand = this.Value(conversion.Operand);
                if (conversion.OperatorMethod != null) {
                    if (conversion.Operand.Type is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                        this.gaps.Add("NullableUserConversion");
                    return this.Call(conversion.OperatorMethod, ("value", "-"), new[] { (0, "value", operand) }, "conversion");
                }
                if (!SymbolEqualityComparer.Default.Equals(conversion.Type, conversion.Operand.Type) &&
                    (this.Role(conversion.Type) != "U" || this.Role(conversion.Operand.Type) != "U"))
                    return this.Unknown(conversion.Type, "TrackedValueConversion");
                return operand;
            case IVariableDeclaratorOperation variable:
                var initial = this.Value(variable.Initializer?.Value);
                if (this.Role(variable.Symbol.Type) == "U") return "-";
                if (variable.Symbol.RefKind != RefKind.None) return this.Unknown(variable.Symbol.Type, "RefLocalStorage");
                if (!this.locals.TryGetValue(variable.Symbol, out var local)) this.locals.Add(variable.Symbol, local = this.Slot(variable.Symbol.Type));
                if (variable.Initializer != null) this.events.Append("=\t").Append(local).Append('\t').Append(initial).Append('\n');
                return initial;
            case IFlowCaptureOperation capture:
                var captureKind = this.captureKinds.Get(capture.Id);
                if (captureKind?.Kind == MethodSynchronizationCaptures.Kind.Location) {
                    // Taking a fixed address does not read/copy the value. Its later
                    // uses refer to the original slot, including mutations in arguments.
                    return this.Storage(captureKind.Location, out _);
                }
                if (captureKind?.Kind == MethodSynchronizationCaptures.Kind.Borrowed) {
                    this.Value(capture.Value); // Evaluate receiver/getter/indices once, at acquisition.
                    return "-";
                }
                if (captureKind?.Kind != MethodSynchronizationCaptures.Kind.Value) this.gaps.Add("FlowCaptureStorage");
                var value = this.Value(capture.Value);
                if (this.Role(capture.Value.Type) == "U") return "-";
                if (!this.captures.TryGetValue(capture.Id, out var captureSlot)) this.captures.Add(capture.Id, captureSlot = this.Slot(capture.Value.Type));
                this.events.Append("=\t").Append(captureSlot).Append('\t').Append(value).Append('\n');
                return value;
            case IInvocationOperation invocation:
                var target = invocation.TargetMethod;
                var reduced = target.ReducedFrom;
                (string Mode, string Slot) receiver;
                if (reduced != null) receiver = this.Operand(invocation.Instance, Passing(reduced.Parameters[0].RefKind));
                else receiver = this.Receiver(invocation.Instance);
                var arguments = this.Arguments(invocation.Arguments, reduced == null ? 0 : 1);
                if (reduced != null) {
                    arguments.Add((0, Passing(reduced.Parameters[0].RefKind), receiver.Slot));
                    receiver = ("value", "-");
                }
                return this.Call(target, receiver, arguments, constrained: this.Dispatch(target, invocation.Instance, invocation.IsVirtual),
                    extraContract: UnityAllocationContracts.Invocation(invocation, this.compilation) + BurstStorageContracts.Invocation(invocation, this.compilation) +
                        BclFormattingContracts.Invocation(invocation, this.compilation) +
                        this.localContainers?.Contract(target, invocation.Instance) + this.localContainers?.Arguments(invocation));
            case IPropertyReferenceOperation property:
                var propertyReceiver = this.Receiver(property.Instance);
                var indices = this.Arguments(property.Arguments);
                var typeOfMetadata = ExternalValueContracts.IsTypeOfMetadata(property.Property.GetMethod, property.Instance, this.compilation);
                return this.Call(property.Property.GetMethod, propertyReceiver, indices,
                    constrained: typeOfMetadata ? null : this.Dispatch(property.Property.GetMethod, property.Instance, property.Property.IsVirtual || property.Property.IsAbstract || property.Property.IsOverride),
                    typeOfMetadata: typeOfMetadata, extraContract: this.localContainers?.Contract(property.Property.GetMethod, property.Instance) ?? "");
            case ISimpleAssignmentOperation assignment:
                if (assignment.IsRef) this.gaps.Add("RefAssignment");
                // A discard has no destination storage. Evaluate its RHS normally:
                // dropping a returned JobHandle must not erase the scheduled work.
                // A real local named '_' is ILocalReferenceOperation, not a discard.
                if (assignment.Target is IDiscardOperation) return this.Value(assignment.Value);
                if (assignment.Target is IFieldReferenceOperation assignedField) {
                    this.Value(assignedField.Instance); // Address evaluation, not a read of the stored value.
                    this.events.Append("F\t").Append(this.Contract("field", assignedField.Field)).Append('\n');
                    var assignedValue = this.Value(assignment.Value);
                    this.ReferenceWrite(assignment.Target);
                    this.events.Append("O\t").Append(MethodSummaryType.From(assignedField.Type!).Encode()).Append('\n');
                    return assignedValue;
                }
                if (assignment.Target is IPropertyReferenceOperation setter && !setter.Property.ReturnsByRef && !setter.Property.ReturnsByRefReadonly) {
                    if (assignment.IsImplicit && setter.Property.DeclaringSyntaxReferences.Any(reference =>
                            reference.GetSyntax(this.cancellation) is PropertyDeclarationSyntax { Initializer: not null } property &&
                            property.SyntaxTree == assignment.Syntax.SyntaxTree && property.Initializer.Span.Contains(assignment.Syntax.Span))) {
                        this.Value(setter.Instance);
                        var initialized = this.Value(assignment.Value);
                        // C# writes the backing field, even for a get-only auto-property.
                        var backing = setter.Property.ContainingType.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(field =>
                            SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, setter.Property));
                        if (backing == null) this.gaps.Add("AutoPropertyInitializerBackingField");
                        else this.events.Append("F\t").Append(this.Contract("field", backing)).Append('\n');
                        this.events.Append("O\t").Append(MethodSummaryType.From(setter.Type!).Encode()).Append('\n');
                        return initialized;
                    }
                    var setterReceiver = this.Receiver(setter.Instance);
                    var setterArguments = this.Arguments(setter.Arguments);
                    var assigned = this.Value(assignment.Value);
                    setterArguments.Add((setter.Property.Parameters.Length, "value", assigned));
                    this.Call(setter.Property.SetMethod, setterReceiver, setterArguments,
                        constrained: this.Dispatch(setter.Property.SetMethod, setter.Instance, setter.Property.IsVirtual || setter.Property.IsAbstract || setter.Property.IsOverride),
                        extraContract: this.localContainers?.Contract(setter.Property.SetMethod, setter.Instance) ?? "");
                    return assigned;
                }
                var destination = this.Storage(assignment.Target, out _);
                // Obtaining a local/parameter address is not a read of its contents.
                // A borrowed scalar/component store is accounted for after the RHS.
                if (destination == "-" && assignment.Target is not (ILocalReferenceOperation or IParameterReferenceOperation) &&
                    !(assignment.Target is IFlowCaptureReferenceOperation capturedTarget &&
                        this.captureKinds.Get(capturedTarget.Id)?.Kind is MethodSynchronizationCaptures.Kind.Location or MethodSynchronizationCaptures.Kind.Borrowed))
                    this.Value(assignment.Target);
                var source = this.Value(assignment.Value);
                this.ReferenceWrite(assignment.Target);
                if (destination != "-") {
                    this.events.Append("=\t").Append(destination).Append('\t').Append(source).Append('\n');
                }
                else if (this.Role(assignment.Target.Type) != "U") this.gaps.Add("AssignmentStorage");
                return source;
            case IFieldReferenceOperation field:
                this.Value(field.Instance);
                this.events.Append("F\t").Append(this.Contract("field", field.Field)).Append('\n');
                this.events.Append("O\t").Append(MethodSummaryType.From(field.Type!).Encode()).Append('\n');
                return this.Slot(field.Type); // O validates a closed generic field before the slot is read.
            case IObjectCreationOperation creation:
                var constructorArguments = this.Arguments(creation.Arguments);
                string created;
                if (creation.Constructor is { IsImplicitlyDeclared: true, Parameters.Length: 0 } && creation.Type?.IsValueType == true) {
                    created = this.Slot(creation.Type);
                    if (created != "-") this.events.Append("Z\t").Append(created).Append('\n');
                } else {
                    this.Call(creation.Constructor, ("value", "-"), constructorArguments, "new",
                        extraContract: UnityContainerContracts.CreationContract(creation, this.compilation));
                    created = this.Role(creation.Type) == "U" ? "-" : this.Unknown(creation.Type, "TrackedConstructorResult");
                }
                if (creation.Initializer != null) { this.gaps.Add("ObjectInitializerReceiver"); this.Value(creation.Initializer); }
                return created;
            case ITypeParameterObjectCreationOperation genericCreation:
                if (genericCreation.Type == null) return this.Unknown(null, "MissingGenericConstructionType");
                var constructed = this.Slot(genericCreation.Type);
                this.events.Append("C\t").Append(constructed).Append('\t')
                    .Append(this.Contract(GenericConstructionContracts.Operation(genericCreation.Type, 0))).Append("\tvalue\t-\n");
                if (genericCreation.Initializer != null) {
                    this.gaps.Add("ObjectInitializerReceiver");
                    this.Value(genericCreation.Initializer);
                }
                return constructed;
            case IBinaryOperation binary:
                if (binary.OperatorMethod == null && binary.Type?.SpecialType == SpecialType.System_String) {
                    // Roslyn converts each string-concat operand before evaluating
                    // the next, including flattened chains and boxed generic values.
                    this.FormatOperand(binary.LeftOperand);
                    this.FormatOperand(binary.RightOperand);
                    return "-";
                }
                var left = this.Value(binary.LeftOperand);
                var right = this.Value(binary.RightOperand);
                if (binary.OperatorMethod != null && binary.IsLifted) this.gaps.Add("LiftedOperator");
                return binary.OperatorMethod == null ? "-" : this.Call(binary.OperatorMethod, ("value", "-"),
                    new[] { (0, "value", left), (1, "value", right) }, "operator");
            case IUnaryOperation unary:
                var unaryOperand = this.Value(unary.Operand);
                if (unary.OperatorMethod != null && unary.IsLifted) this.gaps.Add("LiftedOperator");
                return unary.OperatorMethod == null ? "-" : this.Call(unary.OperatorMethod, ("value", "-"), new[] { (0, "value", unaryOperand) }, "operator");
            case IIsPatternOperation pattern when (ScalarType(pattern.Value.Type) || pattern.Value.Type?.SpecialType == SpecialType.System_Boolean) &&
                pattern.Pattern is IConstantPatternOperation or IDiscardPatternOperation:
                // Primitive constant/discard tests only choose CFG edges. Unlike
                // recursive/property/declaration patterns they have no implicit getters
                // or tracked variable bindings to preserve. Still evaluate the input.
                this.Value(pattern.Value);
                if (pattern.Pattern is IConstantPatternOperation constant) this.Value(constant.Value);
                return "-";
            case IIncrementOrDecrementOperation increment when increment.OperatorMethod == null && ScalarLocal(increment.Target):
                // Scalar values only control which CFG edges are taken. Both edges are
                // explored, so no handle effect is lost by omitting their arithmetic.
                this.Value(increment.Target);
                return "-";
            case IInterpolatedStringOperation interpolated:
                return this.Interpolate(interpolated);
            case ICompoundAssignmentOperation text when text.OperatorMethod == null && text.Type?.SpecialType == SpecialType.System_String &&
                !text.InConversion.IsUserDefined && !text.OutConversion.IsUserDefined:
                return this.CompoundString(text);
            case ICompoundAssignmentOperation compound when compound.OperatorMethod == null && ScalarLocal(compound.Target) &&
                !compound.InConversion.IsUserDefined && !compound.OutConversion.IsUserDefined:
                this.Value(compound.Target);
                this.Value(compound.Value);
                this.ReferenceWrite(compound.Target);
                return "-";
            case IAnonymousFunctionOperation:
            case IFlowAnonymousFunctionOperation:
                return this.Unknown(operation.Type, "DelegateBinding"); // Do not execute a nested body at declaration time.
            case IReturnOperation returned:
                var result = this.Value(returned.ReturnedValue);
                this.events.Append("R\t").Append(result).Append('\n');
                return result;
        }
        foreach (var child in operation.ChildOperations) this.Value(child);
        if (operation is IBlockOperation or IVariableDeclarationGroupOperation or IVariableDeclarationOperation or
            IVariableInitializerOperation or IEmptyOperation) return "-";
        return this.Unknown(operation.Type, "Operation:" + operation.Kind);
    }
}
