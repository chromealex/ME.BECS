using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ME.BECS.SourceGenerator;

// Manifest-owned bootstrap needs no Editor C# marker classes. Legacy hooks are
// supported during regeneration, but manifest validation and entry points are atomic.
[Generator(LanguageNames.CSharp)]
public sealed class BootstrapGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var manifests = context.AdditionalTextsProvider.Where(static file => ME.BECS.CodeGeneration.SourceGeneratorInputFiles.IsInput(file.Path))
            .Combine(context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? ""))
            .Where(static input => ME.BECS.CodeGeneration.SourceGeneratorInputFiles.TargetsCompilation(input.Left.Path, input.Right))
            .Select(static (input, cancellation) => input.Left.GetText(cancellation)?.ToString()).Collect();
        context.RegisterSourceOutput(context.CompilationProvider.Combine(manifests), static (output, input) => {
            var compilation = input.Left;
            // Modern entry points must be emitted in the same successful output as
            // their manifest-owned hooks. Generators cannot inspect each other's output.
            if (RequiresInputs(compilation) || input.Right.Any(text => TargetsAssembly(text, compilation.AssemblyName))) return;
            Emit(output, compilation);
        });
    }

    private static bool TargetsAssembly(string? text, string? assembly) {
        if (text == null) return false;
        var end = text.IndexOf('\n');
        var header = (end < 0 ? text : text.Substring(0, end)).TrimEnd('\r').Split('\t');
        if (header.Length < 2) return false;
        try { return Encoding.UTF8.GetString(System.Convert.FromBase64String(header[1])) == assembly; }
        catch (System.FormatException) { return false; }
    }

    internal static bool RequiresInputs(Compilation compilation) {
        var ns = compilation.AssemblyName == "ME.BECS.Gen.Editor" ? "ME.BECS.Editor" :
            compilation.AssemblyName == "ME.BECS.Gen.Runtime" ? "ME.BECS" : null;
        if (ns == null) return false;
        if (HasHook(compilation, ns + ".StaticTypesInitializer", "SourceBootstrapPlanV1") ||
            HasHook(compilation, ns + ".StaticMethods", "SourceRegistrationPlanV1")) return true;
        var initializer = compilation.Assembly.GetTypeByMetadataName(ns + ".StaticTypesInitializer");
        var registry = compilation.Assembly.GetTypeByMetadataName(ns + ".StaticMethods");
        if (initializer?.GetMembers("Load").Length > 0 && registry?.GetMembers("Load").Length > 0) return false;
        return !((HasHook(compilation, ns + ".StaticTypesInitializer", "RegisterGeneratedTypes") ||
            HasHook(compilation, ns + ".StaticTypesInitializer", "RegisterAdditionalTypes")) &&
            HasHook(compilation, ns + ".StaticMethods", "RegisterGeneratedMethods"));
    }

    internal static bool ValidateCompilerOwners(Compilation compilation, bool debug, out string error) {
        var ns = compilation.AssemblyName == "ME.BECS.Gen.Editor" ? "ME.BECS.Editor" : "ME.BECS";
        foreach (var name in new[] { "StaticTypesInitializer", "StaticMethods", "DebugJobs" }) {
            if (name == "DebugJobs" && !debug) continue;
            var type = compilation.Assembly.GetTypeByMetadataName(ns + "." + name);
            if (type == null) continue;
            if (type.TypeKind != TypeKind.Class || (name == "DebugJobs" ? type.IsStatic : !type.IsStatic) || type.DeclaredAccessibility != Accessibility.Public ||
                type.DeclaringSyntaxReferences.Length == 0 || type.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax() is not ClassDeclarationSyntax declaration || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))) {
                error = "Compiler-owned bootstrap type must be absent or a public partial class: " + ns + "." + name + ". Regenerate bootstrap.";
                return false;
            }
            var forbidden = name == "StaticTypesInitializer" ? new[] { "Load", "RegisterGeneratedTypes", "RegisterAdditionalTypes", "RegisterTypePlan" } :
                name == "StaticMethods" ? new[] { "Load", "RegisterGeneratedMethods", "ValidateGeneratedInputs", "InitializeSystemDependenciesInfo" } : new[] { "InitializeJobsDebug" };
            if (forbidden.Any(member => type.GetMembers(member).Length != 0)) {
                error = "Legacy bodies conflict with manifest-owned bootstrap: " + ns + "." + name + ". Regenerate bootstrap and inputs together.";
                return false;
            }
        }
        if (compilation.Assembly.GetTypeByMetadataName(ns + ".AOTBurstHelper") != null) {
            error = "Legacy AOTBurstHelper conflicts with manifest-owned bootstrap. Regenerate bootstrap.";
            return false;
        }
        error = "";
        return true;
    }

    internal static bool HasExportedHooks(Compilation compilation) {
        var ns = compilation.AssemblyName == "ME.BECS.Gen.Editor" ? "ME.BECS.Editor" : "ME.BECS";
        return (HasHook(compilation, ns + ".StaticTypesInitializer", "SourceBootstrapPlanV1") ||
            HasHook(compilation, ns + ".StaticTypesInitializer", "RegisterAdditionalTypes") ||
            HasHook(compilation, ns + ".StaticTypesInitializer", "RegisterGeneratedTypes")) &&
            (HasHook(compilation, ns + ".StaticMethods", "SourceRegistrationPlanV1") ||
            HasHook(compilation, ns + ".StaticMethods", "RegisterGeneratedMethods"));
    }

    internal static void Emit(SourceProductionContext output, Compilation compilation, bool compilerOwned = false) {
            var editor = compilation.AssemblyName == "ME.BECS.Gen.Editor";
            if (!editor && compilation.AssemblyName != "ME.BECS.Gen.Runtime") return;
            var ns = editor ? "ME.BECS.Editor" : "ME.BECS";
            var sourceCoreTypes = compilerOwned || HasHook(compilation, ns + ".StaticTypesInitializer", "SourceBootstrapPlanV1") ||
                HasHook(compilation, ns + ".StaticTypesInitializer", "RegisterAdditionalTypes");
            if (!compilerOwned && !HasExportedHooks(compilation)) return;
            var source = new StringBuilder("// <auto-generated/>\nnamespace " + ns + " {\n");
            // Old exported templates may still contain the AOT root until regeneration.
            // Do not shadow/duplicate that type. New templates delegate the root entirely here.
            if (compilation.Assembly.GetTypeByMetadataName(ns + ".AOTBurstHelper") == null) {
                source.AppendLine("[global::UnityEngine.Scripting.PreserveAttribute]");
                source.AppendLine("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]");
                source.AppendLine("public static unsafe class AOTBurstHelper {");
                source.AppendLine("[global::UnityEngine.Scripting.PreserveAttribute] public static void AOT() {");
                source.AppendLine("global::ME.BECS.SourceGenerated.SystemAotInputs.PreserveReferences();");
                source.AppendLine("global::ME.BECS.SourceGenerated.CoreTypeInputs.AotComponents();");
                source.AppendLine("} }");
            }
            source.AppendLine("public static unsafe partial class StaticTypesInitializer {");
            source.AppendLine("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]");
            source.AppendLine("[global::UnityEngine.Scripting.PreserveAttribute] public static void Load() {");
            if (sourceCoreTypes) {
                source.AppendLine("global::ME.BECS.BootstrapRuntime.InitializeTypes(RegisterTypePlan);");
                source.AppendLine("}");
                source.AppendLine("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]");
                source.AppendLine("private static void RegisterTypePlan() {");
                source.AppendLine("global::ME.BECS.SourceGenerated.CoreTypeInputs.Initialize();");
                source.AppendLine("RegisterAdditionalTypes();");
            } else {
                // Old exported hooks already include core registration; never register twice.
                source.AppendLine("global::ME.BECS.BootstrapRuntime.InitializeTypes(RegisterGeneratedTypes);");
            }
            source.AppendLine("} }");
            if (compilerOwned) {
                var registry = compilation.Assembly.GetTypeByMetadataName(ns + ".StaticMethods");
                foreach (var attribute in new[] {
                    ("UnityEngine.Scripting.PreserveAttribute", "global::UnityEngine.Scripting.PreserveAttribute"),
                    ("UnityEngine.DefaultExecutionOrder", "global::UnityEngine.DefaultExecutionOrder(-100_000)"),
                    ("Unity.Burst.BurstCompileAttribute", "global::Unity.Burst.BurstCompileAttribute"),
                }) {
                    if (registry?.GetAttributes().Any(item => item.AttributeClass?.ToDisplayString() == attribute.Item1) != true)
                        source.Append('[').Append(attribute.Item2).AppendLine("]");
                }
            }
            source.AppendLine("public static unsafe partial class StaticMethods {");
            source.AppendLine("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]");
            source.AppendLine("[global::UnityEngine.Scripting.PreserveAttribute]");
            source.AppendLine(editor ? "[global::UnityEditor.InitializeOnLoadMethodAttribute]" :
                "[global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]");
            source.AppendLine("private static void PublishBootstrapPlan() {");
            if (compilerOwned) {
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapSystemSelection.Publish();");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapTypeInputs.Publish();");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapEntitySelection.Publish();");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapAspectSelection.Publish();");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapDestroySelection.Publish();");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapConfigSelection.Publish();");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapNetworkSelection.Publish();");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapViewsSelection.Publish();");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapJobInitSelection.Publish();");
            source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapJobSetupSelection.Publish();");
            source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapJobDebugSelection.Publish();");
                if (!editor) source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapGraphSelection.Publish();");
            }
            source.Append(compilerOwned ? "global::ME.BECS.BootstrapRuntime.InstallPlanWithPreflight(" : "global::ME.BECS.BootstrapRuntime.InstallPlan(")
                .Append(SymbolDisplay.FormatLiteral(compilation.AssemblyName!, true))
                .Append(", StaticTypesInitializer.Load, RegisterGeneratedMethods, ")
                .Append(compilerOwned ? "ValidateGeneratedInputs, " : "")
                .AppendLine("editor: " + (editor ? "true" : "false") + ");");
            source.AppendLine("}");
            if (compilerOwned) {
                source.AppendLine("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute] private static void ValidateGeneratedInputs() {");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapNetworkSelection.Validate();");
                source.AppendLine("global::ME.BECS.SourceGenerated.BootstrapViewsSelection.Validate();");
                source.AppendLine("}");
            }
            // Keep the callable compatibility entry point during migration, but
            // automatic startup is now owned exclusively by the framework.
            source.AppendLine("[global::UnityEngine.Scripting.PreserveAttribute]");
            source.AppendLine("public static void Load() {");
            source.AppendLine("PublishBootstrapPlan();");
            source.AppendLine("global::ME.BECS.BootstrapRuntime.LoadInstalled(editor: " + (editor ? "true" : "false") + ");");
            source.AppendLine("} } }");
            output.AddSource("ME.BECS.Bootstrap.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static bool HasHook(Compilation compilation, string name, string hook) {
        var type = compilation.Assembly.GetTypeByMetadataName(name);
        return type != null && type.IsStatic && type.DeclaringSyntaxReferences.Length > 0 &&
            type.DeclaringSyntaxReferences.All(reference => reference.GetSyntax() is ClassDeclarationSyntax declaration &&
                declaration.Modifiers.Any(token => token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))) &&
            !type.GetMembers("Load").Any() &&
            type.GetMembers(hook).OfType<IMethodSymbol>().Any(method => method.IsStatic && method.ReturnsVoid &&
                method.Parameters.Length == 0 && method.Arity == 0);
    }
}
