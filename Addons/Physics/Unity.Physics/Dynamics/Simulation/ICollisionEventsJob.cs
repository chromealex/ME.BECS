using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Physics
{
    /// <summary>
    /// INTERNAL UnityPhysics interface for jobs that iterate through the list of collision events
    /// produced by the solver. Important: Only use inside UnityPhysics code! Jobs in other projects
    /// should implement ICollisionEventsJob.
    /// </summary>
    [JobProducerType(typeof(ICollisionEventJobExtensions.CollisionEventJobProcess<>))]
    public interface ICollisionEventsJobBase
    {
        /// <summary>   Executes the operation on a given collision event. </summary>
        ///
        /// <param name="collisionEvent">   The collision event. </param>
        void Execute(CollisionEvent collisionEvent);
    }

#if !HAVOK_PHYSICS_EXISTS

    /// <summary>
    /// Interface for jobs that iterate through the list of collision events produced by the solver.
    /// </summary>
    public interface ICollisionEventsJob : ICollisionEventsJobBase
    {
    }

#endif

    /// <summary>   A collision event job extensions. </summary>
    public static class ICollisionEventJobExtensions
    {
#if !HAVOK_PHYSICS_EXISTS

        /// <summary>   Default Schedule() implementation for ICollisionEventsJob. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="jobData">              The jobData to act on. </param>
        /// <param name="simulationSingleton">  The simulation singleton. </param>
        /// <param name="inputDeps">            The input deps. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        public static unsafe JobHandle Schedule<T>(this T jobData, SimulationSingleton simulationSingleton, JobHandle inputDeps)
            where T : struct, ICollisionEventsJobBase
        {
            // Should work only for UnityPhysics
            if (simulationSingleton.Type != SimulationType.UnityPhysics)
            {
                return inputDeps;
            }

            return ScheduleUnityPhysicsCollisionEventsJob(jobData, simulationSingleton.AsSimulation(), inputDeps);
        }

#else

        /// <summary>
        /// In this case Schedule() implementation for ICollisionEventsJob is provided by the
        /// Havok.Physics assembly.
        ///  This is a stub to catch when that assembly is missing.
        /// <todo.eoin.modifier Put in a link to documentation for this:
        /// </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="jobData">              The jobData to act on. </param>
        /// <param name="simulationSingleton">  The simulation singleton. </param>
        /// <param name="inputDeps">            The input deps. </param>
        /// <param name="_causeCompileError">   (Optional) The cause compile error. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        [Obsolete("This error occurs when HAVOK_PHYSICS_EXISTS is defined but Havok.Physics is missing from your package's asmdef references. (DoNotRemove)", true)]
        public static unsafe JobHandle Schedule<T>(this T jobData, SimulationSingleton simulationSingleton, JobHandle inputDeps,
            HAVOK_PHYSICS_MISSING_FROM_ASMDEF _causeCompileError = HAVOK_PHYSICS_MISSING_FROM_ASMDEF.HAVOK_PHYSICS_MISSING_FROM_ASMDEF)
            where T : struct, ICollisionEventsJobBase
        {
            return new JobHandle();
        }

        /// <summary>   Values that represent havok physics missing from asmdefs. </summary>
        public enum HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        {
            HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        }
#endif

        internal static unsafe JobHandle ScheduleUnityPhysicsCollisionEventsJob<T>(T jobData, Simulation simulation, JobHandle inputDeps)
            where T : struct, ICollisionEventsJobBase
        {
            // Idle means before or after simulation, which is fine in 99% of cases - the one case where we have trouble is the following:
            // Sim type == Unity.Physics
            // The simulation hasn't run at least once (can happen if we put [UpdateBefore(typeof(PhysicsCreateBdoyPairsGroup)] on the first frame, so we need extra checks
            SafetyChecks.CheckSimulationStageAndThrow(simulation.m_SimulationScheduleStage, SimulationScheduleStage.Idle);
            if (!simulation.ReadyForEventScheduling)
                return inputDeps;

            var data = new CollisionEventJobData<T>
            {
                UserJobData = jobData,
                EventReader = simulation.CollisionEvents
            };

            var jobReflectionData = CollisionEventJobProcess<T>.jobReflectionData.Data;
            CollisionEventJobProcess<T>.CheckReflectionDataCorrect(jobReflectionData);

            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), jobReflectionData, inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
        }

        internal unsafe struct CollisionEventJobData<T> where T : struct
        {
            public T UserJobData;
            public CollisionEvents EventReader;
        }

        internal struct CollisionEventJobProcess<T> where T : struct, ICollisionEventsJobBase
        {
            internal static readonly SharedStatic<IntPtr> jobReflectionData = SharedStatic<IntPtr>.GetOrCreate<CollisionEventJobProcess<T>>();

            [Preserve]
            public static void Initialize()
            {
                if (jobReflectionData.Data == IntPtr.Zero)
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(CollisionEventJobData<T>), typeof(T), (ExecuteJobFunction)Execute);
            }

            public delegate void ExecuteJobFunction(ref CollisionEventJobData<T> jobData, IntPtr additionalData, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(ref CollisionEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                foreach (CollisionEvent collisionEvent in jobData.EventReader)
                {
                    jobData.UserJobData.Execute(collisionEvent);
                }
            }

            [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECK")]
            internal static void CheckReflectionDataCorrect(IntPtr reflectionData)
            {
                if (reflectionData == IntPtr.Zero)
                    SafetyChecks.ThrowInvalidOperationException("Reflection data was not set up by an Initialize() call");
            }
        }

        /// <summary>   Early job initialize. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        public static void EarlyJobInit<T>()
            where T : struct, ICollisionEventsJobBase
        {
            CollisionEventJobProcess<T>.Initialize();
        }
    }
}
