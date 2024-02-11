using System;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics.Extensions;

namespace Unity.Physics.Aspects
{
    internal static class AspectConstants
    {
        public static readonly float k_MinMass = 0.0001f;
        public static readonly float k_MinInertiaComponentValue = 0.0001f;
    }

/// <summary>
/// A rigid body aspect. Contatins transform data, mass, mass overide, velocity, damping and
/// gravity factor information.
/// </summary>
    public readonly partial struct RigidBodyAspect : IAspect
    {
        internal readonly RefRW<LocalTransform> m_Transform;

        internal readonly RefRW<PhysicsVelocity> m_Velocity;

        [Optional]
        internal readonly RefRW<PhysicsMass> m_Mass;
        [Optional]
        internal readonly RefRW<PhysicsMassOverride> m_MassOveride;
        [Optional]
        internal readonly RefRW<PhysicsDamping> m_Damping;
        [Optional]
        internal readonly RefRW<PhysicsGravityFactor> m_GravityFactor;
        [Optional]
        internal readonly RefRW<PhysicsCollider> m_Collider; // Untill aspects fix this

        /// <summary>   The entity of this aspect. </summary>
        public readonly Entity Entity;

        /// <summary>   Gets or sets the world transform of this aspect. </summary>
        ///
        /// <value> The world space transform. </value>
        public LocalTransform WorldFromBody
        {
            get => m_Transform.ValueRO;
            set => m_Transform.ValueRW = value;
        }

        /// <summary>   Gets or sets the world space position. </summary>
        ///
        /// <value> The world space position. </value>
        public float3 Position
        {
            get => m_Transform.ValueRO.Position;
            set => m_Transform.ValueRW.Position = value;
        }

        /// <summary>   Gets or sets the world space rotation. </summary>
        ///
        /// <value> The world space rotation. </value>
        public quaternion Rotation
        {
            get => m_Transform.ValueRO.Rotation;
            set => m_Transform.ValueRW.Rotation = value;
        }

        /// <summary>   Gets or sets the uniform scale. </summary>
        ///
        /// <value> The scale. </value>
        public float Scale
        {
            get => m_Transform.ValueRO.Scale;
            set => m_Transform.ValueRW.Scale = value;
        }

        // Internal helper methods:
        internal quaternion BodyFromMotion_Rot => !m_Mass.IsValid ? quaternion.identity : m_Mass.ValueRO.InertiaOrientation;
        internal bool HasInfiniteMass => IsKinematic || (m_Mass.IsValid && m_Mass.ValueRO.HasInfiniteMass) || !m_Mass.IsValid;
        internal bool HasInfiniteInertia => IsKinematic || (m_Mass.IsValid && m_Mass.ValueRO.HasInfiniteInertia) || !m_Mass.IsValid;
        internal float ScaledInverseMass => Scale != 1.0f ? m_Mass.ValueRO.InverseMass * math.rcp(math.pow(math.abs(Scale), 3)) : m_Mass.ValueRO.InverseMass;
        internal float3 ScaledInverseInertia => Scale != 1.0f ? m_Mass.ValueRO.InverseInertia * math.rcp(math.pow(math.abs(Scale), 5)) : m_Mass.ValueRO.InverseInertia;

        /// <summary>   Gets or sets a value indicating whether this object is kinematic. </summary>
        ///
        /// <value> True if this object is kinematic, false if not. </value>
        public bool IsKinematic
        {
            get => (m_MassOveride.IsValid && m_MassOveride.ValueRO.IsKinematic != 0) || (m_Mass.IsValid && m_Mass.ValueRO.IsKinematic) || !m_Mass.IsValid;
            set
            {
                if (m_MassOveride.IsValid)
                {
                    m_MassOveride.ValueRW.IsKinematic = (byte)(value ? 1 : 0);
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("PhysicsMassOverride component doesn't exist!");
                }
            }
        }

        /// <summary>   Gets or sets the mass. </summary>
        ///
        /// <value> The mass. </value>
        public float Mass
        {
            get => !m_Mass.IsValid ? float.PositiveInfinity : math.select(math.rcp(m_Mass.ValueRO.InverseMass), float.PositiveInfinity, m_Mass.ValueRO.InverseMass == 0.0f);
            set
            {
                if (m_Mass.IsValid)
                {
                    float massValue = math.max(AspectConstants.k_MinMass, value);
                    m_Mass.ValueRW.InverseMass = math.select(math.rcp(massValue), 0.0f, massValue == float.PositiveInfinity);
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("PhysicsMass component doesn't exist!");
                }
            }
        }

        /// <summary>   Gets or sets the inertia. </summary>
        ///
        /// <value> The inertia. </value>
        public float3 Inertia
        {
            get => !m_Mass.IsValid ? new float3(float.PositiveInfinity) : math.select(math.rcp(m_Mass.ValueRO.InverseInertia), new float3(float.PositiveInfinity), m_Mass.ValueRO.InverseInertia == float3.zero);
            set
            {
                if (m_Mass.IsValid)
                {
                    float3 inertiaValue = math.select(value, new float3(AspectConstants.k_MinInertiaComponentValue), value == float3.zero);
                    m_Mass.ValueRW.InverseInertia = math.select(new float3(1.0f) / inertiaValue, float3.zero, inertiaValue == float.PositiveInfinity);
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("PhysicsMass component doesn't exist!");
                }
            }
        }
        /// <summary>   Gets or sets the center of mass in world space. </summary>
        ///
        /// <value> The center of mass in world space. </value>
        public float3 CenterOfMassWorldSpace
        {
            get => WorldFromBody.TransformPoint(CenterOfMassLocalSpace);
            set => CenterOfMassLocalSpace = WorldFromBody.InverseTransformPoint(value);
        }

        /// <summary>   Gets or sets the center of mass in local space. </summary>
        ///
        /// <value> The center of mass in local space. </value>
        public float3 CenterOfMassLocalSpace
        {
            get => !m_Mass.IsValid ? float3.zero : m_Mass.ValueRO.CenterOfMass;
            set
            {
                if (m_Mass.IsValid)
                {
                    m_Mass.ValueRW.CenterOfMass = value;
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("PhysicsMass component doesn't exist!");
                }
            }
        }

        /// <summary>   Gets or sets the linear velocity. </summary>
        ///
        /// <value> The linear velocity. </value>
        public float3 LinearVelocity
        {
            get => m_Velocity.ValueRO.Linear;
            set => m_Velocity.ValueRW.Linear = value;
        }

        /// <summary>   Gets or sets the angular velocity in world space. </summary>
        ///
        /// <value> The angular velocity in world space. </value>
        public float3 AngularVelocityWorldSpace
        {
            get => math.rotate(math.mul(Rotation, BodyFromMotion_Rot), m_Velocity.ValueRO.Angular);
            set => m_Velocity.ValueRW.Angular = math.rotate(math.inverse(math.mul(Rotation, BodyFromMotion_Rot)), value);
        }

        /// <summary>   Gets or sets the angular velocity in local space. </summary>
        ///
        /// <value> The angular velocity in local space. </value>
        public float3 AngularVelocityLocalSpace
        {
            get => math.rotate(BodyFromMotion_Rot, m_Velocity.ValueRO.Angular);
            set => m_Velocity.ValueRW.Angular = math.rotate(math.inverse(BodyFromMotion_Rot), value);
        }

        /// <summary>   Gets or sets the gravity factor. </summary>
        ///
        /// <value> The gravity factor. </value>
        public float GravityFactor
        {
            get => m_GravityFactor.IsValid ? m_GravityFactor.ValueRO.Value : 1.0f;
            set
            {
                if (m_GravityFactor.IsValid)
                {
                    m_GravityFactor.ValueRW.Value = value;
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("PhysicsGravityFactor component doesn't exist!");
                }
            }
        }

        /// <summary>   Gets or sets the linear damping. </summary>
        ///
        /// <value> The linear damping. </value>
        public float LinearDamping
        {
            get => m_Damping.IsValid ? m_Damping.ValueRO.Linear : 0.0f;
            set
            {
                if (m_Damping.IsValid)
                {
                    m_Damping.ValueRW.Linear = value;
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("PhysicsDamping component doesn't exist!");
                }
            }
        }

        /// <summary>   Gets or sets the angular damping. </summary>
        ///
        /// <value> The angular damping. </value>
        public float AngularDamping
        {
            get => m_Damping.IsValid ? m_Damping.ValueRW.Angular : 0.0f;
            set
            {
                if (m_Damping.IsValid)
                {
                    m_Damping.ValueRW.Angular = value;
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("PhysicsDamping component doesn't exist!");
                }
            }
        }

        /// <summary>   Gets effective mass in world space. </summary>
        ///
        /// <param name="impulse">          The impulse. </param>
        /// <param name="pointWorldSpace">  The point in world space. </param>
        ///
        /// <returns>   The effective mass in world space. </returns>
        public float GetEffectiveMassWorldSpace(float3 impulse, float3 pointWorldSpace) => PhysicsWorldExtensions.GetEffectiveMassImpl(CenterOfMassWorldSpace, ScaledInverseInertia, impulse, pointWorldSpace);

        /// <summary>   Gets effective mass in local space. </summary>
        ///
        /// <param name="impulse">          The impulse. </param>
        /// <param name="pointLocalSpace">  The point in local space. </param>
        ///
        /// <returns>   The effective mass local space. </returns>
        public float GetEffectiveMassLocalSpace(float3 impulse, float3 pointLocalSpace) => PhysicsWorldExtensions.GetEffectiveMassImpl(CenterOfMassLocalSpace, ScaledInverseInertia, impulse, pointLocalSpace);

        /// <summary>   Gets linear velocity at point in world space. </summary>
        ///
        /// <param name="pointWorldSpace">  The point in world space. </param>
        ///
        /// <returns>   The linear velocity at point in world space. </returns>
        public float3 GetLinearVelocityAtPointWorldSpace(float3 pointWorldSpace) => LinearVelocity + math.cross(AngularVelocityWorldSpace, pointWorldSpace - CenterOfMassWorldSpace);

        /// <summary>   Gets linear velocity at point in local space. </summary>
        ///
        /// <param name="pointLocalSpace">  The point in local space. </param>
        ///
        /// <returns>   The linear velocity at point in local space. </returns>
        public float3 GetLinearVelocityAtPointLocalSpace(float3 pointLocalSpace) => LinearVelocity + math.cross(AngularVelocityLocalSpace, pointLocalSpace - CenterOfMassLocalSpace);

        /// <summary>   Applies the linear impulse in world space. </summary>
        ///
        /// <param name="impulse">  The impulse to apply. </param>
        public void ApplyLinearImpulseWorldSpace(float3 impulse)
        {
            if (!HasInfiniteMass)
            {
                LinearVelocity += impulse * ScaledInverseMass;
            }
        }

        /// <summary>   Applies the linear impulse in local space. </summary>
        ///
        /// <param name="impulse">  The impulse to apply. </param>
        public void ApplyLinearImpulseLocalSpace(float3 impulse)
        {
            if (!HasInfiniteMass)
            {
                float3 impulseWorldSpace = math.rotate(Rotation, impulse);
                LinearVelocity += impulseWorldSpace * ScaledInverseMass;
            }
        }

        /// <summary>   Applies the angular impulse in world space. </summary>
        ///
        /// <param name="impulse">  The impulse to apply. </param>
        public void ApplyAngularImpulseWorldSpace(float3 impulse)
        {
            if (!HasInfiniteInertia)
            {
                AngularVelocityLocalSpace += math.rotate(math.inverse(Rotation), impulse) * ScaledInverseInertia;
            }
        }

        /// <summary>   Applies the angular impulse in local space. </summary>
        ///
        /// <param name="impulse">  The impulse to apply. </param>
        public void ApplyAngularImpulseLocalSpace(float3 impulse)
        {
            if (!HasInfiniteInertia)
            {
                AngularVelocityLocalSpace += impulse * ScaledInverseInertia;
            }
        }

        /// <summary>   Applies the impulse at point in world space. </summary>
        ///
        /// <param name="impulse">          The impulse. </param>
        /// <param name="pointWorldSpace">  The point in world space. </param>
        public void ApplyImpulseAtPointWorldSpace(float3 impulse, float3 pointWorldSpace)
        {
            ApplyLinearImpulseWorldSpace(impulse);

            float3 angularImpulseWorldSpace = math.cross(pointWorldSpace - CenterOfMassWorldSpace, impulse);
            ApplyAngularImpulseWorldSpace(angularImpulseWorldSpace);
        }

        /// <summary>   Applies the impulse at point in local space. </summary>
        ///
        /// <param name="impulse">          The impulse. </param>
        /// <param name="pointLocalSpace">  The point in local space. </param>
        public void ApplyImpulseAtPointLocalSpace(float3 impulse, float3 pointLocalSpace)
        {
            ApplyLinearImpulseLocalSpace(impulse);

            float3 angularImpulseLocalSpace = math.cross(pointLocalSpace - CenterOfMassLocalSpace, impulse);
            ApplyAngularImpulseLocalSpace(angularImpulseLocalSpace);
        }

        /// <summary>   Applies the explosive impulse. </summary>
        ///
        /// <param name="impulse">                      The impulse. </param>
        /// <param name="explosionPositionWorldSpace">  The explosion position world space. </param>
        /// <param name="explosionRadius">              The explosion radius. </param>
        /// <param name="up">                           The up vector. </param>
        /// <param name="upwardsModifier">              (Optional) The upwards modifier. </param>
        public void ApplyExplosiveImpulse(float impulse, float3 explosionPositionWorldSpace, float explosionRadius, float3 up, float upwardsModifier = 0.0f)
            => ApplyExplosiveImpulse(impulse, explosionPositionWorldSpace, explosionRadius, up, CollisionFilter.Default, upwardsModifier);

        /// <summary>   Applies the explosive impulse. </summary>
        ///
        /// <param name="impulse">                      The impulse. </param>
        /// <param name="explosionPositionWorldSpace">  The explosion position world space. </param>
        /// <param name="explosionRadius">              The explosion radius. </param>
        /// <param name="up">                           The up vector. </param>
        /// <param name="filter">                       Filter determining whether an explosion should be
        /// applied to the impulse. </param>
        /// <param name="upwardsModifier">              (Optional) The upwards modifier. </param>
        public void ApplyExplosiveImpulse(float impulse, float3 explosionPositionWorldSpace, float explosionRadius, float3 up, CollisionFilter filter, float upwardsModifier = 0.0f)
        {
            if (!m_Collider.IsValid || !m_Collider.ValueRO.IsValid)
                return;

            bool bExplosionProportionalToDistance = explosionRadius != 0.0f;
            var bodyFromWorld = WorldFromBody.Inverse();

            PointDistanceInput input = new PointDistanceInput
            {
                Position = bodyFromWorld.TransformPoint(explosionPositionWorldSpace),
                MaxDistance = math.select(float.MaxValue, math.abs(bodyFromWorld.Scale) * explosionRadius, bExplosionProportionalToDistance),
                Filter = filter
            };

            if (!m_Collider.ValueRO.Value.Value.CalculateDistance(input, out DistanceHit closestHit))
            {
                return;
            }

            if (bExplosionProportionalToDistance)
            {
                var closestHitFraction = closestHit.Distance / input.MaxDistance;
                impulse *= 1.0f - closestHitFraction;
            }

            var closestHitPositionWorld = WorldFromBody.TransformPoint(closestHit.Position);
            var forceDirection = math.normalizesafe(closestHitPositionWorld - explosionPositionWorldSpace);
            var impulseToApply = impulse * forceDirection;

            if (0.0f != upwardsModifier)
            {
                closestHitPositionWorld -= up * upwardsModifier;
            }

            ApplyImpulseAtPointWorldSpace(impulseToApply, closestHitPositionWorld);
        }
    }
}
