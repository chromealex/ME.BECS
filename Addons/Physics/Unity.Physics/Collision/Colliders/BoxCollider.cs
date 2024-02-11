using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   A box geometry. </summary>
    public struct BoxGeometry : IEquatable<BoxGeometry>
    {
        /// <summary>   The center of the box. </summary>
        ///
        /// <value> The center. </value>
        public float3 Center { get => m_Center; set => m_Center = value; }
        float3 m_Center;

        /// <summary>   The orientation of the box. </summary>
        ///
        /// <value> The orientation. </value>
        public quaternion Orientation { get => m_Orientation; set => m_Orientation = value; }
        private quaternion m_Orientation;

        /// <summary>   The length of each side of the box. </summary>
        ///
        /// <value> The size. </value>
        public float3 Size { get => m_Size; set => m_Size = value; }
        private float3 m_Size;

        /// <summary>
        /// The radius by which to round off the edges of the box. This helps to optimize collision
        /// detection performance, by reducing the likelihood of the inner hull being penetrated and
        /// incurring expensive collision algorithms.
        /// </summary>
        ///
        /// <value> The bevel radius. </value>
        public float BevelRadius { get => m_BevelRadius; set => m_BevelRadius = value; }
        private float m_BevelRadius;

        /// <summary>   Tests if this BoxGeometry is considered equal to another. </summary>
        ///
        /// <param name="other">    The box geometry to compare to this object. </param>
        ///
        /// <returns>   True if the objects are considered equal, false if they are not. </returns>
        public bool Equals(BoxGeometry other)
        {
            return m_Center.Equals(other.m_Center)
                && m_Orientation.Equals(other.m_Orientation)
                && m_Size.Equals(other.m_Size)
                && m_BevelRadius.Equals(other.m_BevelRadius);
        }

        /// <summary>   Calculates a hash code for this object. </summary>
        ///
        /// <returns>   A hash code for this object. </returns>
        public override int GetHashCode()
        {
            return unchecked((int)math.hash(new uint3(
                math.hash(m_Center),
                math.hash(m_Orientation),
                math.hash(new float4(m_Size, m_BevelRadius))
            )));
        }
    }

    /// <summary>   A collider in the shape of a box. </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct BoxCollider : IConvexCollider
    {
        // Header
        [FieldOffset(0)]
        ConvexColliderHeader m_Header;  // 32 bytes
        [FieldOffset(32)]
        internal ConvexHull ConvexHull; // 52 bytes
        [FieldOffset(84)]
        float3 m_Center;                // 12 bytes

        // Convex hull data
        [StructLayout(LayoutKind.Sequential, Size = 368)]
        struct ConvexHullData
        {
            // Todo: would be nice to use the actual types here but C# only likes fixed arrays of builtin types..

            // FacePlanes memory needs to be 16-byte aligned
            public unsafe fixed byte FacePlanes[sizeof(float) * 4 * 6];       // Plane[6],               96 bytes
            public unsafe fixed byte Vertices[sizeof(float) * 3 * 8];         // float3[8],              96 bytes
            public unsafe fixed byte Faces[4 * 6];                            // ConvexHull.Face[6],     24 bytes
            public unsafe fixed byte FaceVertexIndices[sizeof(byte) * 24];    // byte[24],               24 bytes
            public unsafe fixed byte VertexEdges[4 * 8];                      // ConvexHull.Edge[8],     32 bytes
            public unsafe fixed byte FaceLinks[4 * 24];                       // ConvexHull.Edge[24],    96 bytes
        };

        [FieldOffset(96)]
        ConvexHullData m_ConvexHullData;    // 368 bytes
        [FieldOffset(464)]
        quaternion m_Orientation;           // 16 bytes
        [FieldOffset(480)]
        float3 m_Size;                      // 12 bytes

        /// <summary>   Gets the center. </summary>
        ///
        /// <value> The center. </value>
        public float3 Center => m_Center;

        /// <summary>   Gets the orientation. </summary>
        ///
        /// <value> The orientation. </value>
        public quaternion Orientation => m_Orientation;

        /// <summary>   Gets the size. </summary>
        ///
        /// <value> The size. </value>
        public float3 Size => m_Size;

        /// <summary>   Gets the bevel radius. </summary>
        ///
        /// <value> The bevel radius. </value>
        public float BevelRadius => ConvexHull.ConvexRadius;

        /// <summary>   Gets or sets the geometry. </summary>
        ///
        /// <value> The geometry. </value>
        public BoxGeometry Geometry
        {
            get => new BoxGeometry
            {
                Center = m_Center,
                Orientation = m_Orientation,
                Size = m_Size,
                BevelRadius = ConvexHull.ConvexRadius
            };
            set
            {
                if (!value.Equals(Geometry))
                {
                    SetGeometry(value);
                }
            }
        }

        #region Construction

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(BoxGeometry geometry) =>
            CreateInternal(geometry, CollisionFilter.Default, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        /// <param name="filter">   Specifies the filter. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(BoxGeometry geometry, CollisionFilter filter) =>
            CreateInternal(geometry, filter, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(BoxGeometry geometry, CollisionFilter filter, Material material) =>
            CreateInternal(geometry, filter, material);

        /// <summary>   Initializes the box collider, enables it to be created on stack. </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        public void Initialize(BoxGeometry geometry, CollisionFilter filter, Material material) =>
            InitializeInternal(geometry, filter, material);

        #endregion

        #region Internal Construction

        internal static BlobAssetReference<Collider> CreateInternal(BoxGeometry geometry, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            unsafe
            {
                var collider = default(BoxCollider);
                collider.InitializeInternal(geometry, filter, material, forceUniqueBlobID);
                var blob = BlobAssetReference<Collider>.Create(&collider, sizeof(BoxCollider));
                var boxCollider = (BoxCollider*)blob.GetUnsafePtr();
                SafetyChecks.Check16ByteAlignmentAndThrow(boxCollider->m_ConvexHullData.FacePlanes, nameof(ConvexHullData.FacePlanes));
                return blob;
            }
        }

        void InitializeInternal(BoxGeometry geometry, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            unsafe
            {
                m_Header.Type = ColliderType.Box;
                m_Header.CollisionType = CollisionType.Convex;
                m_Header.Version = 0;
                m_Header.Magic = 0xff;
                m_Header.ForceUniqueBlobID = forceUniqueBlobID;
                m_Header.Filter = filter;
                m_Header.Material = material;

                // Build immutable convex data
                fixed(BoxCollider* collider = &this)
                {
                    ConvexHull.VerticesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.Vertices[0], ref ConvexHull.VerticesBlob);
                    ConvexHull.VerticesBlob.Length = 8;

                    ConvexHull.FacePlanesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.FacePlanes[0], ref ConvexHull.FacePlanesBlob);
                    ConvexHull.FacePlanesBlob.Length = 6;

                    ConvexHull.FacesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.Faces[0], ref ConvexHull.FacesBlob.Offset);
                    ConvexHull.FacesBlob.Length = 6;

                    ConvexHull.FaceVertexIndicesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.FaceVertexIndices[0], ref ConvexHull.FaceVertexIndicesBlob);
                    ConvexHull.FaceVertexIndicesBlob.Length = 24;

                    ConvexHull.VertexEdgesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.VertexEdges[0], ref ConvexHull.VertexEdgesBlob);
                    ConvexHull.VertexEdgesBlob.Length = 8;

                    ConvexHull.FaceLinksBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.FaceLinks[0], ref ConvexHull.FaceLinksBlob);
                    ConvexHull.FaceLinksBlob.Length = 24;

                    ConvexHull.Face* faces = (ConvexHull.Face*)(&collider->m_ConvexHullData.Faces[0]);
                    faces[0] = new ConvexHull.Face { FirstIndex = 0, NumVertices = 4, MinHalfAngleCompressed = 0x80 };
                    faces[1] = new ConvexHull.Face { FirstIndex = 4, NumVertices = 4, MinHalfAngleCompressed = 0x80 };
                    faces[2] = new ConvexHull.Face { FirstIndex = 8, NumVertices = 4, MinHalfAngleCompressed = 0x80 };
                    faces[3] = new ConvexHull.Face { FirstIndex = 12, NumVertices = 4, MinHalfAngleCompressed = 0x80 };
                    faces[4] = new ConvexHull.Face { FirstIndex = 16, NumVertices = 4, MinHalfAngleCompressed = 0x80 };
                    faces[5] = new ConvexHull.Face { FirstIndex = 20, NumVertices = 4, MinHalfAngleCompressed = 0x80 };

                    byte* index = &collider->m_ConvexHullData.FaceVertexIndices[0];
                    // stackalloc short* instead of byte* because packing size 1 not supported by Burst
                    short* faceVertexIndices = stackalloc short[24] { 2, 6, 4, 0, 1, 5, 7, 3, 1, 0, 4, 5, 7, 6, 2, 3, 3, 2, 0, 1, 7, 5, 4, 6 };
                    for (int i = 0; i < 24; i++)
                    {
                        *index++ = (byte)faceVertexIndices[i];
                    }

                    ConvexHull.Edge* vertexEdge = (ConvexHull.Edge*)(&collider->m_ConvexHullData.VertexEdges[0]);
                    short* vertexEdgeValuePairs = stackalloc short[16] { 4, 2, 2, 0, 4, 1, 4, 0, 5, 2, 5, 1, 0, 1, 5, 0 };
                    for (int i = 0; i < 8; i++)
                    {
                        *vertexEdge++ = new ConvexHull.Edge
                        {
                            FaceIndex = vertexEdgeValuePairs[2 * i],
                            EdgeIndex = (byte)vertexEdgeValuePairs[2 * i + 1]
                        };
                    }

                    ConvexHull.Edge* faceLink = (ConvexHull.Edge*)(&collider->m_ConvexHullData.FaceLinks[0]);
                    short* faceLinkValuePairs = stackalloc short[48]
                    {
                        3,
                        1,
                        5,
                        2,
                        2,
                        1,
                        4,
                        1,
                        2,
                        3,
                        5,
                        0,
                        3,
                        3,
                        4,
                        3,
                        4,
                        2,
                        0,
                        2,
                        5,
                        1,
                        1,
                        0,
                        5,
                        3,
                        0,
                        0,
                        4,
                        0,
                        1,
                        2,
                        3,
                        2,
                        0,
                        3,
                        2,
                        0,
                        1,
                        3,
                        1,
                        1,
                        2,
                        2,
                        0,
                        1,
                        3,
                        0
                    };
                    for (int i = 0; i < 24; i++)
                    {
                        *faceLink++ = new ConvexHull.Edge
                        {
                            FaceIndex = faceLinkValuePairs[2 * i],
                            EdgeIndex = (byte)faceLinkValuePairs[2 * i + 1]
                        };
                    }
                }

                // Build mutable convex data
                SetGeometry(geometry);
            }
        }

        unsafe void SetGeometry(BoxGeometry geometry)
        {
            SafetyChecks.CheckValidAndThrow(geometry, nameof(geometry));

            m_Header.Version++;
            ConvexHull.ConvexRadius = geometry.BevelRadius;
            m_Center = geometry.Center;
            m_Orientation = geometry.Orientation;
            m_Size = geometry.Size;

            fixed(BoxCollider* collider = &this)
            {
                var transform = new RigidTransform(m_Orientation, m_Center);

                // Clamp to avoid extents < 0
                float3 he = math.max(0, m_Size * 0.5f - ConvexHull.ConvexRadius); // half extents

                float3* vertices = (float3*)(&collider->m_ConvexHullData.Vertices[0]);
                vertices[0] = math.transform(transform, new float3(he.x, he.y, he.z));
                vertices[1] = math.transform(transform, new float3(-he.x, he.y, he.z));
                vertices[2] = math.transform(transform, new float3(he.x, -he.y, he.z));
                vertices[3] = math.transform(transform, new float3(-he.x, -he.y, he.z));
                vertices[4] = math.transform(transform, new float3(he.x, he.y, -he.z));
                vertices[5] = math.transform(transform, new float3(-he.x, he.y, -he.z));
                vertices[6] = math.transform(transform, new float3(he.x, -he.y, -he.z));
                vertices[7] = math.transform(transform, new float3(-he.x, -he.y, -he.z));

                Plane* planes = (Plane*)(&collider->m_ConvexHullData.FacePlanes[0]);
                planes[0] = Math.TransformPlane(transform, new Plane(new float3(1, 0, 0), -he.x));
                planes[1] = Math.TransformPlane(transform, new Plane(new float3(-1, 0, 0), -he.x));
                planes[2] = Math.TransformPlane(transform, new Plane(new float3(0, 1, 0), -he.y));
                planes[3] = Math.TransformPlane(transform, new Plane(new float3(0, -1, 0), -he.y));
                planes[4] = Math.TransformPlane(transform, new Plane(new float3(0, 0, 1), -he.z));
                planes[5] = Math.TransformPlane(transform, new Plane(new float3(0, 0, -1), -he.z));
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

        /// <summary>   Gets the memory size. </summary>
        ///
        /// <value> The size of the memory. </value>
        public int MemorySize => UnsafeUtility.SizeOf<BoxCollider>();

        /// <summary>   Gets or sets the material. </summary>
        ///
        /// <value> The material. </value>
        public Material Material { get => m_Header.Material; set { if (!m_Header.Material.Equals(value)) { m_Header.Version++; m_Header.Material = value; } } }

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

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties => new MassProperties
        {
            MassDistribution = new MassDistribution
            {
                Transform = new RigidTransform(m_Orientation, m_Center),
                InertiaTensor = new float3(
                    (m_Size.y * m_Size.y + m_Size.z * m_Size.z) / 12.0f,
                    (m_Size.x * m_Size.x + m_Size.z * m_Size.z) / 12.0f,
                    (m_Size.x * m_Size.x + m_Size.y * m_Size.y) / 12.0f)
            },
            Volume = m_Size.x * m_Size.y * m_Size.z,
            AngularExpansionFactor = math.length(m_Size * 0.5f - ConvexHull.ConvexRadius)
        };

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
            float3 x = math.mul(m_Orientation, new float3(m_Size.x, 0.0f, 0.0f));
            float3 y = math.mul(m_Orientation, new float3(0.0f, m_Size.y, 0.0f));
            float3 z = math.mul(m_Orientation, new float3(0.0f, 0.0f, m_Size.z));

            float3 halfExtentsInB = (math.abs(x) + math.abs(y) + math.abs(z)) * 0.5f;
            Aabb localAabb = new Aabb
            {
                Min = m_Center - halfExtentsInB,
                Max = m_Center + halfExtentsInB
            };

            return Math.TransformAabb(localAabb, transform, uniformScale);
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
                fixed(BoxCollider* target = &this)
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
                fixed(BoxCollider* target = &this)
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
                fixed(BoxCollider* target = &this)
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
                fixed(BoxCollider* target = &this)
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
