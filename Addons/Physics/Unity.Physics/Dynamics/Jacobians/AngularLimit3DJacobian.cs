using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    // Solve data for a constraint that limits three degrees of angular freedom
    [NoAlias]
    struct AngularLimit3DJacobian
    {
        // Relative angle limits
        public float MinAngle;
        public float MaxAngle;

        // Relative orientation of motions before solving
        public quaternion BFromA;

        // Angle is zero when BFromA = RefBFromA
        public quaternion RefBFromA;

        // Error before solving
        public float InitialError;

        // Fraction of the position error to correct per step
        public float Tau;

        // Fraction of the velocity error to correct per step
        public float Damping;

        // Build the Jacobian
        public void Build(
            MTransform aFromConstraint, MTransform bFromConstraint,
            MotionVelocity velocityA, MotionVelocity velocityB,
            MotionData motionA, MotionData motionB,
            Constraint constraint, float tau, float damping)
        {
            BFromA = math.mul(math.inverse(motionB.WorldFromMotion.rot), motionA.WorldFromMotion.rot);
            RefBFromA = new quaternion(math.mul(bFromConstraint.Rotation, aFromConstraint.InverseRotation));
            MinAngle = constraint.Min;
            MaxAngle = constraint.Max;
            Tau = tau;
            Damping = damping;

            quaternion jointOrientation = math.mul(math.inverse(RefBFromA), BFromA);
            float initialAngle = math.asin(math.length(jointOrientation.value.xyz)) * 2.0f;
            InitialError = JacobianUtilities.CalculateError(initialAngle, MinAngle, MaxAngle);
        }

        public void Solve(ref JacobianHeader jacHeader, ref MotionVelocity velocityA, ref MotionVelocity velocityB, Solver.StepInput stepInput,
            ref NativeStream.Writer impulseEventsWriter)
        {
            // Predict the relative orientation at the end of the step
            quaternion futureBFromA = JacobianUtilities.IntegrateOrientationBFromA(BFromA, velocityA.AngularVelocity, velocityB.AngularVelocity, stepInput.Timestep);

            // Find the future axis and angle of rotation between the free axes
            float3 jacA0, jacA1, jacA2, jacB0, jacB1, jacB2;
            quaternion jointOrientation;
            float3 effectiveMass; // first column of 3x3 effective mass matrix, don't need the others because only jac0 can have nonzero error
            float futureAngle;
            {
                // Calculate the relative rotation between joint spaces
                jointOrientation = math.mul(math.inverse(RefBFromA), futureBFromA);

                // Find the axis and angle of rotation
                jacA0 = jointOrientation.value.xyz;
                float sinHalfAngleSq = math.lengthsq(jacA0);
                float invSinHalfAngle = Math.RSqrtSafe(sinHalfAngleSq);
                float sinHalfAngle = sinHalfAngleSq * invSinHalfAngle;
                futureAngle = math.asin(sinHalfAngle) * 2.0f;

                // jacA0: triple-axis defined by rotation (jointOrientation).
                // jacA1: triple-axis perpendicular to jacA0
                // jacA2: triple-axis perpendicular to BOTH jacA0 AND jacA1
                //    None of these axes are axis-aligned (ie: to the x,y,z triple-axis)
                jacA0 = math.select(jacA0 * invSinHalfAngle, new float3(1, 0, 0), invSinHalfAngle == 0.0f);
                jacA0 = math.select(jacA0, -jacA0, jointOrientation.value.w < 0.0f);    // determines rotation direction
                Math.CalculatePerpendicularNormalized(jacA0, out jacA1, out jacA2);

                //jacB are the same axes but from Body B's reference frame (ie: negative jacA)
                jacB0 = math.mul(futureBFromA, -jacA0);
                jacB1 = math.mul(futureBFromA, -jacA1);
                jacB2 = math.mul(futureBFromA, -jacA2);

                // A0 * A0  ,  A0 * A1  ,  A0 * A2
                //          ,  A1 * A1  ,  A1 * A2
                //          ,           ,  A2 * A2

                // All forces applied that are axis-aligned have a directly additive effect: diagonal elements
                // All other forces (off-diagonal elements) have are component forces
                //      ie: if you have a xy-plane and you are applying a force relative to the x-axis at 30degrees
                //      Then you are applying force:
                //              in the x-direction: cos(30) * force
                //              in the y-direction: sin(30) * force
                //      The off-diagonal elements are analogous to this force breakdown from the perspective of different
                //      reference axes. So A1 * A2 would be the relative forces between y and z
                // A check: adding all x-component forces should add to the magnitude of x

                // Calculate the effective mass
                float3 invEffectiveMassDiag = new float3(
                    math.csum(jacA0 * jacA0 * velocityA.InverseInertia + jacB0 * jacB0 * velocityB.InverseInertia),
                    math.csum(jacA1 * jacA1 * velocityA.InverseInertia + jacB1 * jacB1 * velocityB.InverseInertia),
                    math.csum(jacA2 * jacA2 * velocityA.InverseInertia + jacB2 * jacB2 * velocityB.InverseInertia));
                float3 invEffectiveMassOffDiag = new float3(
                    math.csum(jacA0 * jacA1 * velocityA.InverseInertia + jacB0 * jacB1 * velocityB.InverseInertia),
                    math.csum(jacA0 * jacA2 * velocityA.InverseInertia + jacB0 * jacB2 * velocityB.InverseInertia),
                    math.csum(jacA1 * jacA2 * velocityA.InverseInertia + jacB1 * jacB2 * velocityB.InverseInertia));

                JacobianUtilities.InvertSymmetricMatrix(invEffectiveMassDiag, invEffectiveMassOffDiag,
                    out float3 effectiveMassDiag, out float3 effectiveMassOffDiag);

                effectiveMass = JacobianUtilities.BuildSymmetricMatrix(effectiveMassDiag, effectiveMassOffDiag).c0;
                // effectiveMass is column0 of matrix: [diag.x, offdiag.x, offdiag.y]
            }

            // Calculate the error, adjust by tau and damping, and apply an impulse to correct it
            // The errors (initial/future/solve) are floats because they are relative to the jac0 rotation frame
            float futureError = JacobianUtilities.CalculateError(futureAngle, MinAngle, MaxAngle);
            float solveError = JacobianUtilities.CalculateCorrection(futureError, InitialError, Tau, Damping);
            float solveVelocity = -solveError * stepInput.InvTimestep;
            float3 impulseA = solveVelocity * (jacA0 * effectiveMass.x + jacA1 * effectiveMass.y + jacA2 * effectiveMass.z);
            float3 impulseB = solveVelocity * (jacB0 * effectiveMass.x + jacB1 * effectiveMass.y + jacB2 * effectiveMass.z);
            velocityA.ApplyAngularImpulse(impulseA);
            velocityB.ApplyAngularImpulse(impulseB);

            if ((jacHeader.Flags & JacobianFlags.EnableImpulseEvents) != 0)
            {
                HandleImpulseEvent(ref jacHeader, solveVelocity * jointOrientation.value.xyz, stepInput.IsLastIteration, ref impulseEventsWriter);
            }
        }

        private void HandleImpulseEvent(ref JacobianHeader jacHeader, float3 impulse, bool isLastIteration, ref NativeStream.Writer impulseEventsWriter)
        {
            ref ImpulseEventSolverData impulseEventData = ref jacHeader.AccessImpulseEventSolverData();
            impulseEventData.AccumulatedImpulse += impulse;
            if (isLastIteration && math.any(math.abs(impulseEventData.AccumulatedImpulse) > impulseEventData.MaxImpulse))
            {
                impulseEventsWriter.Write(new ImpulseEventData
                {
                    Type = ConstraintType.Angular,
                    Impulse = impulseEventData.AccumulatedImpulse,
                    JointEntity = impulseEventData.JointEntity,
                    BodyIndices = jacHeader.BodyPair
                });
            }
        }
    }
}
