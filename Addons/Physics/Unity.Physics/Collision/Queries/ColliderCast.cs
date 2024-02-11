using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Physics.BoundingVolumeHierarchy;
using static Unity.Physics.Math;
using UnityEngine.Assertions;

namespace Unity.Physics
{
    /// <summary>
    /// The input to collider cast queries consists of a Collider and its initial orientation, and
    /// the Start &amp; End positions of a line segment the Collider is to be swept along.
    /// </summary>
    public struct ColliderCastInput
    {
        /// <summary>   Gets or sets the collider used to cast with. </summary>
        ///
        /// <value> The collider to cast with. </value>
        [NativeDisableUnsafePtrRestriction] public unsafe Collider* Collider;

        /// <summary>   Gets or sets the orientation of the collider used to cast with. </summary>
        ///
        /// <value> The orientation. </value>
        public quaternion Orientation { get; set; }

        /// <summary>   Gets or sets the starting point of a cast. </summary>
        ///
        /// <value> The starting point. </value>
        public float3 Start
        {
            get => Ray.Origin;
            set
            {
                float3 end = Ray.Origin + Ray.Displacement;
                Ray.Origin = value;
                Ray.Displacement = end - value;
                Assert.IsTrue(math.all(math.abs(Ray.Displacement) < Math.Constants.MaxDisplacement3F), "ColliderCast length is very long. This would lead to floating point inaccuracies and invalid results.");
            }
        }

        /// <summary>   Gets or sets the ending point of a cast. </summary>
        ///
        /// <value> The ending point. </value>
        public float3 End
        {
            get => Ray.Origin + Ray.Displacement;
            set
            {
                Ray.Displacement = value - Ray.Origin;
                Assert.IsTrue(math.all(math.abs(Ray.Displacement) < Math.Constants.MaxDisplacement3F), "ColliderCast length is very long. This would lead to floating point inaccuracies and invalid results.");
            }
        }

        internal Ray Ray;
        internal QueryContext QueryContext;
        internal ColliderType ColliderType { get { unsafe { return Collider->Type; } } }

        /// <summary>   Gets or sets the query collider scale. </summary>
        ///
        /// <value> The query collider scale. </value>
        public float QueryColliderScale { get; set; }
        internal void InitScale()
        {
            QueryContext.InitScale();
            QueryColliderScale = math.select(1.0f, QueryColliderScale, QueryColliderScale != 0.0f);
        }

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="collider"> The collider to cast with. </param>
        /// <param name="start">    The starting point. </param>
        /// <param name="end">      The ending point. </param>
        public ColliderCastInput(BlobAssetReference<Collider> collider, float3 start, float3 end) : this(collider, start, end, quaternion.identity, 1.0f) {}

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="collider">             The collider to cast with. </param>
        /// <param name="start">                The starting point. </param>
        /// <param name="end">                  The ending point. </param>
        /// <param name="orientation">          The orientation of the collider. </param>
        /// <param name="queryColliderScale">   (Optional) The collider scale. </param>
        public ColliderCastInput(BlobAssetReference<Collider> collider, float3 start, float3 end, quaternion orientation, float queryColliderScale = 1.0f)
        {
            unsafe
            {
                Collider = (Collider*)collider.GetUnsafePtr();
            }
            Orientation = orientation;
            Ray = default;
            QueryContext = default;

            Ray.Origin = start;
            Ray.Displacement = end - start;
            QueryColliderScale = queryColliderScale;
        }

        /// <summary>   Convert this object into a string representation. </summary>
        ///
        /// <returns>   A string that represents this object. </returns>
        public override string ToString()
        {
            unsafe
            {
                return $"ColliderCastInput {{ Start = {Start}, End = {End}, Collider = {Collider->CollisionType}, Orientation = {Orientation}, QueryColliderScale = {QueryColliderScale} }}";
            }
        }
    }

    /// <summary>   A hit from a collider cast query. </summary>
    public struct ColliderCastHit : IQueryResult
    {
        /// <summary>   Fraction of the distance along the Ray where the hit occurred. </summary>
        ///
        /// <value> Returns a value between 0 and 1. </value>
        public float Fraction { get; set; }

        /// <summary>   Gets or sets the zero-based index of the rigid body. </summary>
        ///
        /// <value> Returns RigidBodyIndex of queried body. </value>
        public int RigidBodyIndex { get; set; }

        /// <summary>   Gets or sets the collider key. </summary>
        ///
        /// <value> Returns ColliderKey of queried leaf collider. </value>
        public ColliderKey ColliderKey { get; set; }

        /// <summary>   Gets or sets the material. </summary>
        ///
        /// <value> Returns Material of queried leaf collider. </value>
        public Material Material { get; set; }

        /// <summary>   Gets or sets the entity. </summary>
        ///
        /// <value> Returns Entity of queried body. </value>
        public Entity Entity { get; set; }

        /// <summary>   The point in query space where the hit occurred. </summary>
        ///
        /// <value> Returns the position of the point where the hit occurred. </value>
        public float3 Position { get; set; }

        /// <summary>   Gets or sets the surface normal. </summary>
        ///
        /// <value> Returns the normal of the point where the hit occurred. </value>
        public float3 SurfaceNormal { get; set; }

        /// <summary>   Collider key of the query collider. </summary>
        ///
        /// ### <returns>
        /// If the query input uses composite collider, this field will have the collider key of it's
        /// leaf which participated in the hit, otherwise the value will be undefined.
        /// </returns>
        public ColliderKey QueryColliderKey;

        /// <summary>   Convert this object into a string representation. </summary>
        ///
        /// <returns>   A string that represents this object. </returns>
        public override string ToString() =>
            $"ColliderCastHit {{ Fraction = {Fraction}, RigidBodyIndex = {RigidBodyIndex}, ColliderKey = {ColliderKey}, Entity = {Entity}, Position = {Position}, SurfaceNormal = {SurfaceNormal} }}";
    }

    // Collider cast query implementations
    static class ColliderCastQueries
    {
        private static unsafe void FlipColliderCastQuery<T>(ref ColliderCastInput input, ConvexCollider* target, ref T collector, out FlippedColliderCastQueryCollector<T> flipQueryCollector)
            where T : struct, ICollector<ColliderCastHit>
        {
            float3 worldFromDirection = math.mul(input.QueryContext.WorldFromLocalTransform.Rotation, input.Ray.Displacement * input.QueryContext.TargetScale);

            flipQueryCollector = new FlippedColliderCastQueryCollector<T>(ref collector, worldFromDirection, input.QueryContext.ColliderKey, target->Material);

            // Reset the ColliderKey
            input.QueryContext.ColliderKey = ColliderKey.Empty;
            input.QueryContext.NumColliderKeyBits = 0;

            input.Collider = (Collider*)target;

            ScaledMTransform targetFromQuery = new ScaledMTransform(new RigidTransform(input.Orientation, input.Ray.Origin), input.QueryContext.InvTargetScale * input.QueryColliderScale);

            // Switch the transform, so that it points to the shape that is being cast as a 'target' shape
            var displacement = input.Ray.Displacement;

            //Switch displacement from targetFromDisplacement to queryFromDisplacement, and inverse it's direction
            displacement = math.mul(targetFromQuery.InverseRotation, displacement * input.QueryContext.TargetScale / input.QueryColliderScale);
            displacement *= -1.0f;

            input.Ray.Displacement = displacement;
            var queryFromTarget = Inverse(targetFromQuery);

            float queryScale = input.QueryColliderScale;

            input.QueryColliderScale = input.QueryContext.TargetScale;
            input.QueryContext.InvTargetScale = 1.0f / queryScale;
            input.Ray.Origin = queryFromTarget.Translation;
            input.Orientation = new quaternion(queryFromTarget.Rotation);

            var worldFromQuery = Mul(input.QueryContext.WorldFromLocalTransform, targetFromQuery);
            input.QueryContext.WorldFromLocalTransform = worldFromQuery;
            input.QueryContext.IsFlipped = true;
        }

        internal static unsafe bool ConvexCollider<T>(ColliderCastInput input, Collider* target, ref T collector) where T : struct, ICollector<ColliderCastHit>
        {
            Assert.IsTrue(input.Collider->CollisionType == CollisionType.Convex);

            if (!CollisionFilter.IsCollisionEnabled(input.Collider->GetCollisionFilter(), target->GetCollisionFilter()))
            {
                return false;
            }

            if (!input.QueryContext.IsInitialized)
            {
                input.QueryContext = QueryContext.DefaultContext;
            }

            switch (target->Type)
            {
                case ColliderType.Sphere:
                case ColliderType.Capsule:
                case ColliderType.Triangle:
                case ColliderType.Quad:
                case ColliderType.Box:
                case ColliderType.Cylinder:
                case ColliderType.Convex:
                    if (ConvexConvex(input, target, collector.MaxFraction, out ColliderCastHit hit))
                    {
                        return collector.AddHit(hit);
                    }
                    return false;
                case ColliderType.Compound:
                    return ColliderCompound<ConvexCompoundDispatcher, T>(input, (CompoundCollider*)target, ref collector);
                case ColliderType.Mesh:
                    return ColliderMesh<ConvexConvexDispatcher, T>(input, (MeshCollider*)target, ref collector);
                case ColliderType.Terrain:
                    return ColliderTerrain<ConvexConvexDispatcher, T>(input, (TerrainCollider*)target, ref collector);
                default:
                    SafetyChecks.ThrowNotImplementedException();
                    return default;
            }
        }

        public static unsafe bool ColliderCollider<T>(ColliderCastInput input, Collider* target, ref T collector) where T : struct, ICollector<ColliderCastHit>
        {
            if (!CollisionFilter.IsCollisionEnabled(input.Collider->GetCollisionFilter(), target->GetCollisionFilter()))
            {
                return false;
            }

            if (!input.QueryContext.IsInitialized)
            {
                input.QueryContext = QueryContext.DefaultContext;
            }

            input.QueryColliderScale = math.select(input.QueryColliderScale, 1.0f, input.QueryColliderScale == 0.0f);

            switch (input.Collider->CollisionType)
            {
                case CollisionType.Convex:
                    switch (target->Type)
                    {
                        case ColliderType.Sphere:
                        case ColliderType.Capsule:
                        case ColliderType.Triangle:
                        case ColliderType.Quad:
                        case ColliderType.Box:
                        case ColliderType.Cylinder:
                        case ColliderType.Convex:
                            if (ConvexConvex(input, target, collector.MaxFraction, out ColliderCastHit hit))
                            {
                                return collector.AddHit(hit);
                            }
                            return false;
                        case ColliderType.Compound:
                            return ColliderCompound<DefaultCompoundDispatcher, T>(input, (CompoundCollider*)target, ref collector);
                        case ColliderType.Mesh:
                            return ColliderMesh<ConvexConvexDispatcher, T>(input, (MeshCollider*)target, ref collector);
                        case ColliderType.Terrain:
                            return ColliderTerrain<ConvexConvexDispatcher, T>(input, (TerrainCollider*)target, ref collector);
                        default:
                            SafetyChecks.ThrowNotImplementedException();
                            return default;
                    }
                case CollisionType.Composite:
                    switch (input.Collider->Type)
                    {
                        case ColliderType.Compound:
                            switch (target->Type)
                            {
                                case ColliderType.Sphere:
                                case ColliderType.Capsule:
                                case ColliderType.Triangle:
                                case ColliderType.Quad:
                                case ColliderType.Box:
                                case ColliderType.Cylinder:
                                case ColliderType.Convex:
                                    return CompoundConvex(input, (ConvexCollider*)target, ref collector);
                                case ColliderType.Compound:
                                    return ColliderCompound<DefaultCompoundDispatcher, T>(input, (CompoundCollider*)target, ref collector);
                                case ColliderType.Mesh:
                                    return ColliderMesh<CompoundConvexDispatcher, T>(input, (MeshCollider*)target, ref collector);
                                case ColliderType.Terrain:
                                    return ColliderTerrain<CompoundConvexDispatcher, T>(input, (TerrainCollider*)target, ref collector);
                                default:
                                    return default;
                            }
                        case ColliderType.Mesh:
                            switch (target->Type)
                            {
                                case ColliderType.Sphere:
                                case ColliderType.Capsule:
                                case ColliderType.Triangle:
                                case ColliderType.Quad:
                                case ColliderType.Box:
                                case ColliderType.Cylinder:
                                case ColliderType.Convex:
                                    return MeshConvex(input, (ConvexCollider*)target, ref collector);
                                case ColliderType.Compound:
                                    return ColliderCompound<DefaultCompoundDispatcher, T>(input, (CompoundCollider*)target, ref collector);
                                case ColliderType.Mesh:
                                    return ColliderMesh<MeshConvexDispatcher, T>(input, (MeshCollider*)target, ref collector);
                                case ColliderType.Terrain:
                                    return ColliderTerrain<MeshConvexDispatcher, T>(input, (TerrainCollider*)target, ref collector);
                                default:
                                    return default;
                            }
                        default:
                            return default;
                    }
                case CollisionType.Terrain:
                    switch (target->Type)
                    {
                        case ColliderType.Sphere:
                        case ColliderType.Capsule:
                        case ColliderType.Triangle:
                        case ColliderType.Quad:
                        case ColliderType.Box:
                        case ColliderType.Cylinder:
                        case ColliderType.Convex:
                            return TerrainConvex(input, (ConvexCollider*)target, ref collector);
                        case ColliderType.Compound:
                            return ColliderCompound<DefaultCompoundDispatcher, T>(input, (CompoundCollider*)target, ref collector);
                        case ColliderType.Mesh:
                            return ColliderMesh<TerrainConvexDispatcher, T>(input, (MeshCollider*)target, ref collector);
                        case ColliderType.Terrain:
                            return ColliderTerrain<TerrainConvexDispatcher, T>(input, (TerrainCollider*)target, ref collector);
                        default:
                            return default;
                    }
                default:
                    SafetyChecks.ThrowNotImplementedException();
                    return default;
            }
        }

        private static unsafe bool ConvexConvex(ColliderCastInput input, Collider* target, float maxFraction, out ColliderCastHit hit)
        {
            hit = default;

            // Get the current transform
            MTransform targetFromQuery = new MTransform(input.Orientation, input.Start);
            float queryRelativeScale = input.QueryColliderScale * input.QueryContext.InvTargetScale;

            // Conservative advancement
            const float tolerance = 1e-3f;      // return if this close to a hit
            const float keepDistance = 1e-4f;   // avoid bad cases for GJK (penetration / exact hit)
            int iterations = 10;                // return after this many advances, regardless of accuracy
            float fraction = 0.0f;

            float keepDistanceScaled = keepDistance * math.abs(input.QueryContext.InvTargetScale);
            float toleranceScaled = tolerance * math.abs(input.QueryContext.InvTargetScale);

            while (true)
            {
                if (fraction >= maxFraction)
                {
                    // Exceeded the maximum fraction without a hit
                    return false;
                }

                // Find the current distance
                DistanceQueries.Result distanceResult = DistanceQueries.ConvexConvex(target, input.Collider, targetFromQuery, queryRelativeScale);

                // Check for a hit
                if (distanceResult.Distance < toleranceScaled || --iterations == 0)
                {
                    // In case of penetration (fraction == 0) and non convex input (IsFlipped),
                    // hit position needs to be switched from the surface of the shape B to the surface of the shape A
                    if (input.QueryContext.IsFlipped && fraction == 0)
                    {
                        hit.Position = Mul(input.QueryContext.WorldFromLocalTransform, distanceResult.PositionOnAinA);
                    }
                    else
                    {
                        hit.Position = Mul(input.QueryContext.WorldFromLocalTransform, distanceResult.PositionOnBinA);
                    }

                    float3 normal = math.select(-distanceResult.NormalInA, distanceResult.NormalInA, input.QueryContext.TargetScale < 0.0f);
                    hit.SurfaceNormal = math.mul(input.QueryContext.WorldFromLocalTransform.Rotation, normal);
                    hit.Fraction = fraction;
                    hit.RigidBodyIndex = input.QueryContext.RigidBodyIndex;
                    hit.ColliderKey = input.QueryContext.ColliderKey;
                    hit.QueryColliderKey = ColliderKey.Empty;
                    hit.Material = ((ConvexColliderHeader*)target)->Material;
                    hit.Entity = input.QueryContext.Entity;

                    return true;
                }

                // Check for a miss
                float dot = math.dot(distanceResult.NormalInA, input.Ray.Displacement);
                if (dot <= 0.0f)
                {
                    // Collider is moving away from the target, it will never hit
                    return false;
                }

                // Advance
                fraction += (distanceResult.Distance - keepDistanceScaled) / dot;
                if (fraction >= maxFraction)
                {
                    // Exceeded the maximum fraction without a hit
                    return false;
                }

                targetFromQuery.Translation = math.lerp(input.Start, input.End, fraction);
            }
        }

        internal interface IColliderCastDispatcher
        {
            unsafe bool Dispatch<T>(ColliderCastInput input, ConvexCollider* collider, ref T collector,
                uint numColliderKeyBits, uint subKey)
                where T : struct, ICollector<ColliderCastHit>;
        }

        internal struct ConvexConvexDispatcher : IColliderCastDispatcher
        {
            public unsafe bool Dispatch<T>(ColliderCastInput input, ConvexCollider* collider, ref T collector,
                uint numColliderKeyBits, uint subKey)
                where T : struct, ICollector<ColliderCastHit>
            {
                if (ConvexConvex(input, (Collider*)collider, collector.MaxFraction, out ColliderCastHit hit))
                {
                    hit.ColliderKey = input.QueryContext.SetSubKey(numColliderKeyBits, subKey);
                    return collector.AddHit(hit);
                }
                return false;
            }
        }

        internal struct CompoundConvexDispatcher : IColliderCastDispatcher
        {
            public unsafe bool Dispatch<T>(ColliderCastInput input, ConvexCollider* collider, ref T collector,
                uint numColliderKeyBits, uint subKey)
                where T : struct, ICollector<ColliderCastHit>
            {
                input.QueryContext.ColliderKey = input.QueryContext.PushSubKey(numColliderKeyBits, subKey);
                return CompoundConvex(input, collider, ref collector);
            }
        }

        internal struct MeshConvexDispatcher : IColliderCastDispatcher
        {
            public unsafe bool Dispatch<T>(ColliderCastInput input, ConvexCollider* collider, ref T collector,
                uint numColliderKeyBits, uint subKey)
                where T : struct, ICollector<ColliderCastHit>
            {
                input.QueryContext.ColliderKey = input.QueryContext.PushSubKey(numColliderKeyBits, subKey);
                return MeshConvex(input, collider, ref collector);
            }
        }

        internal struct TerrainConvexDispatcher : IColliderCastDispatcher
        {
            public unsafe bool Dispatch<T>(ColliderCastInput input, ConvexCollider* collider, ref T collector,
                uint numColliderKeyBits, uint subKey)
                where T : struct, ICollector<ColliderCastHit>
            {
                input.QueryContext.ColliderKey = input.QueryContext.PushSubKey(numColliderKeyBits, subKey);
                return TerrainConvex(input, collider, ref collector);
            }
        }

        internal unsafe struct ColliderMeshLeafProcessor<D> : IColliderCastLeafProcessor
            where D : struct, IColliderCastDispatcher
        {
            private readonly Mesh* m_Mesh;
            private readonly uint m_NumColliderKeyBits;

            public ColliderMeshLeafProcessor(MeshCollider* meshCollider)
            {
                m_Mesh = &meshCollider->Mesh;
                m_NumColliderKeyBits = meshCollider->NumColliderKeyBits;
            }

            public bool ColliderCastLeaf<T>(ColliderCastInput input, int primitiveKey, ref T collector)
                where T : struct, ICollector<ColliderCastHit>
            {
                m_Mesh->GetPrimitive(primitiveKey, out float3x4 vertices, out Mesh.PrimitiveFlags flags, out CollisionFilter filter, out Material material);

                if (!CollisionFilter.IsCollisionEnabled(input.Collider->GetCollisionFilter(), filter)) // TODO: could do this check within GetPrimitive()
                {
                    return false;
                }

                D dispatcher = new D();

                int numPolygons = Mesh.GetNumPolygonsInPrimitive(flags);
                bool isQuad = Mesh.IsPrimitiveFlagSet(flags, Mesh.PrimitiveFlags.IsQuad);

                bool acceptHit = false;

                var polygon = new PolygonCollider();
                polygon.InitNoVertices(filter, material);
                for (int polygonIndex = 0; polygonIndex < numPolygons; polygonIndex++)
                {
                    float fraction = collector.MaxFraction;

                    if (isQuad)
                    {
                        polygon.SetAsQuad(vertices[0], vertices[1], vertices[2], vertices[3]);
                    }
                    else
                    {
                        polygon.SetAsTriangle(vertices[0], vertices[1 + polygonIndex], vertices[2 + polygonIndex]);
                    }

                    acceptHit |= dispatcher.Dispatch(input, (ConvexCollider*)&polygon, ref collector, m_NumColliderKeyBits, (uint)(primitiveKey << 1 | polygonIndex));
                }

                return acceptHit;
            }
        }

        private static unsafe bool ColliderMesh<D, T>(ColliderCastInput input, MeshCollider* meshCollider, ref T collector)
            where T : struct, ICollector<ColliderCastHit>
            where D : struct, IColliderCastDispatcher
        {
            var leafProcessor = new ColliderMeshLeafProcessor<D>(meshCollider);
            return meshCollider->Mesh.BoundingVolumeHierarchy.ColliderCast(input, ref leafProcessor, ref collector);
        }

        private static unsafe bool MeshConvex<T>(ColliderCastInput input, ConvexCollider* convexCollider, ref T collector)
            where T : struct, ICollector<ColliderCastHit>
        {
            var meshCollider = (MeshCollider*)input.Collider;

            FlipColliderCastQuery(ref input, convexCollider, ref collector, out FlippedColliderCastQueryCollector<T> flipQueryCollector);
            return ColliderMesh<ConvexConvexDispatcher, FlippedColliderCastQueryCollector<T>>(input, meshCollider, ref flipQueryCollector);
        }

        internal unsafe interface IColliderCompoundCastDispatcher
        {
            bool CastCollider<T>(ColliderCastInput input, ref T collector, Collider* target)
                where T : struct, ICollector<ColliderCastHit>;
        }

        internal unsafe struct DefaultCompoundDispatcher : IColliderCompoundCastDispatcher
        {
            public bool CastCollider<T>(ColliderCastInput input, ref T collector, Collider* target) where T : struct, ICollector<ColliderCastHit>
            {
                return target->CastCollider(input, ref collector);
            }
        }

        internal unsafe struct ConvexCompoundDispatcher : IColliderCompoundCastDispatcher
        {
            public bool CastCollider<T>(ColliderCastInput input, ref T collector, Collider* target) where T : struct, ICollector<ColliderCastHit>
            {
                return ConvexCollider(input, target, ref collector);
            }
        }

        // The need to introduce generic dispatcher parameter arises from the introduction of FlippedColliderCastQueryCollector<ICollector>.
        // With the previous code (return target.CastCollider(...) instead of dispatcher.CastCollider(...)), the code ended up in ColliderCollider() function
        // with a giant switch inside it. One of the switch options is CompoundConvex() function, which turns the provided ICollector into a FlippedColliderCastQueryCollector<provided ICollector>.
        // From there, the control flow also ends up in the same ColliderCollider() function, which is logically fine, since flipping the collector happens only once, and CompoundConvex() will not get called again.
        // But the compiler doesn't know that, and it will endlessly try to resolve type FlipColliderCastQueryCollector<T>, and end up in an endless recursion trying to resolve the type
        // FlippedColliderCastQueryCollector<FlippedColliderCastQueryCollector<...T>>.
        // ConvexCompoundDispatcher solves that problem, as it assumes that the input collider is Convex, and uses a different switch statement, in which CompoundConvex isn't an option.
        internal unsafe struct ColliderCompoundLeafProcessor<D> : IColliderCastLeafProcessor
            where D : struct, IColliderCompoundCastDispatcher
        {
            private readonly CompoundCollider* m_CompoundCollider;

            public ColliderCompoundLeafProcessor(CompoundCollider* compoundCollider)
            {
                m_CompoundCollider = compoundCollider;
            }

            public bool ColliderCastLeaf<T>(ColliderCastInput input, int leafData, ref T collector)
                where T : struct, ICollector<ColliderCastHit>
            {
                ref CompoundCollider.Child child = ref m_CompoundCollider->Children[leafData];

                if (!CollisionFilter.IsCollisionEnabled(input.Collider->GetCollisionFilter(), child.Collider->GetCollisionFilter()))
                {
                    return false;
                }

                // Transform the cast into child space
                ColliderCastInput inputLs = input;
                RigidTransform childFromCompound = math.inverse(child.CompoundFromChild);
                inputLs.Ray.Origin = math.transform(childFromCompound, input.Ray.Origin);
                inputLs.Ray.Displacement = math.mul(childFromCompound.rot, input.Ray.Displacement);
                inputLs.Orientation = math.mul(childFromCompound.rot, input.Orientation);
                inputLs.QueryContext.ColliderKey = input.QueryContext.PushSubKey(m_CompoundCollider->NumColliderKeyBits, (uint)leafData);
                inputLs.QueryContext.NumColliderKeyBits = input.QueryContext.NumColliderKeyBits;
                inputLs.QueryContext.WorldFromLocalTransform = ScaledMTransform.Mul(input.QueryContext.WorldFromLocalTransform, new MTransform(child.CompoundFromChild));

                D dispatcher = new D();

                return dispatcher.CastCollider(inputLs, ref collector, child.Collider);
            }
        }

        private static unsafe bool ColliderCompound<D, T>(ColliderCastInput input, CompoundCollider* compoundCollider, ref T collector)
            where D : struct, IColliderCompoundCastDispatcher
            where T : struct, ICollector<ColliderCastHit>
        {
            var leafProcessor = new ColliderCompoundLeafProcessor<D>(compoundCollider);
            return compoundCollider->BoundingVolumeHierarchy.ColliderCast(input, ref leafProcessor, ref collector);
        }

        private static unsafe bool CompoundConvex<T>(ColliderCastInput input, ConvexCollider* convexCollider, ref T collector)
            where T : struct, ICollector<ColliderCastHit>
        {
            var compoundCollider = (CompoundCollider*)input.Collider;

            FlipColliderCastQuery(ref input, convexCollider, ref collector, out FlippedColliderCastQueryCollector<T> flipQueryCollector);
            return ColliderCompound<ConvexCompoundDispatcher, FlippedColliderCastQueryCollector<T>>(input, compoundCollider, ref flipQueryCollector);
        }

        private static unsafe bool TerrainConvex<T>(ColliderCastInput input, ConvexCollider* convexCollider, ref T collector)
            where T : struct, ICollector<ColliderCastHit>
        {
            var terrainCollider = (TerrainCollider*)input.Collider;

            FlipColliderCastQuery(ref input, convexCollider, ref collector, out FlippedColliderCastQueryCollector<T> flipQueryCollector);
            return ColliderTerrain<ConvexConvexDispatcher, FlippedColliderCastQueryCollector<T>>(input, terrainCollider, ref flipQueryCollector);
        }

        private static unsafe bool ColliderTerrain<D, T>(ColliderCastInput input, TerrainCollider* terrainCollider, ref T collector)
            where D : struct, IColliderCastDispatcher
            where T : struct, ICollector<ColliderCastHit>
        {
            ref Terrain terrain = ref terrainCollider->Terrain;
            Material material = terrainCollider->Material;

            D dispatcher = new D();

            bool hadHit = false;

            // Get a ray for the min corner of the AABB in tree-space and the extents of the AABB in tree-space
            float3 aabbExtents;
            Ray aabbRay;
            Terrain.QuadTreeWalker walker;
            {
                Aabb aabb = input.Collider->CalculateAabb(new RigidTransform(input.Orientation, input.Start),
                    input.QueryContext.InvTargetScale * input.QueryColliderScale);
                Aabb aabbInTree = new Aabb
                {
                    Min = aabb.Min * terrain.InverseScale,
                    Max = aabb.Max * terrain.InverseScale
                };
                aabbExtents = aabbInTree.Extents;
                aabbRay = new Ray
                {
                    Origin = aabbInTree.Min,
                    Displacement = input.Ray.Displacement * terrain.InverseScale
                };

                float3 maxDisplacement = aabbRay.Displacement * collector.MaxFraction;
                Aabb queryAabb = new Aabb
                {
                    Min = aabbInTree.Min + math.min(maxDisplacement, float3.zero),
                    Max = aabbInTree.Max + math.max(maxDisplacement, float3.zero)
                };
                walker = new Terrain.QuadTreeWalker(&terrainCollider->Terrain, queryAabb);
            }

            // Traverse the tree
            int numHits = collector.NumHits;
            while (walker.Pop())
            {
                FourTransposedAabbs bounds = walker.Bounds;
                bounds.Lx -= aabbExtents.x;
                bounds.Ly -= aabbExtents.y;
                bounds.Lz -= aabbExtents.z;
                bool4 hitMask = bounds.Raycast(aabbRay, collector.MaxFraction, out float4 hitFractions);
                hitMask &= (walker.Bounds.Ly <= walker.Bounds.Hy); // Mask off empty children
                if (walker.IsLeaf)
                {
                    // Leaf node, collidercast against hit child quads
                    int4 hitIndex;
                    int hitCount = math.compress((int*)(&hitIndex), 0, new int4(0, 1, 2, 3), hitMask);
                    for (int iHit = 0; iHit < hitCount; iHit++)
                    {
                        // Get the quad vertices
                        walker.GetQuad(hitIndex[iHit], out int2 quadIndex, out float3 a, out float3 b, out float3 c, out float3 d);

                        // Test each triangle in the quad
                        var polygon = new PolygonCollider();
                        polygon.InitNoVertices(CollisionFilter.Default, material);
                        for (int iTriangle = 0; iTriangle < 2; iTriangle++)
                        {
                            // Cast
                            float fraction = collector.MaxFraction;
                            polygon.SetAsTriangle(a, b, c);

                            hadHit |= dispatcher.Dispatch(input, (ConvexCollider*)&polygon, ref collector, terrain.NumColliderKeyBits, terrain.GetSubKey(quadIndex, iTriangle));

                            // Next triangle
                            a = c;
                            c = d;
                        }
                    }
                }
                else
                {
                    // Interior node, add hit child nodes to the stack
                    walker.Push(hitMask);
                }
            }

            return hadHit;
        }
    }
}
