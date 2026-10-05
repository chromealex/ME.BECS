using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Semantic projections of the pinned Collections allocation entry points. These
// are call graphs, NOT leaves: concrete IAllocator.Handle/Try bodies are resolved
// by the normal interface-map/substitution machinery. Prefer real source summaries
// whenever the package itself exports them. No IL or allocator is executed here.
internal static class AllocatorMethodSummaries {
    internal const string OptionalGetter = "allocator-optional-getter";

    internal static void Add(Compilation compilation,
        Dictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods) {
        var manager = compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager");
        var contract = compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager+IAllocator");
        var handle = compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager+AllocatorHandle");
        var block = compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager+Block");
        if (manager == null || manager.ContainingAssembly.Name != "Unity.Collections" || !manager.IsStatic ||
            contract?.TypeKind != TypeKind.Interface || handle?.TypeKind != TypeKind.Struct || block?.TypeKind != TypeKind.Struct ||
            !SymbolEqualityComparer.Default.Equals(contract.ContainingAssembly, manager.ContainingAssembly) ||
            !SymbolEqualityComparer.Default.Equals(handle.ContainingAssembly, manager.ContainingAssembly) ||
            !SymbolEqualityComparer.Default.Equals(block.ContainingAssembly, manager.ContainingAssembly)) return;
        var getters = contract.GetMembers("Handle").OfType<IPropertySymbol>().Where(property =>
            !property.IsStatic && property.Parameters.Length == 0 && SymbolEqualityComparer.Default.Equals(property.Type, handle))
            .Select(property => property.GetMethod).Where(method => method != null && method.DeclaredAccessibility == Accessibility.Public &&
                !method.ReturnsByRef && !method.ReturnsByRefReadonly).ToArray();
        var tryMethods = contract.GetMembers("Try").OfType<IMethodSymbol>().Where(method => !method.IsStatic && method.Arity == 0 &&
            method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public &&
            !method.ReturnsByRef && !method.ReturnsByRefReadonly &&
            method.ReturnType.SpecialType == SpecialType.System_Int32 && method.Parameters.Length == 1 &&
            method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, block)).ToArray();
        if (getters.Length != 1 || tryMethods.Length != 1) return;
        var getter = getters[0]!;
        var allocations = manager.GetMembers("Allocate").OfType<IMethodSymbol>().Where(method =>
            method.DeclaringSyntaxReferences.Length == 0 && method.MethodKind == MethodKind.Ordinary &&
            method.DeclaredAccessibility == Accessibility.Public && method.IsStatic && !method.ReturnsByRef && !method.ReturnsByRefReadonly).ToArray();
        var generic = allocations.Where(method => method.Arity == 1 && method.Parameters.Length == 4 &&
            method.ReturnType is IPointerTypeSymbol pointer && pointer.PointedAtType.SpecialType == SpecialType.System_Void &&
            method.TypeParameters[0].HasUnmanagedTypeConstraint &&
            method.TypeParameters[0].ConstraintTypes.Contains(contract, SymbolEqualityComparer.Default) &&
            method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, method.TypeParameters[0]) &&
            method.Parameters.Skip(1).All(parameter => parameter.RefKind == RefKind.None && parameter.Type.SpecialType == SpecialType.System_Int32)).ToArray();
        if (generic.Length != 1) return;
        var refModes = new Dictionary<INamedTypeSymbol, int?>(SymbolEqualityComparer.Default);
        string[] Operation(IMethodSymbol method, ITypeSymbol? receiver = null) =>
            MethodSummaryContracts.Operation("call", 0, method, compilation, refModes, receiver)!.Split('\t');
        void Add(IMethodSymbol method, IEnumerable<string[]> operations, string flow, params string[] gaps) {
            var id = MethodSummaryIdentity.Get(method);
            if (id == null || method.DeclaringSyntaxReferences.Length != 0) return;
            var key = (method.ContainingAssembly.Identity.ToString(), id);
            if (methods.ContainsKey(key)) return;
            var summary = new MethodSummaryGraph.Summary {
                Id = id,
                Environment = MethodSummaryType.Environment(method),
                Flags = new[] { MethodSummaryContracts.SafetySchema, MethodSummaryContracts.SchedulingSchema,
                    MethodSummaryContracts.SystemAccessSchema, DestroyDispatchContracts.Schema, ImplicitFormattingContracts.Schema, GenericConstructionContracts.Schema,
                    SystemQueryFilterContracts.Schema, QueryScheduleModeFlow.Schema, MethodSummaryControlFlow.EffectUnionSchema,
                    "weight-schema=1", "allocator-projection=1" }.Concat(flow.Split(',')).ToArray(),
                Unresolved = gaps,
            };
            summary.Operations.AddRange(operations);
            methods.Add(key, summary);
        }
        // CheckValid(t.Handle) is conditional on the package's compile flags;
        // block.Range.Allocator=t.Handle and t.Try(ref block) are unconditional.
        // Set-union consumers visit Handle once. Counts/weights must detect an
        // effectful getter rather than assume its multiplicity is exactly one.
        var handleCall = Operation(getter, generic[0].TypeParameters[0]).Concat(new[] { "!" + OptionalGetter }).ToArray();
        var tryCall = Operation(tryMethods[0], generic[0].TypeParameters[0]);
        Add(generic[0], new[] { handleCall, tryCall }, AllocatorSynchronizationProjection.Allocate(generic[0], handleCall, tryCall));

        // Forwarding overloads eventually invoke AllocatorHandle.Try, which looks
        // up a mutable registry entry. Never substitute some registered/default
        // allocator's Try body based only on a numeric or converted handle.
        foreach (var method in allocations) {
            var pointer = method.ReturnType as IPointerTypeSymbol;
            var untyped = method.Arity == 0 && pointer?.PointedAtType.SpecialType == SpecialType.System_Void &&
                method.Parameters.Length == 4 && method.Parameters.Skip(1).All(parameter => parameter.RefKind == RefKind.None &&
                    parameter.Type.SpecialType == SpecialType.System_Int32);
            var typed = method.Arity == 1 && method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                pointer != null && SymbolEqualityComparer.Default.Equals(pointer.PointedAtType, method.TypeParameters[0]) &&
                method.Parameters.Length == 2 && method.Parameters[1].RefKind == RefKind.None &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32;
            if ((untyped || typed) && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, handle)) {
                var target = Operation(generic[0].Construct(handle));
                Add(method, new[] { target }, AllocatorSynchronizationProjection.ForwardAllocate(method, target, typed));
            }
        }
        var registered = MethodSummaryInterfaceMap.Implementation(handle, tryMethods[0]);
        if (registered != null && SymbolEqualityComparer.Default.Equals(registered.ContainingType, handle))
            Add(registered, Array.Empty<string[]>(), MethodSynchronizationFlow.Unavailable("UnclosedAllocatorRegistry"), "UnclosedAllocatorRegistry");

        // Public Free overloads accept AllocatorHandle, not a generic allocator.
        // Size/alignment/default Block setup and the native Handle getter have no
        // user callbacks. A non-null pointer calls the mutable handle registry once.
        foreach (var method in manager.GetMembers("Free").OfType<IMethodSymbol>()) {
            if (registered == null || !SymbolEqualityComparer.Default.Equals(registered.ContainingType, handle) ||
                method.DeclaringSyntaxReferences.Length != 0 || method.MethodKind != MethodKind.Ordinary ||
                method.DeclaredAccessibility != Accessibility.Public || !method.IsStatic || !method.ReturnsVoid ||
                method.Parameters.Length < 2 || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None) ||
                !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, handle) ||
                method.Parameters[1].Type is not IPointerTypeSymbol pointer) continue;
            var untyped = method.Arity == 0 && pointer.PointedAtType.SpecialType == SpecialType.System_Void &&
                method.Parameters.Length is 2 or 5 && method.Parameters.Skip(2).All(parameter => parameter.Type.SpecialType == SpecialType.System_Int32);
            var typed = method.Arity == 1 && method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                SymbolEqualityComparer.Default.Equals(pointer.PointedAtType, method.TypeParameters[0]) && method.Parameters.Length == 3 &&
                method.Parameters[2].Type.SpecialType == SpecialType.System_Int32;
            if (!untyped && !typed) continue;
            var target = Operation(registered);
            Add(method, new[] { target }, AllocatorSynchronizationProjection.Free(method, target));
        }
    }
}
