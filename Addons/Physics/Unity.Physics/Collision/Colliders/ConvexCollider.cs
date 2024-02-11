using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Entities;
using UnityEngine.Assertions;

namespace Unity.Physics
{
    /// <summary>   Convex hull generation parameters. </summary>
    [Serializable]
    public struct ConvexHullGenerationParameters : IEquatable<ConvexHullGenerationParameters>
    {
        internal const string k_BevelRadiusTooltip =
            "Determines how rounded the edges of the convex shape will be. A value greater than 0 results in more optimized collision, at the expense of some shape detail.";

        const float k_DefaultSimplificationTolerance = 0.015f;
        const float k_DefaultBevelRadius = 0.05f;
        const float k_DefaultMinAngle = 2.5f * math.PI / 180f; // 2.5 degrees

        /// <summary>   Default convex hull generation parameters. </summary>
        public static readonly ConvexHullGenerationParameters Default = new ConvexHullGenerationParameters
        {
            SimplificationTolerance = k_DefaultSimplificationTolerance,
            BevelRadius = k_DefaultBevelRadius,
            MinimumAngle = k_DefaultMinAngle
        };

        /// <summary>   Specifies maximum distance that any input point may be moved when simplifying convex hull. </summary>
        ///
        /// <value> The simplification tolerance. </value>
        public float SimplificationTolerance { get => m_SimplificationTolerance; set => m_SimplificationTolerance = value; }
        [UnityEngine.Tooltip("Specifies maximum distance that any input point may be moved when simplifying convex hull.")]
        [UnityEngine.SerializeField]
        float m_SimplificationTolerance;

        /// <summary>   Gets or sets the bevel radius. </summary>
        ///
        /// <value> The bevel radius. </value>
        public float BevelRadius { get => m_BevelRadius; set => m_BevelRadius = value; }
        [UnityEngine.Tooltip(k_BevelRadiusTooltip)]
        [UnityEngine.SerializeField]
        float m_BevelRadius;

        /// <summary>   Specifies the angle between adjacent faces below which they should be made coplanar.. </summary>
        ///
        /// <value> The minimum angle. </value>
        public float MinimumAngle { get => m_MinimumAngle; set => m_MinimumAngle = value; }
        [UnityEngine.Tooltip("Specifies the angle between adjacent faces below which they should be made coplanar.")]
        [UnityEngine.SerializeField]
        float m_MinimumAngle;

        /// <summary>
        /// Tests if this ConvexHullGenerationParameters is considered equal to another.
        /// </summary>
        ///
        /// <param name="other">    The convex hull generation parameters to compare to this object. </param>
        ///
        /// <returns>   True if the objects are considered equal, false if they are not. </returns>
        public bool Equals(ConvexHullGenerationParameters other) =>
            m_SimplificationTolerance == other.m_SimplificationTolerance
            && m_BevelRadius == other.m_BevelRadius
            && m_MinimumAngle == other.m_MinimumAngle;

        /// <summary>   Calculates a hash code for this object. </summary>
        ///
        /// <returns>   A hash code for this object. </returns>
        public override int GetHashCode() =>
            unchecked((int)math.hash(new float3(m_SimplificationTolerance, m_BevelRadius, m_MinimumAngle)));
    }

    /// <summary>
    /// A collider in the shape of an arbitrary convex hull. Warning: This is just the header, it is
    /// followed by variable sized data in memory. Therefore this struct must always be passed by
    /// reference, never by value.
    /// </summary>
    public struct ConvexCollider : IConvexCollider
    {
        // Header
        private ConvexColliderHeader m_Header;
        public ConvexHull ConvexHull;

        internal const int k_MaxVertices = 252;
        internal const int k_MaxFaces = 252;
        internal const int k_MaxFaceVertices = ConvexConvexManifoldQueries.Manifold.k_MaxNumContacts;

        // followed by variable sized convex hull data

        #region Construction

        /// <summary>   Create a convex collider from the given point cloud. </summary>
        ///
        /// <param name="points">               The points. </param>
        /// <param name="generationParameters"> Options for controlling the generation. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(
            NativeArray<float3> points, ConvexHullGenerationParameters generationParameters
        ) =>
            Create(points, generationParameters, CollisionFilter.Default, Material.Default);

        /// <summary>   Creates a convex collider from the given point cloud </summary>
        ///
        /// <param name="points">               The points. </param>
        /// <param name="generationParameters"> Options for controlling the generation. </param>
        /// <param name="filter">               Specifies the filter. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(
            NativeArray<float3> points, ConvexHullGenerationParameters generationParameters, CollisionFilter filter
        ) =>
            Create(points, generationParameters, filter, Material.Default);

        /// <summary>   Creates a convex collider from the given point cloud</summary>
        ///
        /// <param name="points">               The points. </param>
        /// <param name="generationParameters"> Options for controlling the generation. </param>
        /// <param name="filter">               Specifies the filter. </param>
        /// <param name="material">             The material. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(
            NativeArray<float3> points, ConvexHullGenerationParameters generationParameters, CollisionFilter filter, Material material
        ) =>
            CreateInternal(points, generationParameters, filter, material, k_MaxVertices, k_MaxFaces, k_MaxFaceVertices);

        #endregion

        #region Internal Construction

        internal static BlobAssetReference<Collider> CreateInternal(
            NativeArray<float3> points, ConvexHullGenerationParameters generationParameters, CollisionFilter filter, Material material, uint forceUniqueBlobID = ColliderConstants.k_SharedBlobID
        ) =>
            CreateInternal(points, generationParameters, filter, material, k_MaxVertices, k_MaxFaces, k_MaxFaceVertices, forceUniqueBlobID);

        internal static BlobAssetReference<Collider> CreateInternal(
            NativeArray<float3> points, ConvexHullGenerationParameters generationParameters, CollisionFilter filter, Material material,
            int maxVertices, int maxFaces, int maxFaceVertices, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID
        )
        {
            SafetyChecks.CheckValidAndThrow(points, nameof(points), generationParameters, nameof(generationParameters));

            // Build convex hull
            var builder = new ConvexHullBuilder(
                points,
                generationParameters,
                maxVertices,
                maxFaces,
                maxFaceVertices,
                out var builderConvexRadius
            );

            return CreateInternal(builder, builderConvexRadius, filter, material, forceUniqueBlobID);
        }

        internal static unsafe BlobAssetReference<Collider> CreateInternal(ConvexHullBuilder builder, float convexRadius, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            // Convert hull to compact format
            var tempHull = new TempHull(ref builder);

            // Allocate collider
            int totalSize = UnsafeUtility.SizeOf<ConvexCollider>();
            totalSize += tempHull.Vertices.Length * sizeof(float3);
            totalSize = Math.NextMultipleOf16(totalSize);  // planes currently must be aligned for Havok
            totalSize += tempHull.Planes.Length * sizeof(Plane);
            totalSize += tempHull.Faces.Length * sizeof(ConvexHull.Face);
            totalSize += tempHull.FaceVertexIndices.Length * sizeof(byte);
            totalSize += tempHull.VertexEdges.Length * sizeof(ConvexHull.Edge);
            totalSize += tempHull.FaceLinks.Length * sizeof(ConvexHull.Edge);
            ConvexCollider* collider = (ConvexCollider*)UnsafeUtility.Malloc(totalSize, 16, Allocator.Temp);

            // Initialize it
            {
                UnsafeUtility.MemClear(collider, totalSize);
                collider->MemorySize = totalSize;

                collider->m_Header.Type = ColliderType.Convex;
                collider->m_Header.CollisionType = CollisionType.Convex;
                collider->m_Header.Version = 0;
                collider->m_Header.Magic = 0xff;
                collider->m_Header.ForceUniqueBlobID = forceUniqueBlobID;
                collider->m_Header.Filter = filter;
                collider->m_Header.Material = material;

                ref var hull = ref collider->ConvexHull;

                hull.ConvexRadius = convexRadius;

                // Initialize blob arrays
                {
                    byte* end = (byte*)collider + UnsafeUtility.SizeOf<ConvexCollider>();

                    hull.VerticesBlob.Offset = UnsafeEx.CalculateOffset(end, ref hull.VerticesBlob);
                    hull.VerticesBlob.Length = tempHull.Vertices.Length;
                    end += sizeof(float3) * tempHull.Vertices.Length;

                    end = (byte*)Math.NextMultipleOf16((ulong)end); // planes currently must be aligned for Havok

                    hull.FacePlanesBlob.Offset = UnsafeEx.CalculateOffset(end, ref hull.FacePlanesBlob);
                    hull.FacePlanesBlob.Length = tempHull.Planes.Length;
                    end += sizeof(Plane) * tempHull.Planes.Length;

                    hull.FacesBlob.Offset = UnsafeEx.CalculateOffset(end, ref hull.FacesBlob);
                    hull.FacesBlob.Length = tempHull.Faces.Length;
                    end += sizeof(ConvexHull.Face) * tempHull.Faces.Length;

                    hull.FaceVertexIndicesBlob.Offset = UnsafeEx.CalculateOffset(end, ref hull.FaceVertexIndicesBlob);
                    hull.FaceVertexIndicesBlob.Length = tempHull.FaceVertexIndices.Length;
                    end += sizeof(byte) * tempHull.FaceVertexIndices.Length;

                    hull.VertexEdgesBlob.Offset = UnsafeEx.CalculateOffset(end, ref hull.VertexEdgesBlob);
                    hull.VertexEdgesBlob.Length = tempHull.VertexEdges.Length;
                    end += sizeof(ConvexHull.Edge) * tempHull.VertexEdges.Length;

                    hull.FaceLinksBlob.Offset = UnsafeEx.CalculateOffset(end, ref hull.FaceLinksBlob);
                    hull.FaceLinksBlob.Length = tempHull.FaceLinks.Length;
                    end += sizeof(ConvexHull.Edge) * tempHull.FaceLinks.Length;
                }

                // Fill blob arrays
                {
                    for (int i = 0; i < tempHull.Vertices.Length; i++)
                    {
                        hull.Vertices[i] = tempHull.Vertices[i];
                        hull.VertexEdges[i] = tempHull.VertexEdges[i];
                    }

                    for (int i = 0; i < tempHull.Faces.Length; i++)
                    {
                        hull.Planes[i] = tempHull.Planes[i];
                        hull.Faces[i] = tempHull.Faces[i];
                    }

                    for (int i = 0; i < tempHull.FaceVertexIndices.Length; i++)
                    {
                        hull.FaceVertexIndices[i] = tempHull.FaceVertexIndices[i];
                        hull.FaceLinks[i] = tempHull.FaceLinks[i];
                    }
                }

                // Fill mass properties
                {
                    // Build the mass properties if they haven't been computed already.
                    if (builder.HullMassProperties.Volume == 0.0f)
                    {
                        builder.UpdateHullMassProperties();
                    }

                    var massProperties = builder.HullMassProperties;
                    Math.DiagonalizeSymmetricApproximation(massProperties.InertiaTensor, out float3x3 orientation, out float3 inertia);

                    float maxLengthSquared = 0.0f;
                    for (int v = 0, count = hull.Vertices.Length; v < count; ++v)
                    {
                        maxLengthSquared = math.max(maxLengthSquared, math.lengthsq(hull.Vertices[v] - massProperties.CenterOfMass));
                    }

                    collider->MassProperties = new MassProperties
                    {
                        MassDistribution = new MassDistribution
                        {
                            Transform = new RigidTransform(orientation, massProperties.CenterOfMass),
                            InertiaTensor = inertia
                        },
                        Volume = massProperties.Volume,
                        AngularExpansionFactor = math.sqrt(maxLengthSquared)
                    };
                }
            }

            // Copy it into blob
            var asset = BlobAssetReference<Collider>.Create(collider, totalSize);
            var convexCollider = (ConvexCollider*)asset.GetUnsafePtr();
            SafetyChecks.Check16ByteAlignmentAndThrow(convexCollider->ConvexHull.PlanesPtr, "ConvexHull.PlanesPtr");

            UnsafeUtility.Free(collider, Allocator.Temp);
            return asset;
        }

        // Temporary hull of managed arrays, used during construction
        unsafe struct TempHull
        {
            public readonly NativeList<float3> Vertices;
            public readonly NativeList<Plane> Planes;
            public readonly NativeList<ConvexHull.Face> Faces;
            public readonly NativeList<byte> FaceVertexIndices;
            public readonly NativeList<ConvexHull.Edge> VertexEdges;
            public readonly NativeList<ConvexHull.Edge> FaceLinks;

            public TempHull(ref ConvexHullBuilder builder)
            {
                Vertices = new NativeList<float3>(builder.Vertices.PeakCount, Allocator.Temp);
                Faces = new NativeList<ConvexHull.Face>(builder.NumFaces, Allocator.Temp);
                Planes = new NativeList<Plane>(builder.NumFaces, Allocator.Temp);
                FaceVertexIndices = new NativeList<byte>(builder.NumFaceVertices, Allocator.Temp);
                VertexEdges = new NativeList<ConvexHull.Edge>(builder.Vertices.PeakCount, Allocator.Temp);
                FaceLinks = new NativeList<ConvexHull.Edge>(builder.NumFaceVertices, Allocator.Temp);

                // Copy the vertices
                var vertexIndexMap = new NativeArray<byte>(builder.Vertices.PeakCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                foreach (int i in builder.Vertices.Indices)
                {
                    vertexIndexMap[i] = (byte)Vertices.Length;
                    Vertices.Add(builder.Vertices[i].Position);
                    VertexEdges.Add(new ConvexHull.Edge());  // filled below
                }

                // Copy the faces
                switch (builder.Dimension)
                {
                    case 3:
                    {
                        var edgeMap = new NativeParallelHashMap<ConvexHull.Edge, ConvexHull.Edge>(builder.NumFaceVertices, Allocator.Temp);
                        for (ConvexHullBuilder.FaceEdge hullFace = builder.GetFirstFace(); hullFace.IsValid; hullFace = builder.GetNextFace(hullFace))
                        {
                            // Store the plane
                            ConvexHullBuilder.Edge firstEdge = hullFace;
                            Plane facePlane = builder.Planes[builder.Triangles[firstEdge.TriangleIndex].FaceIndex];
                            Planes.Add(facePlane);

                            // Walk the face's outer vertices & edges
                            short firstVertexIndex = (short)FaceVertexIndices.Length;
                            byte numEdges = 0;
                            float maxCosAngle = -1.0f;
                            for (ConvexHullBuilder.FaceEdge edge = hullFace; edge.IsValid; edge = builder.GetNextFaceEdge(edge))
                            {
                                byte vertexIndex = vertexIndexMap[builder.StartVertex(edge)];
                                FaceVertexIndices.Add(vertexIndex);

                                var hullEdge = new ConvexHull.Edge { FaceIndex = (short)edge.Current.TriangleIndex, EdgeIndex = (byte)edge.Current.EdgeIndex };             // will be mapped to the output hull below
                                edgeMap.TryAdd(hullEdge, new ConvexHull.Edge { FaceIndex = (short)Faces.Length, EdgeIndex = numEdges });

                                VertexEdges[vertexIndex] = hullEdge;

                                ConvexHullBuilder.Edge linkedEdge = builder.GetLinkedEdge(edge);
                                FaceLinks.Add(new ConvexHull.Edge { FaceIndex = (short)linkedEdge.TriangleIndex, EdgeIndex = (byte)linkedEdge.EdgeIndex });             // will be mapped to the output hull below

                                ConvexHullBuilder.Triangle linkedTriangle = builder.Triangles[linkedEdge.TriangleIndex];
                                Plane linkedPlane = builder.Planes[linkedTriangle.FaceIndex];
                                maxCosAngle = math.max(maxCosAngle, math.dot(facePlane.Normal, linkedPlane.Normal));

                                numEdges++;
                            }
                            Assert.IsTrue(numEdges >= 3);

                            // Store the face
                            Faces.Add(new ConvexHull.Face
                            {
                                FirstIndex = firstVertexIndex,
                                NumVertices = numEdges,
                                MinHalfAngle = math.acos(maxCosAngle) * 0.5f
                            });
                        }

                        // Remap the edges
                        {
                            for (int i = 0; i < VertexEdges.Length; i++)
                            {
                                edgeMap.TryGetValue(VertexEdges[i], out ConvexHull.Edge vertexEdge);
                                VertexEdges[i] = vertexEdge;
                            }

                            for (int i = 0; i < FaceLinks.Length; i++)
                            {
                                edgeMap.TryGetValue(FaceLinks[i], out ConvexHull.Edge faceLink);
                                FaceLinks[i] = faceLink;
                            }
                        }

                        break;
                    }

                    case 2:
                    {
                        // Make face vertices and edges
                        for (byte i = 0; i < Vertices.Length; i++)
                        {
                            FaceVertexIndices.Add(i);
                            VertexEdges.Add(new ConvexHull.Edge
                            {
                                FaceIndex = 0,
                                EdgeIndex = i
                            });
                            FaceLinks.Add(new ConvexHull.Edge
                            {
                                FaceIndex = 1,
                                EdgeIndex = (byte)(Vertices.Length - 1 - i)
                            });
                        }

                        for (byte i = 0; i < Vertices.Length; i++)
                        {
                            FaceVertexIndices.Add((byte)(Vertices.Length - 1 - i));
                            FaceLinks.Add(VertexEdges[i]);
                        }

                        // Make planes and faces
                        float3 normal;
                        {
                            float3 edge0 = Vertices[1] - Vertices[0];
                            float3 cross = float3.zero;
                            for (int i = 2; i < Vertices.Length; i++)
                            {
                                cross = math.cross(edge0, Vertices[i] - Vertices[0]);
                                if (math.lengthsq(cross) > 1e-8f)             // take the first cross product good enough to calculate a normal
                                {
                                    break;
                                }
                            }
                            normal = math.normalizesafe(cross, new float3(1, 0, 0));
                        }
                        float distance = math.dot(normal, Vertices[0]);
                        Planes.Add(new Plane(normal, -distance));
                        Planes.Add(Planes[0].Flipped);
                        Faces.Add(new ConvexHull.Face
                        {
                            FirstIndex = 0,
                            NumVertices = (byte)Vertices.Length,
                            MinHalfAngleCompressed = 255
                        });
                        Faces.Add(new ConvexHull.Face
                        {
                            FirstIndex = (byte)Vertices.Length,
                            NumVertices = (byte)Vertices.Length,
                            MinHalfAngleCompressed = 255
                        });

                        break;
                    }

                    default: break; // nothing to do for lower-dimensional hulls
                }
            }
        }

        #endregion

        #region IConvexCollider

        /// <summary>   Gets the type. </summary>
        ///
        /// <value> The type. </value>
        public ColliderType Type => m_Header.Type;

        /// <summary>   Gets the collision type. </summary>
        ///
        /// <value> The collision type. </value>
        public CollisionType CollisionType => m_Header.CollisionType;

        /// <summary>   Gets the memory size of this collider. </summary>
        ///
        /// <value> The memory size of this collider. </value>
        public int MemorySize { get; private set; }

        /// <summary>   Gets the collision filter. </summary>
        ///
        /// <returns>   The collision filter. </returns>
        public CollisionFilter GetCollisionFilter() => m_Header.Filter;

        /// <summary>   Sets the collision filter. </summary>
        ///
        /// <param name="filter">   Specifies the filter. </param>
        public void SetCollisionFilter(CollisionFilter filter)
        {
            if (!m_Header.Filter.Equals(filter)) { m_Header.Version++; m_Header.Filter = filter; }
        }

        internal bool RespondsToCollision => m_Header.Material.CollisionResponse != CollisionResponsePolicy.None;

        /// <summary>   Gets or sets the material. </summary>
        ///
        /// <value> The material. </value>
        public Material Material { get => m_Header.Material; set { if (!m_Header.Material.Equals(value)) { m_Header.Version++; m_Header.Material = value; } } }

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties { get; private set; }

        internal void SetMaterialField(Material material, Material.MaterialField option)
        {
            m_Header.Version++;
            m_Header.Material.SetMaterialField(material, option);
        }

        internal float CalculateBoundingRadius(float3 pivot)
        {
            return ConvexHull.CalculateBoundingRadius(pivot);
        }

        /// <summary>   Calculates the aabb. </summary>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb()
        {
            return CalculateAabb(RigidTransform.identity);
        }

        /// <summary>   Calculates the aabb. </summary>
        ///
        /// <param name="transform">    The transform. </param>
        /// <param name="uniformScale"> (Optional) The uniform scale. </param>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb(RigidTransform transform, float uniformScale = 1.0f)
        {
            BlobArray.Accessor<float3> vertices = ConvexHull.Vertices;
            float3 min = math.rotate(transform, vertices[0]);
            float3 max = min;

            for (int i = 1; i < vertices.Length; ++i)
            {
                float3 v = math.rotate(transform, vertices[i]);
                min = math.min(min, v);
                max = math.max(max, v);
            }

            min = (min - new float3(ConvexHull.ConvexRadius)) * uniformScale + transform.pos;
            max = (max + new float3(ConvexHull.ConvexRadius)) * uniformScale + transform.pos;

            return new Aabb
            {
                Min = math.min(min, max),
                Max = math.max(min, max)
            };
        }

        /// <summary>   Cast a ray against this collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input) => QueryWrappers.RayCast(in this, input);

        /// <summary>   Cast ray. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input, out RaycastHit closestHit) => QueryWrappers.RayCast(in this, input, out closestHit);

        /// <summary>   Cast ray. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input, ref NativeList<RaycastHit> allHits) => QueryWrappers.RayCast(in this, input, ref allHits);

        /// <summary>   Cast ray. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay<T>(RaycastInput input, ref T collector) where T : struct, ICollector<RaycastHit>
        {
            unsafe
            {
                fixed(ConvexCollider* target = &this)
                {
                    return RaycastQueries.RayCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        /// <summary>   Cast another collider against this one. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input) => QueryWrappers.ColliderCast(in this, input);

        /// <summary>   Cast collider. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input, out ColliderCastHit closestHit) => QueryWrappers.ColliderCast(in this, input, out closestHit);

        /// <summary>   Cast collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input, ref NativeList<ColliderCastHit> allHits) => QueryWrappers.ColliderCast(in this, input, ref allHits);

        /// <summary>   Cast collider. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider<T>(ColliderCastInput input, ref T collector) where T : struct, ICollector<ColliderCastHit>
        {
            unsafe
            {
                fixed(ConvexCollider* target = &this)
                {
                    return ColliderCastQueries.ColliderCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        /// <summary>   Calculate the distance from a point to this collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input) => QueryWrappers.CalculateDistance(in this, input);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input, out DistanceHit closestHit) => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input, ref NativeList<DistanceHit> allHits) => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance<T>(PointDistanceInput input, ref T collector) where T : struct, ICollector<DistanceHit>
        {
            unsafe
            {
                fixed(ConvexCollider* target = &this)
                {
                    return DistanceQueries.PointCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        /// <summary>   Calculate the distance from another collider to this one. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input) => QueryWrappers.CalculateDistance(in this, input);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input, out DistanceHit closestHit) => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input, ref NativeList<DistanceHit> allHits) => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance<T>(ColliderDistanceInput input, ref T collector) where T : struct, ICollector<DistanceHit>
        {
            unsafe
            {
                fixed(ConvexCollider* target = &this)
                {
                    return DistanceQueries.ColliderCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        #region GO API Queries

        /// <summary>  Checks if a sphere overlaps this collider. </summary>
        ///
        /// <param name="position">         The position. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CheckSphere(float3 position, float radius, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CheckSphere(in this, position, radius, filter, queryInteraction);

        /// <summary>   Overlap sphere. </summary>
        ///
        /// <param name="position">         The position. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapSphere(float3 position, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.OverlapSphere(in this, position, radius, ref outHits, filter, queryInteraction);

        /// <summary>   Overlap sphere custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="position">         The position. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapSphereCustom<T>(float3 position, float radius, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>
            => QueryWrappers.OverlapSphereCustom(in this, position, radius, ref collector, filter, queryInteraction);

        /// <summary>   Check capsule. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CheckCapsule(float3 point1, float3 point2, float radius, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CheckCapsule(in this, point1, point2, radius, filter, queryInteraction);

        /// <summary>   Overlap capsule. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapCapsule(float3 point1, float3 point2, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.OverlapCapsule(in this, point1, point2, radius, ref outHits, filter, queryInteraction);

        /// <summary>   Overlap capsule custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapCapsuleCustom<T>(float3 point1, float3 point2, float radius, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>
            => QueryWrappers.OverlapCapsuleCustom(in this, point1, point2, radius, ref collector, filter, queryInteraction);

        /// <summary>   Check box. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CheckBox(float3 center, quaternion orientation, float3 halfExtents, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CheckBox(in this, center, orientation, halfExtents, filter, queryInteraction);

        /// <summary>   Overlap box. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapBox(float3 center, quaternion orientation, float3 halfExtents, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.OverlapBox(in this, center, orientation, halfExtents, ref outHits, filter, queryInteraction);

        /// <summary>   Overlap box custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool OverlapBoxCustom<T>(float3 center, quaternion orientation, float3 halfExtents, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>
            => QueryWrappers.OverlapBoxCustom(in this, center, orientation, halfExtents, ref collector, filter, queryInteraction);

        /// <summary>   Sphere cast. </summary>
        ///
        /// <param name="origin">           The origin. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.SphereCast(in this, origin, radius, direction, maxDistance, filter, queryInteraction);

        /// <summary>   Sphere cast. </summary>
        ///
        /// <param name="origin">           The origin. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="hitInfo">          [out] Information describing the hit. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.SphereCast(in this, origin, radius, direction, maxDistance, out hitInfo, filter, queryInteraction);

        /// <summary>   Sphere cast all. </summary>
        ///
        /// <param name="origin">           The origin. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool SphereCastAll(float3 origin, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.SphereCastAll(in this, origin, radius, direction, maxDistance, ref outHits, filter, queryInteraction);

        /// <summary>   Sphere cast custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="origin">           The origin. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool SphereCastCustom<T>(float3 origin, float radius, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>
            => QueryWrappers.SphereCastCustom(in this, origin, radius, direction, maxDistance, ref collector, filter, queryInteraction);

        /// <summary>   Box cast. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.BoxCast(in this, center, orientation, halfExtents, direction, maxDistance, filter, queryInteraction);

        /// <summary>   Box cast. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="hitInfo">          [out] Information describing the hit. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.BoxCast(in this, center, orientation, halfExtents, direction, maxDistance, out hitInfo, filter, queryInteraction);

        /// <summary>   Box cast all. </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool BoxCastAll(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.BoxCastAll(in this, center, orientation, halfExtents, direction, maxDistance, ref outHits, filter, queryInteraction);

        /// <summary>   Box cast custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool BoxCastCustom<T>(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>
            => QueryWrappers.BoxCastCustom(in this, center, orientation, halfExtents, direction, maxDistance, ref collector, filter, queryInteraction);

        /// <summary>   Capsule cast. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CapsuleCast(in this, point1, point2, radius, direction, maxDistance, filter, queryInteraction);

        /// <summary>   Capsule cast. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="hitInfo">          [out] Information describing the hit. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CapsuleCast(in this, point1, point2, radius, direction, maxDistance, out hitInfo, filter, queryInteraction);

        /// <summary>   Capsule cast all. </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CapsuleCastAll(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CapsuleCastAll(in this, point1, point2, radius, direction, maxDistance, ref outHits, filter, queryInteraction);

        /// <summary>   Capsule cast custom. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CapsuleCastCustom<T>(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>
            => QueryWrappers.CapsuleCastCustom(in this, point1, point2, radius, direction, maxDistance, ref collector, filter, queryInteraction);

        #endregion

        #endregion
    }
}
