using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;

namespace ME.BECS.Physics.Components {

    /// <summary>
    /// The collision geometry of a rigid body. If not present, the rigid body cannot collide with
    /// anything.
    /// </summary>
    public struct PhysicsColliderBecs : IComponent
    {
        /// <summary>  The collider reference, null is allowed. </summary>
        public MemAllocatorPtrAuto<Collider> Value;

        /// <summary>   Gets a value indicating whether this object is valid. </summary>
        ///
        /// <value> True if this object is valid, false if not. </value>
        public bool IsValid => Value.IsValid();

        /// <summary>   Gets the collider pointer. </summary>
        ///
        /// <value> The collider pointer. </value>
        public unsafe Collider* ColliderPtr => (Collider*)Value.GetUnsafePtr();

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties => Value.IsValid() ? Value.As().MassProperties : MassProperties.UnitSphere;

        /// <summary>
        /// Indicates whether this <see cref="PhysicsColliderBecs"/> contains a unique <see cref="Collider"/> blob.
        /// That is, its Collider blob is not shared with any other <see cref="PhysicsColliderBecs"/>.
        /// </summary>
        ///
        /// <value> True if this <see cref="PhysicsColliderBecs"/> contains a unique <see cref="Collider"/> blob, false if not. </value>
        public bool IsUnique => Value.IsValid() && Value.As().IsUnique;
    }

        /// <summary>
    /// The mass properties of a rigid body. If not present, the rigid body has infinite mass and
    /// inertia.
    /// </summary>
    public struct PhysicsMassBecs : IComponent
    {
        /// <summary>   Center of mass and orientation of principal axes. </summary>
        public RigidTransform Transform;
        /// <summary>   Zero is allowed, for infinite mass. </summary>
        public float InverseMass;
        /// <summary>   Zero is allowed, for infinite inertia. </summary>
        public float3 InverseInertia;
        /// <summary>   See MassProperties.AngularExpansionFactor. </summary>
        public float AngularExpansionFactor;

        /// <summary>   Gets or sets the center of mass. </summary>
        ///
        /// <value> The center of mass. </value>
        public float3 CenterOfMass { get => Transform.pos; set => Transform.pos = value; }

        /// <summary>   Gets or sets the inertia orientation. </summary>
        ///
        /// <value> The inertia orientation. </value>
        public quaternion InertiaOrientation { get => Transform.rot; set => Transform.rot = value; }

        /// <summary>   Gets a value indicating whether this object has infinite mass. </summary>
        ///
        /// <value> True if this object has infinite mass, false if not. </value>
        public bool HasInfiniteMass => InverseMass == 0.0f;

        /// <summary>   Gets a value indicating whether this object has infinite inertia. </summary>
        ///
        /// <value> True if this object has infinite inertia, false if not. </value>
        public bool HasInfiniteInertia => !math.any(InverseInertia);

        /// <summary>   Gets a value indicating whether this object is kinematic. </summary>
        ///
        /// <value> True if this object is kinematic, false if not. </value>
        public bool IsKinematic => HasInfiniteMass && HasInfiniteInertia;

        /// <summary>   Creates a dynamic mass. </summary>
        ///
        /// <param name="massProperties">   The mass properties. </param>
        /// <param name="mass">             The mass. </param>
        ///
        /// <returns>   The new dynamic mass. </returns>
        public static PhysicsMassBecs CreateDynamic(MassProperties massProperties, float mass)
        {
            SafetyChecks.CheckFiniteAndPositiveAndThrow(mass, nameof(mass));

            return new PhysicsMassBecs
            {
                Transform = massProperties.MassDistribution.Transform,
                InverseMass = math.rcp(mass),
                InverseInertia = math.rcp(massProperties.MassDistribution.InertiaTensor * mass),
                AngularExpansionFactor = massProperties.AngularExpansionFactor
            };
        }

        /// <summary>   Creates a kinematic mass. </summary>
        ///
        /// <param name="massProperties">   The mass properties. </param>
        ///
        /// <returns>   The new kinematic mass. </returns>
        public static PhysicsMassBecs CreateKinematic(MassProperties massProperties)
        {
            return new PhysicsMassBecs
            {
                Transform = massProperties.MassDistribution.Transform,
                InverseMass = 0f,
                InverseInertia = float3.zero,
                AngularExpansionFactor = massProperties.AngularExpansionFactor
            };
        }
    }
        
        /// <summary>
    /// Add this component to a dynamic body if it needs to sometimes switch to being kinematic. This
    /// allows you to retain its dynamic mass properties on its <see cref="PhysicsMassBecs"/> component,
    /// but have the physics solver temporarily treat it as if it were kinematic. Kinematic bodies
    /// will have infinite mass and inertia. They should also not be affected by gravity. Hence, if
    /// IsKinematic is non-zero the value in an associated <see cref="PhysicsGravityFactorBecs"/>
    /// component is also ignored. If SetVelocityToZero is non-zero then the value in an associated <see cref="PhysicsVelocityBecs"/>
    /// component is also ignored.
    /// </summary>
    public struct PhysicsMassOverrideBecs : IComponent
    {
        /// <summary>   The is kinematic flag. </summary>
        public byte IsKinematic;
        /// <summary>   The set velocity to zero flag. </summary>
        public byte SetVelocityToZero;
    }

    /// <summary>   The velocity of a rigid body. If absent, the rigid body is static. </summary>
    public struct PhysicsVelocityBecs : IComponent
    {
        /// <summary>   The body's world-space linear velocity in units per second. </summary>
        public float3 Linear;

        /// <summary>
        /// The body's angular velocity in radians per second about each principal axis specified by <see cref="PhyPhysicsMassBecsansform"/>
        /// . In order to get or set world-space values, use <see cref="PhysicsComponentExtensions.GetAngularVelocityWorldSpace"/>
        /// and <see cref="PhysicsComponentExtensions.SetAngularVelocityWorldSpace"/>, respectively.
        /// </summary>
        public float3 Angular;

        /// <summary>   Zero Physics Velocity. All fields are initialized to zero. </summary>
        public static readonly PhysicsVelocityBecs Zero = new PhysicsVelocityBecs
        {
            Linear = new float3(0),
            Angular = new float3(0)
        };

        /// <summary>
        /// Create a <see cref="PhysicsVelocityBecs"/> required to move a body to a target position and
        /// orientation. Use this method to control kinematic bodies directly if they need to generate
        /// contact events when moving to their new positions. If you need to teleport kinematic bodies
        /// you can simply set their <see cref="Unity.Transforms.LocalTransform"/> component values directly.
        /// </summary>
        ///
        /// <param name="bodyMass">         The body's <see cref="PhysicsMassBecs"/> component. </param>
        /// <param name="bodyPosition">     The body's world-space position. </param>
        /// <param name="bodyOrientation">  The body's world-space rotation. </param>
        /// <param name="targetTransform">  The desired translation and rotation values the body should
        /// move to in world space. </param>
        /// <param name="stepFrequency">   The step frequency in the simulation where the body's motion
        /// is solved (i.e., 1 / FixedDeltaTime). </param>
        ///
        /// <returns>   The calculated velocity to target. </returns>
        public static PhysicsVelocityBecs CalculateVelocityToTarget(
            in PhysicsMassBecs bodyMass, in float3 bodyPosition, in quaternion bodyOrientation,
            in RigidTransform targetTransform, in float stepFrequency
        )
        {
            var velocity = new PhysicsVelocityBecs();
            var worldFromBody = new RigidTransform(bodyOrientation, bodyPosition);
            var worldFromMotion = math.mul(worldFromBody, bodyMass.Transform);
            PhysicsWorldExtensions.CalculateVelocityToTargetImpl(
                worldFromBody, math.inverse(worldFromMotion.rot), bodyMass.Transform.pos, targetTransform, stepFrequency,
                out velocity.Linear, out velocity.Angular
            );
            return velocity;
        }
    }
    
    /// <summary>
    /// Optional damping applied to the rigid body velocities during each simulation step. This
    /// scales the velocities using: math.clamp(1 - damping * Timestep, 0, 1)
    /// </summary>
    public struct PhysicsDampingBecs : IComponent
    {
        /// <summary>   Damping applied to the linear velocity. </summary>
        public float Linear;
        /// <summary>   Damping applied to the angular velocity. </summary>
        public float Angular;
    }

    /// <summary>
    /// Optional gravity factor applied to a rigid body during each simulation step. This scales the
    /// gravity vector supplied to the simulation step.
    /// </summary>
    public struct PhysicsGravityFactorBecs : IComponent
    {
        /// <summary>   The value. </summary>
        public float Value;
    }

    /// <summary>
    /// Optional custom tags attached to a rigid body. This will be copied to any contacts and
    /// Jacobians involving this rigid body, providing additional context to any user logic operating
    /// on those structures.
    /// </summary>
    public struct PhysicsCustomTagsBecs : IComponent
    {
        /// <summary>   The value. </summary>
        public byte Value;
    }
    
    public struct IsPhysicsStaticEcs : IComponent { }

    public struct PhysicsConstraintPositionBecs : IComponent {

        public bool freezeX;
        public bool freezeY;
        public bool freezeZ;

    }

}
