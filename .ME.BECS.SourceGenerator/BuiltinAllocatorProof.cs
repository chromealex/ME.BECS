using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Value proof only. A factory/getter and ALL of its arguments remain ordinary
// analyzed calls; returning a built-in allocator does not make their effects pure.
internal static class BuiltinAllocatorProof {
    internal const string ResultFlag = "allocator-result=builtin-v1";
    private static readonly ConditionalWeakTable<Compilation, Cache> Caches = new();

    internal static bool Value(IOperation value, Compilation compilation) => Caches.GetValue(compilation, static c => new Cache(c)).Value(value);
    internal static string MethodFlag(IMethodSymbol method, Compilation compilation) =>
        Caches.GetValue(compilation, static c => new Cache(c)).Method(method) ? "," + ResultFlag : "";

    private sealed class Cache {
        private readonly Compilation compilation;
        private readonly INamedTypeSymbol? manager, handle, allocator;
        private readonly object gate = new();
        private readonly Dictionary<IMethodSymbol, HashSet<int>> methods = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<IAssemblySymbol, Dictionary<string, bool>> imported = new(SymbolEqualityComparer.Default);

        internal Cache(Compilation compilation) {
            this.compilation = compilation;
            this.manager = compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager");
            this.handle = compilation.GetTypeByMetadataName("Unity.Collections.AllocatorManager+AllocatorHandle");
            this.allocator = compilation.GetTypeByMetadataName("Unity.Collections.Allocator");
        }

        private static bool Same(ITypeSymbol? a, ITypeSymbol? b) => a != null && b != null && SymbolEqualityComparer.Default.Equals(a, b);
        internal bool Value(IOperation value) { lock (this.gate) return this.Value(value, new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default), 0); }
        internal bool Method(IMethodSymbol method) { lock (this.gate) return this.Method(method, new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default), 0); }

        private bool Value(IOperation value, HashSet<IMethodSymbol> path, int depth) {
            if (depth > 32 || this.manager?.ContainingAssembly.Name != "Unity.Collections" || this.handle == null || this.allocator?.TypeKind != TypeKind.Enum)
                return false;
            while (value is IParenthesizedOperation parentheses) value = parentheses.Operand;
            if (Same(value.Type, this.allocator) && value.ConstantValue.HasValue && value.ConstantValue.Value != null) {
                var number = Convert.ToInt64(value.ConstantValue.Value, CultureInfo.InvariantCulture);
                return number >= 2 && number < 64 && this.allocator.GetMembers().OfType<IFieldSymbol>().Any(field =>
                    field.HasConstantValue && field.Name is "Temp" or "TempJob" or "Persistent" or "AudioKernel" or "Domain" &&
                    Convert.ToInt64(field.ConstantValue, CultureInfo.InvariantCulture) == number);
            }
            if (value is IConversionOperation conversion && conversion.OperatorMethod is { } method &&
                method.DeclaringSyntaxReferences.Length == 0 && method.MethodKind == MethodKind.Conversion && method.Name == "op_Implicit" &&
                method.IsStatic && method.Arity == 0 && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                Same(method.ContainingType, this.handle) && Same(method.ReturnType, this.handle) && Same(method.Parameters[0].Type, this.allocator))
                return this.Value(conversion.Operand, path, depth + 1);
            if (value is IFieldReferenceOperation field && field.Field.DeclaringSyntaxReferences.Length == 0 &&
                field.Field.IsStatic && field.Field.IsReadOnly && Same(field.Field.ContainingType, this.manager) && Same(field.Field.Type, this.handle))
                return field.Field.Name is "Temp" or "TempJob" or "Persistent" or "AudioKernel";
            if (value is IPropertyReferenceOperation property && property.Property.GetMethod is { } getter)
                return this.Method(getter, path, depth + 1);
            if (value is IInvocationOperation invocation) return this.Method(invocation.TargetMethod, path, depth + 1);
            return false; // Locals, mutable storage, ref aliases, virtual targets and arbitrary casts are not proofs.
        }

        private bool Method(IMethodSymbol method, HashSet<IMethodSymbol> path, int depth) {
            method = method.OriginalDefinition;
            if (depth > 32 || !method.IsStatic || method.IsAbstract || method.IsVirtual || method.ReturnsByRef || method.ReturnsByRefReadonly ||
                method.MethodKind is not (MethodKind.Ordinary or MethodKind.PropertyGet) ||
                !Same(method.ReturnType, this.allocator) && !Same(method.ReturnType, this.handle)) return false;
            if (this.methods.TryGetValue(method, out var depths) && depths.Contains(depth)) return true;
            if (!path.Add(method)) return false;
            var result = false;
            // Include the remaining depth budget in cached proofs, otherwise visiting
            // a short suffix first can make a too-deep chain depend on traversal order.
            // A recursion/budget failure is not cached across different caller paths.
            if (method.DeclaringSyntaxReferences.Length == 0) result = this.Imported(method);
            else if (method.DeclaringSyntaxReferences.Length == 1) {
                var syntax = method.DeclaringSyntaxReferences[0].GetSyntax();
                var expression = syntax switch {
                    ArrowExpressionClauseSyntax arrow => arrow.Expression,
                    PropertyDeclarationSyntax property => property.ExpressionBody?.Expression,
                    AccessorDeclarationSyntax accessor => accessor.ExpressionBody?.Expression ?? Returned(accessor.Body),
                    MethodDeclarationSyntax declaration => declaration.ExpressionBody?.Expression ?? Returned(declaration.Body),
                    _ => null,
                };
                var operation = expression == null || !this.compilation.ContainsSyntaxTree(expression.SyntaxTree) ? null :
                    this.compilation.GetSemanticModel(expression.SyntaxTree).GetOperation(expression);
                result = operation != null && this.Value(operation, path, depth + 1);
            }
            path.Remove(method);
            if (result) {
                if (depths == null) this.methods.Add(method, depths = new HashSet<int>());
                depths.Add(depth);
            }
            return result;
        }

        private static ExpressionSyntax? Returned(BlockSyntax? body) => body?.Statements.Count == 1 && body.Statements[0] is ReturnStatementSyntax returned
            ? returned.Expression : null;

        private bool Imported(IMethodSymbol method) {
            var assembly = method.ContainingAssembly;
            if (!this.imported.TryGetValue(assembly, out var rows)) {
                rows = new Dictionary<string, bool>(StringComparer.Ordinal);
                foreach (var attribute in assembly.GetAttributes()) {
                    if (attribute.AttributeClass?.ToDisplayString() != "System.Reflection.AssemblyMetadataAttribute" ||
                        attribute.ConstructorArguments.Length != 2 || attribute.ConstructorArguments[0].Value as string != MethodSummaryGenerator.MetadataKey ||
                        attribute.ConstructorArguments[1].Value is not string payload) continue;
                    var lines = payload.Split('\n');
                    if (lines.Length == 0) continue;
                    var flags = lines.Length < 5 ? Array.Empty<string>() : lines[1].Split(',');
                    var results = flags.Where(flag => flag.StartsWith("allocator-result", StringComparison.Ordinal)).ToArray();
                    var proven = results.Length == 1 && results[0] == ResultFlag;
                    if (rows.ContainsKey(lines[0])) rows[lines[0]] = false; // Conflicting/duplicate summaries fail closed.
                    else rows.Add(lines[0], proven);
                }
                this.imported.Add(assembly, rows);
            }
            var id = MethodSummaryIdentity.Get(method);
            return id != null && rows.TryGetValue(id, out var result) && result;
        }
    }
}
