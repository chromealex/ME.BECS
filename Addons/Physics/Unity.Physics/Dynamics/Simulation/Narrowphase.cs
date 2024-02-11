using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Physics
{
    /// <summary>   Processes body pairs and creates contacts from them. </summary>
    internal static class NarrowPhase
    {
        /// <summary>
        /// Iterates the provided dispatch pairs and creates contacts and based on them.
        /// </summary>
        ///
        /// <param name="world">            [in,out] The world. </param>
        /// <param name="dispatchPairs">    The dispatch pairs. </param>
        /// <param name="timeStep">         The time step. </param>
        /// <param name="contactsWriter">   [in,out] The contacts writer. </param>
        internal static void CreateContacts(ref PhysicsWorld world, NativeArray<DispatchPairSequencer.DispatchPair> dispatchPairs, float timeStep,
            ref NativeStream.Writer contactsWriter)
        {
            contactsWriter.BeginForEachIndex(0);

            ParallelCreateContactsJob.ExecuteImpl(ref world, timeStep, dispatchPairs, 0, dispatchPairs.Length, ref contactsWriter);

            contactsWriter.EndForEachIndex();
        }

        /// <summary>
        /// Schedules a set of jobs to iterate the provided dispatch pairs and create contacts based on
        /// them.
        /// </summary>
        ///
        /// <param name="world">                [in,out] The world. </param>
        /// <param name="timeStep">             The time step. </param>
        /// <param name="contacts">             [in,out] The contacts. </param>
        /// <param name="jacobians">            [in,out] The jacobians. </param>
        /// <param name="dispatchPairs">        [in,out] The dispatch pairs. </param>
        /// <param name="inputDeps">            The input deps. </param>
        /// <param name="solverSchedulerInfo"> [in,out] Information describing the solver scheduler. </param>
        /// <param name="multiThreaded">        (Optional) True if multi threaded. </param>
        ///
        /// <returns>   The SimulationJobHandles. </returns>
        internal static SimulationJobHandles ScheduleCreateContactsJobs(ref PhysicsWorld world, float timeStep,
            ref NativeStream contacts, ref NativeStream jacobians, ref NativeList<DispatchPairSequencer.DispatchPair> dispatchPairs,
            JobHandle inputDeps, ref DispatchPairSequencer.SolverSchedulerInfo solverSchedulerInfo, bool multiThreaded = true)
        {
            SimulationJobHandles returnHandles = default;

            if (!multiThreaded)
            {
                contacts = new NativeStream(1, Allocator.TempJob);
                jacobians = new NativeStream(1, Allocator.TempJob);
                returnHandles.FinalExecutionHandle = new CreateContactsJob
                {
                    World = world,
                    TimeStep = timeStep,
                    DispatchPairs = dispatchPairs.AsDeferredJobArray(),
                    ContactsWriter = contacts.AsWriter()
                }.Schedule(inputDeps);
            }
            else
            {
                var numWorkItems = solverSchedulerInfo.NumWorkItems;
                var contactsHandle = NativeStream.ScheduleConstruct(out contacts, numWorkItems, inputDeps, Allocator.TempJob);
                var jacobiansHandle = NativeStream.ScheduleConstruct(out jacobians, numWorkItems, inputDeps, Allocator.TempJob);

                var processHandle = new ParallelCreateContactsJob
                {
                    World = world,
                    TimeStep = timeStep,
                    DispatchPairs = dispatchPairs.AsDeferredJobArray(),
                    SolverSchedulerInfo = solverSchedulerInfo,
                    ContactsWriter = contacts.AsWriter()
                }.ScheduleUnsafeIndex0(numWorkItems, 1, JobHandle.CombineDependencies(contactsHandle, jacobiansHandle));
                returnHandles.FinalExecutionHandle = processHandle;
            }

            return returnHandles;
        }

        [BurstCompile]
        [NoAlias]
        struct ParallelCreateContactsJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public PhysicsWorld World;
            [ReadOnly] public float TimeStep;
            [ReadOnly] public NativeArray<DispatchPairSequencer.DispatchPair> DispatchPairs;
            [NoAlias] public NativeStream.Writer ContactsWriter;
            [NoAlias, ReadOnly] public DispatchPairSequencer.SolverSchedulerInfo SolverSchedulerInfo;

            public unsafe void Execute(int workItemIndex)
            {
                int dispatchPairReadOffset = SolverSchedulerInfo.GetWorkItemReadOffset(workItemIndex, out int numPairsToRead);

                ContactsWriter.BeginForEachIndex(workItemIndex);

                ExecuteImpl(ref World, TimeStep, DispatchPairs, dispatchPairReadOffset, numPairsToRead, ref ContactsWriter);

                ContactsWriter.EndForEachIndex();
            }

            internal static unsafe void ExecuteImpl(ref PhysicsWorld world, float timeStep,
                NativeArray<DispatchPairSequencer.DispatchPair> dispatchPairs,
                int dispatchPairReadOffset, int numPairsToRead, ref NativeStream.Writer contactWriter)
            {
                for (int i = 0; i < numPairsToRead; i++)
                {
                    DispatchPairSequencer.DispatchPair dispatchPair = dispatchPairs[dispatchPairReadOffset + i];

                    // Invalid pairs can exist by being disabled by users
                    if (dispatchPair.IsValid)
                    {
                        if (dispatchPair.IsContact)
                        {
                            // Create contact manifolds for this pair of bodies
                            var pair = new BodyIndexPair
                            {
                                BodyIndexA = dispatchPair.BodyIndexA,
                                BodyIndexB = dispatchPair.BodyIndexB
                            };

                            RigidBody rigidBodyA = world.Bodies[pair.BodyIndexA];
                            RigidBody rigidBodyB = world.Bodies[pair.BodyIndexB];

                            MotionVelocity motionVelocityA = pair.BodyIndexA < world.MotionVelocities.Length ?
                                world.MotionVelocities[pair.BodyIndexA] : MotionVelocity.Zero;
                            MotionVelocity motionVelocityB = pair.BodyIndexB < world.MotionVelocities.Length ?
                                world.MotionVelocities[pair.BodyIndexB] : MotionVelocity.Zero;

                            ManifoldQueries.BodyBody(rigidBodyA, rigidBodyB, motionVelocityA, motionVelocityB,
                                world.CollisionWorld.CollisionTolerance, timeStep, pair, ref contactWriter);
                        }
                    }
                }
            }
        }

        [BurstCompile]
        [NoAlias]
        struct CreateContactsJob : IJob
        {
            [NoAlias, ReadOnly] public PhysicsWorld World;
            [ReadOnly] public float TimeStep;
            [ReadOnly] public NativeArray<DispatchPairSequencer.DispatchPair> DispatchPairs;
            [NoAlias] public NativeStream.Writer ContactsWriter;

            public void Execute()
            {
                CreateContacts(ref World, DispatchPairs, TimeStep, ref ContactsWriter);
            }
        }
    }
}
