using ME.BECS.Mono.Reflection;

namespace ME.BECS.Editor {

    using System.Reflection;
    using System.Collections.Generic;
    using System.Reflection.Emit;

    public static class ILAnalyzer {

        private enum AccessType {

            Read,
            Write,

        };

        public struct DependencyInfo {

            public System.Type type;
            public RefOp access;

            public DependencyInfo(System.Type type, RefOp access) {
                this.type = type;
                this.access = access;
            }

            public override string ToString() {
                return $"{this.type.Name}: {this.access}";
            }

        }

        public static List<DependencyInfo> AnalyzeMethod(MethodInfo method) {
            var temp = new Dictionary<System.Type, HashSet<AccessType>>();
            var instructions = method.GetInstructions();

            foreach (var p in method.GetParameters()) {
                if (p.IsOut && p.ParameterType.IsByRef) {
                    var elementType = p.ParameterType.GetElementType();
                    Add(temp, elementType, AccessType.Write);
                }
            }

            foreach (var ins in instructions) {
                var op = ins.OpCode;

                if (ins.Operand is MethodInfo methodInfo && methodInfo.GetCustomAttribute<SafetyCheckAttribute>() != null && methodInfo.IsGenericMethod == true) {
                    var sc = methodInfo.GetCustomAttribute<SafetyCheckAttribute>();
                    var type = methodInfo.GetGenericArguments()[0];
                    if (sc.Op == RefOp.ReadOnly) {
                        Add(temp, type, AccessType.Read);
                    } else if (sc.Op == RefOp.ReadWrite) {
                        Add(temp, type, AccessType.Read);
                        Add(temp, type, AccessType.Write);
                    } else if (sc.Op == RefOp.WriteOnly) {
                        Add(temp, type, AccessType.Write);
                    }
                    continue;
                }
                
                switch (op) {
                    case var _ when op == OpCodes.Ldflda: {
                        var field = (FieldInfo)ins.Operand;
                        var next = ins.Next;
                        if (next != null && next.OpCode == OpCodes.Ldfld) {
                            Add(temp, field.DeclaringType, AccessType.Read);
                        } else if (next != null && next.OpCode == OpCodes.Ldloca_S) {
                            var nextNext = next.Next;
                            if (nextNext != null && nextNext.Operand is MethodInfo m && m.GetCustomAttribute<SafetyCheckAttribute>()?.Op == RefOp.ReadOnly) {
                                // read only
                                Add(temp, field.DeclaringType, AccessType.Read);
                            } else {
                                Add(temp, field.DeclaringType, AccessType.Write);
                            }
                        } else {
                            Add(temp, field.DeclaringType, AccessType.Write);
                        }
                        break;
                    }
                    case var _ when op == OpCodes.Ldfld || op == OpCodes.Ldsfld: {
                        var field = (FieldInfo)ins.Operand;
                        Add(temp, field.DeclaringType, AccessType.Read);
                        break;
                    }

                    case var _ when op == OpCodes.Stfld || op == OpCodes.Stsfld: {
                        var field = (FieldInfo)ins.Operand;
                        Add(temp, field.DeclaringType, AccessType.Write);
                        break;
                    }

                    case var _ when op == OpCodes.Call || op == OpCodes.Callvirt: {
                        if (ins.Operand is MethodInfo m) {
                            if (m.Name.StartsWith("get_")) {
                                Add(temp, m.DeclaringType, AccessType.Read);
                            } else if (m.Name.StartsWith("set_")) {
                                Add(temp, m.DeclaringType, AccessType.Write);
                            }
                        }

                        break;
                    }

                    case var _ when op == OpCodes.Ldelema || op == OpCodes.Ldelem ||
                                    op == OpCodes.Ldelem_I1 || op == OpCodes.Ldelem_U1 ||
                                    op == OpCodes.Ldelem_I2 || op == OpCodes.Ldelem_U2 ||
                                    op == OpCodes.Ldelem_I4 || op == OpCodes.Ldelem_U4 ||
                                    op == OpCodes.Ldelem_I8 || op == OpCodes.Ldelem_I ||
                                    op == OpCodes.Ldelem_R4 || op == OpCodes.Ldelem_R8 ||
                                    op == OpCodes.Ldelem_Ref: {
                        if (ins.Operand is System.Type t) {
                            Add(temp, t, AccessType.Read);
                        } else {
                            Add(temp, typeof(System.Array), AccessType.Read);
                        }

                        break;
                    }

                    case var _ when op == OpCodes.Stelem ||
                                    op == OpCodes.Stelem_I || op == OpCodes.Stelem_I1 ||
                                    op == OpCodes.Stelem_I2 || op == OpCodes.Stelem_I4 || op == OpCodes.Stelem_I8 ||
                                    op == OpCodes.Stelem_R4 || op == OpCodes.Stelem_R8 ||
                                    op == OpCodes.Stelem_Ref: {
                        if (ins.Operand is System.Type t) {
                            Add(temp, t, AccessType.Write);
                        } else {
                            Add(temp, typeof(System.Array), AccessType.Write);
                        }

                        break;
                    }

                    case var _ when op == OpCodes.Ldarg || op == OpCodes.Ldarg_S ||
                                    op == OpCodes.Ldarg_0 || op == OpCodes.Ldarg_1 ||
                                    op == OpCodes.Ldarg_2 || op == OpCodes.Ldarg_3 ||
                                    op == OpCodes.Ldarga || op == OpCodes.Ldarga_S: {
                        if (ins.Operand is ParameterInfo p && p.ParameterType.IsByRef) {
                            var elementType = p.ParameterType.GetElementType();
                            Add(temp, elementType, AccessType.Read);
                        }

                        break;
                    }

                    case var _ when op == OpCodes.Starg || op == OpCodes.Starg_S: {
                        if (ins.Operand is ParameterInfo p && p.ParameterType.IsByRef) {
                            var elementType = p.ParameterType.GetElementType();
                            Add(temp, elementType, AccessType.Write);
                        }

                        break;
                    }
                }
            }

            var result = new List<DependencyInfo>();
            foreach (var kv in temp) {
                var set = kv.Value;
                var access = set.Count == 2
                                 ? RefOp.ReadWrite
                                 : set.Contains(AccessType.Read)
                                     ? RefOp.ReadOnly
                                     : RefOp.WriteOnly;

                result.Add(new DependencyInfo(kv.Key, access));
            }

            return result;
        }

        private static void Add(Dictionary<System.Type, HashSet<AccessType>> dict, System.Type type, AccessType access) {
            if (type.IsValueType == false || typeof(IComponentBase).IsAssignableFrom(type) == false || type.IsGenericType == true) return;
            if (!dict.TryGetValue(type, out var set)) {
                set = new HashSet<AccessType>();
                dict[type] = set;
            }

            set.Add(access);
        }

    }

}