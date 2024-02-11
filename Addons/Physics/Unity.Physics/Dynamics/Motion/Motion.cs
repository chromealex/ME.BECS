using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>
    /// Describes how mass is distributed within an object Represented by a transformed box inertia
    /// of unit mass.
    /// </summary>
    public struct MassDistribution
    {
        /// <summary>   The center of mass and the orientation to principal axis space. </summary>
        public RigidTransform Transform;

        /// <summary>   Diagonalized inertia tensor for a unit mass. </summary>
        public float3 InertiaTensor;

        /// <summary>   Get the inertia as a 3x3 matrix. </summary>
        ///
        /// <value> The inertia matrix. </value>
        public float3x3 InertiaMatrix
        {
            get
            {
                var r = new float3x3(Transform.rot);
                var r2 = new float3x3(InertiaTensor.x * r.c0, InertiaTensor.y * r.c1, InertiaTensor.z * r.c2);
                return math.mul(r2, math.transpose(r));
            }
        }
    }

    /// <summary>   The mass properties of an object. </summary>
    public struct MassProperties
    {
        /// <summary>   The distribution of a unit mass throughout the object. </summary>
        public MassDistribution MassDistribution;

        /// <summary>   The volume of the object. </summary>
        public float Volume;

        /// <summary>
        /// Upper bound on the rate of change of the object's extent in any direction, with respect to
        /// angular speed around its center of mass. Used to determine how much to expand a rigid body's
        /// AABB to enclose its swept volume.
        /// </summary>
        public float AngularExpansionFactor;

        /// <summary>   (Immutable) The mass properties of a unit sphere. </summary>
        public static readonly MassProperties UnitSphere = new MassProperties
        {
            MassDistribution = new MassDistribution
            {
                Transform = RigidTransform.identity,
                InertiaTensor = new float3(2.0f / 5.0f)
            },
            Volume = (4.0f / 3.0f) * math.PI,
            AngularExpansionFactor = 0.0f
        };

        /// <summary>
        /// Scales the mass properties by the given uniform scale factor assuming unit mass.
        /// </summary>
        /// <param name="uniformScale">The uniform scale.</param>
        public void Scale(float uniformScale)
        {
            if (!Math.IsApproximatelyEqual(uniformScale, 1.0f))
            {
                MassDistribution.Transform.pos *= uniformScale;

                var absScale = math.abs(uniformScale);
                var absScale2 = math.square(absScale);

                // Assuming unit mass, the inertia tensor needs to be scaled by the squared scale
                MassDistribution.InertiaTensor *= absScale2;

                Volume *= absScale2 * absScale;
                AngularExpansionFactor *= absScale;
            }
        }

        /// <summary>
        /// Creates mass properties of a box with the provided side lengths, centered on the origin.
        /// </summary>
        /// <param name="size">Side lengths of the box along x, y and z.</param>
        /// <returns>Mass properties of the box.</returns>
        public static MassProperties CreateBox(in float3 size)
        {
            return new MassProperties
            {
                MassDistribution = new MassDistribution
                {
                    Transform = RigidTransform.identity,
                    InertiaTensor = new float3(size.y * size.y + size.z * size.z, size.x * size.x + size.z * size.z, size.x * size.x + size.y * size.y) / 12.0f
                },
                Volume = size.x * size.y * size.z,
                AngularExpansionFactor = 0.0f
            };
        }

        /// <summary>
        /// Creates mass properties of a sphere with the provided radius, centered on the origin.
        /// </summary>
        /// <param name="radius">Sphere radius.</param>
        /// <returns>Mass properties of the sphere.</returns>
        public static MassProperties CreateSphere(in float radius)
        {
            var radius2 = math.square(radius);
            return new MassProperties
            {
                MassDistribution = new MassDistribution
                {
                    Transform = RigidTransform.identity,
                    InertiaTensor = new float3(2.0f / 5.0f * radius2)
                },
                Volume = (4.0f / 3.0f) * math.PI * radius * radius2,
                AngularExpansionFactor = 0.0f
            };
        }
    }

    /// <summary>
    /// A dynamic rigid body's "cold" motion data, used during Jacobian building and integration.
    /// </summary>
    public struct MotionData
    {
        /// <summary>   Center of mass and inertia orientation in world space. </summary>
        public RigidTransform WorldFromMotion;

        /// <summary>   Center of mass and inertia orientation in rigid body space. </summary>
        public RigidTransform BodyFromMotion;

        /// <summary>   Linear damping applied to the motion during each simulation step. </summary>
        public float LinearDamping;
        /// <summary>   Angular damping applied to the motion during each simulation step. </summary>
        public float AngularDamping;

        /// <summary>   The Zero Motion Data. All transforms are identites, all dampings are zero. </summary>
        public static readonly MotionData Zero = new MotionData
        {
            WorldFromMotion = RigidTransform.identity,
            BodyFromMotion = RigidTransform.identity,
            LinearDamping = 0.0f,
            AngularDamping = 0.0f
        };
    }

    /// <summary>   A dynamic rigid body's "hot" motion data, used during solving. </summary>
    public struct MotionVelocity
    {
        /// <summary>  Linear velocity in  World space. </summary>
        public float3 LinearVelocity;
        /// <summary>  Angular velocity in Motion space. </summary>
        public float3 AngularVelocity;
        /// <summary>   The inverse inertia. </summary>
        public float3 InverseInertia;
        /// <summary>   The inverse mass. </summary>
        public float InverseMass;
        /// <summary>   The angular expansion factor. </summary>
        public float AngularExpansionFactor;

        /// <summary>   A multiplier applied to the simulation step's gravity vector. </summary>
        public float GravityFactor;

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

        /// <summary>   The zero Motion Velocity. All fields are initialized to zero. </summary>
        public static readonly MotionVelocity Zero = new MotionVelocity
        {
            LinearVelocity = new float3(0),
            AngularVelocity = new float3(0),
            InverseInertia = new float3(0),
            InverseMass = 0.0f,
            AngularExpansionFactor = 0.0f,
            GravityFactor = 0.0f
        };

        /// <summary>   Apply a linear impulse (in world space) </summary>
        ///
        /// <param name="impulse">  The impulse. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyLinearImpulse(float3 impulse)
        {
            LinearVelocity += impulse * InverseMass;
        }

        /// <summary>   Apply an angular impulse (in motion space) </summary>
        ///
        /// <param name="impulse">  The impulse. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyAngularImpulse(float3 impulse)
        {
            AngularVelocity += impulse * InverseInertia;
        }

        // Calculate the distances by which to expand collision tolerances based on the speed of the object.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MotionExpansion CalculateExpansion(float timeStep) => new MotionExpansion
        {
            Linear = LinearVelocity * timeStep,
            // math.length(AngularVelocity) * timeStep is conservative approximation of sin((math.length(AngularVelocity) * timeStep)
            Uniform = math.min(math.length(AngularVelocity) * timeStep * AngularExpansionFactor, AngularExpansionFactor)
        };
    }

    // Provides an upper bound on change in a body's extents in any direction during a step.
    // Used to determine how far away from the body to look for collisions.
    struct MotionExpansion
    {
        public float3 Linear;   // how far to look ahead of the object
        public float Uniform;   // how far to look around the object

        public float MaxDistance => math.length(Linear) + Uniform;

        public static readonly MotionExpansion Zero = new MotionExpansion
        {
            Linear = new float3(0),
            Uniform = 0.0f
        };

        // Expand an AABB
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Aabb ExpandAabb(Aabb aabb) => new Aabb
        {
            Max = math.max(aabb.Max, aabb.Max + Linear) + Uniform,
            Min = math.min(aabb.Min, aabb.Min + Linear) - Uniform
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator MotionExpansion(float4 motionExpansion) => new MotionExpansion { Linear = motionExpansion.xyz, Uniform = motionExpansion.w };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator float4(MotionExpansion motionExpansion) => new float4(motionExpansion.Linear, motionExpansion.Uniform);
    }

    internal struct Velocity
    {
        /// <summary>   World space linear velocity. </summary>
        public float3 Linear;
        /// <summary>   Motion space angular velocity. </summary>
        public float3 Angular;

        public static readonly Velocity Zero = new Velocity
        {
            Linear = new float3(0),
            Angular = new float3(0)
        };
    }
}
