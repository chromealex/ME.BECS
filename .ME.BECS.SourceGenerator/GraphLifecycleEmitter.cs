using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ME.BECS.SourceGenerator;

// Produces the candidate runtime body from the IR. Registration is intentionally a separate switch.
internal static class GraphLifecycleEmitter {
    private const int StackHandleLimit = 64;
    internal static void Append(StringBuilder source, GraphLifecyclePlan plan, string storageExpression) {
        if (!plan.ValidateCompletion(out var reason)) throw new InvalidOperationException(reason);
        var groups = new List<(int Start, int End, bool Burst)>();
        var start = 0;
        var burst = false;
        for (var index = 0; index < plan.Steps.Count; ++index) {
            var step = plan.Steps[index];
            if (!step.Invoke || step.Burst == burst) continue;
            if (index > start) groups.Add((start, index, burst));
            start = index;
            burst = step.Burst;
        }
        if (start < plan.Steps.Count) groups.Add((start, plan.Steps.Count, burst));
        // Keep candidate Burst attributes off the transitional phase class: merely compiling the
        // candidate must not alter Burst discovery for existing lifecycle methods.
        source.Append("[global::Unity.Burst.BurstCompile] private static class PlannedLifecycle {\n")
            .Append("public static void Execute(uint dt, ref global::ME.BECS.World world, ref global::Unity.Jobs.JobHandle dependsOn) {\n");
        if (plan.Steps.Count == 0) { source.Append("}\n}\n"); return; }
        source.Append("var input = dependsOn;\n");
        var stackHandles = plan.Steps.Count <= StackHandleLimit;
        if (stackHandles) {
            // Completion validation guarantees every referenced handle is an earlier assigned
            // slot. No zero initialization is needed; pointers never escape this Execute call.
            source.Append("global::Unity.Jobs.JobHandle* handles = stackalloc global::Unity.Jobs.JobHandle[")
                .Append(Number(plan.Steps.Count)).Append("];\n");
        } else {
            source.Append("var storage = new global::Unity.Collections.NativeArray<global::Unity.Jobs.JobHandle>(")
                .Append(Number(plan.Steps.Count)).Append(", global::ME.BECS.Constants.ALLOCATOR_TEMP);\ntry {\n")
                .Append("var handles = (global::Unity.Jobs.JobHandle*)global::Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(storage);\n");
        }
        source.Append("var systems = (global::System.IntPtr*)global::Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(")
            .Append(storageExpression).Append(");\n");
        for (var index = 0; index < groups.Count; ++index)
            source.Append("PlannedLifecycleGroup_").Append(Number(index)).Append("(dt, in world, in input, systems, handles);\n");
        source.Append("dependsOn = ").Append(Handle(plan.Result)).Append(";\n");
        if (!stackHandles) source.Append("} finally { storage.Dispose(); }\n");
        source.Append("}\n");
        for (var index = 0; index < groups.Count; ++index) {
            var group = groups[index];
            if (group.Burst) source.Append("[global::Unity.Burst.BurstCompile]\n");
            source.Append("private static void PlannedLifecycleGroup_").Append(Number(index))
                .Append("(uint dt, in global::ME.BECS.World world, in global::Unity.Jobs.JobHandle input, global::System.IntPtr* systems, global::Unity.Jobs.JobHandle* handles) {\n");
            for (var stepIndex = group.Start; stepIndex < group.End; ++stepIndex) {
                var step = plan.Steps[stepIndex];
                source.Append("{\nvar dependency = ").Append(step.Dependencies.Length == 0 ? "default(global::Unity.Jobs.JobHandle)" : Handle(step.Dependencies[0])).Append(";\n");
                for (var dependencyIndex = 1; dependencyIndex < step.Dependencies.Length; ++dependencyIndex)
                    source.Append("dependency = global::Unity.Jobs.JobHandle.CombineDependencies(dependency, ")
                        .Append(Handle(step.Dependencies[dependencyIndex])).Append(");\n");
                if (step.PreApply) source.Append("dependency = global::ME.BECS.Batches.Apply(dependency, in world);\n");
                if (step.Invoke) {
                    if (step.Generic) {
                        source.Append("dependency = Invoke").Append(step.Parallel ? "Parallel_" : "Sequential_")
                            .Append(Number(step.SlotStart)).Append('_').Append(Number(step.SlotCount))
                            .Append("(dt, in world, dependency, systems");
                        if (!step.Parallel) source.Append(step.Apply ? ", true" : ", false");
                        source.Append(");\n");
                    } else {
                        source.Append("var context = global::ME.BECS.SystemContext.Create(dt, in world, dependency);\nInvokeSystem_")
                            .Append(Number(step.SlotStart)).Append("(systems[").Append(Number(step.SlotStart))
                            .Append("], ref context);\ndependency = context.dependsOn;\n");
                    }
                }
                if (step.Apply) source.Append("dependency = global::ME.BECS.Batches.Apply(dependency, in world);\n");
                source.Append("handles[").Append(Number(stepIndex)).Append("] = dependency;\n}\n");
            }
            source.Append("}\n");
        }
        source.Append("}\n");
    }

    private static string Handle(int index) => index == -1 ? "input" : "handles[" + Number(index) + "]";
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
