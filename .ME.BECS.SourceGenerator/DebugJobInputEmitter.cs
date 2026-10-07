using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class DebugJobInputEmitter {
    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static string FieldName(ITypeSymbol type) => "safety_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(TypeName(type));

    internal static void AppendMetadata(StringBuilder source, IReadOnlyList<DebugJobInputPlan> plans, string key = "ME.BECS.DebugJobSafety.v1") {
        foreach (var plan in plans) {
            var payload = "v1\n" + plan.JobIdentity + "\n" + plan.ContractIdentity + "\n" + plan.SafetyOrigin +
                string.Concat(plan.Safety.Select(dependency => "\nS\t" + dependency.Mode + "\t" + JobSafetySummary.ReflectionIdentity(dependency.Type)));
            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(key, true)).Append(", ")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(payload, true)).Append(")]\n");
        }
    }

    internal static string InitializerName(DebugJobInputPlan plan) => "Initialize_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(plan.JobIdentity + "\n" + plan.ContractIdentity);

    internal static void Append(StringBuilder source, string ns, IReadOnlyList<DebugJobInputPlan> plans, string owner = "DebugJobs", bool individual = false) {
        // Preserve the public owner even when collection checks are disabled. It
        // used to be declared by Debug.Cache; now that file is a retirement stub.
        source.Append("\nnamespace ").Append(ns).Append(" { public unsafe partial class ").Append(owner).Append(" { } }\n")
            .Append("#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS\nnamespace ").Append(ns).Append(" {\n")
            .Append("using Unity.Burst; using Unity.Collections.LowLevel.Unsafe; using ME.BECS.Jobs; using static ME.BECS.Cuts;\n")
            .Append("[BurstCompile] public unsafe partial class ").Append(owner).Append(" {\n");
        var initialize = new StringBuilder("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void InitializeJobsDebug() {\n");
        foreach (var plan in plans) {
            if (individual) initialize = new StringBuilder("[global::UnityEngine.Scripting.PreserveAttribute]\n[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void " + InitializerName(plan) + "() {\n");
            var name = "JobDebugData_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(plan.JobIdentity + "\n" + plan.ContractIdentity);
            var job = TypeName(plan.Job);
            source.Append("private struct Cache").Append(name).Append(" { public static readonly SharedStatic<global::System.IntPtr> cache = SharedStatic<global::System.IntPtr>.GetOrCreate<Cache")
                .Append(name).Append(">(); }\n");
            foreach (var unsafeMode in new[] { false, true }) {
                source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic struct ").Append(name).Append(unsafeMode ? "Unsafe" : "").Append(" {\n")
                    .Append("[NativeDisableUnsafePtrRestriction] public ScheduleFlags scheduleFlags;\npublic JobInfo jobInfo;\n")
                    .Append("[NativeDisableUnsafePtrRestriction] public ").Append(job).Append(" jobData;\n")
                    .Append("[NativeDisableUnsafePtrRestriction] public CommandBuffer* buffer;\n");
                var attribute = unsafeMode ? "[NativeDisableContainerSafetyRestriction] " : "";
                if (plan.HasTypedArguments) {
                    for (var index = 0; index < plan.Aspects.Count; ++index)
                        source.Append(attribute).Append("public ").Append(TypeName(plan.Aspects[index])).Append(" a").Append(index.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                    for (var index = 0; index < plan.Components.Count; ++index)
                        source.Append(attribute).Append("public RefRW<").Append(TypeName(plan.Components[index])).Append("> c").Append(index.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                }
                foreach (var dependency in plan.Safety)
                    source.Append(attribute).Append("public SafetyComponentContainer").Append(Mode(dependency.Mode)).Append('<').Append(TypeName(dependency.Type))
                        .Append("> ").Append(FieldName(dependency.Type)).Append(";\n");
                source.Append("}\n");
            }
            initialize.Append("{\nCache").Append(name).Append(".cache.Data = default;\n[BurstCompile]\n")
                .Append("static void* Method(void* jobData, CommandBuffer* buffer, bool unsafeMode, ScheduleFlags scheduleFlags, in JobInfo jobInfo) {\n")
                .Append(name).Append("* data = (").Append(name).Append("*)Cache").Append(name).Append(".cache.Data;\nif (data == null) {\n")
                .Append("if (unsafeMode) data = (").Append(name).Append("*)_makeDefault(new ").Append(name).Append("Unsafe(), Constants.ALLOCATOR_DOMAIN).ptr;\n")
                .Append("else data = (").Append(name).Append("*)_makeDefault(new ").Append(name).Append("(), Constants.ALLOCATOR_DOMAIN).ptr;\n")
                .Append("Cache").Append(name).Append(".cache.Data = (global::System.IntPtr)data;\n}\n")
                .Append("data->scheduleFlags = scheduleFlags; data->jobInfo = jobInfo; data->jobData = *(").Append(job).Append("*)jobData; data->buffer = buffer;\n");
            if (plan.HasTypedArguments) {
                for (var index = 0; index < plan.Aspects.Count; ++index)
                    initialize.Append("data->a").Append(index.ToString(CultureInfo.InvariantCulture)).Append(" = WorldAspectStorage.Initialize<")
                        .Append(TypeName(plan.Aspects[index])).Append(">(buffer->worldId);\n");
                for (var index = 0; index < plan.Components.Count; ++index)
                    initialize.Append("data->c").Append(index.ToString(CultureInfo.InvariantCulture)).Append(" = buffer->state.ptr->components.GetRW<")
                        .Append(TypeName(plan.Components[index])).Append(">(buffer->state, buffer->worldId);\n");
            }
            foreach (var dependency in plan.Safety)
                initialize.Append("data->").Append(FieldName(dependency.Type)).Append(" = new SafetyComponentContainer").Append(Mode(dependency.Mode))
                    .Append('<').Append(TypeName(dependency.Type)).Append(">(buffer->state, buffer->worldId);\n");
            initialize.Append("return data;\n}\nvar fn = BurstCompiler.CompileFunctionPointer<CompiledJobCallback>(Method);\nCompiledJobs<")
                .Append(job).Append(">.SetFunction(fn, (unsafeMode) => unsafeMode ? typeof(").Append(name).Append("Unsafe) : typeof(").Append(name).Append("));\n}\n");
            if (individual) source.Append(initialize).Append("}\n");
        }
        if (!individual) source.Append(initialize).Append("}\n");
        source.Append("} }\n#endif\n");
        if (individual) {
            // Publish the same explicit coverage even in unchecked players. The
            // phase is not executed there, and no debug types/Burst code exist.
            source.Append("#if !(ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS)\nnamespace ").Append(ns)
                .Append(" { public unsafe partial class ").Append(owner).Append(" {\n");
            foreach (var plan in plans) source.Append("[global::UnityEngine.Scripting.PreserveAttribute]\npublic static void ")
                .Append(InitializerName(plan)).Append("() { }\n");
            source.Append("} }\n#endif\n");
        }
    }

    private static string Mode(string value) => value switch { "ReadOnly" => "RO", "WriteOnly" => "WO", "ReadWrite" => "RW", _ => throw new System.ArgumentOutOfRangeException(nameof(value)) };
}
