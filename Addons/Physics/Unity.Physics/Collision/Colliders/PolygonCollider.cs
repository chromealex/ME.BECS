using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Entities;

namespace Unity.Physics
{
    /// <summary>
    /// A flat convex collider with either 3 or 4 coplanar vertices (ie, a triangle or a quad)
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct PolygonCollider : IConvexCollider
    {
        // Header
        [FieldOffset(0)]
        ConvexColliderHeader m_Header;  // 32 bytes
        [FieldOffset(32)]
        internal ConvexHull ConvexHull; // 52 bytes

        // Convex hull data
        [StructLayout(LayoutKind.Sequential, Size = 96)]
        struct ConvexHullData
        {
            // Todo: would be nice to use the actual types here but C# only likes fixed arrays of builtin types

            // FacePlanes memory needs to be 16-byte aligned
            public unsafe fixed byte FacePlanes[sizeof(float) * 4 * 2];   // Plane[2],              32 bytes
            public unsafe fixed byte Vertices[sizeof(float) * 3 * 4];     // float3[4],             48 bytes
            public unsafe fixed byte Faces[4 * 2];                        // ConvexHull.Face[2],    8 bytes
            public unsafe fixed byte FaceVertexIndices[8];                // byte[8]                8 bytes
        }

        [FieldOffset(96)]
        ConvexHullData m_ConvexHullData; // 96 bytes

        /// <summary>   Gets a value indicating whether this object is triangle. </summary>
        ///
        /// <value> True if this object is triangle, false if not. </value>
        public bool IsTriangle => Vertices.Length == 3;

        /// <summary>   Gets a value indicating whether this object is quad. </summary>
        ///
        /// <value> True if this object is quad, false if not. </value>
        public bool IsQuad => Vertices.Length == 4;

        /// <summary>   Gets the vertices. </summary>
        ///
        /// <value> The vertices. </value>
        public BlobArray.Accessor<float3> Vertices => ConvexHull.Vertices;

        /// <summary>   Gets the planes. </summary>
        ///
        /// <value> The planes. </value>
        public BlobArray.Accessor<Plane> Planes => ConvexHull.Planes;

        #region Construction

        /// <summary>   Creates a triangle. </summary>
        ///
        /// <param name="vertex0">  The first vertex of the triangle. </param>
        /// <param name="vertex1">  The second vertex of the triangle. </param>
        /// <param name="vertex2">  The third vertex of the triangle. </param>
        ///
        /// <returns>   The new triangle. </returns>
        public static BlobAssetReference<Collider> CreateTriangle(float3 vertex0, float3 vertex1, float3 vertex2) =>
            CreateTriangle(vertex0, vertex1, vertex2, CollisionFilter.Default, Material.Default);

        /// <summary>   Creates a triangle. </summary>
        ///
        /// <param name="vertex0">  The first vertex of the triangle. </param>
        /// <param name="vertex1">  The second vertex of the triangle. </param>
        /// <param name="vertex2">  The third vertex of the triangle. </param>
        /// <param name="filter">   Specifies the filter. </param>
        ///
        /// <returns>   The new triangle. </returns>
        public static BlobAssetReference<Collider> CreateTriangle(float3 vertex0, float3 vertex1, float3 vertex2,
            CollisionFilter filter) => CreateTriangle(vertex0, vertex1, vertex2, filter, Material.Default);

        /// <summary>   Creates a triangle. </summary>
        ///
        /// <param name="vertex0">  The first vertex of the triangle. </param>
        /// <param name="vertex1">  The second vertex of the triangle. </param>
        /// <param name="vertex2">  The third vertex of the triangle. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        ///
        /// <returns>   The new triangle. </returns>
        public static BlobAssetReference<Collider> CreateTriangle(float3 vertex0, float3 vertex1, float3 vertex2, CollisionFilter filter, Material material)
        {
            unsafe
            {
                SafetyChecks.CheckFiniteAndThrow(vertex0, nameof(vertex0));
                SafetyChecks.CheckFiniteAndThrow(vertex1, nameof(vertex1));
                SafetyChecks.CheckFiniteAndThrow(vertex2, nameof(vertex2));

                var collider = new PolygonCollider();
                collider.InitAsTriangle(vertex0, vertex1, vertex2, filter, material);

                var blob = BlobAssetReference<Collider>.Create(&collider, UnsafeUtility.SizeOf<PolygonCollider>());
                var polyCollider = (PolygonCollider*)blob.GetUnsafePtr();
                SafetyChecks.Check16ByteAlignmentAndThrow(polyCollider->m_ConvexHullData.FacePlanes, nameof(ConvexHullData.FacePlanes));
                return blob;
            }
        }

        /// <summary>   Creates a quad from vertices. Quad vertices must be coplanar and wound consistently. </summary>
        ///
        /// <param name="vertex0">  The first vertex of the quad. </param>
        /// <param name="vertex1">  The second vertex of the quad. </param>
        /// <param name="vertex2">  The third vertex of the quad. </param>
        /// <param name="vertex3">  The fourth vertex of the quad. </param>
        ///
        /// <returns>   The new quad. </returns>
        public static BlobAssetReference<Collider> CreateQuad(float3 vertex0, float3 vertex1, float3 vertex2, float3 vertex3) =>
            CreateQuadInternal(vertex0, vertex1, vertex2, vertex3, CollisionFilter.Default, Material.Default);

        /// <summary>   Creates a quad. </summary>
        ///
        /// <param name="vertex0">  The first vertex of the quad. </param>
        /// <param name="vertex1">  The second vertex of the quad. </param>
        /// <param name="vertex2">  The third vertex of the quad. </param>
        /// <param name="vertex3">  The fourth vertex of the quad. </param>
        /// <param name="filter">   Specifies the filter. </param>
        ///
        /// <returns>   The new quad. </returns>
        public static BlobAssetReference<Collider> CreateQuad(float3 vertex0, float3 vertex1, float3 vertex2, float3 vertex3, CollisionFilter filter) =>
            CreateQuadInternal(vertex0, vertex1, vertex2, vertex3, filter, Material.Default);

        /// <summary>   Creates a quad. </summary>
        ///
        /// <param name="vertex0">  The first vertex of the quad. </param>
        /// <param name="vertex1">  The second vertex of the quad. </param>
        /// <param name="vertex2">  The third vertex of the quad. </param>
        /// <param name="vertex3">  The fourth vertex of the quad. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        ///
        /// <returns>   The new quad. </returns>
        public static BlobAssetReference<Collider> CreateQuad(float3 vertex0, float3 vertex1, float3 vertex2, float3 vertex3, CollisionFilter filter, Material material) =>
            CreateQuadInternal(vertex0, vertex1, vertex2, vertex3, filter, material);

        #endregion

        #region Internal Construction

        internal static BlobAssetReference<Collider> CreateQuadInternal(float3 vertex0, float3 vertex1, float3 vertex2, float3 vertex3, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            unsafe
            {
                SafetyChecks.CheckFiniteAndThrow(vertex0, nameof(vertex0));
                SafetyChecks.CheckFiniteAndThrow(vertex1, nameof(vertex1));
                SafetyChecks.CheckFiniteAndThrow(vertex2, nameof(vertex2));
                SafetyChecks.CheckFiniteAndThrow(vertex3, nameof(vertex3));
                SafetyChecks.CheckCoplanarAndThrow(vertex0, vertex1, vertex2, vertex3, nameof(vertex3));

                PolygonCollider collider = default;
                collider.InitAsQuad(vertex0, vertex1, vertex2, vertex3, filter, material, forceUniqueBlobID);
                return BlobAssetReference<Collider>.Create(&collider, UnsafeUtility.SizeOf<PolygonCollider>());
            }
        }

        internal void InitNoVertices(CollisionFilter filter, Material material)
        {
            Init(filter, material);
            ConvexHull.VerticesBlob.Length = 0;
        }

        internal void InitAsTriangle(float3 vertex0, float3 vertex1, float3 vertex2, CollisionFilter filter, Material material)
        {
            Init(filter, material);
            SetAsTriangle(vertex0, vertex1, vertex2);
        }

        internal void InitAsQuad(float3 vertex0, float3 vertex1, float3 vertex2, float3 vertex3, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            Init(filter, material, forceUniqueBlobID);
            SetAsQuad(vertex0, vertex1, vertex2, vertex3);
        }

        internal unsafe void SetAsTriangle(float3 v0, float3 v1, float3 v2)
        {
            m_Header.Type = ColliderType.Triangle;
            m_Header.Version++;

            ConvexHull.VerticesBlob.Length = 3;
            ConvexHull.FaceVertexIndicesBlob.Length = 6;

            fixed(PolygonCollider* collider = &this)
            {
                float3* vertices = (float3*)(&collider->m_ConvexHullData.Vertices[0]);
                vertices[0] = v0;
                vertices[1] = v1;
                vertices[2] = v2;

                ConvexHull.Face* faces = (ConvexHull.Face*)(&collider->m_ConvexHullData.Faces[0]);
                faces[0] = new ConvexHull.Face { FirstIndex = 0, NumVertices = 3, MinHalfAngleCompressed = 0xff };
                faces[1] = new ConvexHull.Face { FirstIndex = 3, NumVertices = 3, MinHalfAngleCompressed = 0xff };

                byte* index = &collider->m_ConvexHullData.FaceVertexIndices[0];
                *index++ = 0; *index++ = 1; *index++ = 2;
                *index++ = 2; *index++ = 1; *index++ = 0;
            }

            SetPlanes();
        }

        internal unsafe void SetAsQuad(float3 v0, float3 v1, float3 v2, float3 v3)
        {
            m_Header.Type = ColliderType.Quad;
            m_Header.Version++;

            ConvexHull.VerticesBlob.Length = 4;
            ConvexHull.FaceVertexIndicesBlob.Length = 8;

            fixed(PolygonCollider* collider = &this)
            {
                float3* vertices = (float3*)(&collider->m_ConvexHullData.Vertices[0]);
                vertices[0] = v0;
                vertices[1] = v1;
                vertices[2] = v2;
                vertices[3] = v3;

                ConvexHull.Face* faces = (ConvexHull.Face*)(&collider->m_ConvexHullData.Faces[0]);
                faces[0] = new ConvexHull.Face { FirstIndex = 0, NumVertices = 4, MinHalfAngleCompressed = 0xff };
                faces[1] = new ConvexHull.Face { FirstIndex = 4, NumVertices = 4, MinHalfAngleCompressed = 0xff };

                byte* index = &collider->m_ConvexHullData.FaceVertexIndices[0];
                *index++ = 0; *index++ = 1; *index++ = 2; *index++ = 3;
                *index++ = 3; *index++ = 2; *index++ = 1; *index++ = 0;
            }

            SetPlanes();
        }

        unsafe void Init(CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            m_Header.CollisionType = CollisionType.Convex;
            m_Header.Version = 0;
            m_Header.Magic = 0xff;
            m_Header.ForceUniqueBlobID = forceUniqueBlobID;
            m_Header.Filter = filter;
            m_Header.Material = material;

            ConvexHull.ConvexRadius = 0.0f;

            fixed(PolygonCollider* collider = &this)
            {
                ConvexHull.VerticesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.Vertices[0], ref ConvexHull.VerticesBlob);
                ConvexHull.VerticesBlob.Length = 4;

                ConvexHull.FacePlanesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.FacePlanes[0], ref ConvexHull.FacePlanesBlob);
                ConvexHull.FacePlanesBlob.Length = 2;

                ConvexHull.FacesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.Faces[0], ref ConvexHull.FacesBlob);
                ConvexHull.FacesBlob.Length = 2;

                ConvexHull.FaceVertexIndicesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.FaceVertexIndices[0], ref ConvexHull.FaceVertexIndicesBlob);
                ConvexHull.FaceVertexIndicesBlob.Length = 8;

                // No connectivity needed
                ConvexHull.VertexEdgesBlob.Offset = 0;
                ConvexHull.VertexEdgesBlob.Length = 0;
                ConvexHull.FaceLinksBlob.Offset = 0;
                ConvexHull.FaceLinksBlob.Length = 0;
            }
        }

        private void SetPlanes()
        {
            BlobArray.Accessor<float3> hullVertices = ConvexHull.Vertices;
            float3 cross = math.cross(
                hullVertices[1] - hullVertices[0],
                hullVertices[2] - hullVertices[0]);
            float dot = math.dot(cross, hullVertices[0]);
            float invLengthCross = math.rsqrt(math.lengthsq(cross));
            Plane plane = new Plane(cross * invLengthCross, -dot * invLengthCross);

            ConvexHull.Planes[0] = plane;
            ConvexHull.Planes[1] = plane.Flipped;
        }

        #endregion

        #region IConvexCollider

        /// <summary>   Gets the Collider type. </summary>
        ///
        /// <value> Collider Type. </value>
        public ColliderType Type => m_Header.Type;

        /// <summary>   Gets the Collision type. </summary>
        ///
        /// <value> The Collision Type. </value>
        public CollisionType CollisionType => m_Header.CollisionType;

        /// <summary>   Gets the memory size. </summary>
        ///
        /// <value> The size of the memory. </value>
        public int MemorySize => UnsafeUtility.SizeOf<PolygonCollider>();

        internal bool RespondsToCollision => m_Header.Material.CollisionResponse != CollisionResponsePolicy.None;

        /// <summary>   Gets or sets the material. </summary>
        ///
        /// <value> The material. </value>
        public Material Material { get => m_Header.Material; set { if (!m_Header.Material.Equals(value)) { m_Header.Version += 1; m_Header.Material = value; } } }

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

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties
        {
            get
            {
                // TODO - the inertia computed here is incorrect. Computing the correct inertia is expensive, so it probably ought to be cached.
                // Note this is only called for top level polygon colliders, not for polygons within a mesh.
                float3 center = (ConvexHull.Vertices[0] + ConvexHull.Vertices[1] + ConvexHull.Vertices[2]) / 3.0f;
                float radiusSq = math.max(math.max(
                    math.lengthsq(ConvexHull.Vertices[0] - center),
                    math.lengthsq(ConvexHull.Vertices[1] - center)),
                    math.lengthsq(ConvexHull.Vertices[2] - center));
                return new MassProperties
                {
                    MassDistribution = new MassDistribution
                    {
                        Transform = new RigidTransform(quaternion.identity, center),
                        InertiaTensor = new float3(2.0f / 5.0f * radiusSq)
                    },
                    Volume = 0,
                    AngularExpansionFactor = math.sqrt(radiusSq)
                };
            }
        }

        /// <summary>   Calculates the aabb. </summary>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb()
        {
            float3 min = math.min(math.min(ConvexHull.Vertices[0], ConvexHull.Vertices[1]), math.min(ConvexHull.Vertices[2], ConvexHull.Vertices[3]));
            float3 max = math.max(math.max(ConvexHull.Vertices[0], ConvexHull.Vertices[1]), math.max(ConvexHull.Vertices[2], ConvexHull.Vertices[3]));
            return new Aabb
            {
                Min = min - new float3(ConvexHull.ConvexRadius),
                Max = max + new float3(ConvexHull.ConvexRadius)
            };
        }

        /// <summary>   Calculates the aabb. </summary>
        ///
        /// <param name="transform">    The transform. </param>
        /// <param name="uniformScale"> (Optional) The uniform scale. </param>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb(RigidTransform transform, float uniformScale = 1.0f)
        {
            float3 v0 = math.rotate(transform, ConvexHull.Vertices[0]);
            float3 v1 = math.rotate(transform, ConvexHull.Vertices[1]);
            float3 v2 = math.rotate(transform, ConvexHull.Vertices[2]);
            float3 v3 = IsQuad ? math.rotate(transform, ConvexHull.Vertices[3]) : v2;

            float3 min = math.min(math.min(v0, v1), math.min(v2, v3));
            float3 max = math.max(math.max(v0, v1), math.max(v2, v3));

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
                fixed(PolygonCollider* target = &this)
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
                fixed(PolygonCollider* target = &this)
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
                fixed(PolygonCollider* target = &this)
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
                fixed(PolygonCollider* target = &this)
                {
                    return DistanceQueries.ColliderCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        #region GO API Queries

        /// <summary>   Checks if a sphere overlaps this collider. </summary>
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
