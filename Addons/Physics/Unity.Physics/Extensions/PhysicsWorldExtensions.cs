using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Unity.Physics.Extensions
{
    /// <summary>   Utility functions acting on a physics world. </summary>
    public static class PhysicsWorldExtensions
    {
        /// <summary>   An in PhysicsWorld extension method that gets collision filter. </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        ///
        /// <returns>   The collision filter. </returns>
        public static CollisionFilter GetCollisionFilter(this in PhysicsWorld world, int rigidBodyIndex)
        {
            CollisionFilter filter = CollisionFilter.Default;
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumBodies)) return filter;

            unsafe { filter = world.Bodies[rigidBodyIndex].Collider.Value.GetCollisionFilter(); }

            return filter;
        }

        /// <summary>   An in PhysicsWorld extension method that gets the mass. </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        ///
        /// <returns>   The mass. </returns>
        public static float GetMass(this in PhysicsWorld world, int rigidBodyIndex)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return 0;

            MotionVelocity mv = world.MotionVelocities[rigidBodyIndex];

            return 0 == mv.InverseMass ? 0.0f : 1.0f / mv.InverseMass;
        }

        /// <summary>
        /// Get the effective mass of a Rigid Body in a given direction and from a particular point (in
        /// World Space)
        /// </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        /// <param name="impulse">          The impulse. </param>
        /// <param name="point">            The point. </param>
        ///
        /// <returns>   The effective mass. </returns>
        public static float GetEffectiveMass(this in PhysicsWorld world, int rigidBodyIndex, float3 impulse, float3 point)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return 0;

            MotionVelocity mv = world.MotionVelocities[rigidBodyIndex];

            return GetEffectiveMassImpl(GetCenterOfMass(world, rigidBodyIndex), mv.InverseInertia, impulse, point);
        }

        /// <summary>   Gets effective mass implementation. </summary>
        ///
        /// <param name="centerOfMass">     The center of mass. </param>
        /// <param name="inverseInertia">   The inverse inertia. </param>
        /// <param name="impulse">          The impulse. </param>
        /// <param name="point">            The point. </param>
        ///
        /// <returns>   The effective mass implementation. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float GetEffectiveMassImpl(float3 centerOfMass, float3 inverseInertia, float3 impulse, float3 point)
        {
            float3 pointDir = math.normalizesafe(point - centerOfMass);
            float3 impulseDir = math.normalizesafe(impulse);

            float3 jacobian = math.cross(pointDir, impulseDir);
            float invEffMass = math.csum(math.dot(jacobian, jacobian) * inverseInertia);
            return math.select(1.0f / invEffMass, 0.0f, math.abs(invEffMass) < 1e-5);
        }

        /// <summary>   Get the Rigid Bodies Center of Mass (in World Space) </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        ///
        /// <returns>   The center of mass. </returns>
        public static float3 GetCenterOfMass(this in PhysicsWorld world, int rigidBodyIndex)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return float3.zero;

            return world.MotionDatas[rigidBodyIndex].WorldFromMotion.pos;
        }

        /// <summary>   An in PhysicsWorld extension method that gets a position of a body in World Space. </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        ///
        /// <returns>   The position. </returns>
        public static float3 GetPosition(this in PhysicsWorld world, int rigidBodyIndex)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return float3.zero;

            // Motion to body transform
            MotionData md = world.MotionDatas[rigidBodyIndex];

            RigidTransform worldFromBody = math.mul(md.WorldFromMotion, math.inverse(md.BodyFromMotion));
            return worldFromBody.pos;
        }

        /// <summary>   An in PhysicsWorld extension method that gets a rotation. </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        ///
        /// <returns>   The rotation. </returns>
        public static quaternion GetRotation(this in PhysicsWorld world, int rigidBodyIndex)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return quaternion.identity;

            // Motion to body transform
            MotionData md = world.MotionDatas[rigidBodyIndex];

            RigidTransform worldFromBody = math.mul(md.WorldFromMotion, math.inverse(md.BodyFromMotion));
            return worldFromBody.rot;
        }

        /// <summary>   Get the linear velocity of a rigid body (in world space) </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        ///
        /// <returns>   The linear velocity. </returns>
        public static float3 GetLinearVelocity(this in PhysicsWorld world, int rigidBodyIndex)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return float3.zero;

            return world.MotionVelocities[rigidBodyIndex].LinearVelocity;
        }

        /// <summary>   Set the linear velocity of a rigid body (in world space) </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        /// <param name="linearVelocity">   The linear velocity. </param>
        public static void SetLinearVelocity(this PhysicsWorld world, int rigidBodyIndex, float3 linearVelocity)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return;

            Unity.Collections.NativeArray<MotionVelocity> motionVelocities = world.MotionVelocities;
            MotionVelocity mv = motionVelocities[rigidBodyIndex];
            mv.LinearVelocity = linearVelocity;
            motionVelocities[rigidBodyIndex] = mv;
        }

        /// <summary>   Get the linear velocity of a rigid body at a given point (in world space) </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        /// <param name="point">            The point. </param>
        ///
        /// <returns>   The linear velocity. </returns>
        public static float3 GetLinearVelocity(this in PhysicsWorld world, int rigidBodyIndex, float3 point)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return float3.zero;

            MotionVelocity mv = world.MotionVelocities[rigidBodyIndex];
            MotionData md = world.MotionDatas[rigidBodyIndex];

            return GetLinearVelocityImpl(md.WorldFromMotion, mv.AngularVelocity, mv.LinearVelocity, point);
        }

        /// <summary>   Gets linear velocity implementation. </summary>
        ///
        /// <param name="worldFromMotion">  The world from motion. </param>
        /// <param name="angularVelocity">  The angular velocity. </param>
        /// <param name="linearVelocity">   The linear velocity. </param>
        /// <param name="point">            The point. </param>
        ///
        /// <returns>   The linear velocity implementation. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 GetLinearVelocityImpl(RigidTransform worldFromMotion, float3 angularVelocity, float3 linearVelocity, float3 point)
        {
            angularVelocity = math.rotate(worldFromMotion, angularVelocity);
            return linearVelocity + math.cross(angularVelocity, point - worldFromMotion.pos);
        }

        /// <summary>
        /// Get the angular velocity of a rigid body around it's center of mass (in world space)
        /// </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        ///
        /// <returns>   The angular velocity. </returns>
        public static float3 GetAngularVelocity(this in PhysicsWorld world, int rigidBodyIndex)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return float3.zero;

            MotionVelocity mv = world.MotionVelocities[rigidBodyIndex];
            MotionData md = world.MotionDatas[rigidBodyIndex];

            return math.rotate(md.WorldFromMotion, mv.AngularVelocity);
        }

        /// <summary>   Set the angular velocity of a rigid body (in world space) </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        /// <param name="angularVelocity">  The angular velocity. </param>
        public static void SetAngularVelocity(this PhysicsWorld world, int rigidBodyIndex, float3 angularVelocity)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return;

            MotionData md = world.MotionDatas[rigidBodyIndex];
            float3 angularVelocityMotionSpace = math.rotate(math.inverse(md.WorldFromMotion.rot), angularVelocity);

            Unity.Collections.NativeArray<MotionVelocity> motionVelocities = world.MotionVelocities;
            MotionVelocity mv = motionVelocities[rigidBodyIndex];
            mv.AngularVelocity = angularVelocityMotionSpace;
            motionVelocities[rigidBodyIndex] = mv;
        }

        /// <summary>   Apply an impulse to a rigid body at a point (in world space) </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        /// <param name="linearImpulse">    The linear impulse. </param>
        /// <param name="point">            The point. </param>
        public static void ApplyImpulse(this PhysicsWorld world, int rigidBodyIndex, float3 linearImpulse, float3 point)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return;

            MotionData md = world.MotionDatas[rigidBodyIndex];
            float3 angularImpulseWorldSpace = math.cross(point - md.WorldFromMotion.pos, linearImpulse);
            float3 angularImpulseMotionSpace = math.rotate(math.inverse(md.WorldFromMotion.rot), angularImpulseWorldSpace);

            Unity.Collections.NativeArray<MotionVelocity> motionVelocities = world.MotionVelocities;
            MotionVelocity mv = motionVelocities[rigidBodyIndex];
            mv.ApplyLinearImpulse(linearImpulse);
            mv.ApplyAngularImpulse(angularImpulseMotionSpace);
            motionVelocities[rigidBodyIndex] = mv;
        }

        /// <summary>   Apply a linear impulse to a rigid body (in world space) </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        /// <param name="linearImpulse">    The linear impulse. </param>
        public static void ApplyLinearImpulse(this PhysicsWorld world, int rigidBodyIndex, float3 linearImpulse)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return;

            Unity.Collections.NativeArray<MotionVelocity> motionVelocities = world.MotionVelocities;
            MotionVelocity mv = motionVelocities[rigidBodyIndex];
            mv.ApplyLinearImpulse(linearImpulse);
            motionVelocities[rigidBodyIndex] = mv;
        }

        /// <summary>   Apply an angular impulse to a rigidBodyIndex (in world space) </summary>
        ///
        /// <param name="world">            The world to act on. </param>
        /// <param name="rigidBodyIndex">   Zero-based index of the rigid body. </param>
        /// <param name="angularImpulse">   The angular impulse. </param>
        public static void ApplyAngularImpulse(this PhysicsWorld world, int rigidBodyIndex, float3 angularImpulse)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies)) return;

            MotionData md = world.MotionDatas[rigidBodyIndex];
            float3 angularImpulseInertiaSpace = math.rotate(math.inverse(md.WorldFromMotion.rot), angularImpulse);

            Unity.Collections.NativeArray<MotionVelocity> motionVelocities = world.MotionVelocities;
            MotionVelocity mv = motionVelocities[rigidBodyIndex];
            mv.ApplyAngularImpulse(angularImpulseInertiaSpace);
            motionVelocities[rigidBodyIndex] = mv;
        }

        /// <summary>
        /// Calculate a linear and angular velocity required to move the given rigid body to the given
        /// target transform in the given time step.
        /// </summary>
        ///
        /// <param name="world">                    The world to act on. </param>
        /// <param name="rigidBodyIndex">           Zero-based index of the rigid body. </param>
        /// <param name="targetTransform">          Target transform. </param>
        /// <param name="timestep">                 The timestep. </param>
        /// <param name="requiredLinearVelocity">   [out] The required linear velocity. </param>
        /// <param name="requiredAngularVelocity">  [out] The required angular velocity. </param>
        public static void CalculateVelocityToTarget(
            this PhysicsWorld world, int rigidBodyIndex, RigidTransform targetTransform, float timestep,
            out float3 requiredLinearVelocity, out float3 requiredAngularVelocity)
        {
            if (!(0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies))
            {
                requiredLinearVelocity = default;
                requiredAngularVelocity = default;
                return;
            }

            MotionData md = world.MotionDatas[rigidBodyIndex];
            RigidTransform worldFromBody = math.mul(md.WorldFromMotion, math.inverse(md.BodyFromMotion));
            CalculateVelocityToTargetImpl(
                worldFromBody, math.inverse(md.WorldFromMotion.rot), md.BodyFromMotion.pos, targetTransform, timestep,
                out requiredLinearVelocity, out requiredAngularVelocity
            );
        }

        /// <summary>   Calculates the velocity to target implementation. </summary>
        ///
        /// <param name="worldFromBody">            The world from body. </param>
        /// <param name="motionFromWorld">          The motion from world. </param>
        /// <param name="centerOfMass">             The center of mass. </param>
        /// <param name="targetTransform">          Target transform. </param>
        /// <param name="stepFrequency">            The step frequency. </param>
        /// <param name="requiredLinearVelocity">   [out] The required linear velocity. </param>
        /// <param name="requiredAngularVelocity">  [out] The required angular velocity. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateVelocityToTargetImpl(
            RigidTransform worldFromBody, quaternion motionFromWorld, float3 centerOfMass,
            RigidTransform targetTransform, in float stepFrequency,
            out float3 requiredLinearVelocity, out float3 requiredAngularVelocity
        )
        {
            var com = new float4(centerOfMass, 1f);
            requiredLinearVelocity = (math.mul(targetTransform, com) - math.mul(worldFromBody, com)).xyz * stepFrequency;
            var angularVelocity = math.mul(targetTransform.rot, math.inverse(worldFromBody.rot)).ToEulerAngles() * stepFrequency;
            requiredAngularVelocity = math.rotate(motionFromWorld, angularVelocity);
        }
    }
}
