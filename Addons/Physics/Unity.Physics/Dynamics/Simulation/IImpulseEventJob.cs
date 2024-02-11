using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Physics
{
    /// <summary>   Interface for impulse events job base. </summary>
    [JobProducerType(typeof(IImpulseEventJobExtensions.ImpulseEventJobProcess<>))]
    public interface IImpulseEventsJobBase
    {
        /// <summary>   Executes an operation on the given impulse event. </summary>
        ///
        /// <param name="impulseEvent"> The impulse event. </param>
        void Execute(ImpulseEvent impulseEvent);
    }

#if !HAVOK_PHYSICS_EXISTS
    /// <summary>   Interface for impulse events job. </summary>
    public interface IImpulseEventsJob : IImpulseEventsJobBase
    {
    }
#endif

    /// <summary>   An extension class for scheduling ImpulseEventsJob. </summary>
    public static class IImpulseEventJobExtensions
    {
#if !HAVOK_PHYSICS_EXISTS
        /// <summary>   Default Schedule() implementation for IImpulseEventJob. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="jobData">              The jobData to act on. </param>
        /// <param name="simulationSingleton">  The simulation singleton. </param>
        /// <param name="inputDeps">            The input deps. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        public static unsafe JobHandle Schedule<T>(this T jobData, SimulationSingleton simulationSingleton, JobHandle inputDeps)
            where T : struct, IImpulseEventsJobBase
        {
            // Should work only for UnityPhysics
            if (simulationSingleton.Type != SimulationType.UnityPhysics)
            {
                return inputDeps;
            }
            return ScheduleUnityPhysicsImpulseEventsJob(jobData, simulationSingleton.AsSimulation(), inputDeps);
        }

#else
        /// <summary>
        /// In this case Schedule() implementation for IImpulseEventsJob is provided by the Havok.Physics
        /// assembly. This is a stub to catch when that assembly is missing.
        /// </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="jobData">              The jobData to act on. </param>
        /// <param name="simulation">           The simulation. </param>
        /// <param name="inputDeps">            The input deps. </param>
        /// <param name="_causeCompileError">   (Optional) The cause compile error. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        [Obsolete("This error occurs when HAVOK_PHYSICS_EXISTS is defined but Havok.Physics is missing from your package's asmdef references. (DoNotRemove)", true)]
        public static unsafe JobHandle Schedule<T>(this T jobData, ISimulation simulation, JobHandle inputDeps,
            HAVOK_PHYSICS_MISSING_FROM_ASMDEF _causeCompileError = HAVOK_PHYSICS_MISSING_FROM_ASMDEF.HAVOK_PHYSICS_MISSING_FROM_ASMDEF)
            where T : struct, IImpulseEventsJobBase
        {
            return new JobHandle();
        }

        /// <summary>   Values that represent havok physics missing from asmdefs. </summary>
        public enum HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        {
            HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        }
#endif
        internal static unsafe JobHandle ScheduleUnityPhysicsImpulseEventsJob<T>(T jobData, Simulation simulation, JobHandle inputDeps)
            where T : struct, IImpulseEventsJobBase
        {
            SafetyChecks.CheckSimulationStageAndThrow(simulation.m_SimulationScheduleStage, SimulationScheduleStage.Idle);
            if (!simulation.ReadyForEventScheduling)
                return inputDeps;

            var data = new ImpulseEventJobData<T>
            {
                UserJobData = jobData,
                EventReader = simulation.ImpulseEvents
            };

            var jobReflectionData = ImpulseEventJobProcess<T>.jobReflectionData.Data;
            ImpulseEventJobProcess<T>.CheckReflectionDataCorrect(jobReflectionData);

            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), jobReflectionData, inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
        }

        internal unsafe struct ImpulseEventJobData<T> where T : struct
        {
            public T UserJobData;
            [NativeDisableContainerSafetyRestriction] public ImpulseEvents EventReader;
        }

        internal struct ImpulseEventJobProcess<T> where T : struct, IImpulseEventsJobBase
        {
            internal static readonly SharedStatic<IntPtr> jobReflectionData = SharedStatic<IntPtr>.GetOrCreate<ImpulseEventJobProcess<T>>();

            [Preserve]
            public static void Initialize()
            {
                if (jobReflectionData.Data == IntPtr.Zero)
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(ImpulseEventJobData<T>), typeof(T), (ExecuteJobFunction)Execute);
            }

            [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECK")]
            internal static void CheckReflectionDataCorrect(IntPtr reflectionData)
            {
                if (reflectionData == IntPtr.Zero)
                    throw new InvalidOperationException("Reflection data was not set up by an Initialize() call");
            }

            public delegate void ExecuteJobFunction(ref ImpulseEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(ref ImpulseEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                foreach (ImpulseEvent impulseEvent in jobData.EventReader)
                {
                    jobData.UserJobData.Execute(impulseEvent);
                }
            }
        }

        /// <summary>   Early job initialize. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        public static void EarlyJobInit<T>()
            where T : struct, IImpulseEventsJobBase
        {
            ImpulseEventJobProcess<T>.Initialize();
        }
    }
}
