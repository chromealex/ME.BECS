using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

// Selected callbacks already belong to their declaring assemblies. Publication
// is still in the selecting consumer until assembly-scoped fragment inputs replace
// that consumer. The runtime merges these fragments by ordinal, not publisher order.
internal sealed class BootstrapTypePlanEmitter {
    private readonly List<(string Owner, string Target)> steps = new();

    internal void Add(string owner, string target) => this.steps.Add((owner, target));

    internal void Append(StringBuilder source, bool editor, string kind = "Type") {
        var selection = kind == "Type" ? "BootstrapTypeInputs" : "Bootstrap" + kind + "Selection";
        var identity = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(string.Join("\n", this.steps.Select(
            (step, ordinal) => ordinal.ToString(CultureInfo.InvariantCulture) + "\t" + step.Owner + "\t" + step.Target)));
        source.Append("namespace ME.BECS.SourceGenerated { internal static class ").Append(selection).Append(" {\n");
        var fragments = this.steps.Select((step, ordinal) => (step.Owner, step.Target, Ordinal: ordinal))
            .GroupBy(step => step.Owner, System.StringComparer.Ordinal).OrderBy(group => group.Key, System.StringComparer.Ordinal).ToArray();
        var calls = new List<string>();
        foreach (var fragment in fragments) {
            var key = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(fragment.Key);
            source.Append("private static readonly int[] Ordinals_").Append(key).Append(" = new int[] { ")
                .Append(string.Join(",", fragment.Select(step => step.Ordinal.ToString(CultureInfo.InvariantCulture)))).Append(" };\n");
            source.Append("private static readonly global::System.Action[] Callbacks_").Append(key).Append(" = new global::System.Action[] {\n")
                .Append(string.Join(",\n", fragment.Select(step => step.Target))).Append("\n};\n");
            calls.Add("global::ME.BECS.BootstrapRuntime.Install" + kind + "Fragment(" + SymbolDisplay.FormatLiteral(identity, true) + ", " +
                SymbolDisplay.FormatLiteral(fragment.Key, true) + ", " + this.steps.Count.ToString(CultureInfo.InvariantCulture) +
                ", Ordinals_" + key + ", Callbacks_" + key + ", editor: " + (editor ? "true" : "false") + ");");
        }
        if (this.steps.Count == 0)
            calls.Add("global::ME.BECS.BootstrapRuntime.Expect" + kind + "Plan(" + SymbolDisplay.FormatLiteral(identity, true) + ", 0, editor: " + (editor ? "true" : "false") + ");");
        source.Append("public static void Publish() {\n").Append(string.Join("\n", calls)).Append("\n} } }\n");
    }
}
