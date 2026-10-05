namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Reflection.Emit;

    // Keep this implementation separate from the shared scheduling contracts.
    // Its compiled bodies invalidate only the scheduled-jobs summary partition.
    internal static class ILScheduledJobs {
        internal static HashSet<Type> Collect(MethodInfo root) {
            var found = new HashSet<Type>();
            var pending = new Queue<(MethodBase Method, int Depth)>();
            var visited = new HashSet<MethodBase>();
            var initializers = new HashSet<Type>();
            var instructionCount = 0;
            void Enqueue(MethodBase method, int depth) {
                if (method == null || method.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) || !visited.Add(method)) return;
                if (ILInfrastructure.SkipBody(method)) return;
                if (method.ContainsGenericParameters)
                    throw new InvalidOperationException("Scheduled-job IL traversal requires closed methods: " + method);
                if (visited.Count > 10000 || depth > 128)
                    throw new InvalidOperationException("Scheduled-job IL traversal limit exceeded: " + root);
                if (method.GetMethodBody() != null) pending.Enqueue((method, depth));
            }
            void Initialize(Type type, int depth) {
                if (type != null && initializers.Add(type)) Enqueue(type.TypeInitializer, depth);
            }
            Enqueue(root, 0);
            while (pending.Count != 0) {
                var item = pending.Dequeue();
                CodeGeneratorTimings.Work(item.Method);
                var instructions = ILAnalysisSession.Instructions(item.Method);
                instructionCount = checked(instructionCount + instructions.Length);
                if (instructionCount > 1000000)
                    throw new InvalidOperationException("Scheduled-job IL instruction limit exceeded: " + root);
                var delegates = ILDelegateTargets.Read(item.Method, instructions);
                foreach (var callback in ILFormattingCallbacks.Collect(item.Method, instructions, out _))
                    Enqueue(callback, item.Depth + 1);
                // Includes field/property initializers, base/this constructor calls,
                // catch/filter/finally bodies and exact closed generic constructors.
                for (var index = 0; index < instructions.Length; ++index) {
                    var instruction = instructions[index];
                    if (instruction.Operand is FieldInfo field && field.IsStatic &&
                        (instruction.OpCode == OpCodes.Ldsfld || instruction.OpCode == OpCodes.Ldsflda || instruction.OpCode == OpCodes.Stsfld))
                        Initialize(field.DeclaringType, item.Depth + 1);
                    var called = instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt ||
                        instruction.OpCode == OpCodes.Newobj || instruction.OpCode == OpCodes.Jmp;
                    // Retain potential local delegate targets in this conservative
                    // inventory. A loaded address is NOT evidence it actually runs.
                    if (!called && instruction.OpCode != OpCodes.Ldftn && instruction.OpCode != OpCodes.Ldvirtftn) continue;
                    if (!(instruction.Operand is MethodBase target))
                        throw new InvalidOperationException("Missing scheduled-job IL call target in " + item.Method.DeclaringType?.FullName + "." + item.Method +
                            " at IL_" + instruction.Offset.ToString("x4", CultureInfo.InvariantCulture));
                    if (target.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false)) continue;
                    // A BCL interface slot can dispatch to user code (for example
                    // constrained IDisposable.Dispose emitted by using/finally).
                    // Apply the infrastructure boundary to the resolved body,
                    // not to the interface declaration before binding it.
                    if (instruction.OpCode == OpCodes.Callvirt) target = ILCallTargets.Resolve(instructions, index);
                    if (target.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) || ILInfrastructure.SkipBody(target)) continue;
                    if (called && ILDelegateTargets.IsInvoke(target) && delegates.At(instruction.Offset) is var invocation && invocation.complete) {
                        foreach (var callback in invocation.targets) {
                            if (callback.IsStatic || callback.DeclaringType.IsValueType) Initialize(callback.DeclaringType, item.Depth + 1);
                            Enqueue(callback, item.Depth + 1);
                        }
                        continue;
                    }
                    if (target is MethodInfo method) {
                        if (SourceGeneratorScheduledJobsValidation.IsSchedulingMethod(method)) {
                            var job = method.GetGenericArguments()[0];
                            if (job.ContainsGenericParameters)
                                throw new InvalidOperationException("Scheduled-job IL traversal found an open job: " + job);
                            found.Add(job);
                            // A scheduler terminal registers this job, not any jobs
                            // hidden in its Execute or in Unity's implementation.
                            continue;
                        }
                        if (method.DeclaringType == typeof(Activator) && method.Name == nameof(Activator.CreateInstance) &&
                            method.IsGenericMethod && method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 0) {
                            var constructed = method.GetGenericArguments()[0];
                            Initialize(constructed, item.Depth + 1);
                            Enqueue(constructed.GetConstructor(Type.EmptyTypes), item.Depth + 1);
                            continue;
                        }
                    }
                    if (called && (target.IsStatic || target is ConstructorInfo || target.DeclaringType?.IsValueType == true))
                        Initialize(target.DeclaringType, item.Depth + 1);
                    Enqueue(target, item.Depth + 1);
                }
            }
            // A failed traversal must not publish a partial job set to a caller.
            return found;
        }

    }
}

