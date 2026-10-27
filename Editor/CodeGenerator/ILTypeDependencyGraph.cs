namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    // Declaration closure complements the executed-method closure. For example,
    // changing a field inside a component's nested value type changes its layout
    // without changing the component's own declaration or any job's IL.
    internal static class ILTypeDependencyGraph {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        internal static Type[] Closure(IEnumerable<Type> roots) {
            var result = new HashSet<Type>();
            foreach (var root in roots.Where(type => type != null).Distinct().OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)) {
                var reachable = ILAnalysisSession.Get((typeof(ILTypeDependencyGraph), "reachable", root), () => Reachable(root));
                result.UnionWith(reachable);
            }
            return result.OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
        }

        private static Type[] Reachable(Type root) {
            var seen = new HashSet<Type>();
            var pending = new Queue<Type>();
            pending.Enqueue(root);
            while (pending.Count != 0) {
                ILAnalysisSession.Checkpoint();
                var type = pending.Dequeue();
                if (type == null || seen.Add(type) == false) { continue; }
                if (type != root && ILAnalysisSession.TryGet<Type[]>((typeof(ILTypeDependencyGraph), "reachable", type), out var known) == true) {
                    seen.UnionWith(known);
                    continue;
                }
                if (type.HasElementType == true) {
                    pending.Enqueue(type.GetElementType());
                    continue;
                }
                if (type.IsGenericParameter == true) {
                    pending.Enqueue(type.DeclaringType);
                    foreach (var constraint in type.GetGenericParameterConstraints()) { pending.Enqueue(constraint); }
                    continue;
                }
                if (type.IsGenericType == true) {
                    pending.Enqueue(type.GetGenericTypeDefinition());
                    foreach (var argument in type.GetGenericArguments()) { pending.Enqueue(argument); }
                    // Inspect the definition once; expanding a declaration such as
                    // Node<Node<T>> on every closed owner never reaches a fixed point.
                    if (type.IsGenericTypeDefinition == false) { continue; }
                }
                // Library binaries are already hashed by the shared context.
                if (IsScript(type.Assembly) == false) { continue; }
                foreach (var dependency in Direct(type)) { pending.Enqueue(dependency); }
            }
            return seen.Where(type => type.AssemblyQualifiedName != null &&
                (type.ContainsGenericParameters == false || type.IsGenericTypeDefinition == true) && IsScript(type.Assembly) == true)
                .ToArray();
        }

        private static bool IsScript(Assembly assembly) => ILAnalysisSession.Get((typeof(ILTypeDependencyGraph), assembly),
            () => ILContentFingerprint.IsUserCode(assembly));

        private static Type[] Direct(Type type) => ILAnalysisSession.Get((typeof(ILTypeDependencyGraph), type), () => {
            var result = new HashSet<Type>();
            void Add(Type dependency) { if (dependency != null) { result.Add(dependency); } }
            void Argument(CustomAttributeTypedArgument argument) {
                Add(argument.ArgumentType);
                if (argument.Value is Type value) { Add(value); }
                if (argument.Value is IEnumerable<CustomAttributeTypedArgument> values) {
                    foreach (var item in values) { Argument(item); }
                }
            }
            void Attributes(IEnumerable<CustomAttributeData> attributes) {
                foreach (var attribute in attributes) {
                    Add(attribute.AttributeType);
                    foreach (var argument in attribute.ConstructorArguments) { Argument(argument); }
                    foreach (var argument in attribute.NamedArguments) { Argument(argument.TypedValue); }
                }
            }
            void Generic(Type argument) {
                Add(argument);
                if (argument.IsGenericParameter == true) {
                    Attributes(argument.GetCustomAttributesData());
                    foreach (var constraint in argument.GetGenericParameterConstraints()) { Add(constraint); }
                }
            }
            Add(type.BaseType);
            Add(type.DeclaringType);
            foreach (var contract in type.GetInterfaces()) { Add(contract); }
            Attributes(type.GetCustomAttributesData());
            foreach (var argument in type.GetGenericArguments()) { Generic(argument); }
            foreach (var field in type.GetFields(Declared)) {
                Add(field.FieldType);
                foreach (var modifier in field.GetRequiredCustomModifiers()) { Add(modifier); }
                foreach (var modifier in field.GetOptionalCustomModifiers()) { Add(modifier); }
                Attributes(field.GetCustomAttributesData());
            }
            foreach (var method in ILContentFingerprint.Methods(type)) {
                Attributes(method.GetCustomAttributesData());
                // A method's signature is part of the owner's fingerprint, but
                // an unused parameter's layout is not a dependency of this type.
                // Consumed method signatures are captured by ObserveDeclaration.
                foreach (var parameter in method.GetParameters()) { Attributes(parameter.GetCustomAttributesData()); }
                if (method is MethodInfo info) {
                    Attributes(info.ReturnParameter.GetCustomAttributesData());
                    foreach (var argument in info.GetGenericArguments()) { Generic(argument); }
                }
            }
            foreach (var property in type.GetProperties(Declared)) {
                Attributes(property.GetCustomAttributesData());
            }
            foreach (var item in type.GetEvents(Declared)) {
                Attributes(item.GetCustomAttributesData());
            }
            if (typeof(Attribute).IsAssignableFrom(type) == true || typeof(IRefOp).IsAssignableFrom(type) == true) {
                using var executable = new ILDependencyCapture();
                if (ILContentFingerprint.TryExecutableClosure(type, out _) == false) {
                    // The caller must retain the broad assembly scope for an
                    // unbound metadata dispatch, not publish a partial graph.
                    throw new NotSupportedException("Unbound executable metadata dependency: " + type);
                }
                foreach (var dependency in executable.Types) { Add(dependency); }
            }
            return result.ToArray();
        });
    }
}
