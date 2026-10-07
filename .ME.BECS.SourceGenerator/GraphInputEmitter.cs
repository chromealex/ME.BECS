using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

// Storage, lifecycle and injection must be emitted together. The old aggregate
// snapshot and owner publications intentionally share this implementation.
internal static class GraphInputEmitter {
    internal static void Append(StringBuilder source, Compilation compilation, InputManifestTypes resolver,
        IReadOnlyList<(string Prefix, int Id, int Capacity)> graphRegistrations,
        Dictionary<int, List<(INamedTypeSymbol Type, bool UseDefault, uint SourceId, int NodeIndex, string Identity)>> graphSlots,
        Dictionary<int, GraphTopologyInput> graphTopologies, Dictionary<(int Graph, string Phase), GraphLifecyclePlan> lifecyclePlans,
        Dictionary<int, List<GraphJobPatchPlan>> graphJobs, Dictionary<int, List<(GraphJobPatchPlan Plan, int Slot)>> graphInjections,
        Dictionary<int, string[]> graphApply, string dispatcher = "GraphInputs", bool installFirstPass = false) {
                source.Append("namespace ME.BECS.SourceGenerated { internal static unsafe class ").Append(dispatcher).Append(" {\n");
                if (installFirstPass) source
                    .Append("[global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]\n")
                    .Append("private static void Initialize() => global::ME.BECS.CustomModules.RegisterFirstPass(Register);\n");
                source.Append("[global::UnityEngine.Scripting.Preserve]\npublic static void Register() {\n");
                foreach (var graph in graphRegistrations) {
                    var suffix = Math.Abs(graph.Id).ToString(CultureInfo.InvariantCulture) + "_SystemsCodeGenerator";
                    var id = graph.Id.ToString(CultureInfo.InvariantCulture);
                    foreach (var phase in new[] { (Register: "RegisterMethod", Class: "Initialize", Method: "GraphInitialize"),
                                 (Register: "RegisterAwakeMethod", Class: "Awake", Method: "GraphOnAwake"),
                                 (Register: "RegisterStartMethod", Class: "Start", Method: "GraphOnStart"),
                                 (Register: "RegisterUpdateMethod", Class: "Update", Method: "GraphOnUpdate"),
                                 (Register: "RegisterDrawGizmosMethod", Class: "DrawGizmos", Method: "GraphOnDrawGizmos"),
                                 (Register: "RegisterDestroyMethod", Class: "Destroy", Method: "GraphOnDestroy"),
                                 (Register: "RegisterGetSystemMethod", Class: "Initialize", Method: "GraphGetSystem") })
                        source.Append("global::ME.BECS.SystemsStatic.").Append(phase.Register).Append("(global::")
                            .Append(graph.Prefix).Append(phase.Class).Append('.').Append(phase.Method).Append('_').Append(suffix)
                            .Append(", ").Append(id).Append(", false);\n");
                }
                source.Append("} } }\n");
                foreach (var graph in graphRegistrations) {
                    var split = graph.Prefix.LastIndexOf('.');
                    var className = graph.Prefix.Substring(split + 1) + "Initialize";
                    var suffix = Math.Abs(graph.Id).ToString(CultureInfo.InvariantCulture) + "_SystemsCodeGenerator";
                    var storage = "graphNodes" + suffix;
                    foreach (var phase in new[] { "Awake", "Start", "Update", "Destroy", "DrawGizmos" }) {
                        source.Append("namespace ").Append(graph.Prefix.Substring(0, split)).Append(" { public static unsafe partial class ")
                            .Append(graph.Prefix.Substring(split + 1)).Append(phase).Append(" {\n")
                            .Append("[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.SystemsStatic.On").Append(phase).Append("))]\n")
                            .Append("public static void GraphOn").Append(phase).Append('_').Append(suffix)
                            .Append("(uint dt, ref global::ME.BECS.World world, ref global::Unity.Jobs.JobHandle dependsOn) => PlannedLifecycle.Execute(dt, ref world, ref dependsOn);\n");
                        var contract = compilation.GetTypeByMetadataName("ME.BECS.I" + phase);
                        var phaseSlots = graphSlots[graph.Id];
                        for (var slot = 0; slot < phaseSlots.Count; ++slot) {
                            var system = phaseSlots[slot].Type;
                            if (contract == null || !system.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract))) continue;
                            var systemName = system.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            source.Append("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\n")
                                .Append("private static void InvokeSystem_").Append(slot.ToString(CultureInfo.InvariantCulture))
                                .Append("(global::System.IntPtr pointer, ref global::ME.BECS.SystemContext context) => global::ME.BECS.SourceGeneratorSystemCalls.")
                                .Append(phase).Append('<').Append(systemName).Append(">(ref *((").Append(systemName).Append("*)pointer), ref context);\n");
                        }
                        if (graphTopologies.TryGetValue(graph.Id, out var topology)) {
                            var groups = new HashSet<(int Start, int Count, bool Parallel)>();
                            foreach (var node in topology.Occurrences.SelectMany(static occurrence => occurrence.Nodes)) {
                                if (node.SystemType.Length == 0) continue;
                                var definition = resolver.ResolveDefinition(node.SystemType, out _);
                                if (definition == null || !definition.IsGenericType || contract == null ||
                                    !definition.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract)) ||
                                    !groups.Add((node.SlotStart, node.SlotCount, node.Parallel))) continue;
                                source.Append("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\n")
                                    .Append("private static global::Unity.Jobs.JobHandle Invoke").Append(node.Parallel ? "Parallel_" : "Sequential_").Append(node.SlotStart.ToString(CultureInfo.InvariantCulture))
                                    .Append('_').Append(node.SlotCount.ToString(CultureInfo.InvariantCulture))
                                    .Append("(uint dt, in global::ME.BECS.World world, global::Unity.Jobs.JobHandle dependsOn, global::System.IntPtr* systems")
                                    .Append(node.Parallel ? ") {\n" : ", bool apply) {\n");
                                if (node.Parallel && node.SlotCount > 0)
                                    source.Append("var results = new global::Unity.Collections.NativeArray<global::Unity.Jobs.JobHandle>(")
                                        .Append(node.SlotCount.ToString(CultureInfo.InvariantCulture)).Append(", global::ME.BECS.Constants.ALLOCATOR_TEMP);\n");
                                for (var slot = node.SlotStart; slot < node.SlotStart + node.SlotCount; ++slot) {
                                    source.Append("{ var context = global::ME.BECS.SystemContext.Create(dt, in world, dependsOn);\nInvokeSystem_")
                                        .Append(slot.ToString(CultureInfo.InvariantCulture)).Append("(systems[").Append(slot.ToString(CultureInfo.InvariantCulture))
                                        .Append("], ref context);\n");
                                    if (node.Parallel) source.Append("results[").Append((slot - node.SlotStart).ToString(CultureInfo.InvariantCulture)).Append("] = context.dependsOn; }\n");
                                    else source.Append("dependsOn = apply ? global::ME.BECS.Batches.Apply(context.dependsOn, in world) : context.dependsOn; }\n");
                                }
                                if (node.Parallel && node.SlotCount > 0)
                                    source.Append("dependsOn = global::Unity.Jobs.JobHandle.CombineDependencies(results);\n");
                                source.Append("return dependsOn;\n}\n");
                            }
                        }
                        if (lifecyclePlans.TryGetValue((graph.Id, phase), out var lifecyclePlan))
                            GraphLifecycleEmitter.Append(source, lifecyclePlan, "global::" + graph.Prefix + "Initialize." + storage);
                        source.Append("} }\n");
                    }
                    source.Append("namespace ").Append(graph.Prefix.Substring(0, split)).Append(" { using Unity.Collections;\n");
                    // The legacy phase stub used to own this attribute. Emit it here,
                    // without duplicating it while an older stub is still compiled.
                    var graphOwner = compilation.Assembly.GetTypeByMetadataName(graph.Prefix + "Initialize");
                    if (graphOwner?.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Unity.Burst.BurstCompileAttribute") != true)
                        source.Append("[global::Unity.Burst.BurstCompile]\n");
                    source.Append("public unsafe partial class ")
                        .Append(className).Append(" {\nprivate static readonly global::Unity.Burst.SharedStatic<global::Unity.Collections.NativeArray<global::System.IntPtr>> ")
                        .Append(storage).Append("Data = global::Unity.Burst.SharedStatic<global::Unity.Collections.NativeArray<global::System.IntPtr>>.GetOrCreate<")
                        .Append(className).Append(">();\npublic static ref global::Unity.Collections.NativeArray<global::System.IntPtr> ")
                        .Append(storage).Append(" => ref ").Append(storage).Append("Data.Data;\n")
                        .Append("private static void ResetStorage(global::Unity.Collections.AllocatorManager.AllocatorHandle allocator) {\nif (")
                        .Append(storage).Append(".IsCreated) global::ME.BECS.Utils.DisposePtrArray(").Append(storage).Append(", allocator);\n")
                        .Append(storage).Append(" = global::Unity.Collections.CollectionHelper.CreateNativeArray<global::System.IntPtr>(")
                        .Append(graph.Capacity.ToString(CultureInfo.InvariantCulture)).Append(", allocator);\n}\n")
                        .Append("[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.SystemsStatic.InitializeGraph))]\npublic static void GraphInitialize_")
                        .Append(suffix).Append("() {\nValidateInjectionPlan();\nvar allocator = (global::Unity.Collections.AllocatorManager.AllocatorHandle)global::ME.BECS.Constants.ALLOCATOR_DOMAIN;\n")
                        .Append("ResetStorage(allocator);\n");
                    var slots = graphSlots[graph.Id];
                    for (var slotIndex = 0; slotIndex < slots.Count; ++slotIndex) {
                        var slot = slots[slotIndex];
                        var typeName = slot.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        var indexText = slotIndex.ToString(CultureInfo.InvariantCulture);
                        source.Append("{ var item = allocator.Allocate(global::ME.BECS.TSize<").Append(typeName)
                            .Append(">.sizeInt, global::ME.BECS.TAlign<").Append(typeName).Append(">.alignInt);\n*(")
                            .Append(typeName).Append("*)item = ").Append(slot.UseDefault ? "default" :
                                "(" + typeName + ")((global::ME.BECS.FeaturesGraph.Nodes.SystemNode)global::ME.BECS.ObjectReferenceRegistry.GetObjectBySourceId<global::ME.BECS.FeaturesGraph.SystemsGraph>(" +
                                slot.SourceId.ToString(CultureInfo.InvariantCulture) + "u).nodes[" + slot.NodeIndex.ToString(CultureInfo.InvariantCulture) + "]).system")
                            .Append(";\nglobal::ME.BECS.TSystemGraph.Register<").Append(typeName).Append(">(")
                            .Append(Math.Abs(graph.Id).ToString(CultureInfo.InvariantCulture)).Append(", item);\n")
                            .Append(storage).Append('[').Append(indexText).Append("] = (global::System.IntPtr)item; }\n");
                    }
                    source.Append("ApplyInjections();\n}\n");
                    source.Append("private static void ValidateInjectionPlan() {");
                    if (!graphApply.ContainsKey(graph.Id)) source.Append("throw new global::System.InvalidOperationException(")
                        .Append(SymbolDisplay.FormatLiteral("Missing complete injection plan for graph " + graph.Id, true)).Append(");");
                    source.Append("}\n");
                    if (graphJobs.TryGetValue(graph.Id, out var patches))
                        foreach (var patch in patches) patch.Append(source, storage);
                    if (graphInjections.TryGetValue(graph.Id, out var assignments))
                        foreach (var assignment in assignments.Where(static item => item.Plan.Fields.Count != 0)) assignment.Plan.AppendSystem(source, storage, assignment.Slot);
                    if (graphApply.TryGetValue(graph.Id, out var actions)) {
                        source.Append("private static void ApplyInjections() {\n");
                        foreach (var action in actions) {
                            if (action[0] == 's' && graphInjections[graph.Id].Any(assignment => assignment.Plan.Key == action.Substring(2) && assignment.Plan.Fields.Count == 0)) continue;
                            source.Append(action[0] == 's' ? "InjectSystem_" : action[0] == 'j' ? "RegisterJob_" : "global::ME.BECS.SourceGenerated.GenericJobDeltaInputs.Register_")
                                .Append(action.Substring(2)).Append("();\n");
                        }
                        source.Append("}\n");
                    } else {
                        source.Append("private static void ApplyInjections() => ValidateInjectionPlan();\n");
                    }
                    source
                        .Append("[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.SystemsStatic.GetSystem))]\npublic static void GraphGetSystem_")
                        .Append(suffix).Append("(int index, out void* ptr) { ptr = (void*)").Append(storage).Append("[index]; }\n} }\n");
                }
    }

    internal static void AppendDeltas(StringBuilder source, IReadOnlyList<(string Key, INamedTypeSymbol Type, IFieldSymbol[] Fields)> deltaJobs) {
                source.Append("namespace ME.BECS.SourceGenerated { internal static unsafe class GenericJobDeltaInputs {\n");
                foreach (var job in deltaJobs) {
                    var name = job.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    source.Append("public static void Register_").Append(job.Key).Append("() => global::ME.BECS.JobInject<").Append(name)
                        .Append(">.Register(Patch_").Append(job.Key).Append(");\n")
                        .Append("[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.JobPatchInjectDelegate.PatchDelegate))]\n[global::Unity.Burst.BurstCompile]\n")
                        .Append("public static void Patch_").Append(job.Key).Append("(void* jobPtr, ushort worldId) { var job = (").Append(name).Append("*)jobPtr;\n");
                    foreach (var field in job.Fields) {
                        if (field.DeclaredAccessibility != Accessibility.Public) {
                            source.Append(name).Append('.').Append(GraphDeltaSetterContract.Name(field)).Append("(ref *job, worldId);\n");
                            continue;
                        }
                        source.Append("{ var dtMs = global::ME.BECS.Worlds.GetWorldDeltaTime(worldId);\nvar context = global::ME.BECS.SystemContext.Create(dtMs, default, default);\njob->@")
                            .Append(field.Name).Append(" = context.").Append(field.Type.SpecialType == SpecialType.System_UInt32 ? "deltaTimeMs" : "deltaTime").Append("; }\n");
                    }
                    source.Append("}\n");
                }
                source.Append("} }\n");
    }
}
