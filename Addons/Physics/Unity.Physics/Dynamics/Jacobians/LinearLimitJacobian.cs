using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    // Solve data for a constraint that limits the linear distance between a pair of pivots in 1, 2, or 3 degrees of freedom
    [NoAlias]
    struct LinearLimitJacobian
    {
        // Constraint Bodies
        public MTransform BodyFromConstraintA;
        public MTransform BodyFromConstraintB;

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

        // Build the Jacobian
        public void Build(
            MTransform aFromConstraint, MTransform bFromConstraint,
            MotionVelocity velocityA, MotionVelocity velocityB,
            MotionData motionA, MotionData motionB,
            Constraint constraint, float tau, float damping)
        {
            WorldFromA = motionA.WorldFromMotion;
            WorldFromB = motionB.WorldFromMotion;

            BodyFromConstraintA = aFromConstraint;
            BodyFromConstraintB = bFromConstraint;

            AxisInB = float3.zero;
            Is1D = false;

            MinDistance = constraint.Min;
            MaxDistance = constraint.Max;

            Tau = tau;
            Damping = damping;

            // TODO.ma - this code is not always correct in its choice of pivotB.
            // The constraint model is asymmetrical.  B is the master, and the constraint feature is defined in B-space as a region affixed to body B.
            // For example, we can conceive of a 1D constraint as a plane attached to body B through constraint.PivotB, and constraint.PivotA is constrained to that plane.
            // A 2D constraint is a line attached to body B.  A 3D constraint is a point.
            // So, while we always apply an impulse to body A at pivotA, we apply the impulse to body B somewhere on the constraint region.
            // This code chooses that point by projecting pivotA onto the point, line or plane, which seems pretty reasonable and also analogous to how contact constraints work.
            // However, if the limits are nonzero, then the region is not a point, line or plane.  It is a spherical shell, cylindrical shell, or the space between two parallel planes.
            // In that case, it is not projecting A to a point on the constraint region.  This will not prevent solving the constraint, but the solution may not look correct.
            // For now I am leaving it because it is not important to get the most common constraint situations working.  If you use a ball and socket, or a prismatic constraint with a
            // static master body, or a stiff spring, then there's no problem.  However, I think it should eventually be fixed.  The min and max limits have different projections, so
            // probably the best solution is to make two jacobians whenever min != max.  My assumption is that 99% of these are ball and sockets with min = max = 0, so I would rather have
            // some waste in the min != max case than generalize this code to deal with different pivots and effective masses depending on which limit is hit.

            if (!math.all(constraint.ConstrainedAxes))
            {
                Is1D = constraint.ConstrainedAxes.x ^ constraint.ConstrainedAxes.y ^ constraint.ConstrainedAxes.z;

                // Project pivot A onto the line or plane in B that it is attached to
                RigidTransform bFromA = math.mul(math.inverse(WorldFromB), WorldFromA);
                float3 pivotAinB = math.transform(bFromA, BodyFromConstraintA.Translation);
                float3 diff = pivotAinB - BodyFromConstraintB.Translation;
                for (int i = 0; i < 3; i++)
                {
                    float3 column = bFromConstraint.Rotation[i];
                    AxisInB = math.select(column, AxisInB, Is1D ^ constraint.ConstrainedAxes[i]);

                    float3 dot = math.select(math.dot(column, diff), 0.0f, constraint.ConstrainedAxes[i]);
                    BodyFromConstraintB.Translation += column * dot;
                }
            }

            // Calculate the current error
            InitialError = CalculateError(
                new MTransform(WorldFromA.rot, WorldFromA.pos),
                new MTransform(WorldFromB.rot, WorldFromB.pos),
                out float3 directionUnused);
        }

        private static void ApplyImpulse(in float3 impulse, in float3 ang0, in float3 ang1, in float3 ang2, ref MotionVelocity velocity)
        {
            velocity.ApplyLinearImpulse(impulse);
            velocity.ApplyAngularImpulse(impulse.x * ang0 + impulse.y * ang1 + impulse.z * ang2);
        }

        // Solve the Jacobian
        public void Solve(ref JacobianHeader jacHeader, ref MotionVelocity velocityA, ref MotionVelocity velocityB, Solver.StepInput stepInput,
            ref NativeStream.Writer impulseEventsWriter)
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
            CalculateAngulars(BodyFromConstraintA.Translation, futureWorldFromA.Rotation, out float3 angA0, out float3 angA1, out float3 angA2);
            CalculateAngulars(BodyFromConstraintB.Translation, futureWorldFromB.Rotation, out float3 angB0, out float3 angB1, out float3 angB2);

            // Calculate effective mass
            float3 effectiveMassDiag, effectiveMassOffDiag;
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
                JacobianUtilities.InvertSymmetricMatrix(invEffectiveMassDiag, invEffectiveMassOffDiag, out effectiveMassDiag, out effectiveMassOffDiag);
            }

            // Predict error at the end of the step and calculate the impulse to correct it
            float3 impulse;
            {
                // Find the difference between the future distance and the limit range, then apply tau and damping
                float futureDistanceError = CalculateError(futureWorldFromA, futureWorldFromB, out float3 futureDirection);
                float solveDistanceError = JacobianUtilities.CalculateCorrection(futureDistanceError, InitialError, Tau, Damping);

                // Calculate the impulse to correct the error
                float3 solveError = solveDistanceError * futureDirection;
                float3x3 effectiveMass = JacobianUtilities.BuildSymmetricMatrix(effectiveMassDiag, effectiveMassOffDiag);
                impulse = math.mul(effectiveMass, solveError) * stepInput.InvTimestep;
            }

            // Apply the impulse
            ApplyImpulse(impulse, angA0, angA1, angA2, ref velocityA);
            ApplyImpulse(-impulse, angB0, angB1, angB2, ref velocityB);

            if ((jacHeader.Flags & JacobianFlags.EnableImpulseEvents) != 0)
            {
                HandleImpulseEvent(ref jacHeader, impulse, stepInput.IsLastIteration, ref impulseEventsWriter);
            }
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

        private float CalculateError(in MTransform worldFromA, in MTransform worldFromB, out float3 direction)
        {
            // Find the direction from pivot A to B and the distance between them
            float3 pivotA = Mul(worldFromA, BodyFromConstraintA.Translation);
            float3 pivotB = Mul(worldFromB, BodyFromConstraintB.Translation);
            float3 axis = math.mul(worldFromB.Rotation, AxisInB);
            direction = pivotB - pivotA;
            float dot = math.dot(direction, axis);

            // Project for lower-dimension joints
            float distance;
            if (Is1D)
            {
                // In 1D, distance is signed and measured along the axis
                distance = -dot;
                direction = -axis;
            }
            else
            {
                // In 2D / 3D, distance is nonnegative.  In 2D it is measured perpendicular to the axis.
                direction -= axis * dot;
                float futureDistanceSq = math.lengthsq(direction);
                float invFutureDistance = math.select(math.rsqrt(futureDistanceSq), 0.0f, futureDistanceSq == 0.0f);
                distance = futureDistanceSq * invFutureDistance;
                direction *= invFutureDistance;
            }

            // Find the difference between the future distance and the limit range
            return JacobianUtilities.CalculateError(distance, MinDistance, MaxDistance);
        }

        private void HandleImpulseEvent(ref JacobianHeader jacHeader, in float3 appliedImpulse, bool isLastIteration, ref NativeStream.Writer impulseEventsWriter)
        {
            ref ImpulseEventSolverData impulseEventData = ref jacHeader.AccessImpulseEventSolverData();
            impulseEventData.AccumulatedImpulse += appliedImpulse;

            if (isLastIteration && math.any(math.abs(impulseEventData.AccumulatedImpulse) > impulseEventData.MaxImpulse))
            {
                // We have to convert our impulse into constraint space to match what havok is returning from their impulses
                // currently the linear motions are calculated in world space, therefore we need to convert it into constraint space
                RigidTransform bFromA = math.mul(math.inverse(WorldFromB), WorldFromA);
                // Project pivot A on the same plane as pivot B (same world space as body B) and calculate the diff between the two
                float3 pivotAinB = BodyFromConstraintB.Translation - math.transform(bFromA, BodyFromConstraintA.Translation);
                //Inverse the rotation of world space B on the pivot diff
                pivotAinB = math.mul(BodyFromConstraintB.InverseRotation, pivotAinB);
                float3 normal = math.normalize(pivotAinB);
                // Move accumulated impulse into constraint space by multiplying with the direction vector
                float3 constraintSpaceImpulse = normal * math.length(impulseEventData.AccumulatedImpulse);

                impulseEventsWriter.Write(new ImpulseEventData
                {
                    Type = ConstraintType.Linear,
                    Impulse = constraintSpaceImpulse,
                    JointEntity = impulseEventData.JointEntity,
                    BodyIndices = jacHeader.BodyPair
                });
            }
        }

        #endregion
    }
}
