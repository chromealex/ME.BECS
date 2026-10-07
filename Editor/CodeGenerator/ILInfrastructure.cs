namespace ME.BECS.Editor {
    using System;
    using System.Linq;
    using System.Reflection;

    internal static class ILInfrastructure {
        // ME.BECS core API is infrastructure, not user behavior: by default the
        // analyzers do not enter its bodies. A core method stays transparent when
        // it is (or can reach, through core code) something an analysis observes:
        // [SafetyCheck]/[CodeGeneratorIgnoreVisited] methods, entity creation,
        // aspect initialization, job scheduling, IRefOp fields, a call into a
        // generic parameter / interface / delegate (possible user callback), or a
        // typed native-storage API. Methods of user-facing root types (systems,
        // jobs, aspects, config/destroy callbacks) are never boundaries.
        // NOTE: skipping core bodies lowers job IL weights (core instructions
        // no longer count); that is intentional.
        private static readonly Assembly CoreAssembly = typeof(Ent).Assembly;
        private static readonly MethodInfo NewEntMethod = typeof(Ent).GetMethod(nameof(Ent.NewEnt_INTERNAL), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo AspectMethod = typeof(WorldAspectStorage).GetMethod(nameof(WorldAspectStorage.InitializeObj), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<MethodBase, bool> reportedCreators =
            new System.Collections.Concurrent.ConcurrentDictionary<MethodBase, bool>();

        internal static bool IsCoreInfrastructure(MethodBase method) => method?.DeclaringType != null &&
            method.DeclaringType.Assembly == CoreAssembly &&
            ILAnalysisSession.Get((typeof(ILInfrastructure), method, "core-boundary"), () => {
                if (IsRootOwner(method.DeclaringType)) return false;
                var definition = Definition(method);
                if (definition == null) return false;
                return !ILAnalysisSession.Get((typeof(ILInfrastructure), definition, "core-signal"), () => {
                    // The proof walks generic DEFINITIONS. Their bodies are read through
                    // ILAnalysisSession.Instructions, which records them as dependencies of
                    // every enclosing persistent summary; an open method has no persistent
                    // identity, so each such summary was silently left uncached (about half
                    // of all summaries, re-analyzed on every run). Keep closed dependencies.
                    using var capture = new ILDependencyCapture();
                    var reaches = ReachesSignal(definition);
                    capture.Discard(item => item.ContainsGenericParameters || item.DeclaringType == null);
                    return reaches;
                });
            });

        // Third-party code that cannot name ME.BECS (Input System, UnityEngine modules,
        // Unity.Mathematics, JSON libraries…) can neither create entities, touch
        // components nor schedule jobs by itself. The only ways back into BECS are user
        // types passed as generic arguments and user callbacks (delegates, abstract or
        // interface dispatch), so those stay visible; every other body is a leaf.
        // BCL (mscorlib) keeps its own Leaf/Opaque contract below.
        // NOTE: a foreign method that synchronously invokes a callback stored earlier
        // (not passed at this call) is not followed, like native engine callbacks.
        internal static bool IsForeignInfrastructure(MethodBase method) {
            var owner = method?.DeclaringType;
            if (owner == null || owner.Assembly == CoreAssembly || owner.Assembly == typeof(object).Assembly) return false;
            if (ReferencesCore(owner.Assembly)) return false;
            if (method.IsAbstract || owner.IsInterface || typeof(Delegate).IsAssignableFrom(owner) && method.Name == "Invoke") return false;
            if (owner.IsGenericType && owner.GetGenericArguments().Any(HasBecsType)) return false;
            return !method.IsGenericMethod || !method.GetGenericArguments().Any(HasBecsType);
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Assembly, bool> referencesCore =
            new System.Collections.Concurrent.ConcurrentDictionary<Assembly, bool>();

        private static bool HasBecsType(Type type) {
            if (type.HasElementType) return HasBecsType(type.GetElementType());
            if (type.IsGenericParameter) return true;
            if (type.Assembly == CoreAssembly || type.Assembly != typeof(object).Assembly && ReferencesCore(type.Assembly)) return true;
            return type.IsGenericType && type.GetGenericArguments().Any(HasBecsType);
        }

        // Transitive: a foreign assembly referencing a user assembly could call into it.
        private static bool ReferencesCore(Assembly assembly) {
            if (assembly == CoreAssembly) return true;
            if (referencesCore.TryGetValue(assembly, out var known)) return known;
            var loaded = new System.Collections.Generic.Dictionary<string, Assembly>(StringComparer.Ordinal);
            foreach (var item in AppDomain.CurrentDomain.GetAssemblies()) {
                if (item.IsDynamic) continue;
                var name = item.GetName().Name;
                if (!loaded.ContainsKey(name)) loaded.Add(name, item);
            }
            var core = CoreAssembly.GetName().Name;
            var visiting = new System.Collections.Generic.HashSet<Assembly>();
            bool Visit(Assembly current) {
                if (current == CoreAssembly) return true;
                if (referencesCore.TryGetValue(current, out var cached)) return cached;
                if (!visiting.Add(current)) return false; // cycle: decided by the other path
                var result = false;
                AssemblyName[] references;
                try { references = current.GetReferencedAssemblies(); }
                catch (Exception exception) when (!(exception is OperationCanceledException)) { references = null; result = true; } // unknown: stay visible
                if (references != null) {
                    foreach (var reference in references) {
                        if (reference.Name == core) { result = true; break; }
                        if (loaded.TryGetValue(reference.Name, out var next) && next != typeof(object).Assembly && Visit(next)) { result = true; break; }
                    }
                }
                visiting.Remove(current);
                // A false found while a cycle member was pending is still sound: a
                // cycle reaching core would have returned true through that member.
                referencesCore.TryAdd(current, result);
                return result;
            }
            return Visit(assembly);
        }

        private static bool IsRootOwner(Type type) {
            for (var owner = type; owner != null; owner = owner.DeclaringType) {
                if (typeof(ISystem).IsAssignableFrom(owner) || typeof(IAspect).IsAssignableFrom(owner) ||
                    typeof(IConfigInitialize).IsAssignableFrom(owner) || typeof(IComponentDestroy).IsAssignableFrom(owner)) return true;
                foreach (var contract in owner.GetInterfaces()) if (contract.Name.StartsWith("IJob", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static MethodBase Definition(MethodBase method) {
            try {
                if (method is MethodInfo info && info.IsGenericMethod && !info.IsGenericMethodDefinition) method = info.GetGenericMethodDefinition();
                var type = method.DeclaringType;
                if (type != null && type.IsGenericType && !type.IsGenericTypeDefinition)
                    method = MethodBase.GetMethodFromHandle(method.MethodHandle, type.GetGenericTypeDefinition().TypeHandle);
                return method;
            } catch (Exception exception) when (!(exception is OperationCanceledException)) { return null; }
        }

        private static bool Same(MethodBase method, MethodInfo reference) =>
            reference != null && method is MethodInfo info && (info == reference || info.IsGenericMethod && info.GetGenericMethodDefinition() == reference);

        private static bool IsSignal(MethodBase method) =>
            method.IsDefined(typeof(SafetyCheckAttribute), false) || method.IsDefined(typeof(CodeGeneratorIgnoreVisitedAttribute), false) ||
            Same(method, NewEntMethod) || Same(method, AspectMethod) ||
            method is MethodInfo info && SourceGeneratorScheduledJobsValidation.IsSchedulingMethod(info);

        private static bool ForeignSignal(MethodBase target) {
            var owner = target.DeclaringType;
            if (target.IsAbstract || owner != null && typeof(Delegate).IsAssignableFrom(owner) && target.Name == "Invoke") return true;
            if (target is MethodInfo factory && owner == typeof(Activator) && factory.IsGenericMethod) return true;
            if (owner != null && owner.IsGenericType && owner.GetGenericTypeDefinition() == typeof(Unity.Burst.SharedStatic<>)) return true;
            if (target is MethodInfo memory && ILNativeComponentAccess.Memory(memory, out _)) return true;
            // Typed Unity collections over a generic element may touch component storage.
            return owner?.Namespace != null && owner.Namespace.StartsWith("Unity.Collections", StringComparison.Ordinal) && target.ContainsGenericParameters;
        }

        private static bool ReachesSignal(MethodBase root) {
            var pending = new System.Collections.Generic.Stack<MethodBase>();
            var seen = new System.Collections.Generic.HashSet<MethodBase> { root };
            pending.Push(root);
            while (pending.Count > 0) {
                var method = pending.Pop();
                ILAnalysisSession.Checkpoint(method);
                if (IsSignal(method)) {
                    if (method != root && (Same(method, NewEntMethod) || method.IsDefined(typeof(CodeGeneratorIgnoreVisitedAttribute), false)) &&
                        !root.IsDefined(typeof(CodeGeneratorIgnoreVisitedAttribute), false) && reportedCreators.TryAdd(root, true))
                        UnityEngine.Debug.Log("[ME.BECS] Core method " + root.DeclaringType?.FullName + "." + root.Name +
                            " reaches entity creation and stays analyzed; consider marking it [CodeGeneratorIgnoreVisited].");
                    return true;
                }
                if (method.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) || method.GetMethodBody() == null) continue;
                ME.BECS.Mono.Reflection.Instruction[] instructions;
                try { instructions = ILAnalysisSession.Instructions(method); }
                catch (Exception exception) when (!(exception is OperationCanceledException)) { return true; }
                foreach (var instruction in instructions) {
                    if (instruction.OpCode == System.Reflection.Emit.OpCodes.Constrained) return true;
                    if (instruction.Operand is FieldInfo field && typeof(IRefOp).IsAssignableFrom(field.FieldType)) return true;
                    if (!(instruction.Operand is MethodBase target)) continue;
                    if (target.DeclaringType?.Assembly != CoreAssembly) {
                        if (target.DeclaringType?.Assembly != typeof(object).Assembly && ForeignSignal(target)) return true;
                        if (target.DeclaringType?.Assembly == typeof(object).Assembly && (target.IsAbstract ||
                            target.DeclaringType != null && typeof(Delegate).IsAssignableFrom(target.DeclaringType) && target.Name == "Invoke")) return true;
                        continue;
                    }
                    var next = Definition(target);
                    if (next == null) return true;
                    if (seen.Add(next)) {
                        if (seen.Count > 4096) return true; // too large to prove: stay transparent
                        pending.Push(next);
                    }
                }
            }
            return false;
        }

        // BCL implementation details are not ECS roots. Generic instantiations
        // with user types stay visible (e.g. a comparer can call user GetHashCode).
        // Concrete formatting/delegate callbacks are collected at their call
        // sites, before this boundary is applied. Opaque is NOT an effect-free
        // proof: entity-count coverage records a gap instead of claiming zero.
        internal static bool IsOpaque(MethodBase method) => method != null &&
            ILAnalysisSession.Get((typeof(ILInfrastructure), method, "opaque"), () => {
                var owner = method.DeclaringType;
                if (owner == null || owner.Assembly != typeof(object).Assembly) return false;
                if (owner.IsGenericType && owner.GetGenericArguments().Any(HasUserType)) return false;
                return !method.IsGenericMethod || !method.GetGenericArguments().Any(HasUserType);
            });

        private static bool HasUserType(Type type) {
            if (type.HasElementType) return HasUserType(type.GetElementType());
            if (type.IsGenericParameter || type.Assembly != typeof(object).Assembly) return true;
            return type.IsGenericType && type.GetGenericArguments().Any(HasUserType);
        }

        internal static bool SkipBody(MethodBase method) => IsLeaf(method) || IsOpaque(method);

        internal static bool IsLeaf(MethodBase method) => method != null &&
            ILAnalysisSession.Get((typeof(ILInfrastructure), method), () => IsLeafCore(method));

        private static bool IsLeafCore(MethodBase method) {
            if (IsCoreInfrastructure(method)) return true;
            if (IsForeignInfrastructure(method)) return true;
            if (Jobs.ILJobEntityCounts.IsBurstStorageLeaf(method) ||
                SourceGeneratorScheduledJobsValidation.IsUnityAddressIntrinsic(method)) return true;

            var owner = method.DeclaringType;
            if (owner == null || owner.Assembly != typeof(object).Assembly) return false;
            // Identity/arithmetic intrinsics cannot invoke user code. In particular
            // typeof(T) must not lead into RuntimeType's reflection/cache internals.
            if (owner == typeof(object)) return method is ConstructorInfo ||
                method.Name == nameof(GetType) || method.Name == nameof(ReferenceEquals);
            if (owner == typeof(Type) && method.IsStatic &&
                (method.Name == nameof(Type.GetTypeFromHandle) || method.Name == nameof(Type.GetTypeHandle))) return true;
            if (owner == typeof(Math) || owner == typeof(Buffer)) return true;

            // Only these exact public BCL constructors store error data without
            // invoking it. Do NOT skip user exceptions, virtual Message/ToString,
            // serialization constructors, providers, comparers or generic helpers.
            if (!(method is ConstructorInfo) || method.IsStatic || !method.IsPublic) return false;
            var p = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var common = p.Length == 0 || p.SequenceEqual(new[] { typeof(string) }) ||
                p.SequenceEqual(new[] { typeof(string), typeof(Exception) });
            if (owner == typeof(Exception) || owner == typeof(SystemException) || owner == typeof(InvalidOperationException) ||
                owner == typeof(IndexOutOfRangeException) || owner == typeof(NotSupportedException) ||
                owner == typeof(NotImplementedException) || owner == typeof(NullReferenceException) ||
                owner == typeof(ArithmeticException) || owner == typeof(OverflowException)) return common;
            var twoStrings = p.SequenceEqual(new[] { typeof(string), typeof(string) });
            if (owner == typeof(ArgumentException)) return common || twoStrings ||
                p.SequenceEqual(new[] { typeof(string), typeof(string), typeof(Exception) });
            if (owner == typeof(ArgumentNullException)) return common || twoStrings;
            if (owner == typeof(ArgumentOutOfRangeException)) return common || twoStrings ||
                p.SequenceEqual(new[] { typeof(string), typeof(object), typeof(string) });
            return owner == typeof(ObjectDisposedException) && (p.Length > 0 && common || twoStrings);
        }
    }
}
