using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ME.BECS.SourceGenerator;

// Raw semantic summaries, not entity counts. A consumer must resolve calls and substitutions,
// respect Ignore/IgnoreVisited and reject unresolved dispatch before replacing the IL analysis.
[Generator(LanguageNames.CSharp)]
public sealed class MethodSummaryGenerator : IIncrementalGenerator {
    internal const string MetadataKey = "ME.BECS.MethodSummary.v2";

    private static void VisitInstanceInitializers(IMethodSymbol constructor, Compilation compilation,
        SummaryWalker target, System.Threading.CancellationToken cancellation) {
        // Initializers execute before the base call and body, only in the terminal
        // constructor of a this(...) chain. Preserve compilation/declaration order.
        var trees = compilation.SyntaxTrees.Select((tree, index) => (tree, index))
            .ToDictionary(static pair => pair.tree, static pair => pair.index);
        var initializers = constructor.ContainingType.GetMembers()
            .Where(static member => !member.IsStatic && !member.IsImplicitlyDeclared &&
                member is IFieldSymbol or IPropertySymbol)
            .SelectMany(static member => member.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax(cancellation))
            .Select(static declaration => declaration switch {
                VariableDeclaratorSyntax field => field.Initializer,
                PropertyDeclarationSyntax property => property.Initializer,
                _ => null,
            }).Where(static initializer => initializer != null)
            .OrderBy(initializer => trees[initializer!.SyntaxTree]).ThenBy(static initializer => initializer!.SpanStart);
        foreach (var initializer in initializers) {
            cancellation.ThrowIfCancellationRequested();
            var model = compilation.GetSemanticModel(initializer!.SyntaxTree);
            var value = model.GetOperation(initializer, cancellation) ?? model.GetOperation(initializer.Value, cancellation);
            if (value == null) { target.Unresolved.Add("ConstructorInitializerOperationUnavailable"); continue; }
            var walker = new SummaryWalker(model, cancellation);
            if (!MethodSummaryControlFlow.Visit(value, walker.VisitBlockOperation, walker.Unresolved, cancellation, walker.BeginBlock)) {
                walker.Unresolved.Add("InitializerControlFlowUnavailable");
                walker.Visit(value);
            }
            target.Rows.Append(walker.Rows);
            target.Unresolved.UnionWith(walker.Unresolved);
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var summaries = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax ||
                node is PropertyDeclarationSyntax { ExpressionBody: not null } or IndexerDeclarationSyntax { ExpressionBody: not null },
            static (syntax, cancellation) => {
                // Other project assemblies that do not use BECS need no analysis metadata.
                if (syntax.SemanticModel.Compilation.GetTypeByMetadataName("ME.BECS.Ent") == null) return null;
                var declaration = syntax.Node is AnonymousFunctionExpressionSyntax
                    ? (syntax.SemanticModel.GetOperation(syntax.Node, cancellation) as IAnonymousFunctionOperation)?.Symbol
                    : syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, cancellation);
                var symbol = declaration as IMethodSymbol ?? (declaration as IPropertySymbol)?.GetMethod;
                if (symbol == null) return null;
                SyntaxNode body = syntax.Node switch {
                    // The ref-expression syntax is not itself an IOperation. Query the whole
                    // arrow body so Roslyn supplies the return operation and its CFG parent.
                    PropertyDeclarationSyntax property => property.ExpressionBody!,
                    IndexerDeclarationSyntax indexer => indexer.ExpressionBody!,
                    _ => syntax.Node,
                };
                var operation = syntax.SemanticModel.GetOperation(body, cancellation);
                var autoAccessor = syntax.Node is AccessorDeclarationSyntax { Body: null, ExpressionBody: null } &&
                    !symbol.IsAbstract && !symbol.IsExtern && symbol.ContainingType.TypeKind != TypeKind.Interface;
                if (operation == null && !autoAccessor) return null;
                var id = MethodSummaryIdentity.Get(symbol);
                if (id == null) return null;
                var walker = new SummaryWalker(syntax.SemanticModel, cancellation);
                foreach (var parameter in symbol.Parameters) {
                    var mode = MethodSummaryContracts.Has(parameter, "ME.BECS.RWAttribute") ? 2 :
                        MethodSummaryContracts.Has(parameter, "ME.BECS.WOAttribute") ? 1 : MethodSummaryContracts.Has(parameter, "ME.BECS.ROAttribute") ? 0 : -1;
                    if (mode < 0) continue;
                    if (parameter.RefKind == RefKind.None) walker.Unresolved.Add("NonRefParameterOverride");
                    walker.Rows.Append("parameter-override\t0\t").Append(symbol.ContainingAssembly.Identity).Append('\t').Append(id).Append('\t')
                        .Append(MethodSummaryType.From(symbol.ContainingType).Encode()).Append('\t').Append(MethodSummaryType.From(parameter.Type).Encode())
                        .Append("\t!mode=").Append(mode).Append('\n');
                }
                if (symbol.MethodKind == MethodKind.Constructor &&
                    syntax.Node is ConstructorDeclarationSyntax constructorSyntax &&
                    constructorSyntax.Initializer?.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword) != true)
                    VisitInstanceInitializers(symbol, syntax.SemanticModel.Compilation, walker, cancellation);
                var hasControlFlow = MethodSummaryControlFlow.Visit(operation, walker.VisitBlockOperation, walker.Unresolved, cancellation, walker.BeginBlock);
                if (!hasControlFlow) {
                    if (!autoAccessor) walker.Unresolved.Add("SyntaxOnlyControlFlow");
                    if (operation is IAnonymousFunctionOperation anonymous) walker.Visit(anonymous.Body);
                    else if (operation is ILocalFunctionOperation local) walker.Visit(local.Body);
                    else walker.Visit(operation);
                }
                if (autoAccessor) {
                    var backingField = symbol.ContainingType.GetMembers().OfType<IFieldSymbol>()
                        .FirstOrDefault(f => SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, symbol.AssociatedSymbol));
                    if (backingField == null) walker.Unresolved.Add("AutoAccessorBackingField");
                    else walker.Member("field", backingField);
                }
                var flags = string.Join(",", symbol.GetAttributes().Select(static a => a.AttributeClass?.ToDisplayString())
                    .Where(static n => n is "ME.BECS.CodeGeneratorIgnoreAttribute" or "ME.BECS.CodeGeneratorIgnoreVisitedAttribute" or
                        "ME.BECS.DisableContainerSafetyRestrictionAttribute" or "Unity.Collections.LowLevel.Unsafe.DisableContainerSafetyRestrictionAttribute")
                    .OrderBy(static n => n, StringComparer.Ordinal));
                flags += (flags.Length == 0 ? "" : ",") + "safety-schema=1";
                flags += ",weight-schema=1";
                flags += ",schedule-schema=1";
                flags += MethodSummaryInterfaceMap.Flags(symbol);
                if (symbol.Arity == 0 && !symbol.IsStatic) {
                    var destroyContract = syntax.SemanticModel.Compilation.GetTypeByMetadataName("ME.BECS.IComponentDestroy");
                    var destroyMember = destroyContract?.GetMembers("Destroy").OfType<IMethodSymbol>().SingleOrDefault();
                    if (destroyMember != null && SymbolEqualityComparer.Default.Equals(
                            symbol.ContainingType.FindImplementationForInterfaceMember(destroyMember), symbol))
                        flags += ",destroy-owner=" + MethodSummaryType.From(symbol.ContainingType).Encode();
                }
                if (symbol.MethodKind == MethodKind.Constructor) {
                    flags += ",constructor-schema=1";
                }
                if (IsSystemLifecycle(symbol, syntax.SemanticModel.Compilation)) {
                    var owners = string.Join("+", MethodSummaryType.TypeOwners(symbol.ContainingType).Select(static t => t.MetadataName));
                    var ns = symbol.ContainingType.ContainingNamespace;
                    flags += ",system-root,system-type=" + (ns.IsGlobalNamespace ? owners : ns.ToDisplayString() + "." + owners);
                }
                if (symbol.Name == "NewEnt_INTERNAL" && symbol.IsStatic && symbol.Arity == 1 &&
                    SymbolEqualityComparer.Default.Equals(symbol.ContainingType, syntax.SemanticModel.Compilation.GetTypeByMetadataName("ME.BECS.Ent")))
                    flags += ",entity-creation-contract=1";
                if (IsJobExecute(symbol)) {
                    var ownerNames = string.Join("+", MethodSummaryType.TypeOwners(symbol.ContainingType).Select(static t => t.MetadataName));
                    var ns = symbol.ContainingType.ContainingNamespace;
                    var metadataName = ns.IsGlobalNamespace ? ownerNames : ns.ToDisplayString() + "." + ownerNames;
                    flags += (flags.Length == 0 ? "" : ",") + "job-root,job-type=" + metadataName;
                    var entityLimit = symbol.ContainingType.GetAttributes().FirstOrDefault(static attribute =>
                        attribute.AttributeClass?.ToDisplayString() == "ME.BECS.EntitiesJobMaxCountAttribute");
                    var entityMaximum = entityLimit == null ? "0" :
                        entityLimit.ConstructorArguments.Length == 1 && entityLimit.ConstructorArguments[0].Value is uint maximum && maximum > 0u
                            ? maximum.ToString(System.Globalization.CultureInfo.InvariantCulture) : "invalid";
                    flags += ",entity-limit-schema=1,entity-max-count=" + entityMaximum;
                    flags += ",weight-base=" + symbol.ContainingType.AllInterfaces.Sum(static i => MethodSummaryType.TypeOwners(i).Sum(static t => t.TypeArguments.Length))
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                return id + "\n" + flags + "\n" + string.Join(",", walker.Unresolved.OrderBy(static s => s, StringComparer.Ordinal)) + "\n" +
                    string.Join("\t", MethodSummaryType.Environment(symbol).Select(static t => t.Encode())) + "\n" + walker.Rows;
            }).Where(static s => s != null).Collect();
        var implicitConstructors = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntax, cancellation) => {
                if (syntax.SemanticModel.Compilation.GetTypeByMetadataName("ME.BECS.Ent") == null ||
                    syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, cancellation) is not INamedTypeSymbol type || type.IsStatic)
                    return null;
                // Only one partial declaration owns the summary. Record synthesis is not
                // equivalent to a default class constructor and is deliberately excluded.
                var owner = type.DeclaringSyntaxReferences.FirstOrDefault();
                if (owner == null || owner.SyntaxTree != syntax.Node.SyntaxTree || owner.Span != syntax.Node.Span) return null;
                var constructor = type.InstanceConstructors.FirstOrDefault(static method =>
                    method.IsImplicitlyDeclared && method.Parameters.Length == 0);
                if (constructor == null) return null;
                var id = MethodSummaryIdentity.Get(constructor);
                if (id == null) return null;
                var walker = new SummaryWalker(syntax.SemanticModel, cancellation);
                VisitInstanceInitializers(constructor, syntax.SemanticModel.Compilation, walker, cancellation);
                // System.Object's parameterless constructor has no user code or ECS effects.
                // Every other base constructor remains an ordinary transitive call.
                if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType) {
                    var baseConstructor = baseType.InstanceConstructors.FirstOrDefault(static method => method.Parameters.Length == 0);
                    if (baseConstructor == null) walker.Unresolved.Add("ImplicitBaseConstructorUnavailable");
                    else walker.Member("call", baseConstructor);
                }
                return id + "\nsafety-schema=1,weight-schema=1,schedule-schema=1,constructor-schema=1\n" +
                    string.Join(",", walker.Unresolved.OrderBy(static gap => gap, StringComparer.Ordinal)) + "\n" +
                    string.Join("\t", MethodSummaryType.Environment(constructor).Select(static argument => argument.Encode())) + "\n" + walker.Rows;
            }).Where(static summary => summary != null).Collect();
        var allSummaries = summaries.Combine(implicitConstructors).Select(static (pair, _) => pair.Left.AddRange(pair.Right));
        context.RegisterSourceOutput(allSummaries.Combine(context.CompilationProvider), static (output, input) => {
            var rows = input.Left.Where(static s => s != null).Select(static s => s!).Distinct(StringComparer.Ordinal).OrderBy(static s => s, StringComparer.Ordinal).ToArray();
            // A component-only asmdef can specialize imported generic jobs without any local methods.
            if (input.Right.GetTypeByMetadataName("ME.BECS.Ent") == null) return;
            // Attribute data is available through IAssemblySymbol.GetAttributes in downstream asmdefs.
            // It needs neither a runtime BECS API nor loading/invoking generated methods in the Editor.
            const int chunkSize = 128;
            for (var start = 0; start < rows.Length; start += chunkSize) {
                var source = new StringBuilder("// <auto-generated/>\n");
                for (var i = start; i < Math.Min(start + chunkSize, rows.Length); ++i) {
                    source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"").Append(MetadataKey)
                        .Append("\", ").Append(SymbolDisplay.FormatLiteral(rows[i]!, true)).Append(")]\n");
                }
                output.AddSource("ME.BECS.MethodSummaries." + (start / chunkSize) + ".g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
            }
            MethodSummaryGraph.EmitCoverage(output, input.Right, rows);
        });
    }

    private static bool IsSystemLifecycle(IMethodSymbol method, Compilation compilation) {
        var system = compilation.GetTypeByMetadataName("ME.BECS.ISystem");
        if (system == null || !method.ContainingType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, system))) return false;
        foreach (var name in new[] { "IAwake", "IStart", "IUpdate", "IDestroy", "IDrawGizmos" }) {
            var contract = compilation.GetTypeByMetadataName("ME.BECS." + name);
            if (contract == null) continue;
            foreach (var member in contract.GetMembers().OfType<IMethodSymbol>())
                if (SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(member), method)) return true;
        }
        return false;
    }

    private static bool IsJobExecute(IMethodSymbol method) {
        if (method.Name != "Execute" && !method.ExplicitInterfaceImplementations.Any(static m => m.Name == "Execute")) return false;
        foreach (var contract in method.ContainingType.AllInterfaces) {
            var ns = contract.ContainingNamespace.ToDisplayString();
            if (ns != "ME.BECS.Jobs" && ns != "Unity.Jobs") continue;
            if (!contract.Name.StartsWith("IJob", StringComparison.Ordinal)) continue;
            foreach (var execute in contract.GetMembers("Execute")) {
                if (SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(execute), method)) return true;
            }
        }
        return false;
    }

    private sealed class SummaryWalker : OperationWalker {
        private readonly SemanticModel semanticModel;
        private readonly Dictionary<CaptureId, IMethodSymbol> blockDelegates = new Dictionary<CaptureId, IMethodSymbol>();
        internal void BeginBlock() => this.blockDelegates.Clear();
        private readonly Dictionary<SyntaxNode, bool> omittedConditionalCalls = new Dictionary<SyntaxNode, bool>();
        private readonly Dictionary<INamedTypeSymbol, int?> refModes = new Dictionary<INamedTypeSymbol, int?>(SymbolEqualityComparer.Default);
        private readonly System.Threading.CancellationToken cancellation;
        private int loopDepth;
        internal readonly StringBuilder Rows = new StringBuilder();
        internal readonly HashSet<string> Unresolved = new HashSet<string>(StringComparer.Ordinal);

        internal SummaryWalker(SemanticModel semanticModel, System.Threading.CancellationToken cancellation) {
            this.semanticModel = semanticModel;
            this.cancellation = cancellation;
        }

        internal void VisitBlockOperation(IOperation operation, bool repeats) {
            this.loopDepth = repeats ? 1 : 0;
            this.Visit(operation);
            this.loopDepth = 0;
        }

        public override void Visit(IOperation? operation) {
            this.cancellation.ThrowIfCancellationRequested();
            if (operation == null) return;
            // CFG lowering can lift argument evaluation into separate blocks. Filtering only
            // VisitInvocation would retain side effects of arguments to an omitted call.
            foreach (var invocation in operation.Syntax.AncestorsAndSelf().OfType<InvocationExpressionSyntax>()) {
                if (this.IsOmittedConditionalCall(invocation)) return;
            }
            if (operation is IFlowAnonymousFunctionOperation anonymous) {
                this.Member("method-ref", anonymous.Symbol);
                this.Unresolved.Add("DelegateBinding");
                return;
            }
            if (operation is IForEachLoopOperation forEach) {
                this.Unresolved.Add("LoopControlFlow");
                this.VisitForEachLoop(forEach);
                return;
            }
            if (operation is ILoopOperation) {
                this.Unresolved.Add("LoopControlFlow");
                // Structural loop context only. For/foreach initialization and tests require CFG
                // treatment in the count analyzer; this depth must not be read as execution count.
                ++this.loopDepth;
                base.Visit(operation);
                --this.loopDepth;
                return;
            }
            if (operation.Kind is OperationKind.Invalid or OperationKind.DynamicInvocation or OperationKind.DynamicObjectCreation or
                OperationKind.DynamicMemberReference or OperationKind.FunctionPointerInvocation or OperationKind.Await or
                OperationKind.Using or OperationKind.UsingDeclaration or OperationKind.Lock)
                this.Unresolved.Add(operation.Kind.ToString());
            if (operation.Kind is OperationKind.Conditional or OperationKind.Switch or OperationKind.SwitchExpression or
                OperationKind.Try or OperationKind.Branch or OperationKind.Coalesce or OperationKind.ConditionalAccess)
                this.Unresolved.Add("ControlFlow");
            base.Visit(operation);
        }

        public override void VisitForEachLoop(IForEachLoopOperation operation) {
            if (operation.Syntax is not CommonForEachStatementSyntax syntax) {
                this.Unresolved.Add("ImplicitEnumeratorCalls");
                base.VisitForEachLoop(operation);
                return;
            }
            var info = this.semanticModel.GetForEachStatementInfo(syntax);
            // Enumeration acquisition runs once, outside the loop. Disposal is likewise outside
            // the repeating region. Branches/exceptions still require CFG for execution counts.
            this.Visit(operation.Collection);
            if (info.GetEnumeratorMethod != null) this.ImplicitMethod(info.GetEnumeratorMethod);
            if (info.IsAsynchronous) this.Unresolved.Add("AsyncEnumeration");
            if (syntax is ForEachVariableStatementSyntax) this.Unresolved.Add("ForeachDeconstruction");
            ++this.loopDepth;
            if (info.MoveNextMethod != null) this.ImplicitMethod(info.MoveNextMethod);
            if (info.CurrentProperty != null) this.Accessor(info.CurrentProperty.GetMethod);
            if (info.CurrentConversion.MethodSymbol != null) this.Method("conversion", info.CurrentConversion.MethodSymbol);
            if (info.ElementConversion.MethodSymbol != null) this.Method("conversion", info.ElementConversion.MethodSymbol);
            this.Visit(operation.LoopControlVariable);
            this.Visit(operation.Body);
            foreach (var next in operation.NextVariables) this.Visit(next);
            --this.loopDepth;
            if (info.DisposeMethod != null) this.ImplicitMethod(info.DisposeMethod);
        }

        private void ImplicitMethod(IMethodSymbol method) {
            if (NeedsVirtualResolution(method)) this.Unresolved.Add("VirtualDispatch");
            this.Method("call", method);
        }

        public override void VisitEventAssignment(IEventAssignmentOperation operation) {
            if (operation.EventReference is not IEventReferenceOperation target) {
                this.Unresolved.Add("EventAssignment");
                base.VisitEventAssignment(operation);
                return;
            }
            this.Visit(target.Instance);
            this.Visit(operation.HandlerValue);
            this.Accessor(operation.Adds ? target.Event.AddMethod : target.Event.RemoveMethod, ReceiverType(target.Instance));
        }

        private bool IsOmittedConditionalCall(InvocationExpressionSyntax invocation) {
            if (this.omittedConditionalCalls.TryGetValue(invocation, out var omitted)) return omitted;
            var symbols = new HashSet<string>(StringComparer.Ordinal);
            for (var method = this.semanticModel.GetSymbolInfo(invocation, this.cancellation).Symbol as IMethodSymbol;
                 method != null; method = method.OverriddenMethod) {
                foreach (var attribute in method.GetAttributes()) {
                    if (attribute.AttributeClass?.ToDisplayString() == "System.Diagnostics.ConditionalAttribute" &&
                        attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string symbol)
                        symbols.Add(symbol);
                }
            }
            if (symbols.Count != 0) {
                var defined = new HashSet<string>(((CSharpParseOptions)invocation.SyntaxTree.Options).PreprocessorSymbolNames, StringComparer.Ordinal);
                // File-local #define/#undef override assembly symbols at the call site.
                foreach (var trivia in invocation.SyntaxTree.GetRoot(this.cancellation).DescendantTrivia(descendIntoTrivia: true)) {
                    if (trivia.SpanStart >= invocation.SpanStart) break;
                    if (!trivia.HasStructure) continue;
                    if (trivia.GetStructure() is DefineDirectiveTriviaSyntax define && define.IsActive) defined.Add(define.Name.ValueText);
                    else if (trivia.GetStructure() is UndefDirectiveTriviaSyntax undef && undef.IsActive) defined.Remove(undef.Name.ValueText);
                }
                omitted = !symbols.Any(defined.Contains);
            }
            this.omittedConditionalCalls.Add(invocation, omitted);
            return omitted;
        }

        public override void VisitInvocation(IInvocationOperation operation) {
            if (operation.TargetMethod.MethodKind == MethodKind.DelegateInvoke &&
                operation.Instance is IFlowCaptureReferenceOperation captured &&
                this.blockDelegates.TryGetValue(captured.Id, out var capturedTarget)) {
                // The receiver was evaluated when captured; never evaluate it twice.
                foreach (var argument in operation.Arguments) this.Visit(argument);
                if (NeedsVirtualResolution(capturedTarget)) this.Unresolved.Add("VirtualDispatch");
                this.Method("call", capturedTarget);
                return;
            }
            if (operation.TargetMethod.MethodKind == MethodKind.DelegateInvoke &&
                TryDirectDelegateTarget(operation.Instance, out var directTarget, out var boundReceiver)) {
                // Only a delegate constructed in this expression. Captured/local/field
                // delegates need dataflow and must not be guessed from their signature.
                this.Visit(boundReceiver);
                foreach (var argument in operation.Arguments) this.Visit(argument);
                if (NeedsVirtualResolution(directTarget!)) this.Unresolved.Add("VirtualDispatch");
                this.Method("call", directTarget!);
                return;
            }
            base.VisitInvocation(operation); // Receiver and arguments execute before the call.
            if (operation.TargetMethod.ContainingType.TypeKind == TypeKind.Interface &&
                ReceiverType(operation.Instance) is ITypeSymbol receiver && receiver.IsValueType) {
                this.Member("call", operation.TargetMethod, receiver);
                return;
            }
            if (operation.IsVirtual && NeedsVirtualResolution(operation.TargetMethod)) this.Unresolved.Add("VirtualDispatch");
            if (operation.TargetMethod.MethodKind == MethodKind.DelegateInvoke) this.Unresolved.Add("DelegateInvoke");
            this.Method("call", operation.TargetMethod);
        }

        public override void VisitFlowCapture(IFlowCaptureOperation operation) {
            this.blockDelegates.Remove(operation.Id);
            base.VisitFlowCapture(operation);
            if (TryDirectDelegateTarget(operation.Value, out var target, out _)) this.blockDelegates[operation.Id] = target!;
            // Keep delegate-binding diagnostics from the creation: other uses/escapes
            // are not proven safe by resolving this one local invocation.
        }

        private static bool TryDirectDelegateTarget(IOperation? receiver, out IMethodSymbol? target, out IOperation? boundReceiver) {
            target = null;
            boundReceiver = null;
            // User conversions can execute arbitrary code, so only unwrap built-in ones.
            while (receiver is IConversionOperation conversion && conversion.OperatorMethod == null)
                receiver = conversion.Operand;
            if (receiver is not IDelegateCreationOperation creation) return false;
            if (creation.Target is IAnonymousFunctionOperation lambda) {
                target = lambda.Symbol;
                return true;
            }
            if (creation.Target is IFlowAnonymousFunctionOperation flowLambda) {
                target = flowLambda.Symbol;
                return true;
            }
            if (creation.Target is IMethodReferenceOperation method) {
                target = method.Method;
                boundReceiver = method.Instance;
                return true;
            }
            return false;
        }

        public override void VisitObjectCreation(IObjectCreationOperation operation) {
            // Object initializers execute after the constructor, unlike a default postorder walk.
            foreach (var argument in operation.Arguments) this.Visit(argument);
            // An implicit parameterless value-type constructor is zero initialization,
            // not a callable body. Explicit constructors still require full analysis.
            if (operation.Constructor != null && !(operation.Constructor.IsImplicitlyDeclared &&
                operation.Constructor.ContainingType.IsValueType && operation.Constructor.Parameters.Length == 0))
                this.Method("new", operation.Constructor);
            this.Visit(operation.Initializer);
        }

        public override void VisitPropertyReference(IPropertyReferenceOperation operation) {
            base.VisitPropertyReference(operation);
            this.Accessor(operation.Property.GetMethod, ReceiverType(operation.Instance));
        }

        private void PropertyTarget(IPropertyReferenceOperation target) {
            // Evaluate receiver/index arguments exactly once, without treating an assignment
            // target as a read. A ref-return property instead obtains its address via the getter.
            this.Visit(target.Instance);
            foreach (var argument in target.Arguments) this.Visit(argument);
        }

        private void Accessor(IMethodSymbol? method, ITypeSymbol? receiver = null) {
            if (method == null) { this.Unresolved.Add("MissingAccessor"); return; }
            if (method.ContainingType.TypeKind == TypeKind.Interface &&
                receiver != null && receiver.IsValueType) {
                this.Member("call", method, receiver);
                return;
            }
            if (NeedsVirtualResolution(method)) this.Unresolved.Add("VirtualDispatch");
            this.Method("call", method);
        }

        private static bool NeedsVirtualResolution(IMethodSymbol method) => method.IsAbstract ||
            ((method.IsVirtual || method.IsOverride) && !method.IsSealed && !method.ContainingType.IsSealed && !method.ContainingType.IsValueType);

        private static ITypeSymbol? ReceiverType(IOperation? instance) {
            // Boxing does not change which implementation a concrete struct calls. Do not
            // unwrap user conversions: their returned interface may reference another type.
            while (instance is IConversionOperation conversion && conversion.OperatorMethod == null &&
                   !conversion.Conversion.IsUserDefined)
                instance = conversion.Operand;
            return instance?.Type;
        }

        private static bool HasSetterTarget(IOperation target) => target is IPropertyReferenceOperation p &&
            !p.Property.ReturnsByRef && !p.Property.ReturnsByRefReadonly;

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation) {
            if (!HasSetterTarget(operation.Target)) { base.VisitSimpleAssignment(operation); return; }
            var target = (IPropertyReferenceOperation)operation.Target;
            this.PropertyTarget(target);
            this.Visit(operation.Value);
            if (operation.IsImplicit && target.Property.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax(this.cancellation) is PropertyDeclarationSyntax { Initializer: not null } property &&
                    property.SyntaxTree == operation.Syntax.SyntaxTree && property.Initializer.Span.Contains(operation.Syntax.Span))) {
                // Initializer CFGs model the auto-property assignment as a property target,
                // but C# writes its backing field directly, including get-only properties.
                var backing = target.Property.ContainingType.GetMembers().OfType<IFieldSymbol>()
                    .FirstOrDefault(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, target.Property));
                if (backing == null) this.Unresolved.Add("AutoPropertyInitializerBackingField");
                else this.Member("field", backing);
                return;
            }
            this.Accessor(target.Property.SetMethod, ReceiverType(target.Instance));
        }

        public override void VisitCoalesceAssignment(ICoalesceAssignmentOperation operation) {
            if (!HasSetterTarget(operation.Target)) { base.VisitCoalesceAssignment(operation); return; }
            var target = (IPropertyReferenceOperation)operation.Target;
            this.Visit(target);
            this.Visit(operation.Value);
            this.Accessor(target.Property.SetMethod, ReceiverType(target.Instance));
            // RHS/setter only execute on the null branch. Counts must use CFG, not these raw rows.
            this.Unresolved.Add("ConditionalWrite");
        }

        public override void VisitDeconstructionAssignment(IDeconstructionAssignmentOperation operation) {
            base.VisitDeconstructionAssignment(operation);
            this.Unresolved.Add("DeconstructionAssignment");
        }

        public override void VisitFieldReference(IFieldReferenceOperation operation) {
            base.VisitFieldReference(operation);
            this.Member("field", operation.Field);
        }

        public override void VisitConversion(IConversionOperation operation) {
            base.VisitConversion(operation);
            if (operation.OperatorMethod != null) this.Method("conversion", operation.OperatorMethod);
        }

        public override void VisitBinaryOperator(IBinaryOperation operation) {
            base.VisitBinaryOperator(operation);
            if (operation.OperatorMethod != null) this.Method("operator", operation.OperatorMethod);
        }

        public override void VisitUnaryOperator(IUnaryOperation operation) {
            base.VisitUnaryOperator(operation);
            if (operation.OperatorMethod != null) this.Method("operator", operation.OperatorMethod);
        }

        public override void VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation) {
            base.VisitIncrementOrDecrement(operation);
            if (operation.OperatorMethod != null) this.Method("operator", operation.OperatorMethod);
            if (HasSetterTarget(operation.Target)) this.Accessor(((IPropertyReferenceOperation)operation.Target).Property.SetMethod, ReceiverType(((IPropertyReferenceOperation)operation.Target).Instance));
        }

        public override void VisitCompoundAssignment(ICompoundAssignmentOperation operation) {
            this.Visit(operation.Target);
            if (operation.InConversion.MethodSymbol != null) this.Method("conversion", operation.InConversion.MethodSymbol);
            this.Visit(operation.Value);
            if (operation.OperatorMethod != null) this.Method("operator", operation.OperatorMethod);
            if (operation.OutConversion.MethodSymbol != null) this.Method("conversion", operation.OutConversion.MethodSymbol);
            if (HasSetterTarget(operation.Target)) this.Accessor(((IPropertyReferenceOperation)operation.Target).Property.SetMethod, ReceiverType(((IPropertyReferenceOperation)operation.Target).Instance));
        }

        public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation) {
            this.Member("method-ref", operation.Symbol);
            this.Unresolved.Add("DelegateBinding"); // Its body is not executed at declaration.
        }

        public override void VisitLocalFunction(ILocalFunctionOperation operation) {
            // Declared bodies are exported separately. Only a call/reference adds a graph edge.
        }

        public override void VisitMethodReference(IMethodReferenceOperation operation) {
            base.VisitMethodReference(operation);
            this.Member("method-ref", operation.Method);
            this.Unresolved.Add("DelegateBinding");
        }

        private void Method(string kind, IMethodSymbol method) {
            if (method.ReducedFrom is { } reduced) {
                method = reduced.Arity == method.TypeArguments.Length && reduced.Arity != 0 ? reduced.ConstructedFrom.Construct(method.TypeArguments.ToArray()) : reduced;
            }
            this.Member(kind, method);
        }

        internal void Member(string kind, ISymbol symbol, ITypeSymbol? constrainedReceiver = null) {
            var id = MethodSummaryIdentity.Get(symbol);
            if (id == null) { this.Unresolved.Add("MemberWithoutId"); return; }
            this.Rows.Append(kind).Append('\t').Append(this.loopDepth).Append('\t')
                .Append(symbol.ContainingAssembly.Identity.ToString()).Append('\t').Append(id).Append('\t')
                .Append(MethodSummaryType.From(symbol.ContainingType).Encode());
            if (symbol is IMethodSymbol method) {
                foreach (var owner in MethodSummaryType.MethodOwners(method)) {
                    foreach (var argument in owner.TypeArguments) this.Rows.Append('\t').Append(MethodSummaryType.From(argument).Encode());
                }
            }
            MethodSummaryContracts.Append(this.Rows, symbol, this.semanticModel.Compilation, this.refModes);
            if (constrainedReceiver != null) this.Rows.Append("\t!constrained=").Append(MethodSummaryType.From(constrainedReceiver).Encode());
            this.Rows.Append('\n');
        }
    }
}
