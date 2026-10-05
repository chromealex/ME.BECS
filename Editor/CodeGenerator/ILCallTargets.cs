namespace ME.BECS.Editor {
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using ME.BECS.Mono.Reflection;

    // Exact, metadata-only bindings shared by the operation inventory and query
    // value flow. Never infer a receiver from its enclosing generic type.
    internal static class ILCallTargets {
        internal static MethodBase Resolve(Instruction[] instructions, int index) => Resolve(instructions, index, out _);

        internal static MethodBase Resolve(Instruction[] instructions, int index, out bool exactDispatch) {
            exactDispatch = false;
            var instruction = instructions[index];
            if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt && instruction.OpCode != OpCodes.Newobj &&
                instruction.OpCode != OpCodes.Jmp && instruction.OpCode != OpCodes.Ldftn && instruction.OpCode != OpCodes.Ldvirtftn) return null;
            var target = instruction.Operand as MethodBase;
            exactDispatch = target != null;
            if ((instruction.OpCode == OpCodes.Callvirt || instruction.OpCode == OpCodes.Ldvirtftn) && target is MethodInfo method) {
                var receiver = instruction.OpCode == OpCodes.Callvirt ? ConstrainedReceiver(instructions, index) : null;
                if (IsExactReceiver(receiver))
                    return ResolveConstrained(method, receiver) ??
                        throw new InvalidOperationException("Unresolved constrained IL call: " + receiver + " / " + method);
                exactDispatch = !method.IsVirtual || method.IsFinal || method.DeclaringType.IsSealed;
            }
            return target;
        }

        internal static Type ConstrainedReceiver(Instruction[] instructions, int call) {
            for (var index = call - 1; index >= 0 && instructions[index].OpCode.OpCodeType == OpCodeType.Prefix; --index)
                if (instructions[index].OpCode == OpCodes.Constrained) return instructions[index].Operand as Type;
            return null;
        }

        // Arrays report IsSealed, but covariance still permits a different runtime
        // array type (object[] can contain a string[] reference).
        internal static bool IsExactReceiver(Type receiver) => receiver != null && !receiver.ContainsGenericParameters &&
            (receiver.IsValueType || receiver.IsSealed && !receiver.IsArray);

        internal static MethodInfo ResolveConstrained(MethodInfo method, Type receiver) {
            // A closed class constraint is not necessarily the runtime type: a
            // subclass can override a virtual method or reimplement an interface.
            if (!IsExactReceiver(receiver) || method.DeclaringType == null || !method.DeclaringType.IsAssignableFrom(receiver)) return null;
            var definition = method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;
            MethodInfo Bind(MethodInfo target) => method.IsGenericMethod && !method.IsGenericMethodDefinition
                ? target.MakeGenericMethod(method.GetGenericArguments()) : target;
            if (method.DeclaringType.IsInterface) {
                var map = receiver.GetInterfaceMap(method.DeclaringType);
                for (var index = 0; index < map.InterfaceMethods.Length; ++index)
                    if (map.InterfaceMethods[index].Equals(definition)) return Bind(map.TargetMethods[index]);
                return null;
            }
            if (!method.IsVirtual || method.IsFinal) return method;
            var slot = definition.GetBaseDefinition();
            // Walk declared slots, not method names/signatures: a `new` member
            // may hide an inherited implementation without overriding its slot.
            for (var owner = receiver; owner != null; owner = owner.BaseType) {
                var target = owner.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SingleOrDefault(candidate => candidate.IsVirtual && candidate.GetBaseDefinition().Equals(slot));
                if (target != null) return Bind(target);
            }
            return null;
        }

        internal static bool TryConstruction(MethodBase method, out Type type, out ConstructorInfo constructor) {
            type = null;
            constructor = null;
            if (!(method is MethodInfo factory) || factory.DeclaringType != typeof(Activator) || factory.Name != nameof(Activator.CreateInstance) ||
                !factory.IsGenericMethod || factory.ContainsGenericParameters || factory.GetGenericArguments().Length != 1 || factory.GetParameters().Length != 0) return false;
            type = factory.GetGenericArguments()[0];
            constructor = type.GetConstructor(Type.EmptyTypes);
            // An ordinary default value type has no user constructor body.
            if (constructor == null && !type.IsValueType)
                throw new InvalidOperationException("Missing generic construction target: " + type);
            return true;
        }

        // SharedStatic.Data is an opaque mutable ref, unlike its type-identity
        // factory and raw void* getter. Keep component safety before treating
        // the audited implementation as a no-callback leaf.
        internal static bool TryBurstComponentReference(MethodBase candidate, out Type component) {
            component = null;
            if (!(candidate is MethodInfo method) || method.IsStatic || method.Name != "get_Data" ||
                method.DeclaringType?.IsGenericType != true || method.DeclaringType.GetGenericTypeDefinition() != typeof(Unity.Burst.SharedStatic<>) ||
                method.GetParameters().Length != 0) return false;
            var type = method.DeclaringType.GetGenericArguments()[0];
            if (type.ContainsGenericParameters || !typeof(IComponentBase).IsAssignableFrom(type) || method.ReturnType != type.MakeByRefType()) return false;
            component = type;
            return true;
        }
    }
}
