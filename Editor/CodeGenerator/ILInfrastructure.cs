namespace ME.BECS.Editor {
    using System;
    using System.Linq;
    using System.Reflection;

    internal static class ILInfrastructure {
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
