using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using ME.BECS.Jobs;

namespace ME.BECS.Tests.GpuPrototype {

    public struct GpuPosition : IComponent { public float value; }
    public struct GpuVelocity : IComponent { public float value; }

    // Deliberately test-only: translates the actual Execute IL, not a second handwritten formula.
    // Only straight-line scalar arithmetic and these two component fields are supported.
    internal static class ScalarJobShader {

        public static FieldInfo[] Uniforms<T>() {
            var fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields) {
                if (field.FieldType != typeof(float)) {
                    throw new NotSupportedException($"GPU prototype requires float job fields: {field.Name}");
                }
            }
            return fields;
        }

        public static string Compile<T>(int repetitions = 1, bool precise = false) where T : struct, IJobForComponents<GpuPosition, GpuVelocity> {
            if (repetitions < 1 || repetitions > 512) {
                throw new ArgumentOutOfRangeException(nameof(repetitions), "Prototype supports 1..512 repetitions within one dispatch.");
            }
            var method = typeof(T).GetMethod("Execute", new[] {
                typeof(JobInfo).MakeByRefType(), typeof(Ent).MakeByRefType(),
                typeof(GpuPosition).MakeByRefType(), typeof(GpuVelocity).MakeByRefType(),
            });
            var body = method?.GetMethodBody();
            if (body == null || body.ExceptionHandlingClauses.Count != 0) {
                throw new NotSupportedException("GPU prototype requires a public Execute body without exception handlers.");
            }
            var uniforms = Uniforms<T>();
            var il = body.GetILAsByteArray();
            var stack = new System.Collections.Generic.Stack<string>();
            var code = new StringBuilder();
            var temporary = 0;
            var returned = false;
            for (var offset = 0; offset < il.Length;) {
                var instructionOffset = offset;
                var op = il[offset++];
                if (op == 0x00) { // nop
                    continue;
                }
                if (returned == true) {
                    throw new NotSupportedException("Instructions after return are unsupported.");
                }
                if (op >= 0x02 && op <= 0x05) { // ldarg.0 .. ldarg.3
                    stack.Push(Argument(op - 0x02));
                } else if (op == 0x0e) { // ldarg.s
                    stack.Push(Argument(il[offset++]));
                } else if (op == 0x25) { // dup
                    stack.Push(stack.Peek());
                } else if (op == 0x22) { // ldc.r4
                    var value = BitConverter.ToSingle(il, offset);
                    offset += 4;
                    if (float.IsNaN(value) == true || float.IsInfinity(value) == true) {
                        throw new NotSupportedException("Nonfinite constants are unsupported.");
                    }
                    stack.Push("float(" + value.ToString("R", CultureInfo.InvariantCulture) + ")");
                } else if (op == 0x4e) { // ldind.r4 (compound assignments may use a field address)
                    var address = stack.Pop();
                    if (address.StartsWith("@field:", StringComparison.Ordinal) == false) {
                        throw new NotSupportedException("Only known scalar field addresses may be read.");
                    }
                    var name = "t" + temporary++;
                    code.AppendLine($"    float {name} = {address.Substring(7)};");
                    stack.Push(name);
                } else if (op == 0x56) { // stind.r4
                    var value = Scalar(stack.Pop());
                    if (stack.Pop() != "@field:p") {
                        throw new NotSupportedException("Only GpuPosition.value may be written.");
                    }
                    code.AppendLine($"    p = {value};");
                } else if (op == 0x7b || op == 0x7c || op == 0x7d) { // ldfld / ldflda / stfld
                    var field = method.Module.ResolveField(BitConverter.ToInt32(il, offset));
                    offset += 4;
                    var value = op == 0x7d ? Scalar(stack.Pop()) : null;
                    var owner = stack.Pop();
                    string expression;
                    if (owner == "@p" && field.DeclaringType == typeof(GpuPosition) && field.Name == "value") {
                        expression = "p";
                    } else if (owner == "@v" && field.DeclaringType == typeof(GpuVelocity) && field.Name == "value") {
                        expression = "v";
                    } else if (owner == "@job" && field.DeclaringType == typeof(T)) {
                        expression = "j" + Array.IndexOf(uniforms, field);
                    } else {
                        throw new NotSupportedException($"Unsupported field access: {field}");
                    }
                    if (op == 0x7c) {
                        stack.Push("@field:" + expression);
                    } else if (op == 0x7d) {
                        if (expression != "p") {
                            throw new NotSupportedException("Only GpuPosition.value may be written.");
                        }
                        code.AppendLine($"    p = {value};");
                    } else {
                        // Snapshot loads immediately: a later store must not change an earlier stack value.
                        var name = "t" + temporary++;
                        code.AppendLine($"    float {name} = {expression};");
                        stack.Push(name);
                    }
                } else if (op >= 0x58 && op <= 0x5b) { // add, sub, mul, div
                    var right = Scalar(stack.Pop());
                    var left = Scalar(stack.Pop());
                    stack.Push($"({left} {new[] { "+", "-", "*", "/" }[op - 0x58]} {right})");
                } else if (op == 0x28) { // call: explicit scalar Unity.Mathematics whitelist only
                    var called = method.Module.ResolveMethod(BitConverter.ToInt32(il, offset)) as MethodInfo;
                    offset += 4;
                    if (called == null || called.IsStatic == false || called.DeclaringType != typeof(Unity.Mathematics.math) || called.ReturnType != typeof(float)) {
                        throw new NotSupportedException($"Unsupported GPU call: {called}");
                    }
                    var arity = called.Name == "sin" || called.Name == "cos" || called.Name == "sqrt" || called.Name == "abs" ? 1
                        : called.Name == "min" || called.Name == "max" ? 2 : called.Name == "clamp" ? 3 : 0;
                    var parameters = called.GetParameters();
                    if (arity == 0 || parameters.Length != arity || Array.Exists(parameters, static parameter => parameter.ParameterType != typeof(float)) == true) {
                        throw new NotSupportedException($"Unsupported GPU intrinsic: {called}");
                    }
                    var arguments = new string[arity];
                    for (var i = arity - 1; i >= 0; --i) {
                        arguments[i] = Scalar(stack.Pop());
                    }
                    // Snapshot the call result before any later component stores.
                    var name = "t" + temporary++;
                    code.AppendLine($"    float {name} = {called.Name}({string.Join(", ", arguments)});");
                    stack.Push(name);
                } else if (op == 0x65) { // neg
                    stack.Push("(-" + Scalar(stack.Pop()) + ")");
                } else if (op == 0x2a && stack.Count == 0) { // ret
                    returned = true;
                } else {
                    throw new NotSupportedException($"GPU prototype rejects IL 0x{op:X2} at IL_{instructionOffset:X4} in {typeof(T).Name}. Non-whitelisted calls, branches, locals and entity access are unsupported.");
                }
            }
            if (returned == false) {
                throw new NotSupportedException("Execute has no terminal return.");
            }
            var shader = new StringBuilder("#pragma kernel Main\nRWStructuredBuffer<float> Positions;\nStructuredBuffer<float> Velocities;\nuint Count;\n");
            for (var i = 0; i < uniforms.Length; ++i) {
                shader.AppendLine($"float j{i};");
            }
            shader.AppendLine("[numthreads(64, 1, 1)]\nvoid Main(uint3 id : SV_DispatchThreadID) {\n    if (id.x >= Count) return;\n    float p = Positions[id.x];\n    float v = Velocities[id.x];");
            if (repetitions > 1) {
                shader.AppendLine($"    [loop] for (uint iteration = 0; iteration < {repetitions}; ++iteration) {{");
            }
            shader.Append(code);
            if (repetitions > 1) {
                shader.AppendLine("    }");
            }
            shader.AppendLine("    Positions[id.x] = p;\n}");
            var result = shader.ToString();
            if (precise == true) {
                result = result.Replace("#pragma kernel Main", "#pragma kernel Main\n#pragma disable_fastmath");
                result = System.Text.RegularExpressions.Regex.Replace(result, @"(?m)^(    )float ([ptv][0-9]*)( =)", "$1precise float $2$3");
            }
            return result;
        }

        private static string Argument(int index) {
            switch (index) {
                case 0: return "@job";
                case 3: return "@p";
                case 4: return "@v";
                default: throw new NotSupportedException("JobInfo/Ent access is unsupported on the GPU prototype.");
            }
        }

        private static string Scalar(string expression) {
            if (expression.StartsWith("@", StringComparison.Ordinal) == true) {
                throw new NotSupportedException("Component/job addresses cannot be used as scalar values.");
            }
            return expression;
        }
    }
}
