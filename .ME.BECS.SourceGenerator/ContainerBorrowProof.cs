using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Header provenance only, NEVER an effect/synchronization leaf. A helper may
// read/write elements, invoke key callbacks or schedule jobs. Its body must still
// be analyzed. Rebinding/copying/escaping a header cannot certify this contract.
internal static class ContainerBorrowProof {
    private const string Prefix = "native-container-borrow=";
    private const string Version = "v1:";
    private static readonly ConditionalWeakTable<Compilation, Cache> caches = new();

    internal static string MethodFlag(IMethodSymbol method, Compilation compilation, CancellationToken cancellation) {
        var ordinals = method.Parameters.Where(parameter => UnityContainerContracts.IsLocalContainer(parameter.Type, compilation) &&
            Preserves(method, parameter.Ordinal, compilation, cancellation)).Select(parameter => parameter.Ordinal.ToString(CultureInfo.InvariantCulture)).ToArray();
        return ordinals.Length == 0 ? "" : "," + Prefix + Version + string.Join("|", ordinals);
    }

    internal static bool Argument(IArgumentOperation argument, Compilation compilation, CancellationToken cancellation) =>
        argument.Parent is IInvocationOperation invocation && argument.Parameter != null &&
        invocation.TargetMethod.MethodKind != MethodKind.DelegateInvoke &&
        Preserves(invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod,
            argument.Parameter.Ordinal + (invocation.TargetMethod.ReducedFrom != null ? 1 : 0), compilation, cancellation);

    internal static bool Preserves(IMethodSymbol method, int ordinal, Compilation compilation, CancellationToken cancellation) =>
        caches.GetValue(compilation, static value => new Cache(value)).Preserves(method, ordinal, cancellation);

    internal static bool Member(IMethodSymbol? method, Compilation compilation) {
        if (method == null) return false;
        var row = new StringBuilder();
        UnityContainerContracts.Append(row, method, compilation);
        return row.Length != 0 || UnityContainerContracts.LocalAllocationContract(method, compilation).Length != 0 ||
            NativeHashMapGrowthSummaries.Preserves(method, compilation);
    }

    private static IOperation Parentheses(IOperation value) {
        while (value.Parent is IParenthesizedOperation parentheses) value = parentheses;
        return value;
    }

    // A foreach may expose KVPair's header pointer. Permit it only while the
    // iteration variable itself is used through the exact Key/Value/GetKeyValue
    // API. A copy, capture, return or unknown call retaining that pair is an escape.
    internal static bool Enumeration(IOperation reference, Compilation compilation, CancellationToken cancellation, out int cost) {
        cost = 0;
        var value = Parentheses(reference);
        if (value.Parent is IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion &&
            !conversion.Conversion.IsUserDefined && conversion.Parent is IForEachLoopOperation) value = conversion;
        if (value.Parent is not IForEachLoopOperation loop || loop.Collection != value || loop.IsAsynchronous ||
            loop.Locals.Length != 1 || loop.Locals[0].RefKind != RefKind.None ||
            !NativeMapEnumerationContracts.IsPair(loop.Locals[0].Type, compilation) || loop.Syntax is not CommonForEachStatementSyntax syntax) return false;
        var info = compilation.GetSemanticModel(syntax.SyntaxTree).GetForEachStatementInfo(syntax);
        if (info.GetEnumeratorMethod == null || !NativeMapEnumerationContracts.IsEnumeratorFactory(info.GetEnumeratorMethod, compilation)) return false;
        var walker = new PairWalker(loop.Body, loop.Locals[0], compilation, cancellation);
        walker.Visit(loop.Body);
        cost = walker.Work;
        return walker.Valid;
    }

    private sealed class PairWalker : OperationWalker {
        private readonly IOperation root;
        private readonly ILocalSymbol local;
        private readonly Compilation compilation;
        private readonly CancellationToken cancellation;
        internal int Work;
        internal bool Valid = true;
        internal PairWalker(IOperation root, ILocalSymbol local, Compilation compilation, CancellationToken cancellation) {
            this.root = root; this.local = local; this.compilation = compilation; this.cancellation = cancellation;
        }
        public override void Visit(IOperation? operation) {
            if (operation == null || !this.Valid) return;
            this.cancellation.ThrowIfCancellationRequested();
            if (++this.Work > 100000) { this.Valid = false; return; }
            base.Visit(operation);
        }
        public override void VisitLocalReference(ILocalReferenceOperation operation) {
            if (!SymbolEqualityComparer.Default.Equals(operation.Local, this.local)) return;
            for (var parent = operation.Parent; parent != null && parent != this.root; parent = parent.Parent)
                if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation) { this.Valid = false; return; }
            var value = Parentheses(operation);
            this.Valid &= value.Parent switch {
                IPropertyReferenceOperation property => property.Instance == value && property.Property.GetMethod != null &&
                    NativeMapEnumerationContracts.IsPairAccess(property.Property.GetMethod, this.compilation),
                IInvocationOperation invocation => invocation.Instance == value && NativeMapEnumerationContracts.IsPairAccess(invocation.TargetMethod, this.compilation),
                INameOfOperation => true,
                _ => false,
            };
        }
    }

    private sealed class Node {
        internal bool Valid;
        internal int Cost = 1;
        internal readonly List<(IMethodSymbol Method, int Ordinal)> Calls = new();
    }

    private sealed class Cache {
        private readonly Compilation compilation;
        private readonly object gate = new();
        private readonly Dictionary<IMethodSymbol, Dictionary<int, Node>> nodes = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<IAssemblySymbol, Dictionary<string, string?>> imported = new(SymbolEqualityComparer.Default);
        internal Cache(Compilation compilation) => this.compilation = compilation;

        internal bool Preserves(IMethodSymbol method, int ordinal, CancellationToken cancellation) {
            lock (this.gate) {
                var pending = new Queue<(IMethodSymbol Method, int Ordinal)>();
                var visited = new HashSet<Node>();
                pending.Enqueue((method, ordinal));
                long work = 0;
                while (pending.Count != 0) {
                    cancellation.ThrowIfCancellationRequested();
                    var entry = pending.Dequeue();
                    var node = this.Read(entry.Method, entry.Ordinal, cancellation);
                    if (!visited.Add(node)) continue;
                    work += node.Cost; // Charge cached nodes too: cache warming cannot change coverage.
                    if (!node.Valid || visited.Count > 1024 || work > 100000) return false;
                    foreach (var call in node.Calls) pending.Enqueue(call);
                }
                // Cycles are safe for this structural property: every reachable
                // use preserves provenance. Effect/count recursion stays separate.
                return true;
            }
        }

        private Node Read(IMethodSymbol method, int ordinal, CancellationToken cancellation) {
            method = (method.PartialImplementationPart ?? method).OriginalDefinition;
            if (!this.nodes.TryGetValue(method, out var parameters)) this.nodes.Add(method, parameters = new());
            if (parameters.TryGetValue(ordinal, out var cached)) return cached;
            var node = new Node();
            if (method.MethodKind != MethodKind.Ordinary || method.IsAbstract || method.IsVirtual || method.IsExtern ||
                method.IsAsync || method.IsVararg || ordinal < 0 || ordinal >= method.Parameters.Length ||
                method.Parameters[ordinal].RefKind == RefKind.Out ||
                !UnityContainerContracts.IsLocalContainer(method.Parameters[ordinal].Type, this.compilation)) return parameters[ordinal] = node;
            if (method.DeclaringSyntaxReferences.Length == 0) node.Valid = this.Imported(method, ordinal);
            else if (method.DeclaringSyntaxReferences.Length == 1) {
                var syntax = method.DeclaringSyntaxReferences[0].GetSyntax(cancellation);
                var body = this.compilation.ContainsSyntaxTree(syntax.SyntaxTree) ? this.compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax, cancellation) : null;
                if (body != null) {
                    var walker = new Walker(body, method.Parameters[ordinal], this.compilation, cancellation, node);
                    node.Valid = true;
                    walker.Visit(body);
                }
            }
            // Cancellation never installs a partially visited proof.
            return parameters[ordinal] = node;
        }

        private bool Imported(IMethodSymbol method, int ordinal) {
            if (!this.imported.TryGetValue(method.ContainingAssembly, out var records)) {
                records = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var attribute in method.ContainingAssembly.GetAttributes()) {
                    if (attribute.AttributeClass?.ToDisplayString() != "System.Reflection.AssemblyMetadataAttribute" || attribute.ConstructorArguments.Length != 2 ||
                        attribute.ConstructorArguments[0].Value as string != MethodSummaryGenerator.MetadataKey || attribute.ConstructorArguments[1].Value is not string payload) continue;
                    var lines = payload.Split('\n');
                    var flags = lines.Length < 5 ? Array.Empty<string>() : lines[1].Split(',').Where(flag => flag.StartsWith("native-container-borrow", StringComparison.Ordinal)).ToArray();
                    var flag = flags.Length == 1 && flags[0].StartsWith(Prefix + Version, StringComparison.Ordinal) ? flags[0].Substring((Prefix + Version).Length) : null;
                    if (records.ContainsKey(lines[0])) records[lines[0]] = null;
                    else records.Add(lines[0], flag);
                }
                this.imported.Add(method.ContainingAssembly, records);
            }
            var id = MethodSummaryIdentity.Get(method);
            if (id == null || !records.TryGetValue(id, out var values) || string.IsNullOrEmpty(values)) return false;
            var previous = -1;
            var found = false;
            foreach (var value in values!.Split('|')) {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || value != index.ToString(CultureInfo.InvariantCulture) ||
                    index <= previous || index >= method.Parameters.Length || method.Parameters[index].RefKind == RefKind.Out ||
                    !UnityContainerContracts.IsLocalContainer(method.Parameters[index].Type, this.compilation)) return false;
                previous = index; found |= ordinal == index;
            }
            return found;
        }
    }

    private sealed class Walker : OperationWalker {
        private readonly IOperation root;
        private readonly IParameterSymbol parameter;
        private readonly Compilation compilation;
        private readonly CancellationToken cancellation;
        private readonly Node node;
        internal Walker(IOperation root, IParameterSymbol parameter, Compilation compilation, CancellationToken cancellation, Node node) {
            this.root = root; this.parameter = parameter; this.compilation = compilation; this.cancellation = cancellation; this.node = node;
        }
        public override void Visit(IOperation? operation) {
            if (operation == null || !this.node.Valid) return;
            this.cancellation.ThrowIfCancellationRequested();
            // Iterator state machines retain parameters beyond the call, even
            // if each eventual use of the header would otherwise be harmless.
            if (++this.node.Cost > 100000 || operation.Kind is OperationKind.YieldReturn or OperationKind.YieldBreak) { this.node.Valid = false; return; }
            base.Visit(operation);
        }
        public override void VisitParameterReference(IParameterReferenceOperation operation) {
            if (!SymbolEqualityComparer.Default.Equals(operation.Parameter, this.parameter)) return;
            for (var parent = operation.Parent; parent != null && parent != this.root; parent = parent.Parent)
                if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation) { this.node.Valid = false; return; }
            var value = Parentheses(operation);
            if (value.Parent is IArgumentOperation argument && argument.Parent is IInvocationOperation call && argument.Parameter != null) {
                this.node.Calls.Add((call.TargetMethod.ReducedFrom ?? call.TargetMethod,
                    argument.Parameter.Ordinal + (call.TargetMethod.ReducedFrom != null ? 1 : 0)));
                return;
            }
            this.node.Valid &= value.Parent switch {
                IInvocationOperation invocation => invocation.Instance == value && Member(invocation.TargetMethod, this.compilation),
                IPropertyReferenceOperation property => property.Instance == value && Member(property.Property.GetMethod, this.compilation) &&
                    (property.Parent is not ISimpleAssignmentOperation assignment || assignment.Target != property || Member(property.Property.SetMethod, this.compilation)),
                INameOfOperation => true,
                _ => this.Enumeration(value),
            };
        }
        private bool Enumeration(IOperation value) {
            var valid = ContainerBorrowProof.Enumeration(value, this.compilation, this.cancellation, out var cost);
            this.node.Cost += cost;
            return valid && this.node.Cost <= 100000;
        }
    }
}
