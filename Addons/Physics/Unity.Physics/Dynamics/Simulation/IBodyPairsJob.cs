using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Physics
{
    /// <summary>
    /// INTERNAL UnityPhysics interface for jobs that iterate through the list of potentially
    /// overlapping body pairs produced by the broad phase Important: Only use inside UnityPhysics
    /// code! Jobs in other projects should implement IBodyPairsJob.
    /// </summary>
    [JobProducerType(typeof(IBodyPairsJobExtensions.BodyPairsJobProcess<>))]
    public interface IBodyPairsJobBase
    {
        /// <summary>   Execute operation on a given pair. </summary>
        ///
        /// <param name="pair"> [in,out] The pair. </param>
        void Execute(ref ModifiableBodyPair pair);
    }

#if !HAVOK_PHYSICS_EXISTS

    /// <summary>
    /// Interface for jobs that iterate through the list of potentially overlapping body pairs
    /// produced by the broad phase.
    /// </summary>
    public interface IBodyPairsJob : IBodyPairsJobBase
    {
    }

#endif

    /// <summary>   A modifiable body pair. </summary>
    public struct ModifiableBodyPair
    {
        internal EntityPair EntityPair;
        internal BodyIndexPair BodyIndexPair;

        /// <summary>   Gets the entity b. </summary>
        ///
        /// <value> The entity b. </value>
        public Entity EntityB => EntityPair.EntityB;

        /// <summary>   Gets the entity a. </summary>
        ///
        /// <value> The entity a. </value>
        public Entity EntityA => EntityPair.EntityA;

        /// <summary>   Gets the body index b. </summary>
        ///
        /// <value> The body index b. </value>
        public int BodyIndexB => BodyIndexPair.BodyIndexB;

        /// <summary>   Gets the body index a. </summary>
        ///
        /// <value> The body index a. </value>
        public int BodyIndexA => BodyIndexPair.BodyIndexA;

        /// <summary>   Disables this pair. </summary>
        public void Disable()
        {
            BodyIndexPair = BodyIndexPair.Invalid;
        }
    }

    /// <summary>   A body pairs job extensions. </summary>
    public static class IBodyPairsJobExtensions
    {
#if !HAVOK_PHYSICS_EXISTS

        /// <summary>   Default Schedule() implementation for IBodyPairsJob. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="jobData">              The jobData to act on. </param>
        /// <param name="simulationSingleton">  The simulation singleton. </param>
        /// <param name="world">                [in,out] The world. </param>
        /// <param name="inputDeps">            The input deps. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        public static unsafe JobHandle Schedule<T>(this T jobData, SimulationSingleton simulationSingleton, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, IBodyPairsJobBase
        {
            // Should work only for UnityPhysics
            if (simulationSingleton.Type != SimulationType.UnityPhysics)
            {
                return inputDeps;
            }

            return ScheduleUnityPhysicsBodyPairsJob(jobData, simulationSingleton.AsSimulation(), ref world, inputDeps);
        }

#else

        /// <summary>
        /// In this case Schedule() implementation for IBodyPairsJob is provided by the Havok.Physics
        /// assembly.
        /// This is a stub to catch when that assembly is missing.
        /// </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="jobData">              The jobData to act on. </param>
        /// <param name="simulationSingleton">  The simulation singleton. </param>
        /// <param name="world">                [in,out] The world. </param>
        /// <param name="inputDeps">            The input deps. </param>
        /// <param name="_causeCompileError">   (Optional) The cause compile error. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        [Obsolete("This error occurs when HAVOK_PHYSICS_EXISTS is defined but Havok.Physics is missing from your package's asmdef references. (DoNotRemove)", true)]
        public static unsafe JobHandle Schedule<T>(this T jobData, SimulationSingleton simulationSingleton, ref PhysicsWorld world, JobHandle inputDeps,
            HAVOK_PHYSICS_MISSING_FROM_ASMDEF _causeCompileError = HAVOK_PHYSICS_MISSING_FROM_ASMDEF.HAVOK_PHYSICS_MISSING_FROM_ASMDEF)
            where T : struct, IBodyPairsJobBase
        {
            return new JobHandle();
        }

        /// <summary>   Values that represent havok physics missing from asmdefs. </summary>
        public enum HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        {
            HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        }
#endif

        internal static unsafe JobHandle ScheduleUnityPhysicsBodyPairsJob<T>(T jobData, Simulation simulation, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, IBodyPairsJobBase
        {
            SafetyChecks.CheckSimulationStageAndThrow(simulation.m_SimulationScheduleStage, SimulationScheduleStage.PostCreateBodyPairs);

            var data = new BodyPairsJobData<T>
            {
                UserJobData = jobData,
                PhasedDispatchPairs = simulation.StepContext.PhasedDispatchPairs.AsDeferredJobArray(),
                Bodies = world.Bodies
            };

            var jobReflectionData = BodyPairsJobProcess<T>.jobReflectionData.Data;
            BodyPairsJobProcess<T>.CheckReflectionDataCorrect(jobReflectionData);

            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), jobReflectionData, inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
        }

        internal struct BodyPairsJobData<T> where T : struct
        {
            public T UserJobData;
            public NativeArray<DispatchPairSequencer.DispatchPair> PhasedDispatchPairs;
            //Need to disable aliasing restriction in case T has a NativeArray of PhysicsWorld.Bodies:
            [ReadOnly][NativeDisableContainerSafetyRestriction] public NativeArray<RigidBody> Bodies;
        }

        internal struct BodyPairsJobProcess<T> where T : struct, IBodyPairsJobBase
        {
            internal static readonly SharedStatic<IntPtr> jobReflectionData = SharedStatic<IntPtr>.GetOrCreate<BodyPairsJobProcess<T>>();

            [Preserve]
            internal static void Initialize()
            {
                if (jobReflectionData.Data == IntPtr.Zero)
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(BodyPairsJobData<T>), typeof(T), (ExecuteJobFunction)Execute);
            }

            public delegate void ExecuteJobFunction(ref BodyPairsJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(ref BodyPairsJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                for (int currentIdx = 0; currentIdx < jobData.PhasedDispatchPairs.Length; currentIdx++)
                {
                    DispatchPairSequencer.DispatchPair dispatchPair = jobData.PhasedDispatchPairs[currentIdx];

                    // Skip joint pairs and invalid pairs
                    if (dispatchPair.IsJoint || !dispatchPair.IsValid)
                    {
                        continue;
                    }

                    var pair = new ModifiableBodyPair
                    {
                        BodyIndexPair = new BodyIndexPair { BodyIndexA = dispatchPair.BodyIndexA, BodyIndexB = dispatchPair.BodyIndexB },
                        EntityPair = new EntityPair
                        {
                            EntityA = jobData.Bodies[dispatchPair.BodyIndexA].Entity,
                            EntityB = jobData.Bodies[dispatchPair.BodyIndexB].Entity
                        }
                    };

                    jobData.UserJobData.Execute(ref pair);

                    if (pair.BodyIndexA == -1 || pair.BodyIndexB == -1)
                    {
                        jobData.PhasedDispatchPairs[currentIdx] = DispatchPairSequencer.DispatchPair.Invalid;
                    }
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
            where T : struct, IBodyPairsJobBase
        {
            BodyPairsJobProcess<T>.Initialize();
        }
    }
}
