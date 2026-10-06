using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

internal static class ViewsInputEmitter {
    internal static void Append(StringBuilder source, ViewsRegistrationOwners owners, ViewTrackerInputEmitter trackers,
                                ViewTypeInputEmitter views, bool editor) {
        var profile = editor ? "true" : "false";
        source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapViewsSelection {\n");
        // The publication generator validates the distributed owner plan before emission.
            var indices = trackers.Tracked.Select((type, ordinal) => (type, ordinal)).ToDictionary(pair => pair.type, pair => pair.ordinal, SymbolEqualityComparer.Default);
            source.Append("private static readonly int[][] Dependencies = new int[][] {\n");
            foreach (var entry in trackers.Groups)
                source.Append("new int[] { ").Append(string.Join(",", entry.Components.Select(type => indices[type].ToString(CultureInfo.InvariantCulture)))).Append(" },\n");
            source.Append("};\npublic static void Publish() {\n");
            source.Append("global::ME.BECS.Views.BootstrapViews.ExpectPlan(").Append(SymbolDisplay.FormatLiteral(owners.Plan, true)).Append(", ")
                .Append(trackers.Tracked.Count.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(views.Types.Count.ToString(CultureInfo.InvariantCulture))
                .Append(", ").Append(trackers.Capacity.ToString(CultureInfo.InvariantCulture)).Append(", Dependencies, editor: ").Append(profile).Append(");\n");
        source.Append("} public static void Validate() {\n");
        source.Append("global::ME.BECS.Views.BootstrapViews.RequireComplete(editor: ").Append(profile).Append(");\n");
        source.Append("} } }\n");
        AppendFacades(source, editor);
    }

    internal static void AppendFacades(StringBuilder source, bool editor) {
        foreach (var pair in new[] { (Name: "ViewTrackerInputs", Method: "InitializeTrackers"), (Name: "ViewTypeInputs", Method: "RegisterInstalledTypes") })
            source.Append("namespace ME.BECS.SourceGenerated { internal static class ").Append(pair.Name)
                .Append(" { [global::UnityEngine.Scripting.PreserveAttribute] public static void Initialize() => global::ME.BECS.Views.BootstrapViews.")
                .Append(pair.Method).Append("(editor: ").Append(editor ? "true" : "false").Append("); } }\n");
    }
}
