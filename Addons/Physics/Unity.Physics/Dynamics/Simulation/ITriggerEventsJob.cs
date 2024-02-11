using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Physics
{
    /// <summary>
    /// INTERNAL UnityPhysics interface for jobs that iterate through the list of trigger events
    /// produced by the solver. Important: Only use inside UnityPhysics code! Jobs in other projects
    /// should implement ITriggerEventsJob.
    /// </summary>
    [JobProducerType(typeof(ITriggerEventJobExtensions.TriggerEventJobProcess<>))]
    public interface ITriggerEventsJobBase
    {
        /// <summary>   Executes an operation on the given trigger event. </summary>
        ///
        /// <param name="triggerEvent"> The trigger event. </param>
        void Execute(TriggerEvent triggerEvent);
    }

#if !HAVOK_PHYSICS_EXISTS

    /// <summary>
    /// Interface for jobs that iterate through the list of trigger events produced by the solver.
    /// </summary>
    public interface ITriggerEventsJob : ITriggerEventsJobBase
    {
    }

#endif

    /// <summary>   A trigger event job extensions. </summary>
    public static class ITriggerEventJobExtensions
    {
#if !HAVOK_PHYSICS_EXISTS

        /// <summary>   Default Schedule() implementation for ITriggerEventsJob. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="jobData">              The jobData to act on. </param>
        /// <param name="simulationSingleton">  The simulation singleton. </param>
        /// <param name="inputDeps">            The input deps. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        public static unsafe JobHandle Schedule<T>(this T jobData, SimulationSingleton simulationSingleton, JobHandle inputDeps)
            where T : struct, ITriggerEventsJobBase
        {
            // Should work only for UnityPhysics
            if (simulationSingleton.Type != SimulationType.UnityPhysics)
            {
                return inputDeps;
            }

            return ScheduleUnityPhysicsTriggerEventsJob(jobData, simulationSingleton.AsSimulation(), inputDeps);
        }

#else

        /// <summary>
        /// In this case Schedule() implementation for ITriggerEventsJob is provided by the Havok.Physics
        /// assembly.
        ///  This is a stub to catch when that assembly is missing.
        /// <todo.eoin.modifier Put in a link to documentation for this:
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
            where T : struct, ITriggerEventsJobBase
        {
            return new JobHandle();
        }

        /// <summary>   Values that represent havok physics missing from asmdefs. </summary>
        public enum HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        {
            HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        }
#endif

        // Schedules a trigger events job only for UnityPhysics simulation
        internal static unsafe JobHandle ScheduleUnityPhysicsTriggerEventsJob<T>(T jobData, Simulation simulation, JobHandle inputDeps)
            where T : struct, ITriggerEventsJobBase
        {
            SafetyChecks.CheckSimulationStageAndThrow(simulation.m_SimulationScheduleStage, SimulationScheduleStage.Idle);
            if (!simulation.ReadyForEventScheduling)
                return inputDeps;

            var data = new TriggerEventJobData<T>
            {
                UserJobData = jobData,
                EventReader = simulation.TriggerEvents
            };

            var jobReflectionData = TriggerEventJobProcess<T>.jobReflectionData.Data;
            TriggerEventJobProcess<T>.CheckReflectionDataCorrect(jobReflectionData);

            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), jobReflectionData, inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
        }

        internal unsafe struct TriggerEventJobData<T> where T : struct
        {
            public T UserJobData;
            [NativeDisableContainerSafetyRestriction] public TriggerEvents EventReader;
        }

        internal struct TriggerEventJobProcess<T> where T : struct, ITriggerEventsJobBase
        {
            internal static readonly SharedStatic<IntPtr> jobReflectionData = SharedStatic<IntPtr>.GetOrCreate<TriggerEventJobProcess<T>>();

            [Preserve]
            public static void Initialize()
            {
                if (jobReflectionData.Data == IntPtr.Zero)
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(TriggerEventJobData<T>), typeof(T), (ExecuteJobFunction)Execute);
            }

            [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECK")]
            internal static void CheckReflectionDataCorrect(IntPtr reflectionData)
            {
                if (reflectionData == IntPtr.Zero)
                    throw new InvalidOperationException("Reflection data was not set up by an Initialize() call");
            }

            public delegate void ExecuteJobFunction(ref TriggerEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(ref TriggerEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                foreach (TriggerEvent triggerEvent in jobData.EventReader)
                {
                    jobData.UserJobData.Execute(triggerEvent);
                }
            }
        }

        /// <summary>   Early job initialize. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        public static void EarlyJobInit<T>()
            where T : struct, ITriggerEventsJobBase
        {
            TriggerEventJobProcess<T>.Initialize();
        }
    }
}
