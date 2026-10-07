using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Format = ME.BECS.CodeGeneration.SourceGeneratorGraphFragmentFormat;
using Envelope = ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat;

namespace ME.BECS.SourceGenerator;

internal sealed class GraphPublicationPlan {
    private (string Prefix, int Id, int Capacity) graph;
    private readonly Dictionary<int, List<(INamedTypeSymbol Type, bool UseDefault, uint SourceId, int NodeIndex, string Identity)>> slots = new();
    private readonly Dictionary<int, GraphTopologyInput> topologies = new();
    private readonly Dictionary<(int Graph, string Phase), GraphLifecyclePlan> lifecycles = new();
    private readonly Dictionary<int, List<(GraphJobPatchPlan Plan, int Slot)>> injections = new();
    private readonly Dictionary<int, List<GraphJobPatchPlan>> patches = new();
    private readonly Dictionary<int, string[]> actions = new();
    private readonly List<GraphJobSelectionInput> selections = new();
    private readonly List<GraphJobPatchPlan> jobs = new();
    internal readonly List<(string Key, INamedTypeSymbol Type, IFieldSymbol[] Fields)> Deltas = new();

    internal static GraphPublicationPlan? Create(string entry, Compilation compilation, InputManifestTypes resolver, out string error) {
        error = "Invalid graph publication snapshot";
        if (!Format.ValidEntry(entry)) return null;
        var result = new GraphPublicationPlan();
        var rows = Format.Rows(entry).Select(row => row.Split('\t')).ToArray();
        var header = rows[0];
        var id = int.Parse(header[3], CultureInfo.InvariantCulture);
        result.graph = (Envelope.Decode(header[2]), id, int.Parse(header[4], CultureInfo.InvariantCulture));
        if (result.graph.Prefix.Split('.').Any(part => !SyntaxFacts.IsValidIdentifier(part))) return null;
        var graphSlots = new List<(INamedTypeSymbol Type, bool UseDefault, uint SourceId, int NodeIndex, string Identity)>();
        result.slots.Add(id, graphSlots);
        var contract = compilation.GetTypeByMetadataName("ME.BECS.ISystem");
        foreach (var row in rows.Skip(1)) {
            if (row[0] == "graph-topology") {
                if (!GraphTopologyInput.TryParse(Envelope.Decode(row[4]), id, out var topology, out error)) return null;
                result.topologies.Add(id, topology!);
                continue;
            }
            var identity = Envelope.Decode(row[2]);
            var type = resolver.ResolveDefinition(identity, out _);
            if (row[0] == "graph-system") {
                if (type == null || !type.IsUnmanagedType || type.IsRefLikeType || MethodSummaryType.From(type).IsOpen ||
                    !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) || contract == null ||
                    !type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)) {
                    error = "Unavailable graph system " + identity; return null;
                }
                graphSlots.Add((type, row[5] == "1", uint.Parse(row[6], CultureInfo.InvariantCulture), int.Parse(row[7], CultureInfo.InvariantCulture), identity));
            } else {
                var selection = GraphJobSelectionInput.Create(type, Envelope.Decode(row[4]), resolver);
                if (selection == null || !selection.Select(resolver, compilation, out error)) return null;
                result.selections.Add(selection);
            }
        }
        var slotTypes = graphSlots.Select(slot => slot.Type).ToArray();
        var owners = new List<(GraphJobPatchPlan Plan, int Slot)>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        for (var slot = 0; slot < graphSlots.Count; ++slot) {
            var value = graphSlots[slot];
            if (!seen.Add(value.Type)) continue;
            var plan = GraphJobPatchPlan.CreateSystem(compilation, value.Type, value.Identity, slotTypes, out error);
            if (plan == null) return null;
            owners.Add((plan, slot));
        }
        if (!result.selections.Select(selection => selection.Owner).SequenceEqual(owners.Select(owner => owner.Plan.Job), SymbolEqualityComparer.Default)) {
            error = "Graph job selections differ from the first-occurrence system order"; return null;
        }
        result.injections.Add(id, owners);
        var cached = new Dictionary<INamedTypeSymbol, GraphJobPatchPlan>(SymbolEqualityComparer.Default);
        var graphPatches = new List<GraphJobPatchPlan>();
        var apply = new List<string>();
        for (var index = 0; index < owners.Count; ++index) {
            apply.Add("s:" + owners[index].Plan.Key);
            foreach (var job in result.selections[index].Jobs) {
                if (!cached.TryGetValue(job.Type, out var plan)) {
                    plan = GraphJobPatchPlan.CreateJob(compilation, job.Type, job.Identity, slotTypes, out error);
                    if (plan == null) return null;
                    cached.Add(job.Type, plan);
                    result.jobs.Add(plan);
                    if (plan.HasSystem) graphPatches.Add(plan);
                    else if (plan.Fields.Count != 0) result.Deltas.Add((plan.Key, job.Type, plan.Fields.Select(field => field.Field).ToArray()));
                }
                if (plan.Identity != job.Identity) { error = "Conflicting graph job identity " + job.Identity; return null; }
                // Repeated registration at each scheduling owner is intentional.
                if (plan.Fields.Count != 0) apply.Add((plan.HasSystem ? "j:" : "d:") + plan.Key);
            }
        }
        result.patches.Add(id, graphPatches);
        result.actions.Add(id, apply.ToArray());
        if (!result.topologies[id].BindSlots(slotTypes, resolver, out error)) return null;
        var phases = new[] { "Awake", "Start", "Update", "Destroy", "DrawGizmos" };
        for (var index = 0; index < phases.Length; ++index) {
            if (!GraphLifecyclePlan.TryCreate(result.topologies[id], resolver, phases[index], index + 1, out var plan, out error)) return null;
            result.lifecycles.Add((id, phases[index]), plan!);
        }
        error = "";
        return result;
    }

    internal void AppendMetadata(StringBuilder source) {
        void Add(string key, string value) => source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(")
            .Append(SymbolDisplay.FormatLiteral(key.Replace("ME.BECS.Graph", "ME.BECS.PublishedGraph"), true)).Append(", ").Append(SymbolDisplay.FormatLiteral(value, true)).Append(")]\n");
        foreach (var owner in injections[graph.Id]) Add("ME.BECS.GraphSystemInjectionPlan.v1", owner.Plan.DescribeSystem(graph.Id, owner.Slot));
        foreach (var selection in selections) Add("ME.BECS.GraphJobSelection.v1", selection.Describe(graph.Id));
        foreach (var job in jobs) Add("ME.BECS.GraphJobInjectionPlan.v1", job.DescribeJob(graph.Id));
        Add("ME.BECS.GraphInjectionActions.v1", "v1\n" + graph.Id.ToString(CultureInfo.InvariantCulture) + "\n" + string.Join(",", actions[graph.Id]));
        foreach (var plan in lifecycles) {
            var prefix = graph.Id.ToString(CultureInfo.InvariantCulture) + "\n" + plan.Key.Phase + "\n";
            Add("ME.BECS.GraphSyncComparison.v1", prefix + plan.Value.SyncDifferences);
            Add("ME.BECS.GraphLifecyclePlan.v1", prefix + plan.Value.Serialize());
        }
    }

    internal void Append(StringBuilder source, Compilation compilation, InputManifestTypes resolver, int ordinal) =>
        GraphInputEmitter.Append(source, compilation, resolver, new[] { graph }, slots, topologies, lifecycles, patches, injections, actions,
            dispatcher: "GraphRegistration_" + ordinal.ToString(CultureInfo.InvariantCulture));
}
