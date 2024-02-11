using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Physics.Aspects;

namespace Unity.Physics
{
    /// <summary>
    /// A collection of rigid bodies wrapped by a bounding volume hierarchy. This allows to do
    /// collision queries such as raycasting, overlap testing, etc.
    /// </summary>
    [NoAlias]
    public struct CollisionWorld : ICollidable, IAspectQueryable, IDisposable
    {
        [NoAlias] private NativeArray<RigidBody> m_Bodies;    // storage for all the rigid bodies
        [NoAlias] internal Broadphase Broadphase;             // bounding volume hierarchies around subsets of the rigid bodies
        [NoAlias] public NativeParallelHashMap<Entity, int> EntityBodyIndexMap;

        /// <summary>   Gets the number of bodies. </summary>
        ///
        /// <value> The total number of bodies. </value>
        public int NumBodies => Broadphase.NumStaticBodies + Broadphase.NumDynamicBodies;

        /// <summary>   Gets the number of static bodies. </summary>
        ///
        /// <value> The total number of static bodies. </value>
        public int NumStaticBodies => Broadphase.NumStaticBodies;

        /// <summary>   Gets the number of dynamic bodies. </summary>
        ///
        /// <value> The total number of dynamic bodies. </value>
        public int NumDynamicBodies => Broadphase.NumDynamicBodies;

        /// <summary>   Gets the bodies. </summary>
        ///
        /// <value> The bodies. </value>
        public NativeArray<RigidBody> Bodies => m_Bodies.GetSubArray(0, NumBodies);

        /// <summary>   Gets the static bodies. </summary>
        ///
        /// <value> The static bodies. </value>
        public NativeArray<RigidBody> StaticBodies => m_Bodies.GetSubArray(NumDynamicBodies, NumStaticBodies);

        /// <summary>   Gets the dynamic bodies. </summary>
        ///
        /// <value> The dynamic bodies. </value>
        public NativeArray<RigidBody> DynamicBodies => m_Bodies.GetSubArray(0, NumDynamicBodies);

        /// <summary>
        /// Contacts are always created between rigid bodies if they are closer than this distance
        /// threshold.
        /// </summary>
        ///
        /// <value> The collision tolerance. </value>
        public float CollisionTolerance => 0.1f; // todo - make this configurable?

        /// <summary>
        /// Construct a collision world with the given number of uninitialized rigid bodies.
        /// </summary>
        ///
        /// <param name="numStaticBodies">  Number of static bodies. </param>
        /// <param name="numDynamicBodies"> Number of dynamic bodies. </param>
        public CollisionWorld(int numStaticBodies, int numDynamicBodies)
        {
            m_Bodies = new NativeArray<RigidBody>(numStaticBodies + numDynamicBodies, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Broadphase = new Broadphase(numStaticBodies, numDynamicBodies);
            EntityBodyIndexMap = new NativeParallelHashMap<Entity, int>(m_Bodies.Length, Allocator.Persistent);
        }

        /// <summary>   Resets this object. </summary>
        ///
        /// <param name="numStaticBodies">  Number of static bodies. </param>
        /// <param name="numDynamicBodies"> Number of dynamic bodies. </param>
        public void Reset(int numStaticBodies, int numDynamicBodies)
        {
            SetCapacity(numStaticBodies + numDynamicBodies);
            Broadphase.Reset(numStaticBodies, numDynamicBodies);
            EntityBodyIndexMap.Clear();
        }

        /// <summary>   Sets the capacity. </summary>
        ///
        /// <param name="numBodies">    Number of bodies. </param>
        private void SetCapacity(int numBodies)
        {
            // Increase body storage if necessary
            if (m_Bodies.Length < numBodies)
            {
                m_Bodies.Dispose();
                m_Bodies = new NativeArray<RigidBody>(numBodies, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                EntityBodyIndexMap.Capacity = m_Bodies.Length;
            }
        }

        /// <summary>   Free internal memory. </summary>
        public void Dispose()
        {
            if (m_Bodies.IsCreated)
            {
                m_Bodies.Dispose();
            }
            Broadphase.Dispose();
            if (EntityBodyIndexMap.IsCreated)
            {
                EntityBodyIndexMap.Dispose();
            }
        }

        /// <summary>   Clone the world. Bodies and Broadphase are deep copied. Colliders are shallow copied. </summary>
        ///
        /// <returns>   A copy of this object. </returns>
        public CollisionWorld Clone()
        {
            var clone = new CollisionWorld
            {
                m_Bodies = new NativeArray<RigidBody>(m_Bodies, Allocator.Persistent),
                Broadphase = (Broadphase)Broadphase.Clone(),
                EntityBodyIndexMap = new NativeParallelHashMap<Entity, int>(m_Bodies.Length, Allocator.Persistent),
            };
            clone.UpdateBodyIndexMap();
            return clone;
        }

        /// <summary>   Updates the body index map. </summary>
        public void UpdateBodyIndexMap()
        {
            EntityBodyIndexMap.Clear();
            for (int i = 0; i < m_Bodies.Length; i++)
            {
                EntityBodyIndexMap[m_Bodies[i].Entity] = i;
            }
        }

        /// <summary>   Gets the zero-based index of the rigid body. </summary>
        ///
        /// <param name="entity">   The entity. </param>
        ///
        /// <returns>   The rigid body index. </returns>
        public int GetRigidBodyIndex(Entity entity)
        {
            return EntityBodyIndexMap.TryGetValue(entity, out var index) ? index : -1;
        }

        /// <summary>   Build the broadphase based on the given world. </summary>
        ///
        /// <param name="world">            [in,out] The world. </param>
        /// <param name="timeStep">         The time step. </param>
        /// <param name="gravity">          The gravity. </param>
        /// <param name="buildStaticTree">  (Optional) True to build static tree. </param>
        public void BuildBroadphase(ref PhysicsWorld world, float timeStep, float3 gravity, bool buildStaticTree = true)
        {
            Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                world.CollisionWorld.CollisionTolerance, timeStep, gravity, buildStaticTree);
        }

        /// <summary>   Schedule a set of jobs to build the broadphase based on the given world. </summary>
        ///
        /// <param name="world">            [in,out] The world. </param>
        /// <param name="timeStep">         The time step. </param>
        /// <param name="gravity">          The gravity. </param>
        /// <param name="buildStaticTree">  The build static tree. </param>
        /// <param name="inputDeps">        The input deps. </param>
        /// <param name="multiThreaded">    (Optional) True if multi threaded. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        public JobHandle ScheduleBuildBroadphaseJobs(ref PhysicsWorld world, float timeStep, float3 gravity, NativeReference<int>.ReadOnly buildStaticTree, JobHandle inputDeps, bool multiThreaded = true)
        {
            return Broadphase.ScheduleBuildJobs(ref world, timeStep, gravity, buildStaticTree, inputDeps, multiThreaded);
        }

        /// <summary>
        /// Write all overlapping body pairs to the given streams, where at least one of the bodies is
        /// dynamic. The results are unsorted.
        /// </summary>
        ///
        /// <param name="dynamicVsDynamicPairsWriter"> [in,out] The dynamic vs dynamic pairs writer. </param>
        /// <param name="staticVsDynamicPairsWriter">  [in,out] The static vs dynamic pairs writer. </param>
        public void FindOverlaps(ref NativeStream.Writer dynamicVsDynamicPairsWriter, ref NativeStream.Writer staticVsDynamicPairsWriter)
        {
            Broadphase.FindOverlaps(ref dynamicVsDynamicPairsWriter, ref staticVsDynamicPairsWriter);
        }

        /// <summary>
        /// Schedule a set of jobs which will write all overlapping body pairs to the given steam, where
        /// at least one of the bodies is dynamic. The results are unsorted.
        /// </summary>
        ///
        /// <param name="dynamicVsDynamicPairsStream">  [out] The dynamic vs dynamic pairs stream. </param>
        /// <param name="staticVsDynamicPairsStream">   [out] The static vs dynamic pairs stream. </param>
        /// <param name="inputDeps">                    The input deps. </param>
        /// <param name="multiThreaded">                (Optional) True if multi threaded. </param>
        ///
        /// <returns>   The SimulationJobHandles. </returns>
        public SimulationJobHandles ScheduleFindOverlapsJobs(out NativeStream dynamicVsDynamicPairsStream, out NativeStream staticVsDynamicPairsStream,
            JobHandle inputDeps, bool multiThreaded = true)
        {
            return Broadphase.ScheduleFindOverlapsJobs(out dynamicVsDynamicPairsStream, out staticVsDynamicPairsStream, inputDeps, multiThreaded);
        }

        /// <summary>   Synchronize the collision world with the dynamics world. </summary>
        ///
        /// <param name="world">    [in,out] The world. </param>
        /// <param name="timeStep"> The time step. </param>
        /// <param name="gravity">  The gravity. </param>
        public void UpdateDynamicTree(ref PhysicsWorld world, float timeStep, float3 gravity)
        {
            // Synchronize transforms
            for (int i = 0; i < world.DynamicsWorld.NumMotions; i++)
            {
                UpdateRigidBodyTransformsJob.ExecuteImpl(i, world.MotionDatas, m_Bodies);
            }

            // Update broadphase
            float aabbMargin = world.CollisionWorld.CollisionTolerance * 0.5f;
            Broadphase.BuildDynamicTree(world.DynamicBodies, world.MotionVelocities, gravity, timeStep, aabbMargin);
        }

        /// <summary>
        /// Schedule a set of jobs to synchronize the collision world with the dynamics world.
        /// </summary>
        ///
        /// <param name="world">            [in,out] The world. </param>
        /// <param name="timeStep">         The time step. </param>
        /// <param name="gravity">          The gravity. </param>
        /// <param name="inputDeps">        The input deps. </param>
        /// <param name="multiThreaded">    (Optional) True if multi threaded. </param>
        ///
        /// <returns>   A JobHandle. </returns>
        public JobHandle ScheduleUpdateDynamicTree(ref PhysicsWorld world, float timeStep, float3 gravity, JobHandle inputDeps, bool multiThreaded = true)
        {
            if (!multiThreaded)
            {
                return new UpdateDynamicLayerJob
                {
                    World = world,
                    TimeStep = timeStep,
                    Gravity = gravity
                }.Schedule(inputDeps);
            }
            else
            {
                // Synchronize transforms
                JobHandle handle = new UpdateRigidBodyTransformsJob
                {
                    MotionDatas = world.MotionDatas,
                    RigidBodies = m_Bodies
                }.Schedule(world.MotionDatas.Length, 32, inputDeps);

                // Update broadphase
                // Thread count is +1 for main thread
                return Broadphase.ScheduleDynamicTreeBuildJobs(ref world, timeStep, gravity, JobsUtility.JobWorkerCount + 1, handle);
            }
        }

        #region Jobs

        [BurstCompile]
        private struct UpdateRigidBodyTransformsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<MotionData> MotionDatas;
            public NativeArray<RigidBody> RigidBodies;

            public void Execute(int i)
            {
                ExecuteImpl(i, MotionDatas, RigidBodies);
            }

            internal static void ExecuteImpl(int i, NativeArray<MotionData> motionDatas, NativeArray<RigidBody> rigidBodies)
            {
                RigidBody rb = rigidBodies[i];
                rb.WorldFromBody = math.mul(motionDatas[i].WorldFromMotion, math.inverse(motionDatas[i].BodyFromMotion));
                rigidBodies[i] = rb;
            }
        }

        [BurstCompile]
        private struct UpdateDynamicLayerJob : IJob
        {
            public PhysicsWorld World;
            public float TimeStep;
            public float3 Gravity;

            public void Execute()
            {
                World.CollisionWorld.UpdateDynamicTree(ref World, TimeStep, Gravity);
            }
        }

        #endregion

        #region ICollidable implementation

        /// <summary>   Calculates the aabb. </summary>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb()
        {
            return Broadphase.Domain;
        }

        /// <summary>   Cast ray. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input) => QueryWrappers.RayCast(in this, input);

        /// <summary>   Cast ray. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input, out RaycastHit closestHit) => QueryWrappers.RayCast(in this, input, out closestHit);

        /// <summary>   Cast ray. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input, ref NativeList<RaycastHit> allHits) => QueryWrappers.RayCast(in this, input, ref allHits);

        /// <summary>   Cast ray. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay<T>(RaycastInput input, ref T collector) where T : struct, ICollector<RaycastHit>
        {
            input.QueryContext.InitScale();
            return Broadphase.CastRay(input, m_Bodies, ref collector);
        }

        /// <summary>   Cast collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input) => QueryWrappers.ColliderCast(in this, input);

        /// <summary>   Cast collider. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input, out ColliderCastHit closestHit) => QueryWrappers.ColliderCast(in this, input, out closestHit);

        /// <summary>   Cast collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input, ref NativeList<ColliderCastHit> allHits) => QueryWrappers.ColliderCast(in this, input, ref allHits);

        /// <summary>   Cast collider. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider<T>(ColliderCastInput input, ref T collector) where T : struct, ICollector<ColliderCastHit>
        {
            input.InitScale();
            return Broadphase.CastCollider(input, m_Bodies, ref collector);
        }

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input) => QueryWrappers.CalculateDistance(in this, input);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input, out DistanceHit closestHit) => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input, ref NativeList<DistanceHit> allHits) => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance<T>(PointDistanceInput input, ref T collector) where T : struct, ICollector<DistanceHit>
        {
            input.QueryContext.InitScale();
            return Broadphase.CalculateDistance(input, m_Bodies, ref collector);
        }

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input) => QueryWrappers.CalculateDistance(in this, input);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input, out DistanceHit closestHit) => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input, ref NativeList<DistanceHit> allHits) => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance<T>(ColliderDistanceInput input, ref T collector) where T : struct, ICollector<DistanceHit>
        {
            input.InitScale();
            return Broadphase.CalculateDistance(input, m_Bodies, ref collector);
        }

        #region Aspect query impl

        /// <summary>   Cast a collider aspect against this <see cref="CollisionWorld"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CastCollider(in this, colliderAspect, direction, maxDistance, queryInteraction);

        /// <summary>   Cast a collider aspect against this <see cref="CollisionWorld"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="closestHit">       [out] The closest hit. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, out ColliderCastHit closestHit, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CastCollider(in this, colliderAspect, direction, maxDistance, out closestHit, queryInteraction);

        /// <summary>   Cast a collider aspect against this <see cref="CollisionWorld"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="allHits">          [in,out] all hits. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> allHits, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CastCollider(in this, colliderAspect, direction, maxDistance, ref allHits, queryInteraction);

        /// <summary>   Cast a collider aspect against this <see cref="CollisionWorld"/>. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider<T>(in ColliderAspect colliderAspect, float3 direction, float maxDistance, ref T collector, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>
        {
            QueryInteractionCollector<ColliderCastHit, T> interactionCollector = new QueryInteractionCollector<ColliderCastHit, T>(ref collector, queryInteraction == QueryInteraction.Default, colliderAspect.Entity);

            ColliderCastInput input = new ColliderCastInput(colliderAspect.m_Collider.ValueRO.Value, colliderAspect.Position,
                colliderAspect.Position + direction * maxDistance, colliderAspect.Rotation, colliderAspect.Scale);
            return CastCollider(input, ref interactionCollector);
        }

        /// <summary>   Calculates the distance from the collider aspect. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(in ColliderAspect colliderAspect, float maxDistance, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CalculateDistance(in this, colliderAspect, maxDistance, queryInteraction);

        /// <summary>   Calculates the distance from the collider aspect. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="closestHit">       [out] The closest hit. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(in ColliderAspect colliderAspect, float maxDistance, out DistanceHit closestHit, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CalculateDistance(in this, colliderAspect, maxDistance, out closestHit, queryInteraction);

        /// <summary>   Calculates the distance from the collider aspect. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="allHits">          [in,out] all hits. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(in ColliderAspect colliderAspect, float maxDistance, ref NativeList<DistanceHit> allHits, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CalculateDistance(in this, colliderAspect, maxDistance, ref allHits, queryInteraction);

        /// <summary>   Calculates the distance from the collider aspect. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance<T>(in ColliderAspect colliderAspect, float maxDistance, ref T collector, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>
        {
            QueryInteractionCollector<DistanceHit, T> interactionCollector = new QueryInteractionCollector<DistanceHit, T>(ref collector, queryInteraction == QueryInteraction.IgnoreTriggers, colliderAspect.Entity);

            ColliderDistanceInput input = new ColliderDistanceInput(colliderAspect.m_Collider.ValueRO.Value, maxDistance,
                new RigidTransform(colliderAspect.Rotation, colliderAspect.Position), colliderAspect.Scale);
            return CalculateDistance(input, ref interactionCollector);
        }

        #endregion

        #region GO API Queries

        /// <summary>   Interfaces that represent queries that exist in the GameObjects world. </summary>
        ///
        /// <param name="position">         The position. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CheckSphere(float3 position, float radius, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CheckSphere(in this, position, radius, filter, queryInteraction);

        /// <summary>   Overlap sphere. </summary>
        ///
        /// <param name="position">         The position. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapSphere(float3 position, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.OverlapSphere(in this, position, radius, ref outHits, filter, queryInteraction);

        /// <summary>   Overlap sphere custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="position">         The position. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapSphereCustom<T>(float3 position, float radius, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>
            => QueryWrappers.OverlapSphereCustom(in this, position, radius, ref collector, filter, queryInteraction);

        /// <summary>   Check capsule. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CheckCapsule(float3 point1, float3 point2, float radius, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CheckCapsule(in this, point1, point2, radius, filter, queryInteraction);

        /// <summary>   Overlap capsule. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapCapsule(float3 point1, float3 point2, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.OverlapCapsule(in this, point1, point2, radius, ref outHits, filter, queryInteraction);

        /// <summary>   Overlap capsule custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapCapsuleCustom<T>(float3 point1, float3 point2, float radius, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>
            => QueryWrappers.OverlapCapsuleCustom(in this, point1, point2, radius, ref collector, filter, queryInteraction);

        /// <summary>   Check box. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CheckBox(float3 center, quaternion orientation, float3 halfExtents, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CheckBox(in this, center, orientation, halfExtents, filter, queryInteraction);

        /// <summary>   Overlap box. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapBox(float3 center, quaternion orientation, float3 halfExtents, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.OverlapBox(in this, center, orientation, halfExtents, ref outHits, filter, queryInteraction);

        /// <summary>   Overlap box custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapBoxCustom<T>(float3 center, quaternion orientation, float3 halfExtents, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>
            => QueryWrappers.OverlapBoxCustom(in this, center, orientation, halfExtents, ref collector, filter, queryInteraction);

        /// <summary>   Sphere cast. </summary>
        ///
        /// <param name="origin">           The origin. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.SphereCast(in this, origin, radius, direction, maxDistance, filter, queryInteraction);

        /// <summary>   Sphere cast. </summary>
        ///
        /// <param name="origin">           The origin. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="hitInfo">          [out] Information describing the hit. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.SphereCast(in this, origin, radius, direction, maxDistance, out hitInfo, filter, queryInteraction);

        /// <summary>   Sphere cast all. </summary>
        ///
        /// <param name="origin">           The origin. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool SphereCastAll(float3 origin, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.SphereCastAll(in this, origin, radius, direction, maxDistance, ref outHits, filter, queryInteraction);

        /// <summary>   Sphere cast custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="origin">           The origin. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool SphereCastCustom<T>(float3 origin, float radius, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>
            => QueryWrappers.SphereCastCustom(in this, origin, radius, direction, maxDistance, ref collector, filter, queryInteraction);

        /// <summary>   Box cast. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.BoxCast(in this, center, orientation, halfExtents, direction, maxDistance, filter, queryInteraction);

        /// <summary>   Box cast. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="hitInfo">          [out] Information describing the hit. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.BoxCast(in this, center, orientation, halfExtents, direction, maxDistance, out hitInfo, filter, queryInteraction);

        /// <summary>   Box cast all. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool BoxCastAll(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.BoxCastAll(in this, center, orientation, halfExtents, direction, maxDistance, ref outHits, filter, queryInteraction);

        /// <summary>   Box cast custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool BoxCastCustom<T>(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>
            => QueryWrappers.BoxCastCustom(in this, center, orientation, halfExtents, direction, maxDistance, ref collector, filter, queryInteraction);

        /// <summary>   Capsule cast. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CapsuleCast(in this, point1, point2, radius, direction, maxDistance, filter, queryInteraction);

        /// <summary>   Capsule cast. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="hitInfo">          [out] Information describing the hit. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CapsuleCast(in this, point1, point2, radius, direction, maxDistance, out hitInfo, filter, queryInteraction);

        /// <summary>   Capsule cast all. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CapsuleCastAll(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CapsuleCastAll(in this, point1, point2, radius, direction, maxDistance, ref outHits, filter, queryInteraction);

        /// <summary>   Capsule cast custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CapsuleCastCustom<T>(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>
            => QueryWrappers.CapsuleCastCustom(in this, point1, point2, radius, direction, maxDistance, ref collector, filter, queryInteraction);

        #endregion

        #endregion

        /// <summary>
        /// Test input against the broadphase tree, filling allHits with the body indices of every
        /// overlap. Returns true if there was at least overlap.
        /// </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapAabb(OverlapAabbInput input, ref NativeList<int> allHits)
        {
            int hitsBefore = allHits.Length;
            Broadphase.OverlapAabb(input, m_Bodies, ref allHits);
            return allHits.Length > hitsBefore;
        }
    }
}
