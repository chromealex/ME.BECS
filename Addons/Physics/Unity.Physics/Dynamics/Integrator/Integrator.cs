using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   Integrates world's motions. </summary>
    public static class Integrator
    {
        /// <summary>   Integrate the world's motions forward by the given time step. </summary>
        ///
        /// <param name="motionDatas">      The motion datas. </param>
        /// <param name="motionVelocities"> The motion velocities. </param>
        /// <param name="timeStep">         The time step. </param>
        public static void Integrate(NativeArray<MotionData> motionDatas, NativeArray<MotionVelocity> motionVelocities, float timeStep)
        {
            for (int i = 0; i < motionDatas.Length; i++)
            {
                ParallelIntegrateMotionsJob.ExecuteImpl(i, motionDatas, motionVelocities, timeStep);
            }
        }

        /// <summary>   Integrate a single transform for the provided velocity and time. </summary>
        ///
        /// <param name="transform">        [in,out] The transform. </param>
        /// <param name="motionVelocity">   The motion velocity. </param>
        /// <param name="timeStep">         The time step. </param>
        public static void Integrate(ref RigidTransform transform, in MotionVelocity motionVelocity, in float timeStep)
        {
            // center of mass
            IntegratePosition(ref transform.pos, motionVelocity.LinearVelocity, timeStep);

            // orientation
            IntegrateOrientation(ref transform.rot, motionVelocity.AngularVelocity, timeStep);
        }

        // Schedule a job to integrate the world's motions forward by the given time step.
        internal static JobHandle ScheduleIntegrateJobs(ref DynamicsWorld world, float timeStep, JobHandle inputDeps, bool multiThreaded = true)
        {
            if (!multiThreaded)
            {
                var job = new IntegrateMotionsJob
                {
                    MotionDatas = world.MotionDatas,
                    MotionVelocities = world.MotionVelocities,
                    TimeStep = timeStep
                };
                return job.Schedule(inputDeps);
            }
            else
            {
                var job = new ParallelIntegrateMotionsJob
                {
                    MotionDatas = world.MotionDatas,
                    MotionVelocities = world.MotionVelocities,
                    TimeStep = timeStep
                };
                return job.Schedule(world.NumMotions, 64, inputDeps);
            }
        }

        [BurstCompile]
        private struct ParallelIntegrateMotionsJob : IJobParallelFor
        {
            public NativeArray<MotionData> MotionDatas;
            public NativeArray<MotionVelocity> MotionVelocities;
            public float TimeStep;

            public void Execute(int i)
            {
                ExecuteImpl(i, MotionDatas, MotionVelocities, TimeStep);
            }

            internal static void ExecuteImpl(int i, NativeArray<MotionData> motionDatas, NativeArray<MotionVelocity> motionVelocities, float timeStep)
            {
                MotionData motionData = motionDatas[i];
                MotionVelocity motionVelocity = motionVelocities[i];

                // Update motion space
                Integrate(ref motionData.WorldFromMotion, motionVelocity, timeStep);

                // Update velocities
                {
                    // damping
                    motionVelocity.LinearVelocity *= math.clamp(1.0f - motionData.LinearDamping * timeStep, 0.0f, 1.0f);
                    motionVelocity.AngularVelocity *= math.clamp(1.0f - motionData.AngularDamping * timeStep, 0.0f, 1.0f);
                }

                // Write back
                motionDatas[i] = motionData;
                motionVelocities[i] = motionVelocity;
            }
        }

        [BurstCompile]
        private struct IntegrateMotionsJob : IJob
        {
            public NativeArray<MotionData> MotionDatas;
            public NativeArray<MotionVelocity> MotionVelocities;
            public float TimeStep;

            public void Execute()
            {
                Integrate(MotionDatas, MotionVelocities, TimeStep);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void IntegratePosition(ref float3 position, float3 linearVelocity, float timestep)
        {
            position += linearVelocity * timestep;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void IntegrateOrientation(ref quaternion orientation, float3 angularVelocity, float timestep)
        {
            quaternion dq = IntegrateAngularVelocity(angularVelocity, timestep);
            quaternion r = math.mul(orientation, dq);
            orientation = math.normalize(r);
        }

        // Returns a non-normalized quaternion that approximates the change in angle angularVelocity * timestep.
        internal static quaternion IntegrateAngularVelocity(float3 angularVelocity, float timestep)
        {
            float3 halfDeltaTime = new float3(timestep * 0.5f);
            float3 halfDeltaAngle = angularVelocity * halfDeltaTime;
            return new quaternion(new float4(halfDeltaAngle, 1.0f));
        }
    }
}
