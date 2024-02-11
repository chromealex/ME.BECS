using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Unity.Physics
{
    /// <summary>   Values that represent jacobian types. </summary>
    public enum JacobianType : byte
    {
        // Contact Jacobians
        /// <summary>   An enum constant representing the contact jacobian. </summary>
        Contact,
        /// <summary>   An enum constant representing the trigger jacobian. </summary>
        Trigger,

        // Joint Jacobians
        /// <summary>   An enum constant representing the linear limit joint jacobian. </summary>
        LinearLimit,
        /// <summary>   An enum constant representing the angular limit 1 d joint jacobian. </summary>
        AngularLimit1D,
        /// <summary>   An enum constant representing the angular limit 2D joint jacobian. </summary>
        AngularLimit2D,
        /// <summary>   An enum constant representing the angular limit 3D joint jacobian. </summary>
        AngularLimit3D,

        // Motor Jacobians
        /// <summary>   An enum constant representing the rotation motor jacobian. </summary>
        RotationMotor,
        /// <summary>   An enum constant representing the angular velocity motor jacobian. </summary>
        AngularVelocityMotor,
        /// <summary>   An enum constant representing the position motor jacobian. </summary>
        PositionMotor,
        /// <summary>   An enum constant representing the linear velocity motor jacobian. </summary>
        LinearVelocityMotor,
    }

    /// <summary>   Flags which enable optional Jacobian behaviors. </summary>
    [Flags]
    public enum JacobianFlags : byte
    {
        // These flags apply to all Jacobians
        /// <summary>   A binary constant representing the disabled flag. Applies to all jacobians. </summary>
        Disabled = 1 << 0,
        /// <summary>   A binary constant representing the enable mass factors flag. Applies to all jacobians.</summary>
        EnableMassFactors = 1 << 1,
        /// <summary>   A binary constant representing the user flag 0 flag. Applies to all jacobians. </summary>
        UserFlag0 = 1 << 2,
        /// <summary>   A binary constant representing the user flag 1 flag. Applies to all jacobians. </summary>
        UserFlag1 = 1 << 3,
        /// <summary>   A binary constant representing the user flag 2 flag. Applies to all jacobians. </summary>
        UserFlag2 = 1 << 4,

        // These flags apply only to contact Jacobians
        /// <summary>   A binary constant representing the is trigger flag. Apples only to contact jacobian. </summary>
        IsTrigger = 1 << 5,
        /// <summary>   A binary constant representing the enable collision events flag. Apples only to contact jacobian. </summary>
        EnableCollisionEvents = 1 << 6,
        /// <summary>   A binary constant representing the enable surface velocity flag. Apples only to contact jacobian. </summary>
        EnableSurfaceVelocity = 1 << 7,

        // Applies only to joint Jacobians
        /// <summary>   A binary constant representing the enable impulse events options. Apples only to joint jacobian. </summary>
        EnableImpulseEvents = 1 << 5
    }

    // Jacobian header, first part of each Jacobian in the stream
    struct JacobianHeader
    {
        public BodyIndexPair BodyPair { get; internal set; }
        public JacobianType Type { get; internal set; }
        public JacobianFlags Flags { get; internal set; }

        // Whether the Jacobian should be solved or not
        public bool Enabled
        {
            get => ((Flags & JacobianFlags.Disabled) == 0);
            set => Flags = value ? (Flags & ~JacobianFlags.Disabled) : (Flags | JacobianFlags.Disabled);
        }

        // Whether the Jacobian contains manifold data for collision events or not
        public bool HasContactManifold => (Flags & JacobianFlags.EnableCollisionEvents) != 0;

        // Collider keys for the collision events
        public ColliderKeyPair ColliderKeys
        {
            get => HasContactManifold? AccessColliderKeys() : ColliderKeyPair.Empty;
            set
            {
                if (HasContactManifold)
                    AccessColliderKeys() = value;
                else
                    SafetyChecks.ThrowNotSupportedException("Jacobian does not have collision events enabled");
            }
        }

        // Overrides for the mass properties of the pair of bodies
        public bool HasMassFactors => (Flags & JacobianFlags.EnableMassFactors) != 0;
        public MassFactors MassFactors
        {
            get => HasMassFactors? AccessMassFactors() : MassFactors.Default;
            set
            {
                if (HasMassFactors)
                    AccessMassFactors() = value;
                else
                    SafetyChecks.ThrowNotSupportedException("Jacobian does not have mass factors enabled");
            }
        }

        // The surface velocity to apply to contact points
        public bool HasSurfaceVelocity => (Flags & JacobianFlags.EnableSurfaceVelocity) != 0;
        public SurfaceVelocity SurfaceVelocity
        {
            get => HasSurfaceVelocity? AccessSurfaceVelocity() : new SurfaceVelocity();
            set
            {
                if (HasSurfaceVelocity)
                    AccessSurfaceVelocity() = value;
                else
                    SafetyChecks.ThrowNotSupportedException("Jacobian does not have surface velocity enabled");
            }
        }

        // Solve the Jacobian
        public void Solve([NoAlias] ref MotionVelocity velocityA, [NoAlias] ref MotionVelocity velocityB, Solver.StepInput stepInput,
            [NoAlias] ref NativeStream.Writer collisionEventsWriter, [NoAlias] ref NativeStream.Writer triggerEventsWriter,
            [NoAlias] ref NativeStream.Writer impulseEventsWriter, bool enableFrictionVelocitiesHeuristic,
            Solver.MotionStabilizationInput motionStabilizationSolverInputA, Solver.MotionStabilizationInput motionStabilizationSolverInputB)
        {
            if (Enabled)
            {
                switch (Type)
                {
                    case JacobianType.Contact:
                        AccessBaseJacobian<ContactJacobian>().Solve(ref this, ref velocityA, ref velocityB, stepInput, ref collisionEventsWriter,
                            enableFrictionVelocitiesHeuristic, motionStabilizationSolverInputA, motionStabilizationSolverInputB);
                        break;
                    case JacobianType.Trigger:
                        AccessBaseJacobian<TriggerJacobian>().Solve(ref this, ref velocityA, ref velocityB, stepInput, ref triggerEventsWriter);
                        break;
                    case JacobianType.LinearLimit:
                        AccessBaseJacobian<LinearLimitJacobian>().Solve(ref this, ref velocityA, ref velocityB, stepInput, ref impulseEventsWriter);
                        break;
                    case JacobianType.AngularLimit1D:
                        AccessBaseJacobian<AngularLimit1DJacobian>().Solve(ref this, ref velocityA, ref velocityB, stepInput, ref impulseEventsWriter);
                        break;
                    case JacobianType.AngularLimit2D:
                        AccessBaseJacobian<AngularLimit2DJacobian>().Solve(ref this, ref velocityA, ref velocityB, stepInput, ref impulseEventsWriter);
                        break;
                    case JacobianType.AngularLimit3D:
                        AccessBaseJacobian<AngularLimit3DJacobian>().Solve(ref this, ref velocityA, ref velocityB, stepInput, ref impulseEventsWriter);
                        break;
                    case JacobianType.RotationMotor:
                        AccessBaseJacobian<RotationMotorJacobian>().Solve(ref velocityA, ref velocityB, stepInput);
                        break;
                    case JacobianType.AngularVelocityMotor:
                        AccessBaseJacobian<AngularVelocityMotorJacobian>().Solve(ref velocityA, ref velocityB, stepInput);
                        break;
                    case JacobianType.PositionMotor:
                        AccessBaseJacobian<PositionMotorJacobian>().Solve(ref velocityA, ref velocityB, stepInput);
                        break;
                    case JacobianType.LinearVelocityMotor:
                        AccessBaseJacobian<LinearVelocityMotorJacobian>().Solve(ref velocityA, ref velocityB, stepInput);
                        break;
                    default:
                        SafetyChecks.ThrowNotImplementedException();
                        return;
                }
            }
        }

        #region Helpers

        public static bool IsNonMotorizedConstraint(JacobianType type)
        {
            return (type & (JacobianType.LinearLimit | JacobianType.AngularLimit1D | JacobianType.AngularLimit2D | JacobianType.AngularLimit3D)) != 0;
        }

        public static int CalculateSize(JacobianType type, JacobianFlags flags, int numContactPoints = 0)
        {
            return UnsafeUtility.SizeOf<JacobianHeader>() +
                SizeOfBaseJacobian(type) + SizeOfModifierData(type, flags) +
                numContactPoints * UnsafeUtility.SizeOf<ContactJacAngAndVelToReachCp>() +
                SizeOfContactPointData(type, flags, numContactPoints);
        }

        private static int SizeOfColliderKeys(JacobianType type, JacobianFlags flags)
        {
            return (type == JacobianType.Contact && (flags & JacobianFlags.EnableCollisionEvents) != 0) ?
                UnsafeUtility.SizeOf<ColliderKeyPair>() : 0;
        }

        private static int SizeOfEntityPair(JacobianType type, JacobianFlags flags)
        {
            return (type == JacobianType.Contact && (flags & JacobianFlags.EnableCollisionEvents) != 0) ?
                UnsafeUtility.SizeOf<EntityPair>() : 0;
        }

        private static int SizeOfSurfaceVelocity(JacobianType type, JacobianFlags flags)
        {
            return (type == JacobianType.Contact && (flags & JacobianFlags.EnableSurfaceVelocity) != 0) ?
                UnsafeUtility.SizeOf<SurfaceVelocity>() : 0;
        }

        private static int SizeOfMassFactors(JacobianType type, JacobianFlags flags)
        {
            return (type == JacobianType.Contact && (flags & JacobianFlags.EnableMassFactors) != 0) ?
                UnsafeUtility.SizeOf<MassFactors>() : 0;
        }

        private static int SizeOfModifierData(JacobianType type, JacobianFlags flags)
        {
            return SizeOfColliderKeys(type, flags) + SizeOfEntityPair(type, flags) + SizeOfSurfaceVelocity(type, flags) +
                SizeOfMassFactors(type, flags) + SizeOfImpulseEventSolverData(type, flags);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int SizeOfImpulseEventSolverData(JacobianType type, JacobianFlags flags)
        {
            return (IsNonMotorizedConstraint(type) && (flags & JacobianFlags.EnableImpulseEvents) != 0) ?
                UnsafeUtility.SizeOf<ImpulseEventSolverData>() : 0;
        }

        private static int SizeOfContactPointData(JacobianType type, JacobianFlags flags, int numContactPoints = 0)
        {
            return (type == JacobianType.Contact && (flags & JacobianFlags.EnableCollisionEvents) != 0) ?
                numContactPoints * UnsafeUtility.SizeOf<ContactPoint>() : 0;
        }

        private static int SizeOfBaseJacobian(JacobianType type)
        {
            switch (type)
            {
                case JacobianType.Contact:
                    return UnsafeUtility.SizeOf<ContactJacobian>();
                case JacobianType.Trigger:
                    return UnsafeUtility.SizeOf<TriggerJacobian>();
                case JacobianType.LinearLimit:
                    return UnsafeUtility.SizeOf<LinearLimitJacobian>();
                case JacobianType.AngularLimit1D:
                    return UnsafeUtility.SizeOf<AngularLimit1DJacobian>();
                case JacobianType.AngularLimit2D:
                    return UnsafeUtility.SizeOf<AngularLimit2DJacobian>();
                case JacobianType.AngularLimit3D:
                    return UnsafeUtility.SizeOf<AngularLimit3DJacobian>();
                case JacobianType.RotationMotor:
                    return UnsafeUtility.SizeOf<RotationMotorJacobian>();
                case JacobianType.AngularVelocityMotor:
                    return UnsafeUtility.SizeOf<AngularVelocityMotorJacobian>();
                case JacobianType.PositionMotor:
                    return UnsafeUtility.SizeOf<PositionMotorJacobian>();
                case JacobianType.LinearVelocityMotor:
                    return UnsafeUtility.SizeOf<LinearVelocityMotorJacobian>();
                default:
                    SafetyChecks.ThrowNotImplementedException();
                    return default;
            }
        }

        // Access to "base" jacobian - a jacobian that comes after the header
        public unsafe ref T AccessBaseJacobian<T>() where T : struct
        {
            byte* ptr = (byte*)UnsafeUtility.AddressOf(ref this);
            ptr += UnsafeUtility.SizeOf<JacobianHeader>();
            return ref UnsafeUtility.AsRef<T>(ptr);
        }

        public unsafe ref ColliderKeyPair AccessColliderKeys()
        {
            Assert.IsTrue((Flags & JacobianFlags.EnableCollisionEvents) != 0);
            byte* ptr = (byte*)UnsafeUtility.AddressOf(ref this);
            ptr += UnsafeUtility.SizeOf<JacobianHeader>() + SizeOfBaseJacobian(Type);
            return ref UnsafeUtility.AsRef<ColliderKeyPair>(ptr);
        }

        public unsafe ref EntityPair AccessEntities()
        {
            Assert.IsTrue((Flags & JacobianFlags.EnableCollisionEvents) != 0);
            byte* ptr = (byte*)UnsafeUtility.AddressOf(ref this);
            ptr += UnsafeUtility.SizeOf<JacobianHeader>() + SizeOfBaseJacobian(Type) + SizeOfColliderKeys(Type, Flags);
            return ref UnsafeUtility.AsRef<EntityPair>(ptr);
        }

        public unsafe ref SurfaceVelocity AccessSurfaceVelocity()
        {
            Assert.IsTrue((Flags & JacobianFlags.EnableSurfaceVelocity) != 0);
            byte* ptr = (byte*)UnsafeUtility.AddressOf(ref this);
            ptr += UnsafeUtility.SizeOf<JacobianHeader>() + SizeOfBaseJacobian(Type) +
                SizeOfColliderKeys(Type, Flags) + SizeOfEntityPair(Type, Flags);
            return ref UnsafeUtility.AsRef<SurfaceVelocity>(ptr);
        }

        public unsafe ref MassFactors AccessMassFactors()
        {
            Assert.IsTrue((Flags & JacobianFlags.EnableMassFactors) != 0);
            byte* ptr = (byte*)UnsafeUtility.AddressOf(ref this);
            ptr += UnsafeUtility.SizeOf<JacobianHeader>() + SizeOfBaseJacobian(Type) +
                SizeOfColliderKeys(Type, Flags) + SizeOfEntityPair(Type, Flags) + SizeOfSurfaceVelocity(Type, Flags);
            return ref UnsafeUtility.AsRef<MassFactors>(ptr);
        }

        public unsafe ref ContactJacAngAndVelToReachCp AccessAngularJacobian(int pointIndex)
        {
            Assert.IsTrue(Type == JacobianType.Contact || Type == JacobianType.Trigger);
            byte* ptr = (byte*)UnsafeUtility.AddressOf(ref this);
            ptr += UnsafeUtility.SizeOf<JacobianHeader>() + SizeOfBaseJacobian(Type) + SizeOfModifierData(Type, Flags) +
                pointIndex * UnsafeUtility.SizeOf<ContactJacAngAndVelToReachCp>();
            return ref UnsafeUtility.AsRef<ContactJacAngAndVelToReachCp>(ptr);
        }

        public unsafe ref ContactPoint AccessContactPoint(int pointIndex)
        {
            Assert.IsTrue(Type == JacobianType.Contact);

            var baseJac = AccessBaseJacobian<ContactJacobian>();
            byte* ptr = (byte*)UnsafeUtility.AddressOf(ref this);
            ptr += UnsafeUtility.SizeOf<JacobianHeader>() + SizeOfBaseJacobian(Type) + SizeOfModifierData(Type, Flags) +
                baseJac.BaseJacobian.NumContacts * UnsafeUtility.SizeOf<ContactJacAngAndVelToReachCp>() +
                pointIndex * UnsafeUtility.SizeOf<ContactPoint>();
            return ref UnsafeUtility.AsRef<ContactPoint>(ptr);
        }

        public unsafe ref ImpulseEventSolverData AccessImpulseEventSolverData()
        {
            Assert.IsTrue(IsNonMotorizedConstraint(Type) && ((Flags & JacobianFlags.EnableImpulseEvents) != 0));

            byte* ptr = (byte*)UnsafeUtility.AddressOf(ref this);
            ptr += UnsafeUtility.SizeOf<JacobianHeader>() + SizeOfBaseJacobian(Type);
            return ref UnsafeUtility.AsRef<ImpulseEventSolverData>(ptr);
        }

        #endregion
    }

    // Helper functions for working with Jacobians
    static class JacobianUtilities
    {
        // This is the inverse function to CalculateConstraintTauAndDamping
        // Given a final Tau and Damping you can get the original Spring Frequency and Damping Ratio.
        // See Unity.Physics.Constraint struct for discussion about default Spring Frequency and Damping Ratio.
        public static void CalculateSpringFrequencyAndDamping(float constraintTau, float constraintDamping,
            float timeStep, int iterations, out float springFrequency, out float dampingRatio)
        {
            int n = iterations;
            float h = timeStep;
            float hh = h * h;
            float a = 1.0f - constraintDamping;
            float aSum = 1.0f;
            for (int i = 1; i < n; i++)
            {
                aSum += math.pow(a, i);
            }

            float w = math.sqrt(constraintTau * aSum / math.pow(a, n)) / h;
            float ww = w * w;
            springFrequency = w / (2.0f * math.PI);
            dampingRatio = (math.pow(a, -n) - 1 - hh * ww) / (2.0f * h * w);
        }

        // This is the inverse function to CalculateSpringFrequencyAndDamping
        public static void CalculateConstraintTauAndDamping(float springFrequency, float dampingRatio, float timeStep,
            int iterations, out float constraintTau, out float constraintDamping)
        {
            /*
            In the following we derive the formulas for converting spring frequency and damping ratio to the solver constraint regularization parameters tau and damping,
            representing a normalized stiffness factor and damping factor, respectively.
            To this end, we compare the integration of spring-damper using implicit Euler integration with the time stepping formula for the constraint solver, and make both equivalent.

            1.  Implicit Euler integration of a spring-damper

                Constitutive equation of a spring-damper:
                    F = -kx - cx'
                with k = spring stiffness, c = damping coefficient, x = position, and x' = velocity.

                Backwards euler of the equations of motion a = x'' and v = x' with a = F/m where h = step length:

                    x2 = x1 + hv2
                    v2 = v1 + hx''
                       = v1 + hF/m
                       = v1 + h(-kx2 - cv2)/m
                       = v1 + h(-kx1 - hkv2 - cv2)/m
                       = 1 / (1 + h^2k/m + hc/m) * v1 - hk / (m + h^2k + hc) * x1

            2.  Gauss-Seidel iterations of a stiff constraint with Baumgarte stabilization parameters t and a, where
                t = tau, d = damping, and a = 1 - d.

                Example for four iterations:

                    v2 = av1 - (t / h)x1
                    v3 = av2 - (t / h)x1
                    v4 = av3 - (t / h)x1
                    v5 = av4 - (t / h)x1
                       = a^4v1 - (a^3 + a^2 + a + 1)(t / h)x1

                Given the recursive nature of the relationship above we can derive a closed-form expression for the new velocity with n iterations:
                    v_n = a * v_n-1 - (t / h) * x1
                        = a^n * v1 - (a^(n-1) + a^(n-2) + ... + a + 1)(t / h) * x1
                        = a^n * v1 - (\sum_{i=0}^{n-1} a^i)(t / h) * x1
                        = a^n * v1 - ((1 - a^n) / (1 - a))(t / h) * x1                      (1)

                Note that above we replaced the geometric series from 1 to n-1 with the closed form expression (1 - a^n) / (1 - a). This is valid for
                a != 1.0. If a == 1.0, the following closed form expression needs to be used instead:

                  \sum_{i=0}^{n-1} a^i) = n

                In this case, the equation above simplifies to:

                v_n = a^n * v1 - (\sum_{i=0}^{n-1} a^i)(t / h) * x1
                    = a^n * v1 - n(t / h) * x1

                For now we will ignore this special case. We will see if a can become 1 and under which conditions, once we have found an expression for a in the following step.

            3.1 Via coefficient matching, we can map the stiffness and damping parameters in the spring-damper to the tau and damping parameters in the stiff constraint.
                For n iterations, we have the following equations:

                    a^n = 1 / (1 + h^2k / m + hc / m), and                                  (2)
                    ((1 - a^n) / (1 - a))(t / h) = hk / (m + h^2k + hc)                     (3)

                where k is the spring constant, c is the damping constant, m is the mass, h is the time step, and a and t are the
                damping and tau parameters of the stiff constraint, respectively.

                We can solve (2) and (3) for a and t in terms of k, c, m, and h as follows.

                First, solve equation (2) for a:

                    a = (1 / (1 + h^2k / m + hc / m))^(1/n)                                 (4)
                <=> d = 1 - a
                      = 1 - (1 / (1 + h^2k / m + hc / m))^(1/n)                             (5)

                Then plug a into equation (3) to solve for t:

                         ((1 - a^n) / (1 - a))(t / h) = hk / (m + h^2k + hc)
                    <=>  ((1 - 1 / (1 + h^2k / m + hc / m)) / (1 - a))(t / h) = hk / (m + h^2k + hc)
                    <=>  ((1 - 1 / (1 + h^2k / m + hc / m)) / (1 - (1 / (1 + h^2k / m + hc / m))^(1/n)))(t / h) = hk / (m + h^2k + hc)
                    <=> t = h^2k / (m + h^2k + hc) * (1 - (1 / (1 + h^2k / m + hc / m))^(1/n)) / ((1 - 1 / (1 + h^2k / m + hc / m))

               We can simplify this further as follows:

                    t = h^2k / (m + h^2k + hc) * (1 - (1 / (1 + h^2k / m + hc / m))^(1/n)) * (1 + h^2k / m + hc / m) / ((1 + h^2k / m + hc / m) - 1)
                    t = h^2k / (m + h^2k + hc) * (1 - (1 / (1 + h^2k / m + hc / m))^(1/n)) * (1 + h^2k / m + hc / m) / (h^2k / m + hc / m)
                    t = h^2k / (m + h^2k + hc) * (1 - (1 / (1 + h^2k / m + hc / m))^(1/n)) * (m + h^2k + hc) / (h^2k + hc)
                    t = h^2k / (h^2k + hc) * (1 - (1 / (1 + h^2k / m + hc / m))^(1/n)) * (m + h^2k + hc) / (m + h^2k + hc)
                    t = h^2k / (h^2k + hc) * (1 - (1 / (1 + h^2k / m + hc / m))^(1/n))

               This yields the final expression for t:

                    t = h^2k / (h^2k + hc) * (1 - (1 / (1 + h^2k / m + hc / m))^(1/n))
                      = h^2k / (h^2k + hc) * d                                              (6)

            3.2 Coming back to our requirement from above that a != 1, let's examine in what situation a can become 1:

                    a = (1 / (1 + h^2k / m + hc / m))^(1/n) = 1

                We can see that a can only be 1 iff (if and only if) the term h^2k / m + hc / m equals 0.

                Given that k and c are both positive values and both m and h are strictly positive, this can only be the case if both k and h are 0, in which case our spring-damper
                will simply not apply any force, meaning, the constraint will not be active. We can deal with this case by simply setting the constraint regularization parameters
                t and d (= 1 - a) to 0 in this case. This will result in the constraint being inactive, which is what we want.

            3.4 Parametrization using Spring Frequency and Damping Ratio:

                Given spring frequency f, damping ratio z and effective mass m, we have the following relationships:
                    w = f * 2 * pi
                    k = m * w^2 <=> k/m = w^2
                    c = z * 2 * w * m <=> c/m = z * 2 * w
                where w denotes the angular spring frequency, k denotes the spring stiffness coefficient and c denotes the damping coefficient.

                We can use the relationships above to convert the expressions (5) and (6) for d and t to the following expressions in terms of spring frequency and damping ratio:
                    d = 1 - (1 / (1 + h^2 * m * w^2 / m + h * z * 2 * w * m / m))^(1/n)
                      = 1 - (1 / (1 + h^2 * w^2 + h * z * 2 * w))^(1/n)                     (7)

                In (6), substitute k for m * w^2 and c for z * 2 * w * m to get:
                    t = h^2k / (h^2k + hc) * d
                      = h^2 * m * w^2 / (h^2 * m * w^2 + h * z * 2 * w * m) * d

                Eliminate m to obtain the final expression:
                    t = h^2 * w^2 / (h^2 * w^2 + h * z * 2 * w) * d                         (8)

                This allows us to parametrize our constraint using the spring frequency and damping ratio of an equivalent spring-damper system.
            */

            // Compute damping factor d from spring frequency f, damping ratio z, time step h, and number of iterations n using equation (7) above.
            // With d in hand, compute stiffness factor tau from spring frequency f, damping coefficient c, time step h, number of iterations n and damping factor d using equation (8) above.

            // f: spring frequency, w: angular spring frequency, z: damping ratio
            float f = springFrequency;
            float z = dampingRatio;
            float h = timeStep;
            float w = f * 2 * math.PI; // convert frequency to angular frequency, i.e., oscillations/sec to radians/sec
            float hw = h * w;
            float hhww = hw * hw; // = h^2 * w^2
            float denom = hhww + hw * z * 2;
            float exp1 = 1 / (1 + denom);
            float exp2 = math.pow(exp1, 1f / iterations);

            constraintDamping = 1 - exp2;
            constraintTau = hhww / denom * constraintDamping;
        }

        // Returns x - clamp(x, min, max)
        public static float CalculateError(float x, float min, float max)
        {
            float error = math.max(x - max, 0.0f);
            error = math.min(x - min, error);
            return error;
        }

        // Returns the amount of error for the solver to correct, where initialError is the pre-integration error and predictedError is the expected post-integration error
        public static float CalculateCorrection(float predictedError, float initialError, float tau, float damping)
        {
            return (predictedError - initialError) * damping + initialError * tau;
        }

        // Integrate the relative orientation of a pair of bodies, faster and less memory than storing both bodies' orientations and integrating them separately
        public static quaternion IntegrateOrientationBFromA(quaternion bFromA, float3 angularVelocityA,
            float3 angularVelocityB, float timestep)
        {
            quaternion dqA = Integrator.IntegrateAngularVelocity(angularVelocityA, timestep);
            quaternion dqB = Integrator.IntegrateAngularVelocity(angularVelocityB, timestep);
            return math.normalize(math.mul(math.mul(math.inverse(dqB), bFromA), dqA));
        }

        // Calculate the inverse effective mass of a linear jacobian
        public static float CalculateInvEffectiveMassDiag(
            float3 angA, float3 invInertiaA, float invMassA,
            float3 angB, float3 invInertiaB, float invMassB)
        {
            float3 angularPart = angA * angA * invInertiaA + angB * angB * invInertiaB;
            float linearPart = invMassA + invMassB;
            return (angularPart.x + angularPart.y) + (angularPart.z + linearPart);
        }

        // Calculate the inverse effective mass for a pair of jacobians with perpendicular linear parts
        public static float CalculateInvEffectiveMassOffDiag(
            float3 angA0, float3 angA1, float3 invInertiaA,
            float3 angB0, float3 angB1, float3 invInertiaB)
        {
            return math.csum(angA0 * angA1 * invInertiaA + angB0 * angB1 * invInertiaB);
        }

        // Inverts a symmetric 3x3 matrix with diag = (0, 0), (1, 1), (2, 2), offDiag = (0, 1), (0, 2), (1, 2) = (1, 0), (2, 0), (2, 1)
        public static bool InvertSymmetricMatrix(float3 diag, float3 offDiag, out float3 invDiag, out float3 invOffDiag)
        {
            float3 offDiagSq = offDiag.zyx * offDiag.zyx;
            float determinant = (Math.HorizontalMul(diag) + 2.0f * Math.HorizontalMul(offDiag) - math.csum(offDiagSq * diag));
            bool determinantOk = (determinant != 0);
            float invDeterminant = math.select(0.0f, 1.0f / determinant, determinantOk);
            invDiag = (diag.yxx * diag.zzy - offDiagSq) * invDeterminant;
            invOffDiag = (offDiag.yxx * offDiag.zzy - diag.zyx * offDiag) * invDeterminant;
            return determinantOk;
        }

        // Builds a symmetric 3x3 matrix from diag = (0, 0), (1, 1), (2, 2), offDiag = (0, 1), (0, 2), (1, 2) = (1, 0), (2, 0), (2, 1)
        public static float3x3 BuildSymmetricMatrix(float3 diag, float3 offDiag)
        {
            return new float3x3(
                new float3(diag.x, offDiag.x, offDiag.y),
                new float3(offDiag.x, diag.y, offDiag.z),
                new float3(offDiag.y, offDiag.z, diag.z)
            );
        }

        /// <summary>
        /// Compute how much of an impulse can be applied, based on the impulses accumulated over several iterations, to
        /// a motor based on some threshold. This method can be used for positive or negative checks. For 1D corrections.
        /// </summary>
        /// <param name="impulse"> The current calculated impulse that can be applied </param>
        /// <param name="accumulatedImpulse"> The impulse that has been accumulated so far. This is passed by reference
        /// and as an input argument does not include the current impulse. On completion, this will be the accumulated
        /// impulse. This impulse will not exceed the define maximum impulse threshold. </param>
        /// <param name="maxImpulseOfMotor"> A magnitude representing the maximum accumulated impulse that can be
        /// applied. Value should not be negative </param>
        /// <returns>  The impulse to be applied by a motor. </returns>
        internal static float CapImpulse(float impulse, ref float accumulatedImpulse, float maxImpulseOfMotor)
        {
            SafetyChecks.CheckWithinThresholdAndThrow(accumulatedImpulse, maxImpulseOfMotor, "Accumulated Impulse");

            float newAccImpulse = accumulatedImpulse + impulse;
            if (newAccImpulse < -maxImpulseOfMotor)
            {
                // we want an impulse with which we have -maxImpulse = accumulatedImpulse + impulse
                impulse = -maxImpulseOfMotor - accumulatedImpulse;
                accumulatedImpulse = -maxImpulseOfMotor;
            }
            else if (newAccImpulse > maxImpulseOfMotor)
            {
                // we want an impulse with which we have maxImpulse = accumulatedImpulse + impulse
                impulse = maxImpulseOfMotor - accumulatedImpulse;
                accumulatedImpulse = maxImpulseOfMotor;
            }
            else
            {
                // impulse is within range
                accumulatedImpulse += impulse;
            }

            return impulse;
        }

        /// <summary>
        /// Compute how much of an impulse can be applied, based on the impulses accumulated over several iterations, to
        /// a motor based on some threshold. This method can be used for positive or negative checks. For 3D corrections.
        /// </summary>
        /// <param name="impulse"> The current calculated impulse that can be applied </param>
        /// <param name="accumulatedImpulse"> A parameter passed by reference. On input it is the impulse that has been
        /// accumulated so far. This value does not include the current impulse. On completion, it is the accumulated
        /// impulse. This impulse will not exceed the define maximum impulse threshold. </param>
        /// <param name="maxImpulseOfMotor"> A magnitude representing the maximum accumulated impulse that can be
        /// applied. Value should not be negative </param>
        /// <returns>  The impulse to be applied by a motor. </returns>
        internal static float3 CapImpulse(float3 impulse, ref float3 accumulatedImpulse, float maxImpulseOfMotor)
        {
            // Test Case A: Adding impulse to accumulation does not exceed threshold
            if (math.length(accumulatedImpulse + impulse) < maxImpulseOfMotor)
            {
                accumulatedImpulse += impulse;
                return impulse;
            }

            // Test Case D: Accumulation has reached threshold already
            if (maxImpulseOfMotor - math.length(accumulatedImpulse) <= math.EPSILON)
            {
                accumulatedImpulse = maxImpulseOfMotor * math.normalizesafe(accumulatedImpulse); //set to max instead of input > corrective in case given wrong accumulation
                return 0.0f;
            }

            // Cases when threshold is exceeded:
            // Test Case C: No impulses accumulated and the impulse is larger than threshold:
            if ((math.length(accumulatedImpulse) < math.EPSILON) && (math.length(impulse) - maxImpulseOfMotor > math.EPSILON))
            {
                impulse = maxImpulseOfMotor * math.normalizesafe(impulse); //cap impulse at threshold
            }
            else // Test Case B: accumulation + impulse will push over threshold, so apply the remaining impulse balance
            {
                impulse = (maxImpulseOfMotor * math.normalizesafe(accumulatedImpulse)) - accumulatedImpulse;
            }

            accumulatedImpulse += impulse;

            return impulse;
        }

        // isAxisInA should match how axisInA/axisInB is used in the Motor Baking
        // if the authoring axis is an axis relative to bodyA then set isAxisInA = T
        // if the authoring axis is an axis relative to bodyB, then set isAxisInA = F
        internal static BodyFrame CalculateDefaultBodyFramesForConnectedBody(RigidTransform worldFromA, RigidTransform worldFromB,
            float3 positionBodyA, float3 axis, out BodyFrame jointFrameB, bool isAxisInA)
        {
            float3 positionBodyB, perpendicularAxisInA, perpendicularAxisInB;
            float3 axisInA, axisInB;

            RigidTransform bFromA = math.mul(math.inverse(worldFromB), worldFromA);

            if (isAxisInA) // during authoring, axis of motor is relative to bodyA, used for Angular Motors
            {
                axisInA = axis;
                axisInB = math.mul(bFromA.rot, axisInA); //motor axis in Connected Entity space
            }
            else // during authoring, axis of motor is specified relative to bodyB, used for Linear Motors
            {
                RigidTransform aFromB = math.mul(math.inverse(worldFromA), worldFromB);
                axisInA = math.mul(aFromB.rot, axis); //motor axis relative to bodyA
                axisInB = axis;  //motor axis in Connected Entity space
            }

            //position of motored body relative to Connected Entity in world space
            positionBodyB = math.transform(bFromA, positionBodyA);

            // Always calculate the perpendicular axes
            Math.CalculatePerpendicularNormalized(axisInA, out perpendicularAxisInA, out _);
            perpendicularAxisInB = math.mul(bFromA.rot, perpendicularAxisInA); //perp motor axis in Connected Entity space

            var jointFrameA = new BodyFrame
            {
                Axis = axisInA,
                PerpendicularAxis = perpendicularAxisInA,
                Position = positionBodyA
            };
            jointFrameB = new BodyFrame
            {
                Axis = axisInB,
                PerpendicularAxis = perpendicularAxisInB,
                Position = positionBodyB
            };

            return jointFrameA;
        }
    }

    // Iterator (and modifier) for jacobians
    unsafe struct JacobianIterator
    {
        NativeStream.Reader m_Reader;

        public JacobianIterator(NativeStream.Reader jacobianStreamReader, int workItemIndex)
        {
            m_Reader = jacobianStreamReader;
            m_Reader.BeginForEachIndex(workItemIndex);
        }

        public bool HasJacobiansLeft()
        {
            return m_Reader.RemainingItemCount > 0;
        }

        public ref JacobianHeader ReadJacobianHeader()
        {
            int readSize = Read<int>();
            return ref UnsafeUtility.AsRef<JacobianHeader>(Read(readSize));
        }

        private byte* Read(int size)
        {
            byte* dataPtr = m_Reader.ReadUnsafePtr(size);

            return dataPtr;
        }

        private ref T Read<T>() where T : struct
        {
            int size = UnsafeUtility.SizeOf<T>();
            return ref UnsafeUtility.AsRef<T>(Read(size));
        }
    }
}
