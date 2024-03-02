using ME.BECS;
using ME.BECS.Addons.Physics.Runtime.Extensions;
using ME.BECS.Jobs;
using ME.BECS.Physics.Components;
using ME.BECS.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using PhysicsJoint = ME.BECS.Physics.Components.PhysicsJoint;
using PhysicsConstrainedBodyPair = ME.BECS.Physics.Components.PhysicsConstrainedBodyPair;

namespace ME.BECS.Physics {

    [BurstCompile]
    public unsafe struct BuildPhysicsWorldSystem : IAwake, IUpdate, IDestroy {

        public struct PhysicsWorldState : IComponent {

            public int numOfStaticBodies;

        }

        public float3 gravity;

        private PhysicsWorld physicsWorld;
        private Simulation simulation;

        private Query staticBodiesQuery;
        private Query dynamicBodiesQuery;
        private Query jointsQuery;

        private Ent physicsWorldStateEnt;
        
        [BurstCompile]
        private struct CheckStaticBodyChangesJob : IJobParallelForCommandBuffer {

            public ulong currentTick;
            public ulong prevTick;
            [Unity.Collections.NativeDisableParallelForRestrictionAttribute]
            public Unity.Collections.NativeReference<int> result;

            public void Execute(in CommandBufferJobParallel commandBuffer) {

                var ent = commandBuffer.ent;
                var entChanged = ent.Version == this.currentTick || ent.Version == this.prevTick;
                if (entChanged == true) {
                    // Note that multiple worker threads may be running at the same time.
                    // They either write 1 to Result[0] or not write at all.  In case multiple
                    // threads are writing 1 to this variable, in C#, reads or writes of int
                    // data type are atomic, which guarantees that Result[0] is 1.
                    this.result.Value = 1;
                }

            }

        }

        [BurstCompile]
        public struct FillBodiesJob : IJobParallelForCommandBuffer {

            [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute]
            public NativeArray<RigidBody> bodies;
            [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute]
            [NativeDisableParallelForRestriction]
            public NativeParallelHashMap<Entity, int> entityBodyIndexMap;
            public int initialIndex;

            public void Execute(in CommandBufferJobParallel commandBuffer) {

                var index = (int) commandBuffer.index;
                var entity = commandBuffer.ent;

                var physicsEntity = entity.ToPhysicsEntity();

                var ptrCollider = entity.Read<PhysicsColliderBecs>().Value;
                var blobCollider = BlobAssetReference<Collider>.Create(ptrCollider.GetUnsafePtr(), ptrCollider.As().MemorySize);

                this.bodies[index] = new RigidBody() {
                    Entity = physicsEntity,
                    Scale = math.csum(entity.Read<LocalScaleComponent>().value) / 3f,
                    Collider = blobCollider,
                    WorldFromBody = new RigidTransform() {
                        pos = entity.Read<LocalPositionComponent>().value,
                        rot = entity.Read<LocalRotationComponent>().value,
                    },
                    CustomTags = entity.Read<PhysicsCustomTagsBecs>().Value,
                };

                this.entityBodyIndexMap.TryAdd(physicsEntity, index + this.initialIndex);

            }

        }

        [BurstCompile]
        public struct FillMotionsJob : IJobParallelForCommandBuffer {
            
            public NativeArray<MotionData> motionDatas;
            public NativeArray<MotionVelocity> motionVelocities;
            public PhysicsMassBecs defaultPhysicsMass;

            public void Execute(in CommandBufferJobParallel commandBuffer) {

                var ent = commandBuffer.ent;
                var index = (int) commandBuffer.index;

                var pos = ent.Read<LocalPositionComponent>();
                var rot = ent.Read<LocalRotationComponent>();

                var velocity = ent.Read<PhysicsVelocityBecs>();
                var mass = ent.Read<PhysicsMassBecs>();
                var gravityFactor = ent.Read<PhysicsGravityFactorBecs>();
                var damping = ent.Read<PhysicsDampingBecs>();
                var massOverride = ent.Read<PhysicsMassOverrideBecs>();
                var hasMass = ent.Has<PhysicsMassBecs>();
                var hasGravityFactor = ent.Has<PhysicsGravityFactorBecs>();
                var hasDamping = ent.Has<PhysicsDampingBecs>();
                var hasMassOverride = ent.Has<PhysicsMassOverrideBecs>();
                    
                // Note: if a dynamic body infinite mass then assume no gravity should be applied
                float defaultGravityFactor = hasMass ? 1 : 0;
                    
                var isKinematic = !hasMass || hasMassOverride && massOverride.IsKinematic != 0;
                this.motionVelocities[index] = new MotionVelocity {
                    LinearVelocity = velocity.Linear,
                    AngularVelocity = velocity.Angular,
                    InverseInertia = isKinematic ? this.defaultPhysicsMass.InverseInertia : mass.InverseInertia,
                    InverseMass = isKinematic ? this.defaultPhysicsMass.InverseMass : mass.InverseMass,
                    AngularExpansionFactor = hasMass ? mass.AngularExpansionFactor : this.defaultPhysicsMass.AngularExpansionFactor,
                    GravityFactor = isKinematic ? 0 : hasGravityFactor ? gravityFactor.Value : defaultGravityFactor,
                };
                    
                // Note: these defaults assume a dynamic body with infinite mass, hence no damping
                var defaultPhysicsDamping = new PhysicsDampingBecs {
                    Linear = 0,
                    Angular = 0,
                };
                
                // Create motion datas
                PhysicsMassBecs massMotion = hasMass ? mass : this.defaultPhysicsMass;
                PhysicsDampingBecs dampingMotion = hasDamping ? damping : defaultPhysicsDamping;
                
                var a = math.mul(rot.value, massMotion.InertiaOrientation);
                var b = math.rotate(rot.value, massMotion.CenterOfMass) + pos.value;
                this.motionDatas[index] = new MotionData {
                    WorldFromMotion = new RigidTransform(a, b),
                    BodyFromMotion = new RigidTransform(massMotion.InertiaOrientation, massMotion.CenterOfMass),
                    LinearDamping = dampingMotion.Linear,
                    AngularDamping = dampingMotion.Angular,
                };
                
            }

        }

        [BurstCompile]
        public struct FillJointsJob : IJobParallelForCommandBuffer {

            public NativeArray<Joint> joints;
            
            [ReadOnly]
            public NativeParallelHashMap<Entity, int> entityBodyIndexMap;
            [NativeDisableParallelForRestriction]
            public NativeParallelHashMap<Entity, int> entityJointIndexMap;
            
            public int defaultStaticBodyIndex;
            
            public void Execute(in CommandBufferJobParallel commandBuffer) {

                var index = (int) commandBuffer.index;
                var entity = commandBuffer.ent;
                var physicsConstraintBodyPair = entity.Read<PhysicsConstrainedBodyPair>();
                var joint = entity.Read<PhysicsJoint>();

                var entityA = physicsConstraintBodyPair.EntityA;
                var entityB = physicsConstraintBodyPair.EntityB;
                
                var pair = new BodyIndexPair {
                    BodyIndexA = entityA == Ent.Null ? this.defaultStaticBodyIndex : -1,
                    BodyIndexB = entityB == Ent.Null ? this.defaultStaticBodyIndex : -1,
                };

                var physicsEntity = entity.ToPhysicsEntity();

                // Find the body indices
                pair.BodyIndexA = this.entityBodyIndexMap.TryGetValue(physicsEntity, out var idxA) ? idxA : -1;
                pair.BodyIndexB = this.entityBodyIndexMap.TryGetValue(physicsEntity, out var idxB) ? idxB : -1;

                this.joints[index] = new Joint() {
                    Entity = physicsEntity,
                    BodyPair = pair,
                    Constraints = joint.m_Constraints,
                    Version = joint.Version,
                    EnableCollision = (byte) physicsConstraintBodyPair.EnableCollision,
                    AFromJoint = joint.BodyAFromJoint.AsMTransform(),
                    BFromJoint = joint.BodyBFromJoint.AsMTransform(),
                };
                
                this.entityJointIndexMap.TryAdd(physicsEntity, index);

            }

        }

        [BurstCompile]
        public struct ApplyPhysicsResults : IJobParallelForCommandBuffer {

            public NativeArray<RigidBody> dynamicBodies;
            public NativeArray<MotionVelocity> motionVelocities;

            public void Execute(in CommandBufferJobParallel commandBuffer) {

                var entity = commandBuffer.ent;
                var index = (int) commandBuffer.index;

                var positionConstraint = entity.Read<PhysicsConstraintPositionBecs>();
                
                // Apply velocities
                ref var vel = ref entity.Get<PhysicsVelocityBecs>();
                {
                    var physVelAngular = this.motionVelocities[index].AngularVelocity;
                    
                    // even if rotation is locked, object still gets angular velocity. So stop this velocity expansion
                    if (entity.Has<PhysicsMassBecs>() == true) {
                        
                        var mass = entity.Read<PhysicsMassBecs>();
                        var lockX = mass.InverseInertia.x == 0;
                        var lockY = mass.InverseInertia.y == 0;
                        var lockZ = mass.InverseInertia.z == 0;
                        
                        vel.Angular = new float3(
                            lockX ? 0 : physVelAngular.x,
                            lockY ? 0 : physVelAngular.y,
                            lockZ ? 0 : physVelAngular.z);
                        
                    }
                    else {
                        vel.Angular = physVelAngular;
                    }

                    var physVelLinear = this.motionVelocities[index].LinearVelocity;
                    vel.Linear = new float3(
                        positionConstraint.freezeX ? 0 : physVelLinear.x,
                        positionConstraint.freezeY ? 0 : physVelLinear.y,
                        positionConstraint.freezeZ ? 0 : physVelLinear.z
                    );

                }
                
                // Apply positions & rotations
                var dataPhys = this.dynamicBodies[index].WorldFromBody;
                ref var posCur = ref entity.Get<LocalPositionComponent>();
                ref var rotCur = ref entity.Get<LocalRotationComponent>();

                posCur.value = new float3(
                    positionConstraint.freezeX ? posCur.value.x : dataPhys.pos.x,
                    positionConstraint.freezeY ? posCur.value.y : dataPhys.pos.y,
                    positionConstraint.freezeZ ? posCur.value.z : dataPhys.pos.z
                );
                rotCur.value = dataPhys.rot;
                
            }

        }

        [BurstCompile]
        public struct DisposeJob : IJobParallelFor {
            
            [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute]
            public NativeArray<RigidBody> bodies;

            public void Execute(int index) {

                var body = this.bodies[index];
                body.Collider.Dispose();

            }

        }
        
        public void OnAwake(ref SystemContext context) {

            this.physicsWorld = new PhysicsWorld(0, 0, 0);
            this.simulation = Simulation.Create();

            this.staticBodiesQuery = Query
                .With<LocalPositionComponent>(context)
                .With<LocalRotationComponent>()
                .With<LocalScaleComponent>()
                .With<PhysicsColliderBecs>()
                .With<IsPhysicsStaticEcs>()
                .Build();

            this.dynamicBodiesQuery = Query
                .With<LocalPositionComponent>(context)
                .With<LocalRotationComponent>()
                .With<LocalScaleComponent>()
                .With<PhysicsColliderBecs>()
                .With<PhysicsVelocityBecs>()
                .With<PhysicsMassBecs>()
                .Without<IsPhysicsStaticEcs>()
                .Build();

            this.jointsQuery = Query
                .With<PhysicsConstrainedBodyPair>(context)
                .With<PhysicsJoint>()
                .Build();

            this.gravity = new float3(0f, -9.8f, 0f);

            this.physicsWorldStateEnt = Ent.New(context);
            this.physicsWorldStateEnt.Set(new PhysicsWorldState() {
                numOfStaticBodies = 0,
            });

        }

        public void OnUpdate(ref SystemContext context) {

            // set physics world size
            var staticBodiesCount = (int) this.staticBodiesQuery.Count(context);
            var dynamicBodiesCount = (int) this.dynamicBodiesQuery.Count(context);
            var jointsCount = (int) this.jointsQuery.Count(context);

            // com.unity.collections can stuck on inner structures extending size
            // Resetting size to 1 can fix that
            this.physicsWorld.Reset(1, 0, 0);
            this.physicsWorld.Reset(staticBodiesCount, dynamicBodiesCount, jointsCount);

            if (this.physicsWorld.Bodies.Length == 0) {
                return;
            }

            var simulationParameters = new SimulationStepInput() {
                Gravity = this.gravity,
                NumSolverIterations = 4,
                SolverStabilizationHeuristicSettings = Solver.StabilizationHeuristicSettings.Default,
                SynchronizeCollisionWorld = true,
                TimeStep = context.deltaTime,
                World = this.physicsWorld,
            };
            
            ref var prevWorldState = ref this.physicsWorldStateEnt.Get<PhysicsWorldState>();
            var buildStaticTree = new Unity.Collections.NativeReference<int>(0, Unity.Collections.Allocator.TempJob);

            JobHandle outputDependency;
            using (var jobHandles = new NativeList<JobHandle>(4, Allocator.Temp)) {
                
                JobHandle checkStaticBodyHandle = new JobHandle();
                if (prevWorldState.numOfStaticBodies != staticBodiesCount) {
                    buildStaticTree.Value = 1;
                } else {
                    checkStaticBodyHandle = this.staticBodiesQuery.ScheduleParallelFor(new CheckStaticBodyChangesJob() {
                        currentTick = context.world.state->tick,
                        prevTick = context.world.state->tick - 1,
                        result = buildStaticTree,
                    }, 64, context);
                }
                jobHandles.Add(checkStaticBodyHandle);
                
                prevWorldState.numOfStaticBodies = staticBodiesCount;

                // Fill dynamic bodies here
                var dependsOnDynamic = this.dynamicBodiesQuery.ScheduleParallelFor(new FillBodiesJob() {
                    bodies = this.physicsWorld.DynamicBodies,
                    entityBodyIndexMap = this.physicsWorld.CollisionWorld.EntityBodyIndexMap,
                    initialIndex = 0,
                }, 64, context);
                jobHandles.Add(dependsOnDynamic);
            
                // Fill static bodies here
                var dependsOnStatic = this.staticBodiesQuery.ScheduleParallelFor(new FillBodiesJob() {
                    bodies = this.physicsWorld.StaticBodies,
                    entityBodyIndexMap = this.physicsWorld.CollisionWorld.EntityBodyIndexMap,
                    initialIndex = dynamicBodiesCount,
                }, 64, context);
                jobHandles.Add(dependsOnStatic);
            
                JobHandle dependsOnFillBodies = JobHandle.CombineDependencies(dependsOnDynamic, dependsOnStatic);

                // Fill joints here
                var dependsOnJoints =
                    this.jointsQuery.ScheduleParallelFor<FillJointsJob>(new FillJointsJob() {
                        joints = this.physicsWorld.Joints,
                        defaultStaticBodyIndex = this.physicsWorld.Bodies.Length - 1,
                        entityBodyIndexMap = this.physicsWorld.CollisionWorld.EntityBodyIndexMap,
                        entityJointIndexMap = this.physicsWorld.DynamicsWorld.EntityJointIndexMap,
                    }, 64, context, dependsOnFillBodies);
                jobHandles.Add(dependsOnJoints);
            
                // Fill velocities
                var dependsOnMotions =
                    this.dynamicBodiesQuery.ScheduleParallelFor(new FillMotionsJob() {
                        motionDatas = this.physicsWorld.MotionDatas,
                        motionVelocities = this.physicsWorld.MotionVelocities,
                        defaultPhysicsMass = PhysicsMassBecs.CreateDynamic(MassProperties.UnitSphere, 1f),
                    }, 64, context);
                jobHandles.Add(dependsOnMotions);

                outputDependency = JobHandle.CombineDependencies(jobHandles);

            }

            var dependsOnBuildBroadphase = this.physicsWorld.CollisionWorld.ScheduleBuildBroadphaseJobs(
                ref this.physicsWorld,
                simulationParameters.TimeStep,
                this.gravity,
                buildStaticTree.AsReadOnly(),
                outputDependency,
                true);
            
            buildStaticTree.Dispose(dependsOnBuildBroadphase);

            var jobs = this.simulation.ScheduleStepJobs(simulationParameters,
                dependsOnBuildBroadphase,
                true);

            // Apply movement to entities

            var dependsOn =
                this.dynamicBodiesQuery.ScheduleParallelFor<ApplyPhysicsResults>(new ApplyPhysicsResults() {
                        dynamicBodies = this.physicsWorld.DynamicBodies,
                        motionVelocities = this.physicsWorld.MotionVelocities,
                    }, 64,
                    context, jobs.FinalExecutionHandle);

            // Cleanup blobs
            var dependsOnDispose = new DisposeJob() {
                bodies = this.physicsWorld.Bodies,
            }.Schedule(this.physicsWorld.Bodies.Length, 32, dependsOn);
            
            context.SetDependency(dependsOnDispose);

        }

        public void OnDestroy(ref SystemContext context) {

            this.simulation.Dispose();
            this.physicsWorld.Dispose();
            // this.physicsWorldStateEnt.Destroy();

        }

    }

}
