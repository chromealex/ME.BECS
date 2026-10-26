namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Text;
    using ME.BECS.Mono.Reflection;
    using Names = ME.BECS.CodeGeneration.SourceGeneratorNames;

    // Metadata tokens/MVIDs are not method content: unchanged helpers can move
    // to new tokens after a compilation. Normalize operands to CLR identities.
    internal static class ILContentFingerprint {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Roslyn embeds these attributes into ordinary script assemblies. Their
        // simple storage constructors must not turn every body edit into a global
        // declaration change. Name/CompilerGenerated alone is NOT sufficient:
        // audit every constructor instruction before using a local content stamp.
        // Anything else is identified by the code reflection can actually execute:
        // its constructors, property accessors and type initializers plus every
        // user method they reach. Only an unresolvable dispatch keeps the MVID.
        internal static string ExecutableMetadataStamp(Type type, Guid mvid) =>
            TryCompilerMetadata(type, out var content) ? "compiler:" + content :
            TryEmptyAttribute(type, out content) ? "empty-attribute:" + content :
            TryExecutableClosure(type, out content) ? "closure:" + content : "assembly:" + mvid.ToString("D");

        internal const int ExecutableClosureLimit = 4096;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> reportedMvidStamps =
            new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // Reflection over attributes and IRefOp runs constructors, property
        // accessors and static initializers. Hash those bodies and, transitively,
        // every user-code method, constructor and type initializer they can reach.
        // Framework/precompiled code is a leaf: it is covered by the library
        // boundary of the cache context. A non-final virtual or interface call into
        // user code cannot be bound statically and keeps the conservative MVID.
        internal static bool TryExecutableClosure(Type type, out string content) {
            content = null;
            if (type == null) return false;
            try {
                var queue = new Queue<MethodBase>();
                for (var owner = type; owner != null && IsUserCode(owner.Assembly); owner = owner.BaseType) {
                    foreach (var constructor in owner.GetConstructors(Declared)) queue.Enqueue(constructor);
                    if (owner.TypeInitializer != null) queue.Enqueue(owner.TypeInitializer);
                    foreach (var property in owner.GetProperties(Declared))
                        foreach (var accessor in property.GetAccessors(true)) queue.Enqueue(accessor);
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var rows = new List<string>();
                while (queue.Count > 0) {
                    var method = queue.Dequeue();
                    var member = Member(method);
                    if (!seen.Add(member)) continue;
                    if (seen.Count > ExecutableClosureLimit) return false;
                    rows.Add(member + "\t" + Body(method));
                    if (method.GetMethodBody() == null) continue;
                    foreach (var instruction in method.GetInstructions()) {
                        switch (instruction.Operand) {
                            case MethodBase target:
                                if (target.DeclaringType == null || !IsUserCode(target.Module.Assembly)) break;
                                if ((instruction.OpCode == OpCodes.Callvirt || instruction.OpCode == OpCodes.Ldvirtftn) && target.IsVirtual && !target.IsFinal &&
                                    !target.DeclaringType.IsSealed && !target.DeclaringType.IsValueType) return false;
                                queue.Enqueue(target);
                                if (target.DeclaringType.TypeInitializer != null) queue.Enqueue(target.DeclaringType.TypeInitializer);
                                break;
                            case FieldInfo field:
                                if (field.DeclaringType != null && IsUserCode(field.Module.Assembly) && field.DeclaringType.TypeInitializer != null)
                                    queue.Enqueue(field.DeclaringType.TypeInitializer);
                                break;
                            case byte[] _:
                                return false; // calli: an unbound inline signature
                        }
                    }
                }
                rows.Sort(StringComparer.Ordinal);
                content = Names.Hash(string.Join("\n", rows));
                return true;
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                content = null;
                return false;
            }
        }

        // Compiled project code (Library/ScriptAssemblies) and in-memory assemblies.
        // Everything else is framework, Unity or a precompiled plugin.
        internal static bool IsUserCode(Assembly assembly) {
            if (assembly == null) return false;
            if (assembly.IsDynamic) return true;
            string location;
            try { location = assembly.Location; } catch (NotSupportedException) { return true; }
            if (string.IsNullOrEmpty(location)) return true;
            return location.Replace('\\', '/').IndexOf("/Library/ScriptAssemblies/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Compiler artifacts (closures, iterator/async state machines, lambda caches,
        // local functions, <PrivateImplementationDetails>) are renamed by ordinary
        // body edits. Their code is reached through the bodies that use them, whose
        // fingerprints already change, so they are not declarations of the assembly.
        internal static bool IsCompilerArtifact(Type type) {
            for (var owner = type; owner != null; owner = owner.DeclaringType)
                if (owner.Name.StartsWith("<", StringComparison.Ordinal)) return !SourceGeneratorCodeIdentity.IsInputType(type);
            return false;
        }

        internal static bool IsCompilerArtifact(MethodBase method) => method.Name.StartsWith("<", StringComparison.Ordinal);

        // An empty marker cannot observe helpers or mutable state. Audit actual
        // IL rather than trusting the attribute's name, visibility or provenance.
        internal static bool TryEmptyAttribute(Type type, out string content) {
            content = null;
            if (type.BaseType != typeof(Attribute) || type.ContainsGenericParameters || type.GetFields(Declared).Length != 0) return false;
            var methods = Methods(type).OrderBy(Signature, StringComparer.Ordinal).ToArray();
            if (methods.Length == 0) return false;
            foreach (var method in methods) {
                if (!(method is ConstructorInfo) || method.IsStatic) return false;
                var body = method.GetMethodBody();
                if (body == null || body.ExceptionHandlingClauses.Count != 0) return false;
                var instructions = method.GetInstructions().Where(instruction => instruction.OpCode != OpCodes.Nop).ToArray();
                if (instructions.Length != 3 || instructions[0].OpCode != OpCodes.Ldarg_0 ||
                    instructions[1].OpCode != OpCodes.Call || instructions[2].OpCode != OpCodes.Ret ||
                    !(instructions[1].Operand is ConstructorInfo constructor) || constructor.DeclaringType != typeof(Attribute) ||
                    constructor.IsStatic || constructor.GetParameters().Length != 0) return false;
            }
            content = Names.Hash(string.Join("\n", methods.Select(Body)));
            return true;
        }

        internal static bool TryCompilerMetadata(Type type, out string content) {
            content = null;
            if (type.BaseType != typeof(Attribute) || !type.IsSealed || type.IsVisible || type.ContainsGenericParameters ||
                !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)) return false;
            switch (type.FullName) {
                case "Microsoft.CodeAnalysis.EmbeddedAttribute":
                case "System.Runtime.CompilerServices.IsUnmanagedAttribute":
                case "System.Runtime.CompilerServices.IsReadOnlyAttribute":
                case "System.Runtime.CompilerServices.IsByRefLikeAttribute":
                case "System.Runtime.CompilerServices.NullableAttribute":
                case "System.Runtime.CompilerServices.NullableContextAttribute":
                case "System.Runtime.CompilerServices.RefSafetyRulesAttribute":
                case "System.Runtime.CompilerServices.ScopedRefAttribute": break;
                default: return false;
            }
            var methods = Methods(type).ToArray();
            if (methods.Length == 0 || methods.Any(method => !(method is ConstructorInfo) || method.IsStatic)) return false;
            foreach (var method in methods) {
                if (method.GetParameters().Any(parameter => !CompilerMetadataValue(parameter.ParameterType))) return false;
                var body = method.GetMethodBody();
                if (body == null || body.ExceptionHandlingClauses.Count != 0) return false;
                foreach (var instruction in method.GetInstructions()) {
                    switch (instruction.OpCode) {
                        case var op when op == OpCodes.Nop || op == OpCodes.Ret || op == OpCodes.Dup ||
                            op == OpCodes.Ldarg_0 || op == OpCodes.Ldarg_1 || op == OpCodes.Ldarg_2 || op == OpCodes.Ldarg_3 ||
                            op == OpCodes.Ldarg || op == OpCodes.Ldarg_S || op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_S ||
                            op == OpCodes.Ldc_I4_M1 || op == OpCodes.Ldc_I4_0 || op == OpCodes.Ldc_I4_1 || op == OpCodes.Ldc_I4_2 ||
                            op == OpCodes.Ldc_I4_3 || op == OpCodes.Ldc_I4_4 || op == OpCodes.Ldc_I4_5 || op == OpCodes.Ldc_I4_6 ||
                            op == OpCodes.Ldc_I4_7 || op == OpCodes.Ldc_I4_8 || op == OpCodes.Stelem_I1: break;
                        case var op when op == OpCodes.Newarr:
                            if (!Equals(instruction.Operand, typeof(byte))) return false;
                            break;
                        case var op when op == OpCodes.Stfld:
                            if (!(instruction.Operand is FieldInfo field) || field.IsStatic || field.DeclaringType != type ||
                                !CompilerMetadataValue(field.FieldType)) return false;
                            break;
                        case var op when op == OpCodes.Call:
                            if (!(instruction.Operand is ConstructorInfo constructor) || constructor.DeclaringType != typeof(Attribute) ||
                                constructor.IsStatic || constructor.GetParameters().Length != 0) return false;
                            break;
                        default: return false;
                    }
                }
            }
            content = Names.Hash(string.Join("\n", methods.OrderBy(Signature, StringComparer.Ordinal).Select(method => Signature(method) + "\t" + Body(method))));
            return true;
        }

        private static bool CompilerMetadataValue(Type type) => type == typeof(byte) || type == typeof(byte[]) || type == typeof(int);

        [Serializable]
        internal sealed class MethodReference {
            public string owner;
            public string signature;
            public string[] arguments;
            [NonSerialized] private string key;

            internal string Key {
                get {
                    var cached = System.Threading.Volatile.Read(ref this.key);
                    if (cached != null) return cached;
                    var computed = this.owner + "\n" + this.signature + "\n" + string.Join("\n", this.arguments ?? Array.Empty<string>());
                    return System.Threading.Interlocked.CompareExchange(ref this.key, computed, null) ?? computed;
                }
            }

            internal static MethodReference From(MethodBase method) {
                if (method.DeclaringType?.AssemblyQualifiedName == null || method.ContainsGenericParameters)
                    throw new NotSupportedException("Persistent IL analysis requires a closed method: " + method);
                var generic = method as MethodInfo;
                return new MethodReference {
                    owner = method.DeclaringType.AssemblyQualifiedName,
                    signature = Signature(generic?.IsGenericMethod == true ? generic.GetGenericMethodDefinition() : method),
                    arguments = generic?.IsGenericMethod == true
                        ? generic.GetGenericArguments().Select(type => type.AssemblyQualifiedName).ToArray() : Array.Empty<string>(),
                };
            }

            internal MethodBase Resolve() {
                var type = ResolveType(this.owner);
                if (type == null) return null;
                var inventory = ILAnalysisSession.Get((typeof(MethodReference), type), () => Methods(type).GroupBy(Signature)
                    .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal));
                if (!inventory.TryGetValue(this.signature, out var candidates) || candidates.Length != 1) return null;
                var result = candidates[0];
                if (this.arguments?.Length > 0) {
                    var args = this.arguments.Select(ResolveType).ToArray();
                    if (args.Any(argument => argument == null)) return null;
                    result = ((MethodInfo)result).MakeGenericMethod(args);
                }
                return result;
            }

            // Assembly-qualified generic names are expensive to resolve repeatedly.
            // The memo belongs to this analysis snapshot; a new domain/session
            // cannot inherit a missing type from an earlier compilation.
            private static Type ResolveType(string identity) =>
                ILAnalysisSession.Get((typeof(MethodReference), identity), () => Type.GetType(identity, false));
        }

        internal static IEnumerable<MethodBase> Methods(Type type) => type.GetMethods(Declared).Cast<MethodBase>()
            .Concat(type.GetConstructors(Declared)).Concat(type.TypeInitializer == null ? Array.Empty<MethodBase>() : new[] { type.TypeInitializer }).Distinct();

        internal static string TypeName(Type type) => type == null ? "<null>" :
            ILAnalysisSession.Get((typeof(ILContentFingerprint), type), () => TypeNameCore(type));

        private static string TypeNameCore(Type type) {
            if (type == null) return "<null>";
            if (type.IsGenericParameter) return (type.DeclaringMethod == null ? "!" : "!!") + type.GenericParameterPosition;
            if (type.IsByRef) return TypeName(type.GetElementType()) + "&";
            if (type.IsPointer) return TypeName(type.GetElementType()) + "*";
            if (type.IsArray) return TypeName(type.GetElementType()) + (type.GetArrayRank() == 1
                ? type == type.GetElementType().MakeArrayType() ? "[]" : "[*]" : "[" + new string(',', type.GetArrayRank() - 1) + "]");
            if (!type.IsGenericType) return type.AssemblyQualifiedName ?? type.ToString();
            return type.GetGenericTypeDefinition().AssemblyQualifiedName + "<" + string.Join(";", type.GetGenericArguments().Select(TypeName)) + ">";
        }

        private static string Parameter(ParameterInfo parameter) => TypeName(parameter.ParameterType) +
            " required(" + string.Join(";", parameter.GetRequiredCustomModifiers().Select(TypeName)) + ") optional(" +
            string.Join(";", parameter.GetOptionalCustomModifiers().Select(TypeName)) + ")";

        internal static string Signature(MethodBase method) =>
            ILAnalysisSession.Get((typeof(ILContentFingerprint), method), () => SignatureCore(method));

        private static string SignatureCore(MethodBase method) => method.Name + "`" + (method is MethodInfo generic ? generic.GetGenericArguments().Length : 0) +
            "|" + method.IsStatic + "|" + method.CallingConvention + "|" + (method is MethodInfo info ? Parameter(info.ReturnParameter) : "ctor") +
            "(" + string.Join(";", method.GetParameters().Select(Parameter)) + ")";

        private static string Member(MemberInfo member) {
            if (member is Type type) return TypeName(type);
            if (member is MethodBase method) return TypeName(method.DeclaringType) + "::" + Signature(method) +
                (method is MethodInfo info && info.IsGenericMethod ? "<" + string.Join(";", info.GetGenericArguments().Select(TypeName)) + ">" : "");
            if (member is FieldInfo field) return TypeName(field.DeclaringType) + "::" + field.Name + ":" + TypeName(field.FieldType);
            throw new NotSupportedException("Unsupported IL metadata operand: " + member);
        }

        internal static string Body(MethodBase method) {
            ILAnalysisSession.Checkpoint(method);
            var body = method.GetMethodBody();
            if (body == null) return "no-body";
            var text = new StringBuilder().Append(body.InitLocals).Append('|').Append(body.MaxStackSize).Append('\n');
            foreach (var local in body.LocalVariables) text.Append(local.LocalIndex).Append('|').Append(local.IsPinned).Append('|').Append(TypeName(local.LocalType)).Append('\n');
            foreach (var clause in body.ExceptionHandlingClauses) {
                text.Append((int)clause.Flags).Append('|').Append(clause.TryOffset).Append('|').Append(clause.TryLength)
                    .Append('|').Append(clause.HandlerOffset).Append('|').Append(clause.HandlerLength).Append('|');
                if (clause.Flags == ExceptionHandlingClauseOptions.Clause) text.Append(TypeName(clause.CatchType));
                if (clause.Flags == ExceptionHandlingClauseOptions.Filter) text.Append(clause.FilterOffset);
                text.Append('\n');
            }
            // This is a cheap operand scan, not an effects/CFG analysis. Do not
            // feed fingerprint reads back into the active dependency capture.
            foreach (var instruction in method.GetInstructions()) {
                text.Append(instruction.Offset).Append(':').Append(instruction.OpCode.Value).Append(':');
                var operand = instruction.Operand;
                string value;
                if (operand is MemberInfo member) value = Member(member);
                else if (operand is Instruction branch) value = "target:" + branch.Offset;
                else if (operand is Instruction[] branches) value = "targets:" + string.Join(",", branches.Select(branch => branch.Offset));
                else if (operand is ParameterInfo parameter) value = "parameter:" + parameter.Position;
                else if (operand is LocalVariableInfo local) value = "local:" + local.LocalIndex;
                else if (operand is byte[]) {
                    // Inline signatures may contain unresolved compressed tokens.
                    // Do not pretend their raw bytes are a semantic identity.
                    value = method.Module.ModuleVersionId + ":" + Convert.ToBase64String((byte[])operand);
                } else if (operand is float single) value = BitConverter.ToInt32(BitConverter.GetBytes(single), 0).ToString(CultureInfo.InvariantCulture);
                else if (operand is double number) value = BitConverter.DoubleToInt64Bits(number).ToString(CultureInfo.InvariantCulture);
                else if (operand == null || operand is string || operand is IConvertible) value = Convert.ToString(operand, CultureInfo.InvariantCulture) ?? "";
                else throw new NotSupportedException("Unsupported IL operand: " + operand.GetType());
                text.Append(value.Length).Append(':').Append(value).Append('\n');
            }
            return Names.Hash(text.ToString());
        }

        // Declaration changes are intentionally a wider invalidation boundary.
        // This includes new generic implementations, layouts and access attributes;
        // a method-body-only edit does not invalidate unrelated root summaries.
        // This semantic form skips compiler artifacts (see IsCompilerArtifact) and
        // identifies executable metadata by its reachable code, not the MVID.
        internal static string Declarations(Assembly assembly) => DeclarationsCore(assembly, true);

        internal static string AssemblyContent(Assembly assembly) {
            var text = new StringBuilder(DeclarationsCore(assembly, false));
            foreach (var type in assembly.GetTypes().OrderBy(TypeName, StringComparer.Ordinal)) {
                foreach (var method in Methods(type).OrderBy(Signature, StringComparer.Ordinal)) {
                    text.Append('\n').Append(Member(method)).Append('\t').Append(Body(method));
                }
            }
            foreach (var resource in assembly.GetManifestResourceNames().OrderBy(value => value, StringComparer.Ordinal)) {
                using var stream = assembly.GetManifestResourceStream(resource);
                using var sha = System.Security.Cryptography.SHA256.Create();
                text.Append("\nresource:").Append(resource).Append('\t').Append(Convert.ToBase64String(sha.ComputeHash(stream)));
            }
            return Names.Hash(text.ToString());
        }

        // Hash loaded code, never source files which may already be newer than
        // the loaded assembly. Everything except the isolated traversal stays
        // shared, including declarations, resources and compiler-generated data.
        internal static (string shared, string scheduled) AnalyzerContent(Assembly assembly) {
            try {
                var types = assembly.GetTypes();
                var bodies = PartitionBodies(types.SelectMany(Methods), typeof(ILScheduledJobs), Body);
                var declarations = DeclarationsCore(assembly, false);
                if (declarations.StartsWith("opaque:", StringComparison.Ordinal)) throw new NotSupportedException("Analyzer declaration metadata is unavailable.");
                var shared = new StringBuilder(declarations).Append('\n').Append(bodies.shared);
                AppendResources(shared, assembly);
                AppendFieldData(shared, types);
                return (Names.Hash(shared.ToString()), bodies.isolated);
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                // Unsupported metadata must reduce reuse, never omit code/data.
                UnityEngine.Debug.LogWarning("[ME.BECS] Incremental IL analyzer fingerprint uses conservative assembly invalidation: " + exception.Message);
                var opaque = "opaque:" + assembly.ManifestModule.ModuleVersionId;
                return (opaque, opaque);
            }
        }

        // Compiled-content identity of a loaded script assembly: declarations, IL
        // bodies, resources, field RVA data, references and assembly attributes.
        // Unlike the MVID it survives a recompilation that produced the same code
        // (a rebuilt reference, a comment or formatting edit). Throws when some
        // metadata cannot be read; callers must then fall back to the MVID.
        internal static string LoadedContent(Assembly assembly) {
            var declarations = DeclarationsCore(assembly, false);
            if (declarations.StartsWith("opaque:", StringComparison.Ordinal)) throw new NotSupportedException("Declaration metadata is unavailable.");
            var types = assembly.GetTypes();
            var text = new StringBuilder(declarations);
            foreach (var type in types.OrderBy(TypeName, StringComparer.Ordinal)) {
                foreach (var method in Methods(type).OrderBy(Signature, StringComparer.Ordinal)) {
                    text.Append('\n').Append(Member(method)).Append('\t').Append(Body(method));
                }
            }
            AppendResources(text, assembly);
            AppendFieldData(text, types);
            foreach (var reference in assembly.GetReferencedAssemblies().Select(reference => reference.FullName).OrderBy(value => value, StringComparer.Ordinal))
                text.Append("\nreference:").Append(reference);
            text.Append("\nassembly-attributes:\n");
            Attributes(text, assembly.GetCustomAttributesData());
            return Names.Hash(text.ToString());
        }

        private static void AppendResources(StringBuilder text, Assembly assembly) {
            foreach (var resource in assembly.GetManifestResourceNames().OrderBy(value => value, StringComparer.Ordinal)) {
                using var stream = assembly.GetManifestResourceStream(resource);
                using var sha = System.Security.Cryptography.SHA256.Create();
                text.Append("\nresource:").Append(resource).Append('\t').Append(Convert.ToBase64String(sha.ComputeHash(stream)));
            }
        }

        // Static initializer data (array literals, UTF-8 spans) lives in RVA
        // metadata, not in MethodBody. Unknown layouts throw; callers fall back.
        private static void AppendFieldData(StringBuilder text, IEnumerable<Type> types) {
            foreach (var field in types.SelectMany(type => type.GetFields(Declared))
                         .Where(field => (field.Attributes & FieldAttributes.HasFieldRVA) != 0).OrderBy(Member, StringComparer.Ordinal)) {
                // InitializeArray copies RVA metadata into a new byte array;
                // it does not read a static field or execute its initializer.
                var size = field.FieldType.StructLayoutAttribute?.Size ?? 0;
                if (size == 0) {
                    var type = field.FieldType;
                    size = type == typeof(byte) || type == typeof(sbyte) ? 1 :
                        type == typeof(short) || type == typeof(ushort) || type == typeof(char) ? 2 :
                        type == typeof(int) || type == typeof(uint) || type == typeof(float) ? 4 :
                        type == typeof(long) || type == typeof(ulong) || type == typeof(double) ? 8 : 0;
                }
                if (size <= 0 || size > 16 * 1024 * 1024) throw new NotSupportedException("Unknown analyzer RVA layout: " + field);
                var bytes = new byte[size];
                System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(bytes, field.FieldHandle);
                text.Append("\nrva:").Append(Member(field)).Append('\t').Append(Convert.ToBase64String(bytes));
            }
        }

        internal static (string shared, string isolated) PartitionBodies(IEnumerable<MethodBase> methods, Type isolatedType, Func<MethodBase, string> body) {
            var shared = new StringBuilder();
            var isolated = new StringBuilder();
            foreach (var method in methods.OrderBy(Member, StringComparer.Ordinal)) {
                var owner = method.DeclaringType;
                while (owner != null && owner != isolatedType) owner = owner.DeclaringType;
                var target = owner == isolatedType ? isolated : shared;
                target.Append(Member(method)).Append('\t').Append(body(method)).Append('\n');
            }
            return (Names.Hash(shared.ToString()), Names.Hash(isolated.ToString()));
        }

        private static string DeclarationsCore(Assembly assembly, bool includeExecutableMetadataVersion) {
            try {
                var text = new StringBuilder(assembly.FullName).Append('\n');
                // Use supported, known framework attributes on Assembly. Unity's
                // Assembly.GetCustomAttributesData is not universally implemented.
                var metadata = ILAnalysisSession.ReadMetadata(() => (
                    includes: assembly.GetCustomAttributes<CodeGeneratorInclude>().Select(item => TypeName(item.type)).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    values: assembly.GetCustomAttributes<AssemblyMetadataAttribute>().OrderBy(item => item.Key, StringComparer.Ordinal)
                        .ThenBy(item => item.Value, StringComparer.Ordinal).Select(item => (item.Key, item.Value)).ToArray()));
                foreach (var include in metadata.includes)
                    text.Append("include:").Append(include).Append('\n');
                foreach (var attribute in metadata.values)
                    text.Append("metadata:").Append(attribute.Key).Append(':').Append(attribute.Value).Append('\n');
                foreach (var type in assembly.GetTypes().OrderBy(TypeName, StringComparer.Ordinal)) {
                    ILAnalysisSession.Checkpoint();
                    if (includeExecutableMetadataVersion && IsCompilerArtifact(type)) continue;
                    text.Append(TypeName(type)).Append('|').Append((int)type.Attributes).Append('|').Append(TypeName(type.BaseType)).Append('\n');
                    if (type.StructLayoutAttribute is var layout && layout != null)
                        text.Append("layout:").Append(layout.Value).Append('|').Append(layout.Size).Append('|').Append(layout.Pack).Append('|').Append(layout.CharSet).Append('\n');
                    foreach (var contract in type.GetInterfaces().Select(TypeName).OrderBy(value => value, StringComparer.Ordinal)) text.Append("interface:").Append(contract).Append('\n');
                    Attributes(text, type.GetCustomAttributesData());
                    Generics(text, type.GetGenericArguments());
                    foreach (var field in type.GetFields(Declared).OrderBy(field => field.Name, StringComparer.Ordinal)) {
                        text.Append(Member(field)).Append('|').Append((int)field.Attributes).Append('\n');
                        text.Append(string.Join(";", field.GetRequiredCustomModifiers().Select(TypeName))).Append('|')
                            .Append(string.Join(";", field.GetOptionalCustomModifiers().Select(TypeName))).Append('\n');
                        if (field.IsLiteral) text.Append("constant:").Append(Value(field.GetRawConstantValue())).Append('\n');
                        Attributes(text, field.GetCustomAttributesData());
                    }
                    foreach (var method in Methods(type).OrderBy(Signature, StringComparer.Ordinal)) {
                        CodeGeneratorTimings.Work(method);
                        if (includeExecutableMetadataVersion && IsCompilerArtifact(method)) continue;
                        text.Append(Signature(method)).Append('|').Append((int)method.Attributes).Append('|').Append((int)method.GetMethodImplementationFlags()).Append('\n');
                        Attributes(text, method.GetCustomAttributesData());
                        if (method is MethodInfo genericMethod) Generics(text, genericMethod.GetGenericArguments());
                        foreach (var parameter in method.GetParameters().Concat(method is MethodInfo info ? new[] { info.ReturnParameter } : Array.Empty<ParameterInfo>())) {
                            text.Append(Parameter(parameter)).Append('|').Append((int)parameter.Attributes).Append('|').Append(Value(parameter.RawDefaultValue)).Append('\n');
                            Attributes(text, parameter.GetCustomAttributesData());
                        }
                    }
                    foreach (var property in type.GetProperties(Declared).OrderBy(property => property.Name, StringComparer.Ordinal)) {
                        text.Append("property:").Append(property.Name).Append('|').Append(TypeName(property.PropertyType)).Append('\n');
                        Attributes(text, property.GetCustomAttributesData());
                    }
                    foreach (var item in type.GetEvents(Declared).OrderBy(item => item.Name, StringComparer.Ordinal)) {
                        text.Append("event:").Append(item.Name).Append('|').Append(TypeName(item.EventHandlerType)).Append('\n');
                        Attributes(text, item.GetCustomAttributesData());
                    }
                    // Attributes and IRefOp may execute user-defined getters/ctors
                    // during reflection analysis. They need a conservative boundary.
                    if (includeExecutableMetadataVersion && (typeof(Attribute).IsAssignableFrom(type) || typeof(IRefOp).IsAssignableFrom(type))) {
                        var executable = ExecutableMetadataStamp(type, assembly.ManifestModule.ModuleVersionId);
                        if (executable.StartsWith("assembly:", StringComparison.Ordinal) && reportedMvidStamps.TryAdd(TypeName(type), 0))
                            UnityEngine.Debug.Log("[ME.BECS] Executable metadata " + type.FullName + " cannot be bound statically; any rebuild of " +
                                assembly.GetName().Name + " invalidates the summaries that can see it.");
                        text.Append("executable-metadata:").Append(executable).Append('\n');
                    }
                }
                return Names.Hash(text.ToString());
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                // Unsupported reflection metadata reduces reuse, never correctness.
                return "opaque:" + assembly.ManifestModule.ModuleVersionId;
            }
        }

        private static void Generics(StringBuilder text, IEnumerable<Type> parameters) {
            foreach (var parameter in parameters.Where(type => type.IsGenericParameter)) {
                text.Append("generic:").Append(TypeName(parameter)).Append('|').Append((int)parameter.GenericParameterAttributes).Append('\n');
                foreach (var constraint in parameter.GetGenericParameterConstraints().Select(TypeName).OrderBy(value => value, StringComparer.Ordinal)) text.Append(constraint).Append('\n');
                Attributes(text, parameter.GetCustomAttributesData());
            }
        }

        private static string Value(object value) => value is Type type ? TypeName(type) :
            value is IReadOnlyCollection<CustomAttributeTypedArgument> arguments ? "[" + string.Join(";", arguments.Select(argument => TypeName(argument.ArgumentType) + ":" + Value(argument.Value))) + "]" :
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>";

        private static void Attributes(StringBuilder text, IEnumerable<CustomAttributeData> attributes) {
            foreach (var attribute in attributes.Select(attribute => Member(attribute.Constructor) + "(" +
                         string.Join(";", attribute.ConstructorArguments.Select(argument => TypeName(argument.ArgumentType) + ":" + Value(argument.Value))) + ")" +
                         string.Join(";", attribute.NamedArguments.OrderBy(argument => argument.MemberName, StringComparer.Ordinal)
                             .Select(argument => argument.MemberName + ":" + TypeName(argument.TypedValue.ArgumentType) + ":" + Value(argument.TypedValue.Value))))
                         .OrderBy(value => value, StringComparer.Ordinal)) text.Append("attribute:").Append(attribute.Length).Append(':').Append(attribute).Append('\n');
        }
    }
}
