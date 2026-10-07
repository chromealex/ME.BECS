namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    // Bind the contract selected by this scheduler, not every job interface on T.
    // Metadata only: never invoke a scheduler, producer, initializer or job body.
    internal static class ILJobScheduleContract {
        internal static Type GetWorkInterface(MethodInfo scheduler) {
            if (scheduler == null || !SourceGeneratorScheduledJobsValidation.IsSchedulingMethod(scheduler) || scheduler.ContainsGenericParameters)
                throw new InvalidOperationException("Expected a closed supported job scheduler: " + scheduler);
            var parameters = scheduler.GetGenericMethodDefinition().GetGenericArguments();
            var arguments = scheduler.GetGenericArguments();
            var environment = parameters.Select((parameter, index) => (parameter, type: arguments[index]))
                .ToDictionary(entry => entry.parameter, entry => entry.type);
            var contracts = parameters[0].GetGenericParameterConstraints().Select(type => Close(type, environment))
                .Where(IsWorkInterface).Distinct().ToArray();
            if (contracts.Length != 1 || !arguments[0].IsValueType || !contracts[0].IsAssignableFrom(arguments[0]))
                throw new InvalidOperationException("Missing or ambiguous job work interface for scheduler: " + scheduler);
            return contracts[0];
        }

        internal static MethodInfo GetExecuteMethod(Type job, Type contract) {
            if (job == null || contract == null || job.ContainsGenericParameters || contract.ContainsGenericParameters ||
                !job.IsValueType || !IsWorkInterface(contract) || !contract.IsAssignableFrom(job))
                throw new InvalidOperationException("Invalid scheduled job contract: " + job + " / " + contract);
            var map = job.GetInterfaceMap(contract);
            var slots = Enumerable.Range(0, map.InterfaceMethods.Length).Where(index => map.InterfaceMethods[index].Name == "Execute").ToArray();
            if (slots.Length != 1)
                throw new InvalidOperationException("Missing or ambiguous scheduled Execute slot: " + job + " / " + contract);
            return Jobs.JobsEarlyInitCodeGenerator.UnwrapJobExecuteForwarder(map.TargetMethods[slots[0]]);
        }

        private static bool IsWorkInterface(Type type) {
            if (!type.IsInterface || type.ContainsGenericParameters) return false;
            if (type == typeof(Unity.Jobs.IJob) || type == typeof(Unity.Jobs.IJobFor) ||
                type == typeof(Unity.Jobs.IJobParallelFor) || type == typeof(Unity.Jobs.IJobParallelForBatch) ||
                type == typeof(Unity.Jobs.IJobParallelForDefer)) return true;
            return type.Assembly == typeof(Ent).Assembly && type.Namespace == "ME.BECS.Jobs" &&
                type.IsDefined(typeof(Unity.Jobs.LowLevel.Unsafe.JobProducerTypeAttribute), false);
        }

        private static Type Close(Type type, IReadOnlyDictionary<Type, Type> environment) {
            if (environment.TryGetValue(type, out var closed)) return closed;
            if (type.IsArray) return type.GetArrayRank() == 1 ? Close(type.GetElementType(), environment).MakeArrayType() :
                Close(type.GetElementType(), environment).MakeArrayType(type.GetArrayRank());
            return type.IsGenericType ? type.GetGenericTypeDefinition().MakeGenericType(type.GetGenericArguments()
                .Select(argument => Close(argument, environment)).ToArray()) : type;
        }
    }
}
