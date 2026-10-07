namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using TypeInfo = Jobs.JobsEarlyInitCodeGenerator.TypeInfo;

    // Typed effects of audited Unity 6000.2 / Collections APIs whose IL uses
    // native intrinsics, raw pointers or untyped MemCpy. These are additive
    // contracts, NOT traversal leaves or proofs of allocator/synchronization
    // safety. Arguments, callbacks and implementation bodies remain traversed.
    internal static class ILNativeComponentAccess {
        internal static IEnumerable<TypeInfo> Collect(MethodBase candidate) {
            if (!(candidate is MethodInfo method) || !method.IsPublic || method.IsAbstract || method.IsVirtual && !method.IsFinal ||
                method.ContainsGenericParameters || (method.CallingConvention & CallingConventions.VarArgs) != 0) yield break;
            Type[] arguments;
            RefOp mode;
            if (Memory(method, out mode)) arguments = method.GetGenericArguments();
            else if (List(method, out mode)) arguments = method.DeclaringType.GetGenericArguments();
            else yield break;
            foreach (var type in arguments)
                if (type.IsValueType && !type.ContainsGenericParameters && typeof(IComponentBase).IsAssignableFrom(type))
                    yield return new TypeInfo { type = type, op = mode };
        }

        // Exact signatures can also be used by an ordered analyzer. Recognizing
        // one is not itself a storage/alias proof; that consumer must model its
        // reads, writes and escaped references explicitly.
        internal static bool Memory(MethodInfo method, out RefOp mode) {
            mode = RefOp.ReadWrite;
            if (!method.IsPublic || !method.IsStatic || method.IsAbstract || method.IsVirtual || method.ContainsGenericParameters ||
                (method.CallingConvention & CallingConventions.VarArgs) != 0 ||
                method.DeclaringType != typeof(UnsafeUtility) || !method.IsGenericMethod) return false;
            var definition = method.GetGenericMethodDefinition();
            var types = definition.GetGenericArguments();
            var parameters = definition.GetParameters();
            bool Value(int index, Type type) => parameters[index].ParameterType == type && !parameters[index].IsIn && !parameters[index].IsOut;
            bool Ptr(int index) => Value(index, typeof(void).MakePointerType());
            bool Int(int index) => Value(index, typeof(int));
            bool Ref(int index, Type type, bool output = false) => parameters[index].ParameterType == type.MakeByRefType() &&
                !parameters[index].IsIn && parameters[index].IsOut == output;
            if (types.Length == 2)
                return method.Name == "As" && parameters.Length == 1 && Ref(0, types[0]) && definition.ReturnType == types[1].MakeByRefType();
            if (types.Length != 1) return false;
            var element = types[0];
            if (definition.ReturnType == element &&
                (method.Name == "ReadArrayElement" && parameters.Length == 2 && Ptr(0) && Int(1) ||
                 method.Name == "ReadArrayElementWithStride" && parameters.Length == 3 && Ptr(0) && Int(1) && Int(2))) {
                mode = RefOp.ReadOnly;
                return true;
            }
            if (definition.ReturnType == typeof(void) &&
                (method.Name == "WriteArrayElement" && parameters.Length == 3 && Ptr(0) && Int(1) && Value(2, element) ||
                 method.Name == "WriteArrayElementWithStride" && parameters.Length == 4 && Ptr(0) && Int(1) && Int(2) && Value(3, element))) return true;
            if ((element.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0) return false;
            if (method.Name == "AddressOf" && parameters.Length == 1 && Ref(0, element) && definition.ReturnType == typeof(void).MakePointerType()) return true;
            if (definition.ReturnType == element.MakeByRefType() &&
                (method.Name == "AsRef" && parameters.Length == 1 && Ptr(0) ||
                 method.Name == "ArrayElementAsRef" && parameters.Length == 2 && Ptr(0) && Int(1))) return true;
            return definition.ReturnType == typeof(void) && parameters.Length == 2 &&
                (method.Name == "CopyPtrToStructure" && Ptr(0) && Ref(1, element, true) ||
                 method.Name == "CopyStructureToPtr" && Ref(0, element) && Ptr(1));
        }

        private static bool List(MethodInfo method, out RefOp mode) {
            mode = RefOp.ReadWrite;
            var owner = method.DeclaringType;
            if (method.IsStatic || method.IsGenericMethod || owner?.IsGenericType != true) return false;
            var definition = owner.GetGenericTypeDefinition();
            if (definition != typeof(NativeList<>) && definition != typeof(UnsafeList<>)) return false;
            var element = owner.GetGenericArguments()[0];
            var parameters = method.GetParameters();
            bool Value(int index, Type type) => parameters[index].ParameterType == type && !parameters[index].IsIn && !parameters[index].IsOut;
            bool Int(int index) => Value(index, typeof(int));
            bool Input(int index) => parameters[index].ParameterType == element.MakeByRefType() && parameters[index].IsIn && !parameters[index].IsOut;
            bool Buffer() => parameters.Length == 2 && Value(0, typeof(void).MakePointerType()) && Int(1);
            if (method.Name == "get_Item" && method.ReturnType == element && parameters.Length == 1 && Int(0)) {
                mode = RefOp.ReadOnly;
                return true;
            }
            if (method.Name == "ElementAt" && method.ReturnType == element.MakeByRefType() && parameters.Length == 1 && Int(0)) return true;
            if (method.ReturnType != typeof(void)) return false;
            switch (method.Name) {
                case "set_Item": return parameters.Length == 2 && Int(0) && Value(1, element);
                case "Add": return parameters.Length == 1 && Input(0);
                case "AddNoResize": return parameters.Length == 1 && Value(0, element);
                case "AddReplicate": return parameters.Length == 2 && Input(0) && Int(1);
                case "AddRangeNoResize": return Buffer() || parameters.Length == 1 && Value(0, owner);
                case "AddRange": return Buffer() || parameters.Length == 1 && Value(0,
                    definition == typeof(UnsafeList<>) ? owner : typeof(NativeArray<>).MakeGenericType(element));
                case "RemoveAt":
                case "RemoveAtSwapBack":
                case "ResizeUninitialized":
                case "SetCapacity":
                case "set_Length":
                case "set_Capacity": return parameters.Length == 1 && Int(0);
                case "RemoveRange":
                case "RemoveRangeSwapBack": return parameters.Length == 2 && Int(0) && Int(1);
                case "Resize": return parameters.Length == 2 && Int(0) && Value(1, typeof(NativeArrayOptions));
                case "TrimExcess":
                case "Dispose": return parameters.Length == 0;
                default: return false;
            }
        }
    }
}
