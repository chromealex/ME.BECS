using Unity.Burst;
using Unity.Mathematics;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    // Solve data for a constraint that rotates two bodies relative to each other
    // about a given axis and at a desired angular velocity
    [NoAlias]
    struct AngularVelocityMotorJacobian
    {
        // Rotation axis in motion A space
        public float3 AxisInMotionA;
        public float Target;   // in rad/s

        // Index of the limited axis
        public int AxisIndex;

        // Relative orientation of the motions before solving
        public quaternion MotionBFromA;

        // Maximum impulse that can be applied to the motor before it caps out (not a breaking impulse)
        public float MaxImpulseOfMotor;

        // Accumulated impulse applied over the number of solver iterations
        public float AccumulatedImpulse;

        // Fraction of the velocity error to correct per step
        public float Damping;

        // Build the Jacobian
        public void Build(
            MTransform aFromConstraint, MTransform bFromConstraint,
            MotionData motionA, MotionData motionB,
            Constraint constraint, float tau, float damping)
        {
            AxisIndex = constraint.ConstrainedAxis1D;
            AxisInMotionA = aFromConstraint.Rotation[AxisIndex];
            Target = constraint.Target[AxisIndex]; // scalar target angular velocity in rad/s
            Damping = damping;

            MaxImpulseOfMotor = math.abs(constraint.MaxImpulse.x); // using as magnitude, y and z components are unused
            AccumulatedImpulse = 0.0f;

            MotionBFromA = math.mul(math.inverse(motionB.WorldFromMotion.rot), motionA.WorldFromMotion.rot);
        }

        // Solve the Jacobian
        public void Solve(ref MotionVelocity velocityA, ref MotionVelocity velocityB, Solver.StepInput stepInput)
        {
            // Predict the relative orientation at the end of the step
            quaternion futureMotionBFromA = JacobianUtilities.IntegrateOrientationBFromA(MotionBFromA,
                velocityA.AngularVelocity, velocityB.AngularVelocity, stepInput.Timestep);

            // Calculate the effective mass
            float3 axisInMotionB = math.mul(futureMotionBFromA, AxisInMotionA);
            float effectiveMass;
            {
                float invEffectiveMass = math.csum(AxisInMotionA * AxisInMotionA * velocityA.InverseInertia +
                    axisInMotionB * axisInMotionB * velocityB.InverseInertia);
                effectiveMass = math.select(1.0f / invEffectiveMass, 0.0f, invEffectiveMass == 0.0f);
            }

            // Compute the current relative angular velocity between the two bodies about the rotation axis
            var relativeVelocity = math.dot(velocityA.AngularVelocity, AxisInMotionA) -
                math.dot(velocityB.AngularVelocity, axisInMotionB);

            // Compute the error between the target relative velocity and the current relative velocity
            float velocityError = Target - relativeVelocity;
            float velocityCorrection = velocityError * Damping;

            float impulse = math.mul(effectiveMass, velocityCorrection);
            impulse = JacobianUtilities.CapImpulse(impulse, ref AccumulatedImpulse, MaxImpulseOfMotor);

            velocityA.ApplyAngularImpulse(impulse * AxisInMotionA);
            velocityB.ApplyAngularImpulse(-impulse * axisInMotionB);
        }
    }
}
