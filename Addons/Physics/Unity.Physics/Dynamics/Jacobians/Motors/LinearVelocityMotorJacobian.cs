using Unity.Burst;
using Unity.Mathematics;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    // Solve data for a constraint that limits the linear distance between a pair of pivots in 1, 2, or 3 degrees of freedom
    [NoAlias]
    struct LinearVelocityMotorJacobian
    {
        // Pivot positions in motion space
        public float3 PivotAinA;
        public float3 PivotBinB;

        public float3 Target; //a vector of the motor axis with motor target

        // Pivot distance limits
        public float MinDistance;
        public float MaxDistance;

        // Motion transforms before solving
        public RigidTransform WorldFromA;
        public RigidTransform WorldFromB;

        // If the constraint limits 1 DOF, this is the constrained axis.
        // If the constraint limits 2 DOF, this is the free axis.
        // If the constraint limits 3 DOF, this is unused and set to float3.zero
        public float3 AxisInB;

        // True if the jacobian limits one degree of freedom
        public bool Is1D;

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
            WorldFromA = motionA.WorldFromMotion;
            WorldFromB = motionB.WorldFromMotion;

            // Motor drive is independent of bodyA rotation, which drives relative to orientation of bodyB
            PivotAinA = aFromConstraint.Translation;
            PivotBinB = bFromConstraint.Translation;

            AxisInB = bFromConstraint.Rotation[constraint.ConstrainedAxis1D];
            Target = AxisInB * constraint.Target[constraint.ConstrainedAxis1D];  // is velocity vector relative to bodyB, in m/s

            Is1D = true;
            MinDistance = constraint.Min;
            MaxDistance = constraint.Max;
            Tau = tau;
            Damping = damping;

            MaxImpulseOfMotor = math.abs(constraint.MaxImpulse.x); //using as magnitude, y&z components are unused
            AccumulatedImpulsePerAxis = float3.zero;
        }

        private static void ApplyImpulse(float3 impulse, float3 ang0, float3 ang1, float3 ang2, ref MotionVelocity velocity)
        {
            velocity.ApplyLinearImpulse(impulse);
            velocity.ApplyAngularImpulse(impulse.x * ang0 + impulse.y * ang1 + impulse.z * ang2);
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
            float3 EffectiveMassDiag, EffectiveMassOffDiag;
            {
                // Calculate the inverse effective mass matrix
                float3 invEffectiveMassDiag = new float3(
                    JacobianUtilities.CalculateInvEffectiveMassDiag(angA0, velocityA.InverseInertia, velocityA.InverseMass,
                        angB0, velocityB.InverseInertia, velocityB.InverseMass),
                    JacobianUtilities.CalculateInvEffectiveMassDiag(angA1, velocityA.InverseInertia, velocityA.InverseMass,
                        angB1, velocityB.InverseInertia, velocityB.InverseMass),
                    JacobianUtilities.CalculateInvEffectiveMassDiag(angA2, velocityA.InverseInertia, velocityA.InverseMass,
                        angB2, velocityB.InverseInertia, velocityB.InverseMass));

                float3 invEffectiveMassOffDiag = new float3(
                    JacobianUtilities.CalculateInvEffectiveMassOffDiag(angA0, angA1, velocityA.InverseInertia, angB0, angB1, velocityB.InverseInertia),
                    JacobianUtilities.CalculateInvEffectiveMassOffDiag(angA0, angA2, velocityA.InverseInertia, angB0, angB2, velocityB.InverseInertia),
                    JacobianUtilities.CalculateInvEffectiveMassOffDiag(angA1, angA2, velocityA.InverseInertia, angB1, angB2, velocityB.InverseInertia));

                // Invert to get the effective mass matrix
                JacobianUtilities.InvertSymmetricMatrix(invEffectiveMassDiag, invEffectiveMassOffDiag, out EffectiveMassDiag, out EffectiveMassOffDiag);
            }

            float3x3 effectiveMass = JacobianUtilities.BuildSymmetricMatrix(EffectiveMassDiag, EffectiveMassOffDiag);

            var targetFromOrientationB = math.mul(WorldFromB.rot, Target); // Target vector is shifted based on the orientation of body B
            float3 solveError = targetFromOrientationB - velocityA.LinearVelocity; //in world space, units: m/s

            float3 impulse = math.mul(effectiveMass, solveError);
            impulse = JacobianUtilities.CapImpulse(impulse, ref AccumulatedImpulsePerAxis, MaxImpulseOfMotor);

            // Apply the impulse
            ApplyImpulse(impulse, angA0, angA1, angA2, ref velocityA);
            ApplyImpulse(-impulse, angB0, angB1, angB2, ref velocityB);
        }

        #region Helpers

        private static void CalculateAngulars(float3 pivotInMotion, float3x3 worldFromMotionRotation, out float3 ang0, out float3 ang1, out float3 ang2)
        {
            // Jacobian directions are i, j, k
            // Angulars are pivotInMotion x (motionFromWorld * direction)
            float3x3 motionFromWorldRotation = math.transpose(worldFromMotionRotation);
            ang0 = math.cross(pivotInMotion, motionFromWorldRotation.c0);
            ang1 = math.cross(pivotInMotion, motionFromWorldRotation.c1);
            ang2 = math.cross(pivotInMotion, motionFromWorldRotation.c2);
        }

        #endregion
    }
}
