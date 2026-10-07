using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// A whole-local invariant, not a guess from the nearest preceding constructor.
// All assignments must construct a built-in-backed container and all references must
// preserve its allocator. Escapes, copies, captures, ref aliases, header access and
// unknown methods invalidate the entire local, including uses before the escape.
// Proven borrowing helpers and non-escaping foreach pairs preserve this invariant.
// This is independent of CFG traversal/branch order and does not claim memory-lifetime
// correctness (e.g. use after Dispose). Parameter facts remain conditional until
// ContainerAllocatorBindings binds a particular call. Fields are never certified.
internal sealed class LocalContainerAllocatorProof : OperationWalker {
    private readonly IOperation root;
    private readonly Compilation compilation;
    private readonly SemanticModel model;
    private readonly CancellationToken cancellation;
    private readonly System.Func<IOperation, bool> omitted;
    private readonly Dictionary<ILocalSymbol, bool> locals = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<IMethodSymbol, bool> preserves = new(SymbolEqualityComparer.Default);
    private int work;
    private bool exceeded;

    internal LocalContainerAllocatorProof(IOperation root, Compilation compilation, CancellationToken cancellation,
        System.Func<IOperation, bool> omitted) {
        this.root = root;
        this.compilation = compilation;
        this.model = compilation.GetSemanticModel(root.Syntax.SyntaxTree);
        this.cancellation = cancellation;
        this.omitted = omitted;
        this.Visit(root);
    }

    internal string Contract(IMethodSymbol? method, IOperation? receiver) {
        if (this.exceeded || method == null || receiver == null) return "";
        var slot = this.Slot(receiver);
        var type = slot is ILocalSymbol localSlot ? localSlot.Type : (slot as IParameterSymbol)?.Type;
        if (type is INamedTypeSymbol named && method.ContainingType.TypeKind == TypeKind.Interface)
            method = named.FindImplementationForInterfaceMember(method) as IMethodSymbol;
        if (method == null) return "";
        var contract = AllocationContract(method, this.compilation);
        if (contract.Length == 0) return "";
        if (slot is ILocalSymbol local && this.locals.TryGetValue(local, out var valid) && valid) return contract;
        if (slot is IParameterSymbol parameter && this.Borrows(parameter))
            return "\t" + ContainerAllocatorBindings.Parameter + "v1:" + parameter.Ordinal.ToString(CultureInfo.InvariantCulture);
        return "";
    }

    internal static string AllocationContract(IMethodSymbol method, Compilation compilation) =>
        UnityContainerContracts.LocalAllocationContract(method, compilation) + NativeHashMapGrowthSummaries.Contract(method, compilation);

    private bool Borrows(IParameterSymbol parameter) => parameter.ContainingSymbol is IMethodSymbol owner &&
        ContainerBorrowProof.Preserves(owner, parameter.Ordinal, this.compilation, this.cancellation);

    internal string Arguments(IInvocationOperation invocation) {
        if (this.exceeded) return "";
        var method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
        var bindings = new SortedDictionary<int, string>();
        void Bind(int ordinal, IOperation? value) {
            if (!ContainerBorrowProof.Preserves(method, ordinal, this.compilation, this.cancellation)) return;
            var slot = this.Slot(value);
            if (slot is ILocalSymbol local && this.locals.TryGetValue(local, out var valid) && valid) bindings[ordinal] = "b";
            else if (slot is IParameterSymbol parameter && this.Borrows(parameter))
                bindings[ordinal] = "p" + parameter.Ordinal.ToString(CultureInfo.InvariantCulture);
        }
        var reduced = invocation.TargetMethod.ReducedFrom != null;
        foreach (var argument in invocation.Arguments)
            if (argument.Parameter != null && UnityContainerContracts.IsLocalContainer(argument.Parameter.Type, this.compilation))
                Bind(argument.Parameter.Ordinal + (reduced ? 1 : 0), argument.Value);
        if (reduced && UnityContainerContracts.IsLocalContainer(method.Parameters[0].Type, this.compilation)) Bind(0, invocation.Instance);
        return bindings.Count == 0 ? "" : "\t" + ContainerAllocatorBindings.Arguments + "v1:" +
            string.Join("|", bindings.Select(pair => pair.Key.ToString(CultureInfo.InvariantCulture) + "=" + pair.Value));
    }

    private ISymbol? Slot(IOperation? receiver) {
        while (receiver is IParenthesizedOperation parentheses) receiver = parentheses.Operand;
        if (receiver is IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion &&
            !conversion.Conversion.IsUserDefined && conversion.Type?.TypeKind == TypeKind.Interface && conversion.Operand.Type?.IsValueType == true)
            receiver = conversion.Operand; // Implicit constrained using cleanup, not an escaping box.
        ISymbol? slot = receiver is ILocalReferenceOperation local ? local.Local : (receiver as IParameterReferenceOperation)?.Parameter;
        // CFG lowering can capture the receiver address before evaluating arguments.
        // Only a reference to the same identifier is accepted; the invariant covers
        // ALL its uses, including those in subsequently evaluated argument expressions.
        if (slot == null && receiver is IFlowCaptureReferenceOperation && receiver.Syntax is IdentifierNameSyntax name) {
            slot = this.model.GetSymbolInfo(name, this.cancellation).Symbol;
            var type = slot is ILocalSymbol capturedLocal ? capturedLocal.Type : (slot as IParameterSymbol)?.Type;
            // A lowered foreach enumerator can retain the collection's syntax.
            // It is NOT the collection header (notably for IDisposable cleanup).
            if (!SymbolEqualityComparer.Default.Equals(type, receiver.Type)) return null;
        }
        return slot;
    }

    public override void Visit(IOperation? operation) {
        if (operation == null || this.exceeded || this.omitted(operation)) return;
        this.cancellation.ThrowIfCancellationRequested();
        if (++this.work > 100000 || this.locals.Count > 1024) { this.exceeded = true; return; }
        base.Visit(operation);
    }

    private void Assignment(ILocalSymbol local, IOperation? value) {
        if (!UnityContainerContracts.IsLocalContainer(local.Type, this.compilation)) return;
        var valid = local.RefKind == RefKind.None && value is IObjectCreationOperation creation && creation.Initializer == null &&
            UnityContainerContracts.CreationContract(creation, this.compilation).Length != 0;
        if (this.locals.TryGetValue(local, out var previous)) valid &= previous;
        this.locals[local] = valid;
    }

    public override void VisitVariableDeclarator(IVariableDeclaratorOperation operation) {
        // A declaration without an initializer is not a write. The C# compiler
        // proves definite assignment; every later write is inspected below.
        if (operation.Initializer != null) this.Assignment(operation.Symbol, operation.Initializer.Value);
        base.VisitVariableDeclarator(operation);
    }

    public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation) {
        if (operation.Target is ILocalReferenceOperation target)
            this.Assignment(target.Local, operation.IsRef ? null : operation.Value);
        base.VisitSimpleAssignment(operation);
    }

    private bool Preserves(IMethodSymbol? method) {
        if (method == null) return false;
        if (this.preserves.TryGetValue(method, out var result)) return result;
        result = ContainerBorrowProof.Member(method, this.compilation);
        this.preserves.Add(method, result);
        return result;
    }

    public override void VisitLocalReference(ILocalReferenceOperation operation) {
        var local = operation.Local;
        if (!UnityContainerContracts.IsLocalContainer(local.Type, this.compilation)) return;
        for (var ancestor = operation.Parent; ancestor != null && ancestor != this.root; ancestor = ancestor.Parent)
            if (ancestor is IAnonymousFunctionOperation or ILocalFunctionOperation) { this.locals[local] = false; return; }
        IOperation value = operation;
        while (value.Parent is IParenthesizedOperation parentheses) value = parentheses;
        var valid = value.Parent switch {
            ISimpleAssignmentOperation assignment => assignment.Target == value && !assignment.IsRef,
            IInvocationOperation invocation => invocation.Instance == value && this.Preserves(invocation.TargetMethod),
            IArgumentOperation argument => ContainerBorrowProof.Argument(argument, this.compilation, this.cancellation),
            IPropertyReferenceOperation property => property.Instance == value && this.Preserves(property.Property.GetMethod) &&
                (property.Parent is not ISimpleAssignmentOperation assignment || assignment.Target != property ||
                 this.Preserves(property.Property.SetMethod)),
            INameOfOperation => true,
            _ => this.Enumeration(value),
        };
        if (!valid || local.RefKind != RefKind.None) this.locals[local] = false;
    }

    private bool Enumeration(IOperation value) {
        var valid = ContainerBorrowProof.Enumeration(value, this.compilation, this.cancellation, out var cost);
        this.work += cost;
        this.exceeded |= this.work > 100000;
        return valid && !this.exceeded;
    }
}
