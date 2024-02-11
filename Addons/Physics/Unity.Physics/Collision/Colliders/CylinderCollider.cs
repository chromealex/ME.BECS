using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   A cylinder geometry. </summary>
    public struct CylinderGeometry : IEquatable<CylinderGeometry>
    {
        /// <summary>   The minimum number of sides. </summary>
        public const int MinSideCount = 3;
        /// <summary>   The maximum number of sides. </summary>
        public const int MaxSideCount = 32;

        /// <summary>   The center of the cylinder. </summary>
        ///
        /// <value> The center. </value>
        public float3 Center { get => m_Center; set => m_Center = value; }
        float3 m_Center;

        /// <summary>   The orientation of the cylinder. </summary>
        ///
        /// <value> The orientation. </value>
        public quaternion Orientation { get => m_Orientation; set => m_Orientation = value; }
        private quaternion m_Orientation;

        /// <summary>   The height of the cylinder along the local Z axis. </summary>
        ///
        /// <value> The height. </value>
        public float Height { get => m_Height; set => m_Height = value; }
        private float m_Height;

        /// <summary>   The radius of the cylinder. </summary>
        ///
        /// <value> The radius. </value>
        public float Radius { get => m_Radius; set => m_Radius = value; }
        private float m_Radius;

        /// <summary>
        /// The radius by which to round off the edges of the cylinder. This helps to optimize collision
        /// detection performance, by reducing the likelihood of the inner hull being penetrated and
        /// incurring expensive collision algorithms.
        /// </summary>
        ///
        /// <value> The bevel radius. </value>
        public float BevelRadius { get => m_BevelRadius; set => m_BevelRadius = value; }
        private float m_BevelRadius;

        /// <summary>   The number of faces used to represent the rounded part of the cylinder. </summary>
        ///
        /// <value> The number of sides. </value>
        public int SideCount { get => m_SideCount; set => m_SideCount = value; }
        private int m_SideCount;

        /// <summary>   Tests if this CylinderGeometry is considered equal to another. </summary>
        ///
        /// <param name="other">    The cylinder geometry to compare to this object. </param>
        ///
        /// <returns>   True if the objects are considered equal, false if they are not. </returns>
        public bool Equals(CylinderGeometry other)
        {
            return m_Center.Equals(other.m_Center)
                && m_Orientation.Equals(other.m_Orientation)
                && m_Height.Equals(other.m_Height)
                && m_Radius.Equals(other.m_Radius)
                && m_BevelRadius.Equals(other.m_BevelRadius)
                && m_SideCount.Equals(other.m_SideCount);
        }

        /// <summary>   Calculates a hash code for this object. </summary>
        ///
        /// <returns>   A hash code for this object. </returns>
        public override int GetHashCode()
        {
            return unchecked((int)math.hash(new uint4(
                math.hash(m_Center),
                math.hash(m_Orientation),
                math.hash(new float3(m_Height, m_Radius, m_BevelRadius)),
                unchecked((uint)m_SideCount)
            )));
        }
    }

    /// <summary>   A collider in the shape of a cylinder. </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct CylinderCollider : IConvexCollider
    {
        // Header
        [FieldOffset(0)]
        ConvexColliderHeader m_Header;  // 32 bytes
        [FieldOffset(32)]
        internal ConvexHull ConvexHull; // 52 bytes

        [FieldOffset(84)]
        float3 m_Center;                // 12 bytes

        // Convex hull data, sized for the maximum allowed number of cylinder faces
        [StructLayout(LayoutKind.Sequential, Size = 1640)]
        struct ConvexHullData
        {
            // Todo: would be nice to use the actual types here but C# only likes fixed arrays of builtin types..

            // FacePlanes memory needs to be 16-byte aligned
            public unsafe fixed byte FacePlanes[sizeof(float) * 4 * (2 + CylinderGeometry.MaxSideCount)];   // 544 bytes
            public unsafe fixed byte Vertices[sizeof(float) * 3 * 2 * CylinderGeometry.MaxSideCount];       // 768 bytes
            public unsafe fixed byte Faces[4 * (2 + CylinderGeometry.MaxSideCount)];                        // 136 bytes
            public unsafe fixed byte FaceVertexIndices[6 * CylinderGeometry.MaxSideCount];                  // 192 bytes
        }

        [FieldOffset(96)]
        ConvexHullData m_ConvexHullData;    // 1640 bytes

        [FieldOffset(1736)]
        quaternion m_Orientation;           // 16 bytes
        [FieldOffset(1752)]
        float m_Height;                     // 4 bytes
        [FieldOffset(1756)]
        float m_Radius;                     // 4 bytes
        [FieldOffset(1760)]
        int m_SideCount;                    // 4 bytes

        [FieldOffset(1764)]
        MassProperties m_MassProperties;    // 4 bytes

        /// <summary>   Gets the center. </summary>
        ///
        /// <value> The center. </value>
        public float3 Center => m_Center;

        /// <summary>   Gets the orientation. </summary>
        ///
        /// <value> The orientation. </value>
        public quaternion Orientation => m_Orientation;

        /// <summary>   Gets the height. </summary>
        ///
        /// <value> The height. </value>
        public float Height => m_Height;

        /// <summary>   Gets the radius. </summary>
        ///
        /// <value> The radius. </value>
        public float Radius => m_Radius;

        /// <summary>   Gets the bevel radius. </summary>
        ///
        /// <value> The bevel radius. </value>
        public float BevelRadius => ConvexHull.ConvexRadius;

        /// <summary>   Gets the number of sides. </summary>
        ///
        /// <value> The number of sides. </value>
        public int SideCount => m_SideCount;

        /// <summary>   Gets or sets the geometry. </summary>
        ///
        /// <value> The geometry. </value>
        public CylinderGeometry Geometry
        {
            get => new CylinderGeometry
            {
                Center = m_Center,
                Orientation = m_Orientation,
                Height = m_Height,
                Radius = m_Radius,
                BevelRadius = ConvexHull.ConvexRadius,
                SideCount = m_SideCount
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
        public static BlobAssetReference<Collider> Create(CylinderGeometry geometry) =>
            Create(geometry, CollisionFilter.Default, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        /// <param name="filter">   Specifies the filter. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(CylinderGeometry geometry, CollisionFilter filter) =>
            Create(geometry, filter, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(CylinderGeometry geometry, CollisionFilter filter,  Material material) =>
            CreateInternal(geometry, filter, material);

        /// <summary>   Initializes the cylinder collider, enables it to be created on stack. </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        public void Initialize(CylinderGeometry geometry, CollisionFilter filter, Material material) =>
            InitializeInternal(geometry, filter, material);

        #endregion

        #region Internal Construction

        internal static BlobAssetReference<Collider> CreateInternal(CylinderGeometry geometry, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            unsafe
            {
                var collider = default(CylinderCollider);
                collider.InitializeInternal(geometry, filter, material, forceUniqueBlobID);
                var blob = BlobAssetReference<Collider>.Create(&collider, sizeof(CylinderCollider));
                var cylCollider = (CylinderCollider*)(blob.GetUnsafePtr());
                SafetyChecks.Check16ByteAlignmentAndThrow(cylCollider->m_ConvexHullData.FacePlanes, nameof(ConvexHullData.FacePlanes));
                return blob;
            }
        }

        void InitializeInternal(CylinderGeometry geometry, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            unsafe
            {
                m_Header.Type = ColliderType.Cylinder;
                m_Header.CollisionType = CollisionType.Convex;
                m_Header.Version = 0;
                m_Header.Magic = 0xff;
                m_Header.ForceUniqueBlobID = forceUniqueBlobID;
                m_Header.Filter = filter;
                m_Header.Material = material;

                // Initialize immutable convex data
                fixed(CylinderCollider* collider = &this)
                {
                    ConvexHull.VerticesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.Vertices[0], ref ConvexHull.VerticesBlob);
                    ConvexHull.VerticesBlob.Length = 0;

                    ConvexHull.FacePlanesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.FacePlanes[0], ref ConvexHull.FacePlanesBlob);
                    ConvexHull.FacePlanesBlob.Length = 0;

                    ConvexHull.FacesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.Faces[0], ref ConvexHull.FacesBlob);
                    ConvexHull.FacesBlob.Length = 0;

                    ConvexHull.FaceVertexIndicesBlob.Offset = UnsafeEx.CalculateOffset(ref collider->m_ConvexHullData.FaceVertexIndices[0], ref ConvexHull.FaceVertexIndicesBlob);
                    ConvexHull.FaceVertexIndicesBlob.Length = 0;

                    // No connectivity
                    ConvexHull.VertexEdgesBlob.Offset = 0;
                    ConvexHull.VertexEdgesBlob.Length = 0;
                    ConvexHull.FaceLinksBlob.Offset = 0;
                    ConvexHull.FaceLinksBlob.Length = 0;
                }

                // Set mutable data
                SetGeometry(geometry);
            }
        }

        unsafe void SetGeometry(CylinderGeometry geometry)
        {
            SafetyChecks.CheckValidAndThrow(geometry, nameof(geometry));

            m_Header.Version++;
            m_Center = geometry.Center;
            m_Orientation = geometry.Orientation;
            m_Height = geometry.Height;
            m_Radius = geometry.Radius;
            m_SideCount = geometry.SideCount;

            ConvexHull.ConvexRadius = geometry.BevelRadius;
            ConvexHull.VerticesBlob.Length = m_SideCount * 2;
            ConvexHull.FacePlanesBlob.Length = m_SideCount + 2;
            ConvexHull.FacesBlob.Length = m_SideCount + 2;
            ConvexHull.FaceVertexIndicesBlob.Length = m_SideCount * 6;

            var transform = new RigidTransform(m_Orientation, m_Center);
            var radius = math.max(m_Radius - ConvexHull.ConvexRadius, 0);
            var halfHeight = math.max(m_Height * 0.5f - ConvexHull.ConvexRadius, 0);

            fixed(CylinderCollider* collider = &this)
            {
                // vertices
                float3* vertices = (float3*)(&collider->m_ConvexHullData.Vertices[0]);
                var arcStep = 2f * (float)math.PI / m_SideCount;
                for (var i = 0; i < m_SideCount; i++)
                {
                    var x = math.cos(arcStep * i) * radius;
                    var y = math.sin(arcStep * i) * radius;
                    vertices[i] = math.transform(transform, new float3(x, y, -halfHeight));
                    vertices[i + m_SideCount] = math.transform(transform, new float3(x, y, halfHeight));
                }

                // planes
                Plane* planes = (Plane*)(&collider->m_ConvexHullData.FacePlanes[0]);
                planes[0] = Math.TransformPlane(transform, new Plane(new float3(0f, 0f, -1f), -halfHeight));
                planes[1] = Math.TransformPlane(transform, new Plane(new float3(0f, 0f, 1f), -halfHeight));
                float d = radius * math.cos((float)math.PI / m_SideCount);
                for (int i = 0; i < m_SideCount; ++i)
                {
                    float angle = 2.0f * (float)math.PI * (i + 0.5f) / m_SideCount;
                    planes[2 + i] = Math.TransformPlane(transform, new Plane(new float3(math.cos(angle), math.sin(angle), 0f), -d));
                }

                // faces
                ConvexHull.Face* faces = (ConvexHull.Face*)(&collider->m_ConvexHullData.Faces[0]);
                byte* indices = (byte*)(&collider->m_ConvexHullData.FaceVertexIndices[0]);
                float halfAngle = (float)math.PI * 0.25f;
                {
                    faces[0].FirstIndex = 0;
                    faces[0].NumVertices = (byte)m_SideCount;
                    faces[0].MinHalfAngle = halfAngle;
                    for (int i = 0; i < m_SideCount; ++i)
                    {
                        indices[i] = (byte)(m_SideCount - 1 - i);
                    }

                    faces[1].FirstIndex = (short)m_SideCount;
                    faces[1].NumVertices = (byte)m_SideCount;
                    faces[1].MinHalfAngle = halfAngle;
                    for (int i = m_SideCount; i < 2 * m_SideCount; ++i)
                    {
                        indices[i] = (byte)(i);
                    }
                }
                halfAngle = (float)math.PI / m_SideCount;
                for (int i = 0; i < m_SideCount; ++i)
                {
                    int firstIndex = (2 * m_SideCount) + (4 * i);

                    faces[i + 2].FirstIndex = (short)firstIndex;
                    faces[i + 2].NumVertices = 4;
                    faces[i + 2].MinHalfAngle = halfAngle;

                    indices[firstIndex + 0] = (byte)i;
                    indices[firstIndex + 1] = (byte)((i + 1) % m_SideCount);
                    indices[firstIndex + 2] = (byte)((i + 1) % m_SideCount + m_SideCount);
                    indices[firstIndex + 3] = (byte)(i + m_SideCount);
                }
            }

            m_MassProperties = new MassProperties
            {
                MassDistribution = new MassDistribution
                {
                    Transform = transform,
                    InertiaTensor = new float3(
                        (m_Radius * m_Radius + m_Height * m_Height) / 12f,
                        (m_Radius * m_Radius + m_Height * m_Height) / 12f,
                        (m_Radius * m_Radius) * 0.5f)
                },
                Volume = (float)math.PI * m_Radius * m_Radius * m_Height,
                AngularExpansionFactor = math.sqrt(radius * radius + halfHeight * halfHeight)
            };
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
        public int MemorySize => UnsafeUtility.SizeOf<CylinderCollider>();

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

        internal bool RespondsToCollision => m_Header.Material.CollisionResponse != CollisionResponsePolicy.None;

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties => m_MassProperties;

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
            unsafe
            {
                UnityEngine.Assertions.Assert.IsTrue(uniformScale != 0.0f);

                transform = math.mul(transform, new RigidTransform(m_Orientation, m_Center * uniformScale));

                // No need to take abs() in case of negative scale
                float scaledHeight = m_Height * uniformScale;

                var halfAxis = math.rotate(transform, new float3(0.0f, 0.0f, scaledHeight * 0.5f));
                float3 v0 = transform.pos + halfAxis;
                float3 v1 = transform.pos - halfAxis;

                var axis = v1 - v0;

                float invAxisLenSq = math.rcp(scaledHeight * scaledHeight);
                float3 root = axis.yzx * axis.yzx + axis.zxy * axis.zxy;
                root *= invAxisLenSq;

                float3 expansion = math.sqrt(root);
                expansion *= m_Radius * math.abs(uniformScale);

                return new Aabb
                {
                    Min = math.min(v0, v1) - expansion,
                    Max = math.max(v0, v1) + expansion
                };
            }
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
                fixed(CylinderCollider* target = &this)
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
                fixed(CylinderCollider* target = &this)
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
                fixed(CylinderCollider* target = &this)
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
                fixed(CylinderCollider* target = &this)
                {
                    return DistanceQueries.ColliderCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        #region GO API Queries

        /// <summary>   Checks if the sphere overlaps this collider. </summary>
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
