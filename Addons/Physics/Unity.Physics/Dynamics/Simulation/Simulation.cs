using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>
    /// Holds temporary data in a storage that lives as long as simulation lives and is only re-
    /// allocated if necessary.
    /// </summary>
    public struct SimulationContext : IDisposable
    {
        private int m_NumDynamicBodies;
        private NativeArray<Velocity> m_InputVelocities;

        // Solver stabilization data (it's completely ok to be unallocated)
        [NativeDisableContainerSafetyRestriction]
        private NativeArray<Solver.StabilizationMotionData> m_SolverStabilizationMotionData;
        internal NativeArray<Solver.StabilizationMotionData> SolverStabilizationMotionData => m_SolverStabilizationMotionData.GetSubArray(0, m_NumDynamicBodies);

        internal float TimeStep;

        internal NativeArray<Velocity> InputVelocities => m_InputVelocities.GetSubArray(0, m_NumDynamicBodies);

        internal NativeStream CollisionEventDataStream;
        internal NativeStream TriggerEventDataStream;
        internal NativeStream ImpulseEventDataStream;

        /// <summary>   Gets the collision events. </summary>
        ///
        /// <value> The collision events. </value>
        public CollisionEvents CollisionEvents => new CollisionEvents(CollisionEventDataStream, InputVelocities, TimeStep);

        /// <summary>   Gets the trigger events. </summary>
        ///
        /// <value> The trigger events. </value>
        public TriggerEvents TriggerEvents => new TriggerEvents(TriggerEventDataStream);

        /// <summary>   Gets the impulse events. </summary>
        ///
        /// <value> The impulse events. </value>
        public ImpulseEvents ImpulseEvents => new ImpulseEvents(ImpulseEventDataStream);

        private NativeArray<int> WorkItemCount;

        internal bool ReadyForEventScheduling => m_InputVelocities.IsCreated && CollisionEventDataStream.IsCreated && TriggerEventDataStream.IsCreated && ImpulseEventDataStream.IsCreated;

        /// <summary>
        /// Resets the simulation storage
        /// - Reallocates input velocities storage if necessary
        /// - Disposes event streams and allocates new ones with a single work item
        /// NOTE: Reset or ScheduleReset needs to be called before passing the SimulationContext to a
        /// simulation step job. If you don't then you may get initialization errors.
        /// </summary>
        ///
        /// <param name="stepInput">    The step input. </param>
        public void Reset(SimulationStepInput stepInput)
        {
            m_NumDynamicBodies = stepInput.World.NumDynamicBodies;
            if (!m_InputVelocities.IsCreated || m_InputVelocities.Length < m_NumDynamicBodies)
            {
                if (m_InputVelocities.IsCreated)
                {
                    m_InputVelocities.Dispose();
                }
                m_InputVelocities = new NativeArray<Velocity>(m_NumDynamicBodies, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            // Solver stabilization data
            if (stepInput.SolverStabilizationHeuristicSettings.EnableSolverStabilization)
            {
                if (!m_SolverStabilizationMotionData.IsCreated || m_SolverStabilizationMotionData.Length < m_NumDynamicBodies)
                {
                    if (m_SolverStabilizationMotionData.IsCreated)
                    {
                        m_SolverStabilizationMotionData.Dispose();
                    }
                    m_SolverStabilizationMotionData = new NativeArray<Solver.StabilizationMotionData>(m_NumDynamicBodies, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }
                else if (m_NumDynamicBodies > 0)
                {
                    unsafe
                    {
                        UnsafeUtility.MemClear(m_SolverStabilizationMotionData.GetUnsafePtr(), m_NumDynamicBodies * UnsafeUtility.SizeOf<Solver.StabilizationMotionData>());
                    }
                }
            }

            if (CollisionEventDataStream.IsCreated)
            {
                CollisionEventDataStream.Dispose();
            }
            if (TriggerEventDataStream.IsCreated)
            {
                TriggerEventDataStream.Dispose();
            }
            if (ImpulseEventDataStream.IsCreated)
            {
                ImpulseEventDataStream.Dispose();
            }

            {
                if (!WorkItemCount.IsCreated)
                {
                    WorkItemCount = new NativeArray<int>(1, Allocator.Persistent);
                    WorkItemCount[0] = 1;
                }
                CollisionEventDataStream = new NativeStream(WorkItemCount[0], Allocator.Persistent);
                TriggerEventDataStream = new NativeStream(WorkItemCount[0], Allocator.Persistent);
                ImpulseEventDataStream = new NativeStream(WorkItemCount[0], Allocator.Persistent);
            }
        }

        // TODO: We need to make a public version of ScheduleReset for use with
        // local simulation calling StepImmediate and chaining jobs over a number
        // of steps. This becomes a problem if new bodies are added to the world
        // between simulation steps.
        // A public version could take the form:
        //         public JobHandle ScheduleReset(ref PhysicsWorld world, JobHandle inputDeps = default)
        //         {
        //             return ScheduleReset(ref world, inputDeps, true);
        //         }
        // However, to make that possible we need a why to allocate InputVelocities within a job.
        // The core simulation does not chain jobs across multiple simulation steps and so
        // will not hit this issue.
        internal JobHandle ScheduleReset(SimulationStepInput stepInput, JobHandle inputDeps, bool allocateEventDataStreams)
        {
            m_NumDynamicBodies = stepInput.World.NumDynamicBodies;
            if (!m_InputVelocities.IsCreated || m_InputVelocities.Length < m_NumDynamicBodies)
            {
                // TODO: can we find a way to setup InputVelocities within a job?
                if (m_InputVelocities.IsCreated)
                {
                    m_InputVelocities.Dispose();
                }
                m_InputVelocities = new NativeArray<Velocity>(m_NumDynamicBodies, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            // Solver stabilization data
            if (stepInput.SolverStabilizationHeuristicSettings.EnableSolverStabilization)
            {
                if (!m_SolverStabilizationMotionData.IsCreated || m_SolverStabilizationMotionData.Length < m_NumDynamicBodies)
                {
                    if (m_SolverStabilizationMotionData.IsCreated)
                    {
                        m_SolverStabilizationMotionData.Dispose();
                    }
                    m_SolverStabilizationMotionData = new NativeArray<Solver.StabilizationMotionData>(m_NumDynamicBodies, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }
                else if (m_NumDynamicBodies > 0)
                {
                    unsafe
                    {
                        UnsafeUtility.MemClear(m_SolverStabilizationMotionData.GetUnsafePtr(), m_NumDynamicBodies * UnsafeUtility.SizeOf<Solver.StabilizationMotionData>());
                    }
                }
            }

            var handle = inputDeps;
            if (CollisionEventDataStream.IsCreated)
            {
                handle = CollisionEventDataStream.Dispose(handle);
            }
            if (TriggerEventDataStream.IsCreated)
            {
                handle = TriggerEventDataStream.Dispose(handle);
            }
            if (ImpulseEventDataStream.IsCreated)
            {
                handle = ImpulseEventDataStream.Dispose(handle);
            }
            if (allocateEventDataStreams)
            {
                if (!WorkItemCount.IsCreated)
                {
                    WorkItemCount = new NativeArray<int>(1, Allocator.Persistent);
                    WorkItemCount[0] = 1;
                }
                handle = NativeStream.ScheduleConstruct(out CollisionEventDataStream, WorkItemCount, handle, Allocator.Persistent);
                handle = NativeStream.ScheduleConstruct(out TriggerEventDataStream, WorkItemCount, handle, Allocator.Persistent);
                handle = NativeStream.ScheduleConstruct(out ImpulseEventDataStream, WorkItemCount, handle, Allocator.Persistent);
            }
            return handle;
        }

        /// <summary>
        /// Disposes the simulation context.
        /// </summary>
        public void Dispose()
        {
            if (m_InputVelocities.IsCreated)
            {
                m_InputVelocities.Dispose();
            }

            if (m_SolverStabilizationMotionData.IsCreated)
            {
                m_SolverStabilizationMotionData.Dispose();
            }

            if (CollisionEventDataStream.IsCreated)
            {
                CollisionEventDataStream.Dispose();
            }

            if (TriggerEventDataStream.IsCreated)
            {
                TriggerEventDataStream.Dispose();
            }

            if (ImpulseEventDataStream.IsCreated)
            {
                ImpulseEventDataStream.Dispose();
            }

            if (WorkItemCount.IsCreated)
            {
                WorkItemCount.Dispose();
            }
        }
    }

    // Temporary data created and destroyed during the step
    internal struct StepContext
    {
        // Built by the scheduler. Groups body pairs into phases in which each
        // body appears at most once, so that the interactions within each phase can be solved
        // in parallel with each other but not with other phases. This is consumed by the
        // ProcessBodyPairsJob, which outputs contact and joint Jacobians.
        public NativeList<DispatchPairSequencer.DispatchPair> PhasedDispatchPairs;

        // Job handle for the scheduler's job that creates the phased dispatch pairs.
        // Results will appear in the SolverSchedulerInfo property upon job completion.
        public JobHandle CreatePhasedDispatchPairsJobHandle;

        // Built by the scheduler. Describes the grouping of phased dispatch pairs for parallel processing
        // of joints and contacts in the solver.
        // Informs how we can schedule the solver jobs and what data locations they read info from.
        public DispatchPairSequencer.SolverSchedulerInfo SolverSchedulerInfo;

        public NativeStream Contacts;
        public NativeStream Jacobians;
    }

    /// <summary>   Steps a physics world. </summary>
    public struct Simulation : ISimulation
    {
        /// <summary>   Gets the simulation type. </summary>
        ///
        /// <value> <see cref="SimulationType.UnityPhysics"/>. </value>
        public SimulationType Type => SimulationType.UnityPhysics;

        /// <summary>   Gets the handle of the final simulation job (not including dispose jobs). </summary>
        ///
        /// <value> The final simulation job handle. </value>
        public JobHandle FinalSimulationJobHandle => m_StepHandles.FinalExecutionHandle;

        /// <summary>   Gets the handle of the final job. </summary>
        ///
        /// <value> The final job handle. </value>
        public JobHandle FinalJobHandle => JobHandle.CombineDependencies(FinalSimulationJobHandle, m_StepHandles.FinalDisposeHandle);

        internal SimulationScheduleStage m_SimulationScheduleStage;

        internal StepContext StepContext;

        /// <summary>   Gets the contacts stream. </summary>
        ///
        /// This value is only valid after the CreateContactsJob (Narrowphase System), and before BuildJacobiansJob (CreateJacobiansSystem)
        ///
        /// <value> The contacts stream-->. </value>
        public readonly NativeStream Contacts => StepContext.Contacts;

        /// <summary>   Gets the collision events. </summary>
        ///
        /// <value> The collision events. </value>
        public CollisionEvents CollisionEvents => SimulationContext.CollisionEvents;

        /// <summary>   Gets the trigger events. </summary>
        ///
        /// <value> The trigger events. </value>
        public TriggerEvents TriggerEvents => SimulationContext.TriggerEvents;

        /// <summary>   Gets the impulse events. </summary>
        ///
        /// <value> The impulse events. </value>
        public ImpulseEvents ImpulseEvents => SimulationContext.ImpulseEvents;

        internal SimulationContext SimulationContext;

        private DispatchPairSequencer m_Scheduler;
        internal SimulationJobHandles m_StepHandles;

        internal bool ReadyForEventScheduling => SimulationContext.ReadyForEventScheduling;

        /// <summary>   Creates a new Simulation. </summary>
        ///
        /// <returns>   A Simulation. </returns>
        public static Simulation Create()
        {
            Simulation sim = new Simulation();
            sim.Init();
            return sim;
        }

        /// <summary>
        /// Disposes the simulation.
        /// </summary>
        public void Dispose()
        {
            m_Scheduler.Dispose();
            SimulationContext.Dispose();
        }

        private void Init()
        {
            StepContext = new StepContext();
            SimulationContext = new SimulationContext();
            m_Scheduler = DispatchPairSequencer.Create();
            m_StepHandles = new SimulationJobHandles(new JobHandle());
            m_SimulationScheduleStage = SimulationScheduleStage.Idle;
        }

        /// <summary>
        /// Steps the simulation immediately on a single thread without spawning any jobs.
        /// </summary>
        ///
        /// <param name="input">                The input. </param>
        /// <param name="simulationContext">    [in,out] Context for the simulation. </param>
        public static void StepImmediate(SimulationStepInput input, ref SimulationContext simulationContext)
        {
            SafetyChecks.CheckFiniteAndPositiveAndThrow(input.TimeStep, nameof(input.TimeStep));
            SafetyChecks.CheckInRangeAndThrow(input.NumSolverIterations, new int2(1, int.MaxValue), nameof(input.NumSolverIterations));

            if (input.World.NumDynamicBodies == 0)
            {
                // No need to do anything, since nothing can move
                return;
            }

            // Inform the context of the timeStep
            simulationContext.TimeStep = input.TimeStep;

            // Find all body pairs that overlap in the broadphase
            var dynamicVsDynamicBodyPairs = new NativeStream(1, Allocator.Temp);
            var dynamicVsStaticBodyPairs = new NativeStream(1, Allocator.Temp);
            {
                var dynamicVsDynamicBodyPairsWriter = dynamicVsDynamicBodyPairs.AsWriter();
                var dynamicVsStaticBodyPairsWriter = dynamicVsStaticBodyPairs.AsWriter();
                input.World.CollisionWorld.FindOverlaps(ref dynamicVsDynamicBodyPairsWriter, ref dynamicVsStaticBodyPairsWriter);
            }

            // Create dispatch pairs
            var dispatchPairs = new NativeList<DispatchPairSequencer.DispatchPair>(Allocator.Temp);
            DispatchPairSequencer.CreateDispatchPairs(ref dynamicVsDynamicBodyPairs, ref dynamicVsStaticBodyPairs,
                input.World.NumDynamicBodies, input.World.Joints, ref dispatchPairs);

            // Apply gravity and copy input velocities
            Solver.ApplyGravityAndCopyInputVelocities(input.World.DynamicsWorld.MotionVelocities,
                simulationContext.InputVelocities, input.TimeStep * input.Gravity);

            // Narrow phase
            var contacts = new NativeStream(1, Allocator.Temp);
            {
                var contactsWriter = contacts.AsWriter();
                NarrowPhase.CreateContacts(ref input.World, dispatchPairs.AsArray(), input.TimeStep, ref contactsWriter);
            }

            // Build Jacobians
            var jacobians = new NativeStream(1, Allocator.Temp);
            {
                var contactsReader = contacts.AsReader();
                var jacobiansWriter = jacobians.AsWriter();
                Solver.BuildJacobians(ref input.World, input.TimeStep, input.Gravity, input.NumSolverIterations,
                    dispatchPairs.AsArray(), ref contactsReader, ref jacobiansWriter);
            }

            // Solve Jacobians
            {
                var jacobiansReader = jacobians.AsReader();
                var collisionEventsWriter = simulationContext.CollisionEventDataStream.AsWriter();
                var triggerEventsWriter = simulationContext.TriggerEventDataStream.AsWriter();
                var impulseEventsWriter = simulationContext.ImpulseEventDataStream.AsWriter();
                Solver.StabilizationData solverStabilizationData = new Solver.StabilizationData(input, simulationContext);
                Solver.SolveJacobians(ref jacobiansReader, input.World.DynamicsWorld.MotionVelocities,
                    input.TimeStep, input.NumSolverIterations, ref collisionEventsWriter, ref triggerEventsWriter, ref impulseEventsWriter, solverStabilizationData);
            }

            // Integrate motions
            Integrator.Integrate(input.World.DynamicsWorld.MotionDatas, input.World.DynamicsWorld.MotionVelocities, input.TimeStep);

            // Synchronize the collision world if asked for
            if (input.SynchronizeCollisionWorld)
            {
                input.World.CollisionWorld.UpdateDynamicTree(ref input.World, input.TimeStep, input.Gravity);
            }
        }

        /// <summary>   Schedule broadphase jobs. </summary>
        ///
        /// <param name="input">            The input. </param>
        /// <param name="inputDeps">        The input deps. </param>
        /// <param name="multiThreaded">    (Optional) True if multi threaded. </param>
        ///
        /// <returns>   The SimulationJobHandles. </returns>
        public SimulationJobHandles ScheduleBroadphaseJobs(SimulationStepInput input, JobHandle inputDeps, bool multiThreaded = true)
        {
            SafetyChecks.CheckFiniteAndPositiveAndThrow(input.TimeStep, nameof(input.TimeStep));
            SafetyChecks.CheckInRangeAndThrow(input.NumSolverIterations, new int2(1, int.MaxValue), nameof(input.NumSolverIterations));
            SafetyChecks.CheckSimulationStageAndThrow(m_SimulationScheduleStage, SimulationScheduleStage.Idle);
            m_SimulationScheduleStage = SimulationScheduleStage.PostCreateBodyPairs;

            // Dispose and reallocate input velocity buffer, if dynamic body count has increased.
            // Dispose previous collision, trigger and impulse event data streams.
            // New event streams are reallocated later when the work item count is known.
            JobHandle handle = SimulationContext.ScheduleReset(input, inputDeps, false);
            SimulationContext.TimeStep = input.TimeStep;

            StepContext = new StepContext();

            if (input.World.NumDynamicBodies == 0)
            {
                // No need to do anything, since nothing can move
                m_StepHandles = new SimulationJobHandles(handle);
                return m_StepHandles;
            }

            // Find all body pairs that overlap in the broadphase
            var handles = input.World.CollisionWorld.ScheduleFindOverlapsJobs(
                out NativeStream dynamicVsDynamicBodyPairs, out NativeStream dynamicVsStaticBodyPairs, handle, multiThreaded);
            handle = handles.FinalExecutionHandle;
            var disposeHandle = handles.FinalDisposeHandle;
            var postOverlapsHandle = handle;

            // Sort all overlapping and jointed body pairs into phases
            handles = m_Scheduler.ScheduleCreatePhasedDispatchPairsJob(
                ref input.World, ref dynamicVsDynamicBodyPairs, ref dynamicVsStaticBodyPairs, handle,
                ref StepContext.PhasedDispatchPairs, out StepContext.SolverSchedulerInfo, multiThreaded);

            StepContext.CreatePhasedDispatchPairsJobHandle = handles.FinalExecutionHandle;

            handle = handles.FinalExecutionHandle;
            disposeHandle = JobHandle.CombineDependencies(handles.FinalDisposeHandle, disposeHandle);

            // Apply gravity and copy input velocities at this point (in parallel with the scheduler, but before the callbacks)
            var applyGravityAndCopyInputVelocitiesHandle = Solver.ScheduleApplyGravityAndCopyInputVelocitiesJob(
                input.World.DynamicsWorld.MotionVelocities, SimulationContext.InputVelocities,
                input.TimeStep * input.Gravity, multiThreaded ? postOverlapsHandle : handle, multiThreaded);

            m_StepHandles.FinalExecutionHandle = JobHandle.CombineDependencies(applyGravityAndCopyInputVelocitiesHandle, handle);
            m_StepHandles.FinalDisposeHandle = disposeHandle;

            return m_StepHandles;
        }

        /// <summary>   Schedule narrowphase jobs. </summary>
        ///
        /// <param name="input">            The input. </param>
        /// <param name="inputDeps">        The input deps. </param>
        /// <param name="multiThreaded">    (Optional) True if multi threaded. </param>
        ///
        /// <returns>   The SimulationJobHandles. </returns>
        public SimulationJobHandles ScheduleNarrowphaseJobs(SimulationStepInput input, JobHandle inputDeps, bool multiThreaded = true)
        {
            SafetyChecks.CheckSimulationStageAndThrow(m_SimulationScheduleStage, SimulationScheduleStage.PostCreateBodyPairs);
            m_SimulationScheduleStage = SimulationScheduleStage.PostCreateContacts;

            if (input.World.NumDynamicBodies == 0)
            {
                // No need to do anything, since nothing can move
                m_StepHandles = new SimulationJobHandles(inputDeps);
                return m_StepHandles;
            }

            var disposeHandle = m_StepHandles.FinalDisposeHandle;
            m_StepHandles = NarrowPhase.ScheduleCreateContactsJobs(ref input.World, input.TimeStep,
                ref StepContext.Contacts, ref StepContext.Jacobians, ref StepContext.PhasedDispatchPairs, inputDeps,
                ref StepContext.SolverSchedulerInfo, multiThreaded);
            m_StepHandles.FinalDisposeHandle = JobHandle.CombineDependencies(disposeHandle, m_StepHandles.FinalDisposeHandle);

            return m_StepHandles;
        }

        /// <summary>   Schedule create jacobians jobs. </summary>
        ///
        /// <param name="input">            The input. </param>
        /// <param name="inputDeps">        The input deps. </param>
        /// <param name="multiThreaded">    (Optional) True if multi threaded. </param>
        ///
        /// <returns>   The SimulationJobHandles. </returns>
        public SimulationJobHandles ScheduleCreateJacobiansJobs(SimulationStepInput input, JobHandle inputDeps, bool multiThreaded = true)
        {
            SafetyChecks.CheckSimulationStageAndThrow(m_SimulationScheduleStage, SimulationScheduleStage.PostCreateContacts);
            m_SimulationScheduleStage = SimulationScheduleStage.PostCreateJacobians;

            if (input.World.NumDynamicBodies == 0)
            {
                // No need to do anything, since nothing can move
                m_StepHandles = new SimulationJobHandles(inputDeps);
                return m_StepHandles;
            }


            // Create contact Jacobians
            var disposeHandle = m_StepHandles.FinalDisposeHandle;
            m_StepHandles = Solver.ScheduleBuildJacobiansJobs(ref input.World, input.TimeStep, input.Gravity, input.NumSolverIterations,
                inputDeps, ref StepContext.PhasedDispatchPairs, ref StepContext.SolverSchedulerInfo,
                ref StepContext.Contacts, ref StepContext.Jacobians, multiThreaded);
            m_StepHandles.FinalDisposeHandle = JobHandle.CombineDependencies(disposeHandle, m_StepHandles.FinalDisposeHandle);

            return m_StepHandles;
        }

        /// <summary>   Schedule solve and integrate jobs. </summary>
        ///
        /// <param name="input">            The input. </param>
        /// <param name="inputDeps">        The input deps. </param>
        /// <param name="multiThreaded">    (Optional) True if multi threaded. </param>
        ///
        /// <returns>   The SimulationJobHandles. </returns>
        public SimulationJobHandles ScheduleSolveAndIntegrateJobs(SimulationStepInput input, JobHandle inputDeps, bool multiThreaded = true)
        {
            SafetyChecks.CheckSimulationStageAndThrow(m_SimulationScheduleStage, SimulationScheduleStage.PostCreateJacobians);
            m_SimulationScheduleStage = SimulationScheduleStage.Idle;

            if (input.World.NumDynamicBodies == 0)
            {
                // No need to do anything, since nothing can move
                m_StepHandles = new SimulationJobHandles(inputDeps);
                return m_StepHandles;
            }

            // Solve all Jacobians

            // make sure we know the number of phased dispatch pairs so that we can efficiently schedule the solve jobs in Solver.ScheduleSolveJacobiansJobs() below
            if (multiThreaded)
            {
                StepContext.CreatePhasedDispatchPairsJobHandle.Complete();
            }

            var disposeHandle = m_StepHandles.FinalDisposeHandle;
            Solver.StabilizationData solverStabilizationData = new Solver.StabilizationData(input, SimulationContext);
            m_StepHandles = Solver.ScheduleSolveJacobiansJobs(ref input.World.DynamicsWorld, input.TimeStep, input.NumSolverIterations,
                ref StepContext.Jacobians, ref SimulationContext.CollisionEventDataStream, ref SimulationContext.TriggerEventDataStream,
                ref SimulationContext.ImpulseEventDataStream, ref StepContext.SolverSchedulerInfo, solverStabilizationData, inputDeps, multiThreaded);

            // Integrate motions
            m_StepHandles.FinalExecutionHandle = Integrator.ScheduleIntegrateJobs(ref input.World.DynamicsWorld, input.TimeStep, m_StepHandles.FinalExecutionHandle, multiThreaded);
            m_StepHandles.FinalDisposeHandle = JobHandle.CombineDependencies(disposeHandle, m_StepHandles.FinalDisposeHandle);

            // Synchronize the collision world
            if (input.SynchronizeCollisionWorld)
            {
                m_StepHandles.FinalExecutionHandle = input.World.CollisionWorld.ScheduleUpdateDynamicTree(ref input.World, input.TimeStep, input.Gravity, m_StepHandles.FinalExecutionHandle, multiThreaded);  // TODO: timeStep = 0?
            }

            // Different dispose logic for single threaded simulation compared to "standard" threading (multi threaded)
            if (!multiThreaded)
            {
                m_StepHandles.FinalDisposeHandle = StepContext.PhasedDispatchPairs.Dispose(m_StepHandles.FinalExecutionHandle);
                m_StepHandles.FinalDisposeHandle = StepContext.Contacts.Dispose(m_StepHandles.FinalDisposeHandle);
                m_StepHandles.FinalDisposeHandle = StepContext.Jacobians.Dispose(m_StepHandles.FinalDisposeHandle);
                m_StepHandles.FinalDisposeHandle = StepContext.SolverSchedulerInfo.ScheduleDisposeJob(m_StepHandles.FinalDisposeHandle);
            }

            return m_StepHandles;
        }

        /// <summary>   Steps the world immediately. </summary>
        ///
        /// <param name="input">    The input. </param>
        public void Step(SimulationStepInput input)
        {
            StepImmediate(input, ref SimulationContext);
        }

        /// <summary>
        /// Schedule all the jobs for the simulation step. Enqueued callbacks can choose to inject
        /// additional jobs at defined sync points. multiThreaded defines which simulation type will be
        /// called:
        ///     - true will result in default multithreaded simulation
        ///     - false will result in a very small number of jobs (1 per physics step phase) that are
        ///     scheduled sequentially
        /// Behavior doesn't change regardless of the multiThreaded argument provided.
        /// </summary>
        ///
        /// <param name="input">            The input. </param>
        /// <param name="inputDeps">        The input deps. </param>
        /// <param name="multiThreaded">    (Optional) True if multi threaded. </param>
        ///
        /// <returns>   The SimulationJobHandles. </returns>
        public unsafe SimulationJobHandles ScheduleStepJobs(SimulationStepInput input, JobHandle inputDeps, bool multiThreaded = true)
        {
            ScheduleBroadphaseJobs(input, inputDeps, multiThreaded);
            ScheduleNarrowphaseJobs(input, m_StepHandles.FinalExecutionHandle, multiThreaded);
            ScheduleCreateJacobiansJobs(input, m_StepHandles.FinalExecutionHandle, multiThreaded);
            ScheduleSolveAndIntegrateJobs(input, m_StepHandles.FinalExecutionHandle, multiThreaded);

            return m_StepHandles;
        }
    }
}
