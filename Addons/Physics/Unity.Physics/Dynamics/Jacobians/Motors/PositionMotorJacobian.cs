using Unity.Burst;
using Unity.Mathematics;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    // Solve data for a constraint that limits the linear distance between a pair of pivots in 1, 2, or 3 degrees of freedom
    [NoAlias]
    struct PositionMotorJacobian
    {
        // Pivot positions in motion space
        public float3 PivotAinA; //the anchor point of body with motor
        public float3 PivotBinB; //anchor position is shared: position of bodyB relative to bodyA

        // Target is a vector that represents the direction of movement relative to bodyB (axis) with a target magnitude
        public float3 TargetInB;

        // Motion transforms before solving
        public RigidTransform WorldFromA;
        public RigidTransform WorldFromB;

        // If the constraint limits 1 DOF, this is the constrained axis.
        // If the constraint limits 2 DOF, this is the free axis.
        // If the constraint limits 3 DOF, this is unused and set to float3.zero
        public float3 AxisInB;

        // Position error at the beginning of the step
        public float InitialError;

        // Fraction of the position error to correct per step
        public float Tau;

        // Fraction of the velocity error to correct per step
        public float Damping;

        // Maximum impulse that can be applied to the motor before it caps out (not a breaking impulse)
        public float MaxImpulseOfMotor;

        // Accumulated impulse applied over the number of solver iterations
        public float3 AccumulatedImpulsePerAxis;

        // Build the Jacobian
        public void Build(
            MTransform aFromConstraint, MTransform bFromConstraint,
            MotionData motionA, MotionData motionB,
            Constraint constraint, float tau, float damping)
        {
            // In World Space
            WorldFromA = motionA.WorldFromMotion;
            WorldFromB = motionB.WorldFromMotion;

            // In Constraint Space
            PivotAinA = aFromConstraint.Translation; //anchor of bodyA in bodyA space
            PivotBinB = bFromConstraint.Translation; //anchorA offset in bodyB space (anchor shared with bodyA)

            AxisInB = bFromConstraint.Rotation[constraint.ConstrainedAxis1D];

            // (constraint.target[axis of movement] = target) where direction is relative to bodyB. Therefore add this to the pivot of bodyB to anchor the target
            TargetInB = (AxisInB * constraint.Target[constraint.ConstrainedAxis1D]) + PivotBinB;

            Tau = tau;
            Damping = damping;

            MaxImpulseOfMotor = math.abs(constraint.MaxImpulse.x); //using as magnitude, y&z components are unused
            AccumulatedImpulsePerAxis = float3.zero;

            // Calculate the initial distance between bodyA and bodyB, in world-space
            InitialError = CalculateError(
                new MTransform(WorldFromA.rot, WorldFromA.pos),
                new MTransform(WorldFromB.rot, WorldFromB.pos),
                out float3 directionUnused);
        }

        private static void ApplyImpulse(in float3 impulse, in float3 ang0, in float3 ang1, in float3 ang2, ref MotionVelocity velocity)
        {
            velocity.ApplyLinearImpulse(impulse); // world space
            velocity.ApplyAngularImpulse(impulse.x * ang0 + impulse.y * ang1 + impulse.z * ang2); //motion space
        }

        // Solve the Jacobian
        // Predict error at the end of the step and calculate the impulse to correct it
        public void Solve(ref MotionVelocity velocityA, ref MotionVelocity velocityB, Solver.StepInput stepInput)
        {
            // Predict the motions' transforms at the end of the step
            MTransform futureWorldFromA;
            MTransform futureWorldFromB;
            {
                quaternion dqA = Integrator.IntegrateAngularVelocity(velocityA.AngularVelocity, stepInput.Timestep);
                quaternion dqB = Integrator.IntegrateAngularVelocity(velocityB.AngularVelocity, stepInput.Timestep);
                quaternion futureOrientationA = math.normalize(math.mul(WorldFromA.rot, dqA));
                quaternion futureOrientationB = math.normalize(math.mul(WorldFromB.rot, dqB));
                futureWorldFromA = new MTransform(futureOrientationA, WorldFromA.pos + velocityA.LinearVelocity * stepInput.Timestep);
                futureWorldFromB = new MTransform(futureOrientationB, WorldFromB.pos + velocityB.LinearVelocity * stepInput.Timestep);
            }

            // Calculate the angulars
            CalculateAngulars(PivotAinA, futureWorldFromA.Rotation, out float3 angA0, out float3 angA1, out float3 angA2);
            CalculateAngulars(PivotBinB, futureWorldFromB.Rotation, out float3 angB0, out float3 angB1, out float3 angB2);

            // Calculate effective mass
            float3 effectiveMassDiag, effectiveMassOffDiag;
            {
                // Calculate the inverse effective mass matrix
                float3 invEffectiveMassDiag = new float3(
                    JacobianUtilities.CalculateInvEffectiveMassDiag(
                        angA0, velocityA.InverseInertia, velocityA.InverseMass,
                        angB0, velocityB.InverseInertia, velocityB.InverseMass),
                    JacobianUtilities.CalculateInvEffectiveMassDiag(
                        angA1, velocityA.InverseInertia, velocityA.InverseMass,
                        angB1, velocityB.InverseInertia, velocityB.InverseMass),
                    JacobianUtilities.CalculateInvEffectiveMassDiag(
                        angA2, velocityA.InverseInertia, velocityA.InverseMass,
                        angB2, velocityB.InverseInertia, velocityB.InverseMass));

                float3 invEffectiveMassOffDiag = new float3(
                    JacobianUtilities.CalculateInvEffectiveMassOffDiag(
                        angA0, angA1, velocityA.InverseInertia,
                        angB0, angB1, velocityB.InverseInertia),
                    JacobianUtilities.CalculateInvEffectiveMassOffDiag(
                        angA0, angA2, velocityA.InverseInertia,
                        angB0, angB2, velocityB.InverseInertia),
                    JacobianUtilities.CalculateInvEffectiveMassOffDiag(
                        angA1, angA2, velocityA.InverseInertia,
                        angB1, angB2, velocityB.InverseInertia));

                // Invert to get the effective mass matrix
                JacobianUtilities.InvertSymmetricMatrix(
                    invEffectiveMassDiag, invEffectiveMassOffDiag,
                    out effectiveMassDiag, out effectiveMassOffDiag);
            }

            // Find the predicted direction and distance between bodyA and bodyB at the end of the step,
            // determine the difference between this and the initial difference, apply softening to this
            // impulse is what is required to move from the initial to the predicted (all in world space)
            float futureDistanceError = CalculateError(futureWorldFromA, futureWorldFromB, out float3 futureDirection);
            float solveDistanceError = JacobianUtilities.CalculateCorrection(futureDistanceError, InitialError, Tau, Damping); //units=m

            // Calculate the impulse to correct the error
            float3 solveError = solveDistanceError * futureDirection;
            float3x3 effectiveMass = JacobianUtilities.BuildSymmetricMatrix(effectiveMassDiag, effectiveMassOffDiag);
            float3 impulse = math.mul(effectiveMass, solveError) * stepInput.InvTimestep;
            impulse = JacobianUtilities.CapImpulse(impulse, ref AccumulatedImpulsePerAxis, MaxImpulseOfMotor);

            // Apply the impulse
            ApplyImpulse(impulse, angA0, angA1, angA2, ref velocityA);
            ApplyImpulse(-impulse, angB0, angB1, angB2, ref velocityB);
        }

        #region Helpers

        private static void CalculateAngulars(in float3 pivotInMotion, in float3x3 worldFromMotionRotation, out float3 ang0, out float3 ang1, out float3 ang2)
        {
            // Jacobian directions are i, j, k
            // Angulars are pivotInMotion x (motionFromWorld * direction)
            float3x3 motionFromWorldRotation = math.transpose(worldFromMotionRotation);
            ang0 = math.cross(pivotInMotion, motionFromWorldRotation.c0);
            ang1 = math.cross(pivotInMotion, motionFromWorldRotation.c1);
            ang2 = math.cross(pivotInMotion, motionFromWorldRotation.c2);
        }

        // Given two bodies (in world space), determine the direction (in world space) that the impulse needs to act on
        // and the distance remaining between the bodyA and the target
        private float CalculateError(in MTransform worldFromA, in MTransform worldFromB, out float3 directionInWorld)
        {
            float3 anchorAinWorld = Mul(worldFromA, PivotAinA);
            float3 targetInWorld = Mul(worldFromB, TargetInB);
            float3 axisInWorld = math.mul(worldFromB.Rotation, AxisInB);

            var toTargetOffset = targetInWorld - anchorAinWorld;
            float distance = -math.dot(toTargetOffset, axisInWorld);

            directionInWorld = -axisInWorld;

            return distance;
        }

        #endregion
    }
}
