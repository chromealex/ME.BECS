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
        source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapViewsSelection { public static void Publish() {\n");
        if (owners.Distributed) {
            var indices = trackers.Tracked.Select((type, ordinal) => (type, ordinal)).ToDictionary(pair => pair.type, pair => pair.ordinal, SymbolEqualityComparer.Default);
            source.Append("global::ME.BECS.Views.BootstrapViews.ExpectPlan(").Append(SymbolDisplay.FormatLiteral(owners.Plan, true)).Append(", ")
                .Append(trackers.Tracked.Count.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(views.Types.Count.ToString(CultureInfo.InvariantCulture))
                .Append(", ").Append(trackers.Capacity.ToString(CultureInfo.InvariantCulture)).Append(", new int[][] {\n");
            foreach (var entry in trackers.Groups)
                source.Append("new int[] { ").Append(string.Join(",", entry.Components.Select(type => indices[type].ToString(CultureInfo.InvariantCulture)))).Append(" },\n");
            source.Append("}, editor: ").Append(profile).Append(");\n");
        }
        source.Append("} public static void Validate() {\n");
        if (owners.Distributed) source.Append("global::ME.BECS.Views.BootstrapViews.RequireComplete(editor: ").Append(profile).Append(");\n");
        source.Append("} } }\n");
        if (!owners.Distributed) {
            // Upgrade old snapshots through the normal data exporter.
            trackers.Append(source); views.Append(source); return;
        }
        foreach (var pair in new[] { (Name: "ViewTrackerInputs", Method: "InitializeTrackers"), (Name: "ViewTypeInputs", Method: "RegisterInstalledTypes") })
            source.Append("namespace ME.BECS.SourceGenerated { internal static class ").Append(pair.Name)
                .Append(" { [global::UnityEngine.Scripting.PreserveAttribute] public static void Initialize() => global::ME.BECS.Views.BootstrapViews.")
                .Append(pair.Method).Append("(editor: ").Append(profile).Append("); } }\n");
    }
}
