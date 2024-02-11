using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>
    /// INTERNAL UnityPhysics interface for jobs that iterate through the list of Jacobians before
    /// they are solved Important: Only use inside UnityPhysics code! Jobs in other projects should
    /// implement IJacobiansJob.
    /// </summary>
    [JobProducerType(typeof(IJacobiansJobExtensions.JacobiansJobProcess<>))]
    public interface IJacobiansJobBase
    {
        /// <summary>
        /// Executes an operation on a header and a contact jacobian.
        /// Note, multiple Jacobians can share the same header.
        /// </summary>
        ///
        /// <param name="header">   [in,out] The header. </param>
        /// <param name="jacobian"> [in,out] The jacobian. </param>
        void Execute(ref ModifiableJacobianHeader header, ref ModifiableContactJacobian jacobian);

        /// <summary>   Executes an operation on a header and a trigger jacobian. </summary>
        ///
        /// <param name="header">   [in,out] The header. </param>
        /// <param name="jacobian"> [in,out] The jacobian. </param>
        void Execute(ref ModifiableJacobianHeader header, ref ModifiableTriggerJacobian jacobian);
    }

#if !HAVOK_PHYSICS_EXISTS

    /// <summary>
    /// Interface for jobs that iterate through the list of Jacobians before they are solved.
    /// </summary>
    public interface IJacobiansJob : IJacobiansJobBase
    {
    }

#endif

    /// <summary>   A modifiable jacobian header. </summary>
    public unsafe struct ModifiableJacobianHeader
    {
        internal JacobianHeader* m_Header;

        /// <summary>   Gets a value indicating whether the modifiers was changed. </summary>
        ///
        /// <value> True if modifiers changed, false if not. </value>

        public bool ModifiersChanged { get; private set; }

        /// <summary>   Gets a value indicating whether the angular was changed. </summary>
        ///
        /// <value> True if angular changed, false if not. </value>

        public bool AngularChanged { get; private set; }

        internal EntityPair EntityPair;

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

        public int BodyIndexB => m_Header->BodyPair.BodyIndexB;

        /// <summary>   Gets the body index a. </summary>
        ///
        /// <value> The body index a. </value>

        public int BodyIndexA => m_Header->BodyPair.BodyIndexA;

        /// <summary>   Gets the Jacobian type. </summary>
        ///
        /// <value> The Jacobian type. </value>

        public JacobianType Type => m_Header->Type;

        /// <summary>   Gets or sets the Jacobian flags. </summary>
        ///
        /// <value> The Jacobian flags. </value>

        public JacobianFlags Flags
        {
            get => m_Header->Flags;
            set
            {
                // Some flags change the size of the jacobian; don't allow these to be changed:
                byte notPermitted = (byte)(JacobianFlags.EnableSurfaceVelocity | JacobianFlags.EnableMassFactors | JacobianFlags.EnableCollisionEvents);
                byte userFlags = (byte)value;
                byte alreadySet = (byte)m_Header->Flags;

                if ((notPermitted & (userFlags ^ alreadySet)) != 0)
                {
                    SafetyChecks.ThrowNotSupportedException("Cannot change flags which alter jacobian size");
                    return;
                }

                m_Header->Flags = value;
            }
        }

        /// <summary>   Gets a value indicating whether this object has mass factors. </summary>
        ///
        /// <value> True if this object has mass factors, false if not. </value>

        public bool HasMassFactors => m_Header->HasMassFactors;

        /// <summary>   Gets or sets the mass factors. </summary>
        ///
        /// <value> The mass factors. </value>

        public MassFactors MassFactors
        {
            get => m_Header->MassFactors;
            set
            {
                m_Header->MassFactors = value;
                ModifiersChanged = true;
            }
        }

        /// <summary>   Gets a value indicating whether this object has surface velocity. </summary>
        ///
        /// <value> True if this object has surface velocity, false if not. </value>

        public bool HasSurfaceVelocity => m_Header->HasSurfaceVelocity;

        /// <summary>   Gets or sets the surface velocity. </summary>
        ///
        /// <value> The surface velocity. </value>

        public SurfaceVelocity SurfaceVelocity
        {
            get => m_Header->SurfaceVelocity;
            set
            {
                m_Header->SurfaceVelocity = value;
                ModifiersChanged = true;
            }
        }

        /// <summary>   Gets angular jacobian. </summary>
        ///
        /// <param name="i">    Zero-based index of the jacobian. </param>
        ///
        /// <returns>   The angular jacobian. </returns>

        public ContactJacAngAndVelToReachCp GetAngularJacobian(int i)
        {
            return m_Header->AccessAngularJacobian(i);
        }

        /// <summary>   Sets angular jacobian. </summary>
        ///
        /// <param name="i">    Zero-based index of the jacobian. </param>
        /// <param name="j">    A ContactJacAngAndVelToReachCp to set. </param>

        public void SetAngularJacobian(int i, ContactJacAngAndVelToReachCp j)
        {
            m_Header->AccessAngularJacobian(i) = j;
            AngularChanged = true;
        }
    }

    /// <summary>   A modifiable contact jacobian. </summary>
    public unsafe struct ModifiableContactJacobian
    {
        internal ContactJacobian* m_ContactJacobian;

        /// <summary>   Gets a value indicating whether this object is modified. </summary>
        ///
        /// <value> True if modified, false if not. </value>
        public bool Modified { get; private set; }

        /// <summary>   Gets the number of contacts. </summary>
        ///
        /// <value> The total number of contacts. </value>
        public int NumContacts => m_ContactJacobian->BaseJacobian.NumContacts;

        /// <summary>   Gets or sets the normal. </summary>
        ///
        /// <value> The normal. </value>
        public float3 Normal
        {
            get => m_ContactJacobian->BaseJacobian.Normal;
            set
            {
                m_ContactJacobian->BaseJacobian.Normal = value;
                Modified = true;
            }
        }

        /// <summary>   Gets or sets the coefficient of friction. </summary>
        ///
        /// <value> The coefficient of friction. </value>
        public float CoefficientOfFriction
        {
            get => m_ContactJacobian->CoefficientOfFriction;
            set
            {
                m_ContactJacobian->CoefficientOfFriction = value;
                Modified = true;
            }
        }

        /// <summary>   Gets or sets the friction 0. </summary>
        ///
        /// <value> The friction 0. </value>
        public ContactJacobianAngular Friction0
        {
            get => m_ContactJacobian->Friction0;
            set
            {
                m_ContactJacobian->Friction0 = value;
                Modified = true;
            }
        }

        /// <summary>   Gets or sets the friction 1. </summary>
        ///
        /// <value> The friction 1. </value>
        public ContactJacobianAngular Friction1
        {
            get => m_ContactJacobian->Friction1;
            set
            {
                m_ContactJacobian->Friction1 = value;
                Modified = true;
            }
        }

        /// <summary>   Gets or sets the angular friction. </summary>
        ///
        /// <value> The angular friction. </value>
        public ContactJacobianAngular AngularFriction
        {
            get => m_ContactJacobian->AngularFriction;
            set
            {
                m_ContactJacobian->AngularFriction = value;
                Modified = true;
            }
        }
    }

    /// <summary>   A modifiable trigger jacobian. </summary>
    public struct ModifiableTriggerJacobian
    {
        internal unsafe TriggerJacobian* m_TriggerJacobian;
    }

    /// <summary>   The jacobians job extensions. </summary>
    public static class IJacobiansJobExtensions
    {
#if !HAVOK_PHYSICS_EXISTS

        /// <summary>   Default Schedule() implementation for IJacobiansJob. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="jobData">              The jobData to act on. </param>
        /// <param name="simulationSingleton">  The simulation singleton. </param>
        /// <param name="world">                [in,out] The world. </param>
        /// <param name="inputDeps">            The input deps. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        public static unsafe JobHandle Schedule<T>(this T jobData, SimulationSingleton simulationSingleton, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, IJacobiansJobBase
        {
            // Should work only for UnityPhysics
            if (simulationSingleton.Type != SimulationType.UnityPhysics)
            {
                return inputDeps;
            }

            return ScheduleUnityPhysicsJacobiansJob(jobData, simulationSingleton.AsSimulation(), ref world, inputDeps);
        }

#else

        /// <summary>
        /// In this case Schedule() implementation for IJacobiansJob is provided by the Havok.Physics
        /// assembly.
        ///  This is a stub to catch when that assembly is missing.
        /// <todo.eoin.modifier Put in a link to documentation for this:
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
            where T : struct, IJacobiansJobBase
        {
            return new JobHandle();
        }

        /// <summary>   Values that represent havok physics missing from asmdefs. </summary>
        public enum HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        {
            HAVOK_PHYSICS_MISSING_FROM_ASMDEF
        }
#endif

        internal static unsafe JobHandle ScheduleUnityPhysicsJacobiansJob<T>(T jobData, Simulation simulation, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, IJacobiansJobBase
        {
            SafetyChecks.CheckSimulationStageAndThrow(simulation.m_SimulationScheduleStage, SimulationScheduleStage.PostCreateJacobians);

            var data = new JacobiansJobData<T>
            {
                UserJobData = jobData,
                StreamReader = simulation.StepContext.Jacobians.AsReader(),
                NumWorkItems = simulation.StepContext.SolverSchedulerInfo.NumWorkItems,
                Bodies = world.Bodies
            };

            var jobReflectionData = JacobiansJobProcess<T>.jobReflectionData.Data;
            JacobiansJobProcess<T>.CheckReflectionDataCorrect(jobReflectionData);

            var parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref data), jobReflectionData, inputDeps, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
        }

        internal unsafe struct JacobiansJobData<T> where T : struct
        {
            public T UserJobData;
            public NativeStream.Reader StreamReader;

            [ReadOnly] public NativeArray<int> NumWorkItems;

            // Disable aliasing restriction in case T has a NativeArray of PhysicsWorld.Bodies
            [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<RigidBody> Bodies;

            int m_CurrentWorkItem;

            public bool HasItemsLeft => StreamReader.RemainingItemCount > 0;

            public JacobianHeader* ReadJacobianHeader()
            {
                int readSize = Read<int>();
                return (JacobianHeader*)Read(readSize);
            }

            private byte* Read(int size)
            {
                byte* dataPtr = StreamReader.ReadUnsafePtr(size);
                MoveReaderToNextForEachIndex();
                return dataPtr;
            }

            private ref T2 Read<T2>() where T2 : struct
            {
                int size = UnsafeUtility.SizeOf<T2>();
                return ref UnsafeUtility.AsRef<T2>(Read(size));
            }

            public void MoveReaderToNextForEachIndex()
            {
                int numWorkItems = NumWorkItems[0];
                while (StreamReader.RemainingItemCount == 0 && m_CurrentWorkItem < numWorkItems)
                {
                    StreamReader.BeginForEachIndex(m_CurrentWorkItem);
                    m_CurrentWorkItem++;
                }
            }
        }

        internal struct JacobiansJobProcess<T> where T : struct, IJacobiansJobBase
        {
            internal static readonly SharedStatic<IntPtr> jobReflectionData = SharedStatic<IntPtr>.GetOrCreate<JacobiansJobProcess<T>>();

            [Preserve]
            public static void Initialize()
            {
                if (jobReflectionData.Data == IntPtr.Zero)
                    jobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JacobiansJobData<T>), typeof(T), (ExecuteJobFunction)Execute);
            }

            [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECK")]
            internal static void CheckReflectionDataCorrect(IntPtr reflectionData)
            {
                if (reflectionData == IntPtr.Zero)
                    SafetyChecks.ThrowInvalidOperationException("Reflection data was not set up by an Initialize() call");
            }

            public delegate void ExecuteJobFunction(ref JacobiansJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(ref JacobiansJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                jobData.MoveReaderToNextForEachIndex();
                while (jobData.HasItemsLeft)
                {
                    JacobianHeader* header = jobData.ReadJacobianHeader();

                    var h = new ModifiableJacobianHeader
                    {
                        m_Header = header,
                        EntityPair = new EntityPair
                        {
                            EntityA = jobData.Bodies[header->BodyPair.BodyIndexA].Entity,
                            EntityB = jobData.Bodies[header->BodyPair.BodyIndexB].Entity
                        }
                    };
                    if (header->Type == JacobianType.Contact)
                    {
                        var contact = new ModifiableContactJacobian
                        {
                            m_ContactJacobian = (ContactJacobian*)UnsafeUtility.AddressOf(ref header->AccessBaseJacobian<ContactJacobian>())
                        };
                        jobData.UserJobData.Execute(ref h, ref contact);
                    }
                    else if (header->Type == JacobianType.Trigger)
                    {
                        var trigger = new ModifiableTriggerJacobian
                        {
                            m_TriggerJacobian = (TriggerJacobian*)UnsafeUtility.AddressOf(ref header->AccessBaseJacobian<TriggerJacobian>())
                        };

                        jobData.UserJobData.Execute(ref h, ref trigger);
                    }
                }
            }
        }

        /// <summary>   Early job initialize. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        public static void EarlyJobInit<T>()
            where T : struct, IJacobiansJobBase
        {
            JacobiansJobProcess<T>.Initialize();
        }
    }
}
