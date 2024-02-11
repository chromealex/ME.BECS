using System;
using UnityEngine.Assertions;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   Defines the collision response policy of a collider. </summary>
    public enum CollisionResponsePolicy : byte
    {
        /// <summary>
        /// The collider will collide normally
        /// </summary>
        Collide = 0,
        /// <summary>
        /// The collider will collide normally and raise collision events
        /// </summary>
        CollideRaiseCollisionEvents = 1,
        /// <summary>
        /// The collider will raise trigger events when it overlaps another collider
        /// </summary>
        RaiseTriggerEvents = 3,
        /// <summary>
        /// The collider will skip collision, but can still move and intercept queries
        /// </summary>
        None = byte.MaxValue - 1
    }

    /// <summary>   Describes how an object should respond to collisions with other objects. </summary>
    public struct Material : IEquatable<Material>
    {
        internal MaterialFlags Flags;
        /// <summary>   The friction combine policy. </summary>
        public CombinePolicy FrictionCombinePolicy;
        /// <summary>   The restitution combine policy. </summary>
        public CombinePolicy RestitutionCombinePolicy;
        /// <summary>   The custom tags set by the user. </summary>
        public byte CustomTags;
        /// <summary>   The friction. </summary>
        public float Friction;
        /// <summary>   The restitution. </summary>
        public float Restitution;

        /// <summary>   Gets or sets the collision response. </summary>
        ///
        /// <value> The collision response. </value>
        public CollisionResponsePolicy CollisionResponse
        {
            get => FlagsToCollisionResponse(Flags);
            set
            {
                switch (value)
                {
                    case CollisionResponsePolicy.None:
                        Flags |= MaterialFlags.DisableCollisions;
                        Flags &= ~MaterialFlags.IsTrigger & ~MaterialFlags.EnableCollisionEvents;
                        return;
                    case CollisionResponsePolicy.RaiseTriggerEvents:
                        Flags |= MaterialFlags.IsTrigger;
                        Flags &= ~MaterialFlags.DisableCollisions & ~MaterialFlags.EnableCollisionEvents;
                        return;
                    case CollisionResponsePolicy.CollideRaiseCollisionEvents:
                        Flags |= MaterialFlags.EnableCollisionEvents;
                        Flags &= ~MaterialFlags.IsTrigger & ~MaterialFlags.DisableCollisions;
                        return;
                    case CollisionResponsePolicy.Collide:
                        Flags &= ~MaterialFlags.DisableCollisions & ~MaterialFlags.EnableCollisionEvents & ~MaterialFlags.IsTrigger;
                        return;
                    default:
                        Assert.IsTrue(false, "Invalid collision response provided!");
                        return;
                }
            }
        }

        /// <summary>
        ///   Get or Set EnableMassFactors
        /// </summary>
        ///
        /// <value> If true, the object can have its inertia and mass overridden during solving. </value>
        public bool EnableMassFactors
        {
            get { return (Flags & MaterialFlags.EnableMassFactors) != 0; }
            set
            {
                if (value != EnableMassFactors)
                {
                    // Toggle the bit since the value is changing
                    Flags ^= MaterialFlags.EnableMassFactors;
                }
            }
        }

        /// <summary>   Get or Set EnableSurfaceVelocity. </summary>
        ///
        /// <value> If true, the object can apply a surface velocity to its contact points.. </value>
        public bool EnableSurfaceVelocity
        {
            get { return (Flags & MaterialFlags.EnableSurfaceVelocity) != 0; }
            set
            {
                if (value != EnableSurfaceVelocity)
                {
                    // Toggle the bit since the value is changing
                    Flags ^= MaterialFlags.EnableSurfaceVelocity;
                }
            }
        }

        // Higher priority flags render lower priority ones useless, while same priority flags can co-exist.
        // Priority is as follows:
        // 1. DisableCollisions
        // 2. IsTrigger
        // 3. EnableCollisionEvents
        // 3. EnableMassFactors
        // 3. EnableSurfaceVelocity
        [Flags]
        internal enum MaterialFlags : byte
        {
            None = 0,
            IsTrigger = 1 << 0,
            EnableCollisionEvents = 1 << 1,
            EnableMassFactors = 1 << 2,
            EnableSurfaceVelocity = 1 << 3,
            DisableCollisions = 1 << 4
        }

        /// <summary>   Defines how a value from a pair of materials should be combined. </summary>
        public enum CombinePolicy : byte
        {
            /// <summary>   sqrt(a * b) </summary>
            GeometricMean,
            /// <summary>   min(a, b) </summary>
            Minimum,
            /// <summary>   max(a, b) </summary>
            Maximum,
            /// <summary>   (a + b) / 2. </summary>
            ArithmeticMean
        }

        /// <summary>   (Immutable) A default material. </summary>
        public static readonly Material Default = new Material
        {
            FrictionCombinePolicy = CombinePolicy.GeometricMean,
            RestitutionCombinePolicy = CombinePolicy.GeometricMean,
            Friction = 0.5f,
            Restitution = 0.0f
        };

        private static CollisionResponsePolicy FlagsToCollisionResponse(MaterialFlags flags)
        {
            if ((flags & MaterialFlags.DisableCollisions) != 0)
            {
                return CollisionResponsePolicy.None;
            }
            else if ((flags & MaterialFlags.IsTrigger) != 0)
            {
                return CollisionResponsePolicy.RaiseTriggerEvents;
            }
            else if ((flags & MaterialFlags.EnableCollisionEvents) != 0)
            {
                return CollisionResponsePolicy.CollideRaiseCollisionEvents;
            }
            else
            {
                return CollisionResponsePolicy.Collide;
            }
        }

        // Get combined collision response of the 2 materials.
        // Used only internally by the manifold creation pipeline.
        internal static CollisionResponsePolicy GetCombinedCollisionResponse(Material materialA, Material materialB)
        {
            var flags = materialA.Flags | materialB.Flags;
            return FlagsToCollisionResponse(flags);
        }

        /// <summary>
        /// Get a combined friction value for a pair of materials. The combine policy with the highest
        /// value takes priority.
        /// </summary>
        ///
        /// <param name="materialA">    The material a. </param>
        /// <param name="materialB">    The material b. </param>
        ///
        /// <returns>   The combined friction. </returns>
        public static float GetCombinedFriction(Material materialA, Material materialB)
        {
            var policy = (CombinePolicy)math.max((int)materialA.FrictionCombinePolicy, (int)materialB.FrictionCombinePolicy);
            switch (policy)
            {
                case CombinePolicy.GeometricMean:
                    return math.sqrt(materialA.Friction * materialB.Friction);
                case CombinePolicy.Minimum:
                    return math.min(materialA.Friction, materialB.Friction);
                case CombinePolicy.Maximum:
                    return math.max(materialA.Friction, materialB.Friction);
                case CombinePolicy.ArithmeticMean:
                    return (materialA.Friction + materialB.Friction) * 0.5f;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Get a combined restitution value for a pair of materials. The combine policy with the highest
        /// value takes priority.
        /// </summary>
        ///
        /// <param name="materialA">    The material a. </param>
        /// <param name="materialB">    The material b. </param>
        ///
        /// <returns>   The combined restitution. </returns>
        public static float GetCombinedRestitution(Material materialA, Material materialB)
        {
            var policy = (CombinePolicy)math.max((int)materialA.RestitutionCombinePolicy, (int)materialB.RestitutionCombinePolicy);
            switch (policy)
            {
                case CombinePolicy.GeometricMean:
                    return math.sqrt(materialA.Restitution * materialB.Restitution);
                case CombinePolicy.Minimum:
                    return math.min(materialA.Restitution, materialB.Restitution);
                case CombinePolicy.Maximum:
                    return math.max(materialA.Restitution, materialB.Restitution);
                case CombinePolicy.ArithmeticMean:
                    return (materialA.Restitution + materialB.Restitution) * 0.5f;
                default:
                    return 0;
            }
        }

        internal enum MaterialField
        {
            FrictionCombinePolicy,
            RestitutionCombinePolicy,
            CustomTags,
            Friction,
            Restitution,
            CollisionResponsePolicy,
            All
        }

        internal void SetMaterialField(in Material other, MaterialField option)
        {
            switch (option)
            {
                case MaterialField.FrictionCombinePolicy:
                    FrictionCombinePolicy = other.FrictionCombinePolicy;
                    break;
                case MaterialField.RestitutionCombinePolicy:
                    RestitutionCombinePolicy = other.RestitutionCombinePolicy;
                    break;
                case MaterialField.CustomTags:
                    CustomTags = other.CustomTags;
                    break;
                case MaterialField.Friction:
                    Friction = other.Friction;
                    break;
                case MaterialField.Restitution:
                    Restitution = other.Restitution;
                    break;
                case MaterialField.CollisionResponsePolicy:
                    CollisionResponse = other.CollisionResponse;
                    break;
                case MaterialField.All:
                    this = other;
                    break;
                default:
                    SafetyChecks.ThrowInvalidOperationException("Invalid option.");
                    break;
            }
        }

        /// <summary>   Tests if this Material is considered equal to another. </summary>
        ///
        /// <param name="other">    The material to compare to this object. </param>
        ///
        /// <returns>   True if the objects are considered equal, false if they are not. </returns>
        public bool Equals(Material other)
        {
            return
                Flags == other.Flags &&
                FrictionCombinePolicy == other.FrictionCombinePolicy &&
                RestitutionCombinePolicy == other.RestitutionCombinePolicy &&
                CustomTags == other.CustomTags &&
                Friction == other.Friction &&
                Restitution == other.Restitution;
        }

        /// <summary>   Calculates a hash code for this object. </summary>
        ///
        /// <returns>   A hash code for this object. </returns>
        public override int GetHashCode()
        {
            return unchecked((int)math.hash(new uint2(
                unchecked((uint)(
                    (byte)Flags
                    | ((byte)FrictionCombinePolicy << 4)
                    | ((byte)RestitutionCombinePolicy << 8)
                    | (CustomTags << 12))
                ),
                math.hash(new float2(Friction, Restitution))
            )));
        }
    }
}
