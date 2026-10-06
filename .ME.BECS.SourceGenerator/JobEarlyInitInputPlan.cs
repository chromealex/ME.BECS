using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Resolves the exact ordered EarlyInit calls for one publication owner.
internal sealed class JobEarlyInitInputPlan {
    internal bool HasSchema { get; private set; }
    private readonly List<(string Identity, INamedTypeSymbol Job, string Call, string Payload)> slots = new();
    internal IReadOnlyList<(string Identity, INamedTypeSymbol Job, string Call, string Payload)> Slots => this.slots;

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation) {
        if (fields[0] == "job-early-init-schema") {
            if (HasSchema || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            HasSchema = true;
            return true;
        }
        if (!HasSchema || fields.Length != 3 || fields[1] != slots.Count.ToString(CultureInfo.InvariantCulture)) return false;
        string[] rows;
        try { rows = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])).Split('\n'); }
        catch (FormatException) { return false; }
        if (rows.Length < 4 || rows[0] != "v1") return false;
        var job = resolver.ResolveDefinition(rows[1], out _);
        if (job == null || !job.IsUnmanagedType || IsOpen(job) || !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly)) return false;
        var call = "";
        if (rows[2].Length == 0) {
            if (rows.Length != 4 || rows[3].Length != 0) return false;
        } else {
            var arguments = new ITypeSymbol[rows.Length - 4];
            for (var index = 0; index < arguments.Length; ++index) {
                var argument = resolver.ResolveDefinition(rows[index + 4], out _);
                if (argument == null || IsOpen(argument) || !compilation.IsSymbolAccessibleWithin(argument, compilation.Assembly)) return false;
                arguments[index] = argument;
            }
            var localName = "ME.BECS.SourceGenerated.JobEarlyInit_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Encode(compilation.AssemblyName!);
            if (SymbolEqualityComparer.Default.Equals(job.ContainingAssembly, compilation.Assembly) && rows[2] == localName + ", " + compilation.Assembly.Identity) {
                // Other generators' output is not part of this Compilation. Use
                // their exact semantic selection, never an unchecked method name.
                var owners = new Stack<INamedTypeSymbol>();
                for (var current = job; current != null; current = current.ContainingType) owners.Push(current);
                if (!arguments.SequenceEqual(owners.SelectMany(type => type.TypeArguments), SymbolEqualityComparer.Default) ||
                    !JobEarlyInitGenerator.EmitsWrapper(job, compilation.GetTypeByMetadataName("ME.BECS.Jobs.EarlyInit"), rows[3])) return false;
                call = "global::" + localName + "." + rows[3];
            } else {
                var owner = resolver.ResolveDefinition(rows[2], out _);
                if (owner == null || IsOpen(owner) || !compilation.IsSymbolAccessibleWithin(owner, compilation.Assembly)) return false;
                var methods = owner.GetMembers(rows[3]).OfType<IMethodSymbol>().ToArray();
                if (methods.Length != 1) return false;
                var selected = methods[0];
                if (!selected.IsStatic || !selected.ReturnsVoid || selected.Parameters.Length != 0 || selected.Arity != arguments.Length ||
                    !compilation.IsSymbolAccessibleWithin(selected, compilation.Assembly)) return false;
                call = owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + selected.Name;
            }
            if (arguments.Length != 0) call += "<" + string.Join(", ", arguments.Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) + ">";
            call += "();";
        }
        slots.Add((rows[1], job, call, string.Join("\n", rows)));
        return true;
    }

    private static bool IsOpen(INamedTypeSymbol type) => type.IsUnboundGenericType ||
        type.TypeArguments.Any(argument => argument is ITypeParameterSymbol || argument is INamedTypeSymbol named && IsOpen(named)) ||
        type.ContainingType != null && IsOpen(type.ContainingType);
}
