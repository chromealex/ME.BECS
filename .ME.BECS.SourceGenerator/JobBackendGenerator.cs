using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ME.BECS.SourceGenerator;

// Keep the public contracts in source: other generators must be able to bind
// IJob*/Schedule/EarlyInit symbols in this same compilation. Only implementations
// are generated. Runtime execution is unchanged from the embedded job template.
[Generator(LanguageNames.CSharp)]
public sealed class JobBackendGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        RegisterFamily(context, "Components", "JobComponentsExtensions");
        RegisterFamily(context, "Aspect", "JobAspectExtensions");
        RegisterFamily(context, "ParallelFor.Components", "JobParallelForComponentsExtensions");
        RegisterFamily(context, "ParallelFor.Aspect", "JobParallelForAspectExtensions");
        RegisterFamily(context, "AspectsComponents", "JobAspectsComponentsExtensions", mixed: true);
    }

    private static void RegisterFamily(IncrementalGeneratorInitializationContext context, string templateName, string ownerName, bool mixed = false) {
        var variantsPerCount = mixed ? 4 : 1;
        var variantCount = 9 * variantsPerCount;
        var ownedVariants = context.CompilationProvider.Select((compilation, cancellation) => {
            ulong mask = 0;
            for (var index = 0; index < variantCount; ++index) {
                cancellation.ThrowIfCancellationRequested();
                var count = index / variantsPerCount + 1;
                var components = mixed ? index % variantsPerCount + 1 : 0;
                var owner = compilation.GetTypeByMetadataName("ME.BECS.Jobs." + ownerName + (mixed ? Suffix(count, components) : ""));
                if (owner != null && SymbolEqualityComparer.Default.Equals(owner.ContainingAssembly, compilation.Assembly) &&
                    owner.GetMembers("JobEarlyInitialize").OfType<IMethodSymbol>().Any(method =>
                        method.Arity == count + components + 1 && method.IsPartialDefinition)) mask |= 1UL << index;
            }
            return mask;
        });
        // The body text depends only on ownership and parse defines, not on job
        // discovery or each script edit. Unchanged inputs reuse incremental output.
        context.RegisterSourceOutput(ownedVariants.Combine(context.ParseOptionsProvider), (output, input) => {
            if (input.Left == 0) return; // Includes downstream assemblies and old BECS versions.
            var resource = "ME.BECS.JobTemplates." + templateName;
            using var stream = typeof(JobBackendGenerator).Assembly.GetManifestResourceStream(resource) ??
                throw new InvalidOperationException("Missing embedded BECS job template: " + resource);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var template = reader.ReadToEnd();
            for (var index = 0; index < variantCount; ++index) {
                if ((input.Left & (1UL << index)) == 0) continue;
                output.CancellationToken.ThrowIfCancellationRequested();
                var count = index / variantsPerCount + 1;
                var components = mixed ? index % variantsPerCount + 1 : 0;
                var suffix = Suffix(count, components);
                var text = Expand(template, count, components);
                var tree = CSharpSyntaxTree.ParseText(text, (CSharpParseOptions)input.Right, cancellationToken: output.CancellationToken);
                if (tree.GetDiagnostics(output.CancellationToken).Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                    throw new InvalidOperationException("Invalid embedded job template: " + templateName + suffix);
                var root = (CompilationUnitSyntax)tree.GetRoot(output.CancellationToken);
                // Emit the selected variant. Removing disabled text and directives
                // avoids moving an #if boundary when declarations are split apart.
                // Keep warning pragmas, notably for the deprecated parallel APIs.
                root = root.ReplaceTrivia(root.DescendantTrivia(descendIntoTrivia: true)
                    .Where(static trivia => trivia.IsKind(SyntaxKind.IfDirectiveTrivia) || trivia.IsKind(SyntaxKind.ElifDirectiveTrivia) ||
                        trivia.IsKind(SyntaxKind.ElseDirectiveTrivia) || trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia) ||
                        trivia.IsKind(SyntaxKind.DisabledTextTrivia)), static (_, _) => default);
                var ns = root.Members.OfType<NamespaceDeclarationSyntax>().Single();
                var implementations = ns.Members.OfType<ClassDeclarationSyntax>().Select(static owner =>
                    owner.WithMembers(SyntaxFactory.List(owner.Members.Select(static member => member is MethodDeclarationSyntax method
                        ? Implement(method) : member))));
                root = root.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                    ns.WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(implementations))));
                output.AddSource("ME.BECS.Jobs." + templateName + suffix + ".g.cs",
                    SourceText.From("// <auto-generated/>\n" + root.NormalizeWhitespace(eol: "\n").ToFullString(), Encoding.UTF8));
            }
        });
    }

    private static MethodDeclarationSyntax Implement(MethodDeclarationSyntax method) => method
        .WithModifiers(method.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
        .WithAttributeLists(default) // Attributes belong to the original public declaration.
        .WithParameterList(method.ParameterList.WithParameters(SyntaxFactory.SeparatedList(
            method.ParameterList.Parameters.Select(static parameter => parameter.WithDefault(null)))));

    private static string Suffix(int count, int components) => count.ToString(CultureInfo.InvariantCulture) +
        (components == 0 ? "" : "_" + components.ToString(CultureInfo.InvariantCulture));

    private static string Expand(string template, int count, int components) {
        var text = template.Replace("{{inref}}", "ref").Replace("{{GetRead}}", "Get").Replace("{{RWRO}}", "RW")
            .Replace("{{PREFIX}}", Suffix(count, components))
            .Replace("{{countAspects}}", count.ToString(CultureInfo.InvariantCulture))
            .Replace("{{countComponents}}", components.ToString(CultureInfo.InvariantCulture));
        text = ExpandRepeats(text, "count", count + components);
        text = ExpandRepeats(text, "countAspects", count);
        return ExpandRepeats(text, "countComponents", components);
    }

    private static string ExpandRepeats(string text, string counter, int count) {
        text = Regex.Replace(text, "{" + counter + @"\[(.+?)\]}", match => string.Concat(Enumerable.Range(0, count)
            .Select(index => match.Groups[1].Value.Replace("#i#", index.ToString(CultureInfo.InvariantCulture)))), RegexOptions.CultureInvariant);
        return Regex.Replace(text, "{" + counter + @"\((.+?)\)\[(.+?)\]}", match => string.Join(match.Groups[1].Value, Enumerable.Range(0, count)
            .Select(index => match.Groups[2].Value.Replace("#i#", index.ToString(CultureInfo.InvariantCulture)))), RegexOptions.CultureInvariant);
    }
}
