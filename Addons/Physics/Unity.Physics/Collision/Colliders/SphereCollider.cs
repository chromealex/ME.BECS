using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   A sphere geometry. </summary>
    public struct SphereGeometry : IEquatable<SphereGeometry>
    {
        /// <summary>   The center of the sphere. </summary>
        ///
        /// <value> The center. </value>
        public float3 Center { get => m_Center; set => m_Center = value; }
        float3 m_Center;

        /// <summary>   The radius of the sphere. </summary>
        ///
        /// <value> The radius. </value>
        public float Radius { get => m_Radius; set => m_Radius = value; }
        private float m_Radius;

        /// <summary>   Tests if this SphereGeometry is considered equal to another. </summary>
        ///
        /// <param name="other">    The sphere geometry to compare to this object. </param>
        ///
        /// <returns>   True if the objects are considered equal, false if they are not. </returns>
        public bool Equals(SphereGeometry other)
        {
            return m_Center.Equals(other.m_Center)
                && m_Radius.Equals(other.m_Radius);
        }

        /// <summary>   Calculates a hash code for this object. </summary>
        ///
        /// <returns>   A hash code for this object. </returns>
        public override int GetHashCode() => unchecked((int)math.hash(new float4(m_Center, m_Radius)));
    }

    /// <summary>   A collider in the shape of a sphere. </summary>
    public struct SphereCollider : IConvexCollider
    {
        // Header
        private ConvexColliderHeader m_Header;
        internal ConvexHull ConvexHull;

        private float3 m_Vertex;

        /// <summary>   Gets the center. </summary>
        ///
        /// <value> The center. </value>
        public float3 Center => m_Vertex;

        /// <summary>   Gets the radius. </summary>
        ///
        /// <value> The radius. </value>
        public float Radius => ConvexHull.ConvexRadius;

        /// <summary>   Gets or sets the geometry. </summary>
        ///
        /// <value> The geometry. </value>
        public SphereGeometry Geometry
        {
            get => new SphereGeometry
            {
                Center = m_Vertex,
                Radius = ConvexHull.ConvexRadius
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
        public static BlobAssetReference<Collider> Create(SphereGeometry geometry) =>
            CreateInternal(geometry, CollisionFilter.Default, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        /// <param name="filter">   Specifies the filter. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(SphereGeometry geometry, CollisionFilter filter) =>
            CreateInternal(geometry, filter, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(SphereGeometry geometry, CollisionFilter filter, Material material) =>
            CreateInternal(geometry, filter, material);

        /// <summary>   Initializes the sphere collider, enables it to be created on stack. </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        public void Initialize(SphereGeometry geometry, CollisionFilter filter, Material material) =>
            InitializeInternal(geometry, filter, material);

        /// <summary>   Sets a geometry. </summary>
        ///
        /// <param name="geometry"> The geometry. </param>
        void SetGeometry(SphereGeometry geometry)
        {
            SafetyChecks.CheckValidAndThrow(geometry, nameof(geometry));

            m_Header.Version++;
            ConvexHull.ConvexRadius = geometry.Radius;
            m_Vertex = geometry.Center;
        }

        #endregion

        #region Internal Construction

        internal static BlobAssetReference<Collider> CreateInternal(SphereGeometry geometry, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            unsafe
            {
                var collider = default(SphereCollider);
                collider.InitializeInternal(geometry, filter, material, forceUniqueBlobID);
                return BlobAssetReference<Collider>.Create(&collider, sizeof(SphereCollider));
            }
        }

        void InitializeInternal(SphereGeometry geometry, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            m_Header.Type = ColliderType.Sphere;
            m_Header.CollisionType = CollisionType.Convex;
            m_Header.Version = 0;
            m_Header.Magic = 0xff;
            m_Header.ForceUniqueBlobID = forceUniqueBlobID;
            m_Header.Filter = filter;
            m_Header.Material = material;

            ConvexHull.VerticesBlob.Offset = UnsafeEx.CalculateOffset(ref m_Vertex, ref ConvexHull.VerticesBlob);
            ConvexHull.VerticesBlob.Length = 1;
            // note: no faces

            SetGeometry(geometry);
        }

        #endregion

        #region IConvexCollider

        /// <summary>   Gets the collider type. </summary>
        ///
        /// <value> Collider Type. </value>
        public ColliderType Type => m_Header.Type;

        /// <summary>   Gets the Collision Type. </summary>
        ///
        /// <value> Collision Type. </value>
        public CollisionType CollisionType => m_Header.CollisionType;

        /// <summary>   Gets the memory size. </summary>
        ///
        /// <value> The size of the memory. </value>
        public int MemorySize => UnsafeUtility.SizeOf<SphereCollider>();

        /// <summary>   Gets or sets the filter. </summary>
        ///
        /// <value> The filter. </value>
        internal bool RespondsToCollision => m_Header.Material.CollisionResponse != CollisionResponsePolicy.None;

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

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties => new MassProperties
        {
            MassDistribution = new MassDistribution
            {
                Transform = new RigidTransform(quaternion.identity, Center),
                InertiaTensor = new float3(2.0f / 5.0f * Radius * Radius)
            },
            Volume = (4.0f / 3.0f) * (float)math.PI * Radius * Radius * Radius,
            AngularExpansionFactor = 0.0f
        };

        /// <summary>   Calculates the aabb. </summary>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb()
        {
            return new Aabb
            {
                Min = Center - new float3(Radius),
                Max = Center + new float3(Radius)
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
            float3 centerInWorld = math.transform(transform, Center * uniformScale);
            float3 scaledRadius = new float3(Radius * math.abs(uniformScale));

            return new Aabb
            {
                Min = centerInWorld - scaledRadius,
                Max = centerInWorld + scaledRadius
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
                fixed(SphereCollider* target = &this)
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
                fixed(SphereCollider* target = &this)
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
                fixed(SphereCollider* target = &this)
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
                fixed(SphereCollider* target = &this)
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
