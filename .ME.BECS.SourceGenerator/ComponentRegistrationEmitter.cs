using System.Text;

namespace ME.BECS.SourceGenerator;

// Shared by declaring-assembly wrappers and the precompiled-component fallback.
// A phase is a separate method: callers retain the global, phase-major ID order.
internal static class ComponentRegistrationEmitter {
    internal static string[] Operations(int flags) {
        var operations = new System.Collections.Generic.List<string> { "Size", "Register", "Aot" };
        if ((flags & 8) != 0) operations.AddRange(new[] { "RegisterShared", "AotShared" });
        if ((flags & 2) != 0) operations.AddRange(new[] { "RegisterStatic", "AotStatic" });
        if ((flags & 32) != 0) operations.AddRange(new[] { "RegisterConfig", "AotConfig" });
        return operations.ToArray();
    }

    internal static void Append(StringBuilder source, string type, int flags, string key,
        string parameters = "", string constraints = "", string? forwardOwner = null, string? forwardKey = null,
        string arguments = "") {
        var tag = (flags & 1) != 0 ? "true" : "false";
        var isStatic = (flags & 2) != 0 ? "true" : "false";
        var validate = "global::ME.BECS.StaticTypes<" + type + ">.Validate(isTag: " + tag + ", isStatic: " + isStatic + ");";
        foreach (var operation in Operations(flags)) {
            source.Append("public static ").Append(operation == "Size" ? "uint " : "void ")
                .Append(operation).Append('_').Append(key).Append(parameters).Append("()").Append(constraints).Append(" {\n");
            if (forwardOwner != null) {
                if (operation == "Size") source.Append("return ");
                source.Append(forwardOwner).Append('.').Append(operation).Append('_').Append(forwardKey).Append(arguments).Append("();\n");
            } else {
                switch (operation) {
                    // The owner only needs a BECS reference, not a new direct
                    // Unity.Collections reference in the component's asmdef.
                    case "Size": source.Append("return global::ME.BECS.TSize<").Append(type).Append(">.size;\n"); break;
                    case "Register":
                        source.AppendLine(validate);
                        if ((flags & 4) != 0) source.Append("global::ME.BECS.StaticTypes<").Append(type).Append(">.SetDefaultValue(").Append(type).Append(".Default);\n");
                        break;
                    case "Aot": source.Append("global::ME.BECS.StaticTypes<").Append(type).Append(">.AOT();\n"); break;
                    case "RegisterShared": source.Append("global::ME.BECS.StaticTypes<").Append(type).Append(">.ValidateShared(isTag: ").Append(tag)
                        .Append(", hasCustomHash: ").Append((flags & 16) != 0 ? "true" : "false").Append(");\n"); break;
                    case "AotShared": source.Append("global::ME.BECS.StaticTypesShared<").Append(type).Append(">.AOT();\n"); break;
                    case "RegisterStatic": source.Append("global::ME.BECS.StaticTypes<").Append(type).Append(">.ValidateStatic(isTag: ").Append(tag).Append(");\n"); break;
                    case "AotStatic": source.Append("global::ME.BECS.StaticTypesStatic<").Append(type).Append(">.AOT();\n"); break;
                    case "RegisterConfig": source.AppendLine(validate); break;
                    case "AotConfig": source.Append("global::ME.BECS.ConfigInitializeTypes<").Append(type).Append(">.AOT();\n"); break;
                }
            }
            source.AppendLine("}");
        }
    }
}
