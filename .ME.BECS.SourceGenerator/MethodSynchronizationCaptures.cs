using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// CFG captures are not uniformly values: receivers and ref arguments may capture an
// address before later arguments execute. Classify all definitions/uses before emission.
internal sealed class MethodSynchronizationCaptures {
    internal enum Kind { Unknown, Value, Location, Borrowed }
    internal sealed class Capture {
        internal Kind Kind;
        internal IOperation? Location;
        internal readonly List<IOperation> Definitions = new();
        internal readonly List<(IFlowCaptureReferenceOperation Reference, bool Value)> Uses = new();
    }
    private readonly Dictionary<CaptureId, Capture> captures = new();

    internal Capture? Get(CaptureId id) => this.captures.TryGetValue(id, out var capture) ? capture : null;

    internal MethodSynchronizationCaptures(ControlFlowGraph graph, IMethodSymbol method,
        Func<IOperation, bool> omitted, Func<ITypeSymbol?, bool> untracked, CancellationToken cancellation) {
        var pending = new Stack<(IOperation Operation, bool Value)>();
        foreach (var block in graph.Blocks) {
            if (!block.IsReachable) continue;
            foreach (var operation in block.Operations) pending.Push((operation, false));
            if (block.BranchValue != null) pending.Push((block.BranchValue,
                block.ConditionKind != ControlFlowConditionKind.None ||
                block.FallThroughSuccessor?.Semantics == ControlFlowBranchSemantics.Return &&
                !method.ReturnsByRef && !method.ReturnsByRefReadonly));
        }
        while (pending.Count != 0) {
            cancellation.ThrowIfCancellationRequested();
            var (operation, value) = pending.Pop();
            if (omitted(operation) || operation is ILocalFunctionOperation or IAnonymousFunctionOperation or IFlowAnonymousFunctionOperation) continue;
            if (operation is IFlowCaptureOperation definition) this.For(definition.Id).Definitions.Add(definition.Value);
            if (operation is IFlowCaptureReferenceOperation reference) this.For(reference.Id).Uses.Add((reference, value));
            foreach (var child in operation.ChildOperations) pending.Push((child, ValueUse(operation, child, method)));
        }
        // Nested conditional values may feed another capture. Resolve only monotone
        // value facts; cycles/ambiguous aliases remain Unknown, never guessed as copies.
        bool changed;
        do {
            changed = false;
            foreach (var capture in this.captures.Values) {
                cancellation.ThrowIfCancellationRequested();
                if (capture.Kind != Kind.Unknown || capture.Definitions.Count == 0 || capture.Uses.Count == 0 ||
                    capture.Uses.Any(static use => use.Reference.IsInitialization)) continue;
                var valueUses = capture.Uses.All(static use => use.Value);
                if (capture.Definitions.All(definition => this.IsValue(definition, valueUses))) {
                    capture.Kind = Kind.Value;
                    changed = true;
                    continue;
                }
                // A fixed mutable address can use the original slot. Do not alias in/
                // readonly receivers: their defensive copy can precede argument effects.
                var location = capture.Definitions[0];
                var symbol = MutableLocation(location, method);
                if (symbol != null && capture.Definitions.All(definition =>
                        SymbolEqualityComparer.Default.Equals(symbol, MutableLocation(definition, method)) &&
                        capture.Uses.All(use => definition.Syntax.SyntaxTree == use.Reference.Syntax.SyntaxTree &&
                            definition.Syntax.Span == use.Reference.Syntax.Span))) {
                    capture.Kind = Kind.Location;
                    capture.Location = location;
                    changed = true;
                    continue;
                }
                // An address into ordinary data needs no handle-cell identity. Keep
                // its acquisition effects at each definition and an access at every
                // use/store, including after an RHS or a later argument schedules work.
                // Open T is not untracked here: it could become JobHandle/SystemContext.
                if (capture.Definitions.All(definition => definition.Type != null && untracked(definition.Type) && this.DataAddress(definition))) {
                    capture.Kind = Kind.Borrowed;
                    changed = true;
                }
            }
        } while (changed);
    }

    private Capture For(CaptureId id) {
        if (!this.captures.TryGetValue(id, out var capture)) this.captures.Add(id, capture = new Capture());
        return capture;
    }

    private bool DataAddress(IOperation operation) => operation switch {
        IFieldReferenceOperation => true,
        IInvocationOperation invocation => invocation.TargetMethod.ReturnsByRef || invocation.TargetMethod.ReturnsByRefReadonly,
        IPropertyReferenceOperation property => property.Property.ReturnsByRef || property.Property.ReturnsByRefReadonly,
        IParameterReferenceOperation parameter => parameter.Parameter.RefKind != RefKind.None,
        ILocalReferenceOperation local => local.Local.RefKind != RefKind.None,
        IFlowCaptureReferenceOperation reference => this.Get(reference.Id)?.Kind is Kind.Borrowed or Kind.Location,
        _ => false,
    };

    private static bool ValueUse(IOperation parent, IOperation child, IMethodSymbol method) => parent switch {
        IArgumentOperation argument => argument.Parameter?.RefKind == RefKind.None,
        ISimpleAssignmentOperation assignment => !assignment.IsRef && ReferenceEquals(assignment.Value, child),
        IReturnOperation => !method.ReturnsByRef && !method.ReturnsByRefReadonly,
        IVariableInitializerOperation initializer => initializer.Parent is IVariableDeclaratorOperation variable && variable.Symbol.RefKind == RefKind.None,
        IConversionOperation => true,
        IBinaryOperation => true,
        IUnaryOperation => true,
        IIsPatternOperation pattern => ReferenceEquals(pattern.Value, child),
        _ => false,
    };

    private bool IsValue(IOperation operation, bool valueUses) {
        // A capture of an already classified snapshot remains a snapshot. A capture
        // of an address is not turned into a value merely because a later use reads it.
        if (operation is IFlowCaptureReferenceOperation reference) return this.Get(reference.Id)?.Kind == Kind.Value;
        var syntax = operation.Syntax;
        while (syntax.Parent is ParenthesizedExpressionSyntax) syntax = syntax.Parent;
        if (syntax is RefExpressionSyntax || syntax.Parent is RefExpressionSyntax) return false;
        if (syntax.Parent is ConditionalExpressionSyntax conditional &&
            (ReferenceEquals(conditional.WhenTrue, syntax) || ReferenceEquals(conditional.WhenFalse, syntax)) &&
            conditional.WhenTrue is not RefExpressionSyntax && conditional.WhenFalse is not RefExpressionSyntax) return true;
        if (syntax.Parent is SwitchExpressionArmSyntax arm && ReferenceEquals(arm.Expression, syntax)) return true;
        if (operation is IInvocationOperation invocation) return !invocation.TargetMethod.ReturnsByRef && !invocation.TargetMethod.ReturnsByRefReadonly;
        if (operation is IPropertyReferenceOperation property) return !property.Property.ReturnsByRef && !property.Property.ReturnsByRefReadonly;
        if (operation is IDefaultValueOperation or IObjectCreationOperation or IConversionOperation or ILiteralOperation or
            IBinaryOperation or IUnaryOperation or ISimpleAssignmentOperation) return true;
        // Ordinary argument/return/assignment captures snapshot storage before later
        // expressions. Receiver/ref uses do not provide that guarantee.
        return valueUses && operation is ILocalReferenceOperation or IParameterReferenceOperation or IInstanceReferenceOperation or IFieldReferenceOperation;
    }

    private static ISymbol? MutableLocation(IOperation operation, IMethodSymbol method) => operation switch {
        ILocalReferenceOperation local when local.Local.RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(local.Local.ContainingSymbol, method) => local.Local,
        IParameterReferenceOperation parameter when parameter.Parameter.RefKind != RefKind.In &&
            SymbolEqualityComparer.Default.Equals(parameter.Parameter.ContainingSymbol, method) => parameter.Parameter,
        IInstanceReferenceOperation instance when instance.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
            !method.IsReadOnly && !method.ContainingType.IsReadOnly => method,
        _ => null,
    };
}
