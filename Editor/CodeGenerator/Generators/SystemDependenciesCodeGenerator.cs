using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;
using ME.BECS.Editor.Jobs;

namespace ME.BECS.Editor.Systems {

    /// <summary>
    /// Exports system dependencies registration data for generated code.
    /// </summary>
    public class SystemDependenciesCodeGenerator : CustomCodeGenerator {
        /// <summary>
        /// Whether cache compiled inputs behavior or state is selected.
        /// </summary>
        public override bool CacheCompiledInputs => this.GetType() == typeof(SystemDependenciesCodeGenerator);
        private readonly SourceGeneratorSystemDependencies sourceDependencies = new SourceGeneratorSystemDependencies();

        private readonly System.Collections.Generic.HashSet<System.Type> sourceReferences = new System.Collections.Generic.HashSet<System.Type>();

        /// <summary>
        /// Adds the assembly references required by this feature's generated code.
        /// </summary>
        public override void AddSourceGeneratorReferences(System.Collections.Generic.List<System.Type> references) =>
            references.AddRange(this.sourceReferences.OrderBy(type => type.AssemblyQualifiedName, System.StringComparer.Ordinal));

        private sealed class SourcePlan {
            public System.Type system;
            public readonly System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> operations = new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
            public readonly System.Collections.Generic.List<MethodInfoDependencies.Error> errors = new System.Collections.Generic.List<MethodInfoDependencies.Error>();
        }

        internal System.Action<int, int> analysisProgress;
        internal void PrepareAnalysis() {
            var selected = this.systems.Distinct().Where(type => type.IsValueType && type.IsVisible).ToArray();
            var completed = 0;
            this.analysisProgress?.Invoke(0, selected.Length * 4);
            foreach (var system in selected) {
                if (!system.IsValueType || !system.IsVisible) continue;
                foreach (var name in new[] { "OnUpdate", "OnAwake", "OnStart", "OnDestroy" }) {
                    ILAnalysisSession.Checkpoint();
                    this.GetLegacyDeps(SourceGeneratorScheduledJobsValidation.GetLifecycleMethod(system, name));
                    this.analysisProgress?.Invoke(++completed, selected.Length * 4);
                }
            }
        }

        /// <summary>
        /// Adds this feature's registration inputs to the source-generator export.
        /// </summary>
        public override void AppendSourceGeneratorInputs(System.Text.StringBuilder manifest) {
            this.sourceReferences.Clear();
            if (!this.editorAssembly) return;
            var plans = new System.Collections.Generic.Dictionary<System.Type, SourcePlan>();
            foreach (var candidate in this.systems) {
                if (!candidate.IsValueType || !candidate.IsVisible) continue;
                // The manifest supplies the fully expanded selection. Choosing an
                // arbitrary first constraint implementation loses other specializations.
                var system = candidate;
                if (system.ContainsGenericParameters)
                    throw new System.InvalidOperationException("System dependency analysis requires a closed specialization: " + system.AssemblyQualifiedName);
                if (plans.ContainsKey(system)) continue;
                CodeGeneratorTimings.Subject(system.FullName);
                var plan = new SourcePlan { system = system };
                foreach (var name in new[] { "OnUpdate", "OnAwake", "OnStart", "OnDestroy" }) {
                    var method = SourceGeneratorScheduledJobsValidation.GetLifecycleMethod(system, name);
                    if (method == null) continue;
                    var deps = this.sourceDependencies.SelectForExport(method, GetLegacyDeps, out _, out _);
                    if (deps.ops != null) plan.operations.UnionWith(deps.ops);
                    if (deps.errors != null) plan.errors.AddRange(deps.errors);
                }
                JobsEarlyInitCodeGenerator.UpdateDeps(plan.operations);
                plans.Add(system, plan);
                foreach (var operation in plan.operations) this.sourceReferences.Add(operation.type);
            }
            manifest.Append("system-dependencies-schema\t0\tdjI=\n");
            var ordinal = 0;
            var exports = plans.Values.Select(node => (owner: node.system, members: new[] { node }))
                .Concat(plans.Values.Where(node => node.system.IsGenericType).GroupBy(node => node.system.GetGenericTypeDefinition())
                    .Select(group => (owner: group.Key, members: group.OrderBy(node => node.system.AssemblyQualifiedName, System.StringComparer.Ordinal).ToArray())))
                .OrderBy(item => item.owner.AssemblyQualifiedName, System.StringComparer.Ordinal);
            foreach (var entry in exports) {
                var owner = entry.owner;
                this.sourceReferences.Add(owner);
                var payload = new System.Text.StringBuilder("v2\n").Append(owner.AssemblyQualifiedName);
                if (owner.IsGenericTypeDefinition) {
                    foreach (var member in entry.members) payload.Append("\nM\t").Append(member.system.AssemblyQualifiedName);
                } else {
                    var plan = entry.members.Single();
                    payload.Append("\nS\toperations\til")
                        .Append("\nS\tsynchronization\til");
                    foreach (var op in plan.operations.OrderBy(item => item.type.AssemblyQualifiedName, System.StringComparer.Ordinal))
                        payload.Append("\nC\t").Append(((byte)op.op).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t').Append(op.type.AssemblyQualifiedName);
                    foreach (var error in plan.errors.GroupBy(item => (item.code, message: item.GetDisplayMessage())).Select(group => group.First()))
                        payload.Append("\nE\t").Append(((int)error.code).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                            .Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(error.GetDisplayMessage())));
                }
                manifest.Append("system-dependencies\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                    .Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload.ToString()))).Append('\n');
            }
        }

        /// <summary>
        /// Defines the graph structure used for graph.
        /// </summary>
        public class Graph {

            /// <summary>
            /// Defines a node entry in the associated graph.
            /// </summary>
            public class Node {

                /// <summary>
                /// System instance used by the associated operation.
                /// </summary>
                public System.Type system;
                /// <summary>
                /// Dependencies used by <c>SystemDependenciesCodeGenerator.Graph.Node</c>.
                /// </summary>
                public System.Collections.Generic.List<System.Type> dependencies;
                /// <summary>
                /// Inputs used by <c>SystemDependenciesCodeGenerator.Graph.Node</c>.
                /// </summary>
                public System.Collections.Generic.List<System.Type> inputs;
                /// <summary>
                /// Outputs used by <c>SystemDependenciesCodeGenerator.Graph.Node</c>.
                /// </summary>
                public System.Collections.Generic.List<System.Type> outputs;
                /// <summary>
                /// Diagnostics produced while validating or processing the inputs.
                /// </summary>
                [System.NonSerializedAttribute]
                public System.Collections.Generic.List<MethodInfoDependencies.Error> errors; 

                /// <summary>
                /// Formats this value for display or diagnostics.
                /// </summary>
                public override string ToString() {
                    return "// " + this.system.FullName + "\n// |------ " + string.Join("\n// |------ ", this.dependencies.Select(x => x.ToString()).Distinct().OrderBy(x => x).ToArray());
                }

                /// <summary>
                /// Tests whether at least one requested value is present.
                /// </summary>
                public bool ContainsAny(System.Collections.Generic.List<System.Type> types) {
                    for (int i = 0; i < this.outputs.Count; ++i) {
                        for (int j = 0; j < types.Count; ++j) {
                            if (this.outputs[i] == types[j]) return true;
                        }
                    }
                    return false;
                }

            }

            /// <summary>
            /// Nodes composing the associated graph.
            /// </summary>
            public Node[] nodes;

            /// <summary>
            /// Initializes <c>Graph</c> from the supplied nodes.
            /// </summary>
            public Graph(System.Collections.Generic.Dictionary<System.Type, Graph.Node> nodes) {
                this.nodes = nodes.Select(x => x.Value).ToArray();
                // find dependencies for each node
                // node has dependency if current node's inputs contained in any outputs
                foreach (var node in this.nodes) {
                    if (node.inputs.Count > 0) {
                        var arr = nodes.Where(x => x.Value != node && x.Value.ContainsAny(node.inputs) == true).Select(x => x.Value.system).ToArray();
                        node.dependencies.AddRange(arr);
                    }
                    node.dependencies = node.dependencies.OrderBy(x => x.FullName).ToList();
                }
            }

        }

        /// <summary>
        /// Defines method info dependencies state and operations for <c>SystemDependenciesCodeGenerator</c>.
        /// </summary>
        public struct MethodInfoDependencies {

            /// <summary>
            /// Defines error state and operations for <c>SystemDependenciesCodeGenerator.MethodInfoDependencies</c>.
            /// </summary>
            public struct Error : System.IEquatable<Error> {

                /// <summary>
                /// Defines the supported code values.
                /// </summary>
                public enum Code {

                    /// <summary>
                    /// Method call required option for <c>SystemDependenciesCodeGenerator.MethodInfoDependencies.Error.Code</c>.
                    /// </summary>
                    MethodCallRequired,
                    /// <summary>
                    /// Method not required option for <c>SystemDependenciesCodeGenerator.MethodInfoDependencies.Error.Code</c>.
                    /// </summary>
                    MethodNotRequired,

                }

                /// <summary>
                /// Code used by <c>SystemDependenciesCodeGenerator.MethodInfoDependencies.Error</c>.
                /// </summary>
                public Code code;
                /// <summary>
                /// Caller method info used by <c>SystemDependenciesCodeGenerator.MethodInfoDependencies.Error</c>.
                /// </summary>
                public MethodInfo callerMethodInfo;
                /// <summary>
                /// Message used by <c>SystemDependenciesCodeGenerator.MethodInfoDependencies.Error</c>.
                /// </summary>
                public string message;
                
                /// <summary>
                /// Returns display message.
                /// </summary>
                public string GetDisplayMessage() {
                    if (!string.IsNullOrEmpty(this.message)) return this.message;
                    string msg = string.Empty;
                    var methodName = this.callerMethodInfo?.Name ?? "<unknown>";
                    if (this.code == Code.MethodCallRequired) {
                        msg = $"Method {methodName} may access component data while work is still pending. Review synchronization; this advisory analysis may miss existing guarantees.";
                    } else if (this.code == Code.MethodNotRequired) {
                        msg = $"Method {methodName} contains a Complete() call with no component access recognized by this analysis. The call may still synchronize other effects.";
                    }

                    return msg;
                }

                /// <summary>
                /// Tests equality using the identity or value comparison defined by this type.
                /// </summary>
                public bool Equals(Error other) {
                    return this.code == other.code && Equals(this.callerMethodInfo, other.callerMethodInfo) &&
                        System.StringComparer.Ordinal.Equals(this.message, other.message);
                }

                /// <summary>
                /// Tests equality using the identity or value comparison defined by this type.
                /// </summary>
                public override bool Equals(object obj) {
                    return obj is Error other && this.Equals(other);
                }

                /// <summary>
                /// Returns a hash code consistent with this type's equality comparison.
                /// </summary>
                public override int GetHashCode() {
                    return System.HashCode.Combine((int)this.code, this.callerMethodInfo, this.message);
                }

            }

            /// <summary>
            /// Ops used by <c>SystemDependenciesCodeGenerator.MethodInfoDependencies</c>.
            /// </summary>
            public System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> ops;
            /// <summary>
            /// Diagnostics produced while validating or processing the inputs.
            /// </summary>
            public System.Collections.Generic.List<Error> errors;

            /// <summary>
            /// Initializes <c>MethodInfoDependencies</c> from the supplied types.
            /// </summary>
            public MethodInfoDependencies(System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> types) {
                this.ops = new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
                this.errors = new System.Collections.Generic.List<Error>();
                foreach (var item in types) {
                    this.ops.Add(item);
                }
            }

            /// <summary>
            /// Adds error.
            /// </summary>
            public void AddError(Error error) {
                this.errors.Add(error);
            }

            /// <summary>
            /// Returns inputs.
            /// </summary>
            public System.Collections.Generic.List<System.Type> GetInputs() {
                return this.ops.Where(x => x.op == RefOp.ReadOnly || x.op == RefOp.ReadWrite).Select(x => x.type).OrderBy(x => x.FullName).ToList();
            }

            /// <summary>
            /// Returns outputs.
            /// </summary>
            public System.Collections.Generic.List<System.Type> GetOutputs() {
                return this.ops.Where(x => (x.op == RefOp.WriteOnly || x.op == RefOp.ReadWrite) && typeof(ISystem).IsAssignableFrom(x.type) == false).Select(x => x.type).OrderBy(x => x.FullName).ToList();
            }

            /// <summary>
            /// Returns dependencies.
            /// </summary>
            public System.Collections.Generic.List<System.Type> GetDependencies() {
                return this.ops.Where(x => typeof(ISystem).IsAssignableFrom(x.type) == true).Select(x => x.type).Distinct().OrderBy(x => x.FullName).ToList();
            }

        }

        // Comparison runs the production IL operation inventory independently of
        // diagnostic source catalogs and synchronization selection.
        internal MethodInfoDependencies GetComparisonDependencies(MethodInfo root) => this.GetLegacyDeps(root);

        internal MethodInfoDependencies GetComparisonAnalysis(MethodInfo root, out string[] unresolvedDispatch) {
            var diagnostics = new ILDispatchDiagnostics();
            var result = this.GetLegacyDepsCore(root, diagnostics);
            unresolvedDispatch = diagnostics.GetIssues();
            return result;
        }

        private MethodInfoDependencies GetLegacyDeps(MethodInfo root) {
            if (root == null) return default;
            var snapshot = ILAnalysisSession.Get((typeof(MethodInfoDependencies), root), () =>
                ILPersistentAnalysis.Get("system-dependencies", ILPersistentAnalysis.MethodIdentity(root), () => this.GetLegacyDepsCore(root, null),
                    EncodeDependencies, DecodeDependencies));
            return new MethodInfoDependencies(snapshot.ops) {
                errors = new System.Collections.Generic.List<MethodInfoDependencies.Error>(snapshot.errors),
            };
        }

        [System.Serializable] private sealed class DependencyErrorData {
            public int code;
            public string message;
            public bool hasCaller;
            public ILContentFingerprint.MethodReference caller;
        }
        [System.Serializable] private sealed class DependenciesData {
            public ILSummaryData.Accesses accesses;
            public DependencyErrorData[] errors;
        }
        private static DependenciesData EncodeDependencies(MethodInfoDependencies value) => new DependenciesData {
            accesses = ILSummaryData.Encode(value.ops), errors = value.errors.Select(error => new DependencyErrorData {
                code = (int)error.code, message = error.message, hasCaller = error.callerMethodInfo != null,
                caller = error.callerMethodInfo == null ? null : ILContentFingerprint.MethodReference.From(error.callerMethodInfo),
            }).ToArray(),
        };
        private static MethodInfoDependencies DecodeDependencies(DependenciesData data) {
            if (data?.errors == null) throw new System.FormatException("Missing cached system dependencies.");
            var value = new MethodInfoDependencies(ILSummaryData.Decode(data.accesses));
            foreach (var error in data.errors) value.errors.Add(new MethodInfoDependencies.Error {
                code = (MethodInfoDependencies.Error.Code)error.code, message = error.message,
                callerMethodInfo = !error.hasCaller ? null : (ILPersistentAnalysis.ResolveMethod(error.caller) as MethodInfo
                    ?? throw new System.MissingMethodException("Cached dependency caller no longer exists.")),
            });
            return value;
        }

        private MethodInfoDependencies GetLegacyDepsCore(MethodInfo root, ILDispatchDiagnostics diagnostics) {

            if (root == null) return default;

            var errors = new System.Collections.Generic.List<MethodInfoDependencies.Error>();
            
            var completeHandleMethod = typeof(Unity.Jobs.JobHandle).GetMethod(nameof(Unity.Jobs.JobHandle.Complete));
            var getSystemMethod = typeof(SystemsWorldExt).GetMethod(nameof(SystemsWorldExt.GetSystemPtr));
            var presence = new ILQueryPresence();
            
            var uniqueTypes = new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
            var bodies = new System.Collections.Generic.Dictionary<MethodBase, Instruction[]>();
            var scheduled = new System.Collections.Generic.List<(MethodBase body, int offset, JobsEarlyInitCodeGenerator.TypeInfo[] accesses)>();
            var jobAccesses = new System.Collections.Generic.Dictionary<(System.Type job, System.Type contract), JobsEarlyInitCodeGenerator.TypeInfo[]>();
            var q = new System.Collections.Generic.Queue<(MethodBase method, int depth)>();
            var methodCallRequired = false;
            var hasCompleteHandle = false;
            var hasInterestInstructions = false;
            var visited = new System.Collections.Generic.HashSet<MethodBase>();
            var initializers = new System.Collections.Generic.HashSet<System.Type>();
            var instructionCount = 0;
            void Enqueue(MethodBase method, int depth) {
                if (method == null || method.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) || !visited.Add(method)) return;
                if (ILInfrastructure.SkipBody(method)) return;
                if (method.ContainsGenericParameters) throw new System.InvalidOperationException("System dependency IL requires closed methods: " + method);
                if (visited.Count > 10000 || depth > 128) throw new System.InvalidOperationException("System dependency IL traversal limit exceeded: " + root);
                if (method.GetMethodBody() != null) q.Enqueue((method, depth));
            }
            void Initialize(System.Type type, int depth) {
                if (type != null && initializers.Add(type)) Enqueue(type.TypeInitializer, depth);
            }
            Enqueue(root, 0);
            while (q.Count > 0) {
                var item = q.Dequeue();
                var body = item.method;
                CodeGeneratorTimings.Work(body);
                var instructions = ILAnalysisSession.Instructions(body);
                bodies.Add(body, instructions);
                instructionCount = checked(instructionCount + instructions.Length);
                if (instructionCount > 1000000) throw new System.InvalidOperationException("System dependency IL instruction limit exceeded: " + root);
                // Initializers are possible effects, not an assumption that they
                // execute on every invocation. They get unbound flow contexts.
                Initialize(body.DeclaringType, item.depth + 1);
                // Callback roots have no literal incoming call instruction from
                // which to collect their own field/safety accesses.
                uniqueTypes.UnionWith(JobsEarlyInitCodeGenerator.GetBodyTypesInfo(body, traverseHierarchy: false, methodParameters: false));
                var callbacks = ILFormattingCallbacks.Collect(body, instructions, out var unknownFormatting);
                foreach (var callback in callbacks)
                    Enqueue(callback, item.depth + 1);
                var delegates = ILDelegateTargets.Read(body, instructions);
                diagnostics?.Read(body, instructions, unknownFormatting, delegates);
                for (var index = 0; index < instructions.Length; ++index) {
                    var inst = instructions[index];
                    var continueTraverse = true;
                    if (inst.Operand is FieldInfo field && field.IsStatic &&
                        (inst.OpCode == System.Reflection.Emit.OpCodes.Ldsfld || inst.OpCode == System.Reflection.Emit.OpCodes.Ldsflda || inst.OpCode == System.Reflection.Emit.OpCodes.Stsfld))
                        Initialize(field.DeclaringType, item.depth + 1);
                    var target = ILCallTargets.Resolve(instructions, index);
                    if (target == null) continue;
                    if (ILDelegateTargets.IsInvoke(target) && delegates.At(inst.Offset) is var invocation && invocation.complete) {
                        // Receiver/argument value flow remains independent. These
                        // roots enter the conservative query-mode inventory.
                        foreach (var callback in invocation.targets) Enqueue(callback, item.depth + 1);
                        continue;
                    }
                    if (ILInfrastructure.SkipBody(target)) continue;
                    if (ILCallTargets.TryConstruction(target, out var constructed, out var constructor)) {
                        Initialize(constructed, item.depth + 1);
                        Enqueue(constructor, item.depth + 1);
                        continue;
                    }
                    if (target is MethodInfo methodInfo) {
                        if (hasCompleteHandle == false && hasInterestInstructions == false && body == root) {
                            // search for Complete
                            if (IsMethod(methodInfo, completeHandleMethod) == true) {
                                hasCompleteHandle = true;
                            }
                        }
                        if (IsMethod(methodInfo, getSystemMethod) == true) {
                            hasInterestInstructions = true;
                            uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                type = methodInfo.GetGenericArguments()[0],
                                op = RefOp.ReadWrite,
                            });
                            continueTraverse = false;
                        } else if (presence.TryFilter(methodInfo, out var components)) {
                            hasInterestInstructions = true;
                            foreach (var component in components) {
                                uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                    type = component,
                                    op = RefOp.ReadOnly,
                                });
                            }
                            continueTraverse = false;
                        } else if (SourceGeneratorScheduledJobsValidation.IsSchedulingMethod(methodInfo)) {
                            hasInterestInstructions = true;
                            // A scheduler is a terminal: analyze the selected job,
                            // not internal scheduling helpers or unrelated Execute bodies.
                            continueTraverse = false;
                            foreach (var component in presence.Scheduled(methodInfo))
                                uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo { type = component, op = RefOp.ReadOnly });
                            // CodeGeneratorIgnore on a known low-level scheduler
                            // hides its implementation, not the scheduled job's effects.
                            var jobType = methodInfo.GetGenericArguments()[0];
                            var jobContract = ILJobScheduleContract.GetWorkInterface(methodInfo);
                            if (!jobAccesses.TryGetValue((jobType, jobContract), out var info)) {
                                var jobRoot = ILJobScheduleContract.GetExecuteMethod(jobType, jobContract);
                                var accesses = new System.Collections.Generic.List<JobsEarlyInitCodeGenerator.TypeInfo>(JobsEarlyInitCodeGenerator.GetMethodTypesInfo(jobRoot));
                                // Compatibility safety merging retains one isArg
                                // flag per component/op, erasing overlapping body
                                // accesses. Project query arguments first, then
                                // union independent accesses without narrowing.
                                accesses.AddRange(JobsEarlyInitCodeGenerator.GetMethodTypesInfo(jobRoot, methodParameters: false));
                                jobAccesses.Add((jobType, jobContract), info = accesses.ToArray());
                            }
                            scheduled.Add((body, inst.Offset, info));
                        } else {
                            if (methodInfo.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
                                var directSafety = methodInfo.GetCustomAttribute<SafetyCheckAttribute>();
                                System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> info;
                                if (directSafety != null && methodInfo.IsGenericMethod &&
                                    typeof(IComponentBase).IsAssignableFrom(methodInfo.GetGenericArguments()[0])) {
                                    // Match the job IL analyzer's explicit safety
                                    // contract: collect the declared access, not
                                    // the allocator/validation internals behind it.
                                    info = new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> {
                                        new JobsEarlyInitCodeGenerator.TypeInfo { type = methodInfo.GetGenericArguments()[0], op = directSafety.Op },
                                    };
                                    continueTraverse = false;
                                } else info = methodInfo.GetMethodBody() != null ? JobsEarlyInitCodeGenerator.GetMethodTypesInfo(methodInfo, false) :
                                    new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
                                if (info.Count > 0) {
                                    hasInterestInstructions = true;
                                    // Check if complete method exists
                                    if (hasCompleteHandle == false) {
                                        methodCallRequired = true;
                                    }
                                }
                                foreach (var typeInfo in info) {
                                    uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                        type = typeInfo.type,
                                        op = typeInfo.op,
                                    });
                                    //continueTraverse = false;
                                }
                            }
                        }
                    }
                    
                    if (continueTraverse && (inst.OpCode == System.Reflection.Emit.OpCodes.Call || inst.OpCode == System.Reflection.Emit.OpCodes.Callvirt ||
                        inst.OpCode == System.Reflection.Emit.OpCodes.Newobj || inst.OpCode == System.Reflection.Emit.OpCodes.Jmp ||
                        inst.OpCode == System.Reflection.Emit.OpCodes.Ldftn || inst.OpCode == System.Reflection.Emit.OpCodes.Ldvirtftn)) {
                        if (!target.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) &&
                            inst.OpCode != System.Reflection.Emit.OpCodes.Ldftn && inst.OpCode != System.Reflection.Emit.OpCodes.Ldvirtftn &&
                            (target.IsStatic || target is ConstructorInfo || target.DeclaringType?.IsValueType == true))
                            Initialize(target.DeclaringType, item.depth + 1);
                        Enqueue(target, item.depth + 1);
                    }
                }
            }

            // Bind every invocation before consuming modes: the same helper can
            // receive readonly and writable queries from different call sites.
            // A traversal-order-dependent first result must never win that union.
            if (scheduled.Count > 0) {
                var modes = ILQueryScheduleModes.ReadBodyGraph(root, bodies);
                foreach (var call in scheduled) {
                    if (!modes[call.body].TryGetValue(call.offset, out var mode)) mode = ILQueryScheduleModes.Mode.Unknown;
                    foreach (var typeInfo in call.accesses)
                        uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo {
                            type = typeInfo.type,
                            op = ILQueryScheduleModes.Apply(mode, typeInfo.op, typeInfo.isArg),
                        });
                }
            }

            if (methodCallRequired == true) {
                // Add error
                var err = new MethodInfoDependencies.Error() {
                    callerMethodInfo = root,
                    code = MethodInfoDependencies.Error.Code.MethodCallRequired,
                };
                errors.Add(err);
            }

            // Absence of component access does not make Complete unnecessary:
            // it can synchronize native containers or other side effects. This
            // compatibility walk cannot prove that removing it is safe.
            
            JobsEarlyInitCodeGenerator.UpdateDeps(uniqueTypes);
            var deps = new MethodInfoDependencies(uniqueTypes);
            foreach (var err in errors) deps.AddError(err);
            return deps;
        }

        private static bool IsMethod(MethodInfo method1, MethodInfo method2) {
            return method1.MetadataToken == method2.MetadataToken && method1.Module == method2.Module && method1.DeclaringType == method2.DeclaringType;
        }

        /// <summary>
        /// Defines used objects state and operations for <c>SystemDependenciesCodeGenerator</c>.
        /// </summary>
        public struct UsedObjects {

            /// <summary>
            /// Systems used by <c>SystemDependenciesCodeGenerator.UsedObjects</c>.
            /// </summary>
            public System.Collections.Generic.List<System.Type> systems;
            /// <summary>
            /// Component storage or descriptors used by this operation.
            /// </summary>
            public System.Collections.Generic.List<System.Type> components; 
            /// <summary>
            /// Components group used by <c>SystemDependenciesCodeGenerator.UsedObjects</c>.
            /// </summary>
            public System.Collections.Generic.List<System.Type> componentsGroup;
            /// <summary>
            /// Job types used by <c>SystemDependenciesCodeGenerator.UsedObjects</c>.
            /// </summary>
            public System.Collections.Generic.List<System.Type> jobTypes;
            /// <summary>
            /// Entity types used by <c>SystemDependenciesCodeGenerator.UsedObjects</c>.
            /// </summary>
            public System.Collections.Generic.List<System.Type> entityTypes;
            /// <summary>
            /// Aspect descriptors used by this operation.
            /// </summary>
            public System.Collections.Generic.List<System.Type> aspects;

        }

        /// <summary>
        /// Returns used objects.
        /// </summary>
        public static void GetUsedObjects(bool editorAssembly, out UsedObjects usedObjects) {
            // Editor registers all declared types; source declaration catalogs are
            // appropriate there. Runtime reachability comes from compiled IL.
            GetUsedObjects(editorAssembly, out usedObjects, useSourceCatalogs: editorAssembly);
        }

        internal static void GetUsedObjects(bool editorAssembly, out UsedObjects usedObjects, bool useSourceCatalogs) {
            if (!editorAssembly) {
                usedObjects = AnalyzeRuntimeDiscovery(CaptureRuntimeDiscovery(), useSourceCatalogs);
                return;
            }
            var systemsSet = new System.Collections.Generic.HashSet<System.Type>(10);
            var componentsSet = new System.Collections.Generic.HashSet<System.Type>(10);
            var jobTypesSet = new System.Collections.Generic.HashSet<System.Type>(10);
            var entityTypesSet = new System.Collections.Generic.HashSet<System.Type>(10);
            var aspectsSet = new System.Collections.Generic.HashSet<System.Type>(10);
            AddAllEditorTypes(systemsSet, componentsSet, jobTypesSet, entityTypesSet, aspectsSet, useSourceCatalogs);
            usedObjects = FinishDiscovery(systemsSet, componentsSet, jobTypesSet, entityTypesSet, aspectsSet);
        }

        // Capture Unity assets on the Editor thread. The analysis below keeps no
        // ScriptableObject, graph node, system instance or config value alive and
        // cannot observe an asset changing halfway through its IL traversal.
        internal sealed class RuntimeDiscoveryInputs {
            internal readonly System.Type[] includes, modules, systems, components, aspects;
            internal RuntimeDiscoveryInputs(System.Type[] includes, System.Type[] modules, System.Type[] systems,
                System.Type[] components, System.Type[] aspects) {
                this.includes = (System.Type[])includes.Clone();
                this.modules = (System.Type[])modules.Clone();
                this.systems = (System.Type[])systems.Clone();
                this.components = (System.Type[])components.Clone();
                this.aspects = (System.Type[])aspects.Clone();
            }
        }

        internal static RuntimeDiscoveryInputs CaptureRuntimeDiscovery() {
            var includes = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm => asm.GetCustomAttributes<CodeGeneratorInclude>()).Select(attribute => attribute.type).ToArray();
            var modules = UnityEditor.TypeCache.GetTypesDerivedFrom<Module>().ToArray();
            var systems = new System.Collections.Generic.List<System.Type>();
            var components = new System.Collections.Generic.List<System.Type>();
            var aspects = new System.Collections.Generic.List<System.Type>();

            var guids = UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph");
            // Discovery collects a set of types, not execution occurrences. Shared or
            // cyclic subgraphs must not cause repeated/unbounded traversal.
            var discoveredGraphNodes = new System.Collections.Generic.HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode>();
            foreach (var guid in guids) {
                var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<ME.BECS.FeaturesGraph.SystemsGraph>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (graph.isInnerGraph == true) continue;
                var nodes = graph.nodes.ToList();
                var q = new System.Collections.Generic.Queue<ME.BECS.Extensions.GraphProcessor.BaseNode>(nodes);
                while (q.Count > 0) {
                    var node = q.Dequeue();
                    if (node == null || !discoveredGraphNodes.Add(node)) continue;
                    if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode) {
                        if (systemNode.system != null) systems.Add(systemNode.system.GetType());
                    } else if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                        foreach (var n in graphNode.graphValue.nodes) {
                            q.Enqueue(n);
                        }
                    }
                }
            }

            guids = UnityEditor.AssetDatabase.FindAssets("t:EntityConfig");
            foreach (var guid in guids) {
                var config = UnityEditor.AssetDatabase.LoadAssetAtPath<EntityConfig>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                foreach (var component in config.data.components) {
                    components.Add(component.GetType());
                }
                foreach (var component in config.staticData.components) {
                    components.Add(component.GetType());
                }
                foreach (var component in config.sharedData.components) {
                    components.Add(component.GetType());
                }
                foreach (var component in config.aspects.components) {
                    aspects.Add(component.GetType());
                }
            }
            return new RuntimeDiscoveryInputs(includes, modules, systems.ToArray(), components.ToArray(), aspects.ToArray());
        }

        internal static UsedObjects AnalyzeRuntimeDiscovery(RuntimeDiscoveryInputs input, bool useSourceCatalogs = false) {
            return AnalyzeRuntimeDiscoveryWithProgress(input, useSourceCatalogs, null);
        }

        internal static UsedObjects AnalyzeRuntimeDiscoveryWithProgress(RuntimeDiscoveryInputs input, bool useSourceCatalogs, System.Action<int, int> progress) {
            var completed = 0;
            var total = input.modules.Length + input.systems.Length + 1;
            var systemsSet = new System.Collections.Generic.HashSet<System.Type>(10);
            var componentsSet = new System.Collections.Generic.HashSet<System.Type>(10);
            var jobTypesSet = new System.Collections.Generic.HashSet<System.Type>(10);
            var entityTypesSet = new System.Collections.Generic.HashSet<System.Type>(10);
            var aspectsSet = new System.Collections.Generic.HashSet<System.Type>(10);
            var lookup = new UsedObjectsLookup(useSourceCatalogs);
            foreach (var type in input.includes) {
                if (typeof(IComponentBase).IsAssignableFrom(type)) componentsSet.Add(type);
                else if (typeof(IAspect).IsAssignableFrom(type)) aspectsSet.Add(type);
                else if (typeof(IEntityType).IsAssignableFrom(type)) entityTypesSet.Add(type);
                else if (typeof(ISystem).IsAssignableFrom(type)) systemsSet.Add(type);
            }
            progress?.Invoke(0, total);
            foreach (var module in input.modules) {
                if (module.IsAbstract || module.ContainsGenericParameters) { progress?.Invoke(++completed, total); continue; }
                lookup.AddMethod(module, nameof(Module.OnAwake), systemsSet, componentsSet, jobTypesSet, entityTypesSet, aspectsSet);
                lookup.AddMethod(module, nameof(Module.OnStart), systemsSet, componentsSet, jobTypesSet, entityTypesSet, aspectsSet);
                lookup.AddMethod(module, nameof(Module.OnUpdate), systemsSet, componentsSet, jobTypesSet, entityTypesSet, aspectsSet);
                lookup.AddMethod(module, nameof(Module.DoDestroy), systemsSet, componentsSet, jobTypesSet, entityTypesSet, aspectsSet);
                progress?.Invoke(++completed, total);
            }
            foreach (var type in input.systems) {
                lookup.LookUp(type, systemsSet, componentsSet, jobTypesSet, entityTypesSet, aspectsSet);
                progress?.Invoke(++completed, total);
            }
            componentsSet.UnionWith(input.components);
            aspectsSet.UnionWith(input.aspects);
            lookup.LookUpComponents(systemsSet, componentsSet, jobTypesSet, entityTypesSet, aspectsSet);
            progress?.Invoke(++completed, total);
            return FinishDiscovery(systemsSet, componentsSet, jobTypesSet, entityTypesSet, aspectsSet);
        }

        private static UsedObjects FinishDiscovery(System.Collections.Generic.HashSet<System.Type> systemsSet,
            System.Collections.Generic.HashSet<System.Type> componentsSet, System.Collections.Generic.HashSet<System.Type> jobTypesSet,
            System.Collections.Generic.HashSet<System.Type> entityTypesSet, System.Collections.Generic.HashSet<System.Type> aspectsSet) {
            var usedObjects = new UsedObjects();
            usedObjects.jobTypes = jobTypesSet.OrderBy(x => x.FullName).ToList();
            usedObjects.systems = systemsSet.OrderBy(x => x.FullName).ToList();
            usedObjects.components = componentsSet.OrderBy(x => x.FullName).ToList();
            usedObjects.entityTypes = entityTypesSet.OrderBy(x => x.FullName).ToList();
            usedObjects.aspects = aspectsSet.OrderBy(x => x.FullName).ToList();
            
            var componentsGroupSet = new System.Collections.Generic.HashSet<System.Type>(usedObjects.components.Count);
            foreach (var component in usedObjects.components) {
                var attr = component.GetCustomAttribute<ComponentGroupAttribute>();
                if (attr == null) continue;
                componentsGroupSet.Add(component);
            }

            usedObjects.componentsGroup = componentsGroupSet.OrderBy(x => x.FullName).ToList();
            return usedObjects;
        }

        private static void AddAllEditorTypes(System.Collections.Generic.HashSet<System.Type> systems,
                                              System.Collections.Generic.HashSet<System.Type> components,
                                              System.Collections.Generic.HashSet<System.Type> jobTypes,
                                              System.Collections.Generic.HashSet<System.Type> entityTypes,
                                              System.Collections.Generic.HashSet<System.Type> aspects,
                                              bool useSourceCatalogs) {

            AddTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<ISystem>(), systems, allowOpenGeneric: true);
            if (useSourceCatalogs) {
                SourceGeneratorBridge.AddEditorTypes(components, aspects);
            } else {
                AddTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentBase>(), components);
                AddTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<IAspect>(), aspects);
            }
            AddTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<IEntityType>(), entityTypes);

            AddJobTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<IJobParallelForAspectsComponentsBase>(), jobTypes);
            AddJobTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<IJobParallelForComponentsBase>(), jobTypes);
            AddJobTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<IJobParallelForAspectsBase>(), jobTypes);
            AddJobTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<IJobForAspectsComponentsBase>(), jobTypes);
            AddJobTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<IJobForComponentsBase>(), jobTypes);
            AddJobTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<IJobForAspectsBase>(), jobTypes);

        }

        private static void AddTypes(System.Collections.Generic.IEnumerable<System.Type> source,
                                     System.Collections.Generic.HashSet<System.Type> target,
                                     bool allowOpenGeneric = false) {

            foreach (var type in source) {
                if (type.IsValueType == false) continue;
                if (type.IsVisible == false) continue;
                if (allowOpenGeneric == false && type.ContainsGenericParameters == true) continue;
                target.Add(type);
            }

        }

        private static void AddJobTypes(System.Collections.Generic.IEnumerable<System.Type> source,
                                        System.Collections.Generic.HashSet<System.Type> target) {

            foreach (var type in source) {
                if (type.IsValueType == false) continue;
                if (type.IsVisible == false) continue;
                if (type.ContainsGenericParameters == true && type.DeclaringType?.IsGenericType != true) continue;
                target.Add(type);
            }

        }

        // All memoized work is scoped to one discovery run, never shared with worker threads.
        private sealed class UsedObjectsLookup {

        private readonly SourceGeneratorRuntimeUsage sourceUsage;
        internal UsedObjectsLookup(bool useSourceCatalogs) {
            // Runtime source assistance is an explicit comparison oracle only.
            // Production runtime discovery passes false and never reads catalogs.
            if (useSourceCatalogs) this.sourceUsage = new SourceGeneratorRuntimeUsage();
        }

        private readonly System.Collections.Generic.HashSet<MethodInfo> scannedMethods = new System.Collections.Generic.HashSet<MethodInfo>();
        private readonly System.Collections.Generic.HashSet<MethodBase> scannedBodies = new System.Collections.Generic.HashSet<MethodBase>();
        private readonly System.Collections.Generic.HashSet<(System.Type Owner, MethodInfo Method)> scannedRoots = new System.Collections.Generic.HashSet<(System.Type, MethodInfo)>();
        private readonly System.Collections.Generic.HashSet<System.Type> scannedAspects = new System.Collections.Generic.HashSet<System.Type>();
        private readonly System.Collections.Generic.Dictionary<System.Type, MethodInfo[]> methodsByType = new System.Collections.Generic.Dictionary<System.Type, MethodInfo[]>();
        private readonly MethodInfo newEntMethod = typeof(Ent).GetMethod(nameof(Ent.NewEnt_INTERNAL), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private readonly MethodInfo aspectMethod = typeof(WorldAspectStorage).GetMethod(nameof(WorldAspectStorage.InitializeObj), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        public void LookUpComponents(System.Collections.Generic.HashSet<System.Type> types,
                                             System.Collections.Generic.HashSet<System.Type> components,
                                             System.Collections.Generic.HashSet<System.Type> jobTypes,
                                             System.Collections.Generic.HashSet<System.Type> entityTypes,
                                             System.Collections.Generic.HashSet<System.Type> aspects) {

            // Config assets can select an aspect without a preceding job/access.
            // Its optional storage fields still require component registration.
            foreach (var aspect in aspects.ToArray()) AddToLookup(aspect, types, components, jobTypes, entityTypes, aspects);
            var pending = new System.Collections.Generic.Queue<System.Type>(components);
            var queued = new System.Collections.Generic.HashSet<System.Type>(components);
            while (pending.Count != 0) {
                var comp = pending.Dequeue();
                var previousCount = components.Count;
                if (typeof(IConfigInitialize).IsAssignableFrom(comp) == true) {
                    AddMethod(comp, nameof(IConfigInitialize.OnInitialize), types, components, jobTypes, entityTypes, aspects);
                }

                if (typeof(IComponentDestroy).IsAssignableFrom(comp) == true) {
                    AddMethod(comp, nameof(IComponentDestroy.Destroy), types, components, jobTypes, entityTypes, aspects);
                }
                if (components.Count != previousCount)
                    foreach (var discovered in components)
                        if (queued.Add(discovered)) pending.Enqueue(discovered);
            }
            
        }

        public void LookUp(System.Type type,
                                   System.Collections.Generic.HashSet<System.Type> types,
                                   System.Collections.Generic.HashSet<System.Type> components,
                                   System.Collections.Generic.HashSet<System.Type> jobTypes,
                                   System.Collections.Generic.HashSet<System.Type> entityTypes,
                                   System.Collections.Generic.HashSet<System.Type> aspects) {
            if (type == null) return;
            if (type.IsGenericType == true && type.IsGenericTypeDefinition == false) {
                type = type.GetGenericTypeDefinition();
            }
            types.Add(type);
            AddMethod(type, nameof(IAwake.OnAwake), types, components, jobTypes, entityTypes, aspects);
            AddMethod(type, nameof(IStart.OnStart), types, components, jobTypes, entityTypes, aspects);
            AddMethod(type, nameof(IUpdate.OnUpdate), types, components, jobTypes, entityTypes, aspects);
            AddMethod(type, nameof(IDrawGizmos.OnDrawGizmos), types, components, jobTypes, entityTypes, aspects);
            AddMethod(type, nameof(IDestroy.OnDestroy), types, components, jobTypes, entityTypes, aspects);
        }
        
        
        public void AddMethod(System.Type type, string name,
                              System.Collections.Generic.HashSet<System.Type> types, 
                              System.Collections.Generic.HashSet<System.Type> components,
                              System.Collections.Generic.HashSet<System.Type> jobTypes,
                              System.Collections.Generic.HashSet<System.Type> entityTypes,
                              System.Collections.Generic.HashSet<System.Type> aspects) {
            if (typeof(ISystem).IsAssignableFrom(type)) {
                var selected = new System.Collections.Generic.List<System.Type> { type };
                if (type.ContainsGenericParameters) CodeGenerator.PatchSystemsList(selected);
                foreach (var system in selected) Run(system);
            } else if (type.ContainsGenericParameters == true) {
                var without = type.GetInterfaces().Where(x => typeof(IGenericWithout).IsAssignableFrom(x) && x.IsGenericType == true).Select(x => x.GetGenericArguments()[0]).ToArray();
                var constraints = type.GetGenericArguments()[0].GetGenericParameterConstraints();
                foreach (var constraint in constraints) {
                    if (constraint == typeof(System.ValueType)) continue;
                    var constTypes = UnityEditor.TypeCache.GetTypesDerivedFrom(constraint);
                    foreach (var t in constTypes) {
                        if (t.IsValueType == false) continue;
                        if (without.Any(x => x.IsAssignableFrom(t)) == true) continue;
                        var genType = type.MakeGenericType(t);
                        Run(genType);
                    }
                }
            } else {
                Run(type);
            }

            void Run(System.Type type) {
                MethodInfo[] methods;
                var callbackOwner = typeof(Module).IsAssignableFrom(type) ||
                    name == nameof(IConfigInitialize.OnInitialize) && typeof(IConfigInitialize).IsAssignableFrom(type) ||
                    name == nameof(IComponentDestroy.Destroy) && typeof(IComponentDestroy).IsAssignableFrom(type);
                if (typeof(ISystem).IsAssignableFrom(type)) {
                    var lifecycle = SourceGeneratorScheduledJobsValidation.GetLifecycleMethod(type, name);
                    methods = lifecycle == null ? System.Array.Empty<MethodInfo>() : new[] { lifecycle };
                } else if (callbackOwner) {
                    var callback = SourceGeneratorRuntimeUsage.GetCallback(type, name);
                    methods = callback == null ? System.Array.Empty<MethodInfo>() : new[] { callback };
                } else if (this.methodsByType.TryGetValue(type, out methods) == false) {
                    methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    this.methodsByType.Add(type, methods);
                }
                foreach (var method in methods) {
                    if (!typeof(ISystem).IsAssignableFrom(type) && !callbackOwner && method.Name != name) continue;
                    if (this.scannedRoots.Add((type, method)) == false) continue;
                    if (this.sourceUsage != null && this.sourceUsage.TryReadForOwner(type, method, out var sourceTypes)) {
                        components.UnionWith(sourceTypes.components);
                        aspects.UnionWith(sourceTypes.aspects);
                        entityTypes.UnionWith(sourceTypes.entityTypes);
                        jobTypes.UnionWith(sourceTypes.jobs);
                        continue;
                    }
                    if (this.scannedMethods.Add(method) == false) continue;
                    CodeGeneratorTimings.Subject(type.FullName + "." + method.Name);
                    var pending = new System.Collections.Generic.Queue<MethodBase>();
                    pending.Enqueue(method);
                    var bodyCount = 0;
                    var instructionCount = 0;
                    while (pending.Count > 0) {
                        var body = pending.Dequeue();
                        if (!this.scannedBodies.Add(body)) continue;
                        CodeGeneratorTimings.Work(body);
                        if (++bodyCount > 10000) throw new System.InvalidOperationException("Discovery IL traversal limit exceeded: " + method);
                        var discovered = ILAnalysisSession.Get((typeof(DiscoveryBody), body), () =>
                            ILPersistentAnalysis.Get("discovery", ILPersistentAnalysis.MethodIdentity(body), () => CollectBody(body), EncodeBody, DecodeBody));
                        instructionCount = checked(instructionCount + discovered.instructions);
                        if (instructionCount > 1000000) throw new System.InvalidOperationException("Discovery IL instruction limit exceeded: " + method);
                        components.UnionWith(discovered.components);
                        aspects.UnionWith(discovered.aspects);
                        entityTypes.UnionWith(discovered.entities);
                        jobTypes.UnionWith(discovered.jobs);
                        foreach (var next in discovered.next) pending.Enqueue(next);
                    }
                }
            }
        }

        private sealed class DiscoveryBody {
            internal readonly System.Collections.Generic.HashSet<System.Type> components = new System.Collections.Generic.HashSet<System.Type>();
            internal readonly System.Collections.Generic.HashSet<System.Type> aspects = new System.Collections.Generic.HashSet<System.Type>();
            internal readonly System.Collections.Generic.HashSet<System.Type> entities = new System.Collections.Generic.HashSet<System.Type>();
            internal readonly System.Collections.Generic.HashSet<System.Type> jobs = new System.Collections.Generic.HashSet<System.Type>();
            internal readonly System.Collections.Generic.HashSet<MethodBase> next = new System.Collections.Generic.HashSet<MethodBase>();
            internal int instructions;
        }

        [System.Serializable]
        private sealed class DiscoveryData {
            public ILSummaryData.Types components, aspects, entities, jobs;
            public ILContentFingerprint.MethodReference[] next;
            public int instructions;
        }

        private static DiscoveryData EncodeBody(DiscoveryBody body) => new DiscoveryData {
            components = ILSummaryData.EncodeTypes(body.components), aspects = ILSummaryData.EncodeTypes(body.aspects),
            entities = ILSummaryData.EncodeTypes(body.entities), jobs = ILSummaryData.EncodeTypes(body.jobs), instructions = body.instructions,
            next = body.next.Select(ILContentFingerprint.MethodReference.From).OrderBy(method => method.Key, System.StringComparer.Ordinal).ToArray(),
        };

        private static DiscoveryBody DecodeBody(DiscoveryData data) {
            if (data?.next == null || data.instructions < 0) throw new System.FormatException("Invalid cached discovery body.");
            var body = new DiscoveryBody { instructions = data.instructions };
            body.components.UnionWith(ILSummaryData.DecodeTypes(data.components));
            body.aspects.UnionWith(ILSummaryData.DecodeTypes(data.aspects));
            body.entities.UnionWith(ILSummaryData.DecodeTypes(data.entities));
            body.jobs.UnionWith(ILSummaryData.DecodeTypes(data.jobs));
            foreach (var method in data.next) body.next.Add(ILPersistentAnalysis.ResolveMethod(method) ?? throw new System.MissingMethodException("Cached discovery target no longer exists."));
            return body;
        }

        private static DiscoveryBody CollectBody(MethodBase body) {
            var result = new DiscoveryBody();
            // Local aspect expansion must not depend on what an earlier root
            // happened to visit. Every cached body is a complete local summary.
            var lookup = new UsedObjectsLookup(false);
            var types = new System.Collections.Generic.HashSet<System.Type>();
            var components = result.components;
            var jobTypes = result.jobs;
            var entityTypes = result.entities;
            var aspects = result.aspects;
            var componentTypes = JobsEarlyInitCodeGenerator.GetDiscoveryBodyInfo(body, result.next, onInstruction: (inst, q) => {
                if (inst.Operand is MethodInfo methodInfo) {
                    // SafetyCheck describes access modes, not all types
                    // reached by a user helper. Keep its body in discovery;
                    // core ECS storage boundaries remain terminal.
                    if (methodInfo.DeclaringType.Assembly != typeof(Ent).Assembly &&
                        methodInfo.IsDefined(typeof(SafetyCheckAttribute), false) &&
                        !methodInfo.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) && methodInfo.GetMethodBody() != null)
                        q.Enqueue(methodInfo);
                    if (IsMethod(methodInfo, lookup.aspectMethod) == true) {
                        var t = methodInfo.GetGenericArguments()[0];
                        lookup.AddToLookup(t, types, components, jobTypes, entityTypes, aspects);
                    } else if (IsMethod(methodInfo, lookup.newEntMethod) == true) {
                        var entityType = methodInfo.GetGenericArguments()[0];
                        entityTypes.Add(entityType);
                    } else if (SourceGeneratorScheduledJobsValidation.IsSchedulingMethod(methodInfo)) {
                        if (methodInfo.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
                            var jobType = methodInfo.GetGenericArguments()[0];
                            jobTypes.Add(jobType);
                            var contract = ILJobScheduleContract.GetWorkInterface(methodInfo);
                            foreach (var arg in contract.GenericTypeArguments)
                                lookup.AddToLookup(arg, types, components, jobTypes, entityTypes, aspects);
                            q.Enqueue(ILJobScheduleContract.GetExecuteMethod(jobType, contract));
                        }
                        // The scheduling machinery is not a usage root.
                        // Its selected deferred Execute body was queued above.
                        return true;
                    }
                }

                return false;
            });
            foreach (var componentType in componentTypes) {
                if (typeof(IComponentBase).IsAssignableFrom(componentType.type) == true) {
                    components.Add(componentType.type);
                }
            }
            if (!ILInfrastructure.SkipBody(body) && body.GetMethodBody() != null)
                result.instructions = ILAnalysisSession.Instructions(body).Length;
            return result;
        }

        private void AddToLookup(System.Type type, System.Collections.Generic.HashSet<System.Type> types, System.Collections.Generic.HashSet<System.Type> components, System.Collections.Generic.HashSet<System.Type> jobTypes, System.Collections.Generic.HashSet<System.Type> entityTypes, System.Collections.Generic.HashSet<System.Type> aspects) {
            if (type.IsGenericTypeParameter == true) {
                var constraints = type.GetGenericParameterConstraints();
                foreach (var constraint in constraints) {
                    if (constraint == typeof(System.ValueType)) continue;
                    var constTypes = UnityEditor.TypeCache.GetTypesDerivedFrom(constraint);
                    foreach (var constType in constTypes) {
                        AddToLookup(constType, types, components, jobTypes, entityTypes, aspects);
                    }
                }
                return;
            }
            if (typeof(IComponentBase).IsAssignableFrom(type) == true) {
                components.Add(type);
            } else if (typeof(IAspect).IsAssignableFrom(type) == true) {
                aspects.Add(type);
                if (this.scannedAspects.Add(type) == false) return;
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var field in fields) {
                    if (typeof(IAspectData).IsAssignableFrom(field.FieldType) == true) {
                        components.Add(field.FieldType.GenericTypeArguments[0]);
                    }
                }
            }
        }

        }
    }
    
}
