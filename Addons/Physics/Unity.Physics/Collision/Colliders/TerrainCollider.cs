using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Unity.Physics
{
    /// <summary>
    /// A collider representing a terrain described by a uniform grid of height samples. Warning:
    /// This is just the header, it is followed by variable sized data in memory. Therefore this
    /// struct must always be passed by reference, never by value.
    /// </summary>
    public struct TerrainCollider : ICompositeCollider
    {
        internal ColliderHeader m_Header;
        /// <summary>   The material. </summary>
        public Material Material;

        /// <summary>   Gets the memory size. </summary>
        ///
        /// <value> The size of the memory. </value>
        public int MemorySize { get; private set; }
        internal Terrain Terrain;

        // followed by variable sized terrain data

        #region Construction

        /// <summary>
        /// TerrainCollider offers two methods for rigid body collision with performance / quality
        /// tradeoffs.
        /// </summary>
        public enum CollisionMethod
        {
            /// <summary>
            /// Each vertex of the other shape is projected downward onto the terrain and a contact plane is generated from
            /// the triangle. This is very fast, but it is possible for the other shape's edges and faces to penetrate edges
            /// and vertices of the terrain, or for the other shape to tunnel completely through steep faces in the terrain.
            /// It works best with relatively smooth terrain, and worst with cliffs and sharp peaks.  It also has the trait
            /// that bodies below the terrain but within its AABB will be pushed up, even if they are not touching the
            /// surface triangles.
            /// </summary>
            VertexSamples,

            /// <summary>   In this mode the collision behaves exactly as if it were a MeshCollider. </summary>
            Triangles
        }

        /// <summary>   Create a terrain collider from a grid of heights. </summary>
        ///
        /// <param name="heights">          The heights. </param>
        /// <param name="size">             The size. </param>
        /// <param name="scale">            The scale. </param>
        /// <param name="collisionMethod">  The collision method. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(
            NativeArray<float> heights, int2 size, float3 scale, CollisionMethod collisionMethod
        ) =>
            Create(heights, size, scale, collisionMethod, CollisionFilter.Default, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="heights">          The heights. </param>
        /// <param name="size">             The size. </param>
        /// <param name="scale">            The scale. </param>
        /// <param name="collisionMethod">  The collision method. </param>
        /// <param name="filter">           Specifies the filter. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(
            NativeArray<float> heights, int2 size, float3 scale, CollisionMethod collisionMethod, CollisionFilter filter
        ) =>
            Create(heights, size, scale, collisionMethod, filter, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="heights">          The heights. </param>
        /// <param name="size">             The size. </param>
        /// <param name="scale">            The scale. </param>
        /// <param name="collisionMethod">  The collision method. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="material">         The material. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(
            NativeArray<float> heights, int2 size, float3 scale, CollisionMethod collisionMethod, CollisionFilter filter, Material material
        )
        {
            unsafe
            {
                SafetyChecks.CheckInRangeAndThrow(size.x, new int2(2, int.MaxValue), nameof(size));
                SafetyChecks.CheckInRangeAndThrow(size.y, new int2(2, int.MaxValue), nameof(size));
                SafetyChecks.CheckFiniteAndPositiveAndThrow(scale, nameof(scale));

                // Allocate memory for the collider
                int totalSize = sizeof(TerrainCollider) + Terrain.CalculateDataSize(size);
                var collider = (TerrainCollider*)UnsafeUtility.Malloc(totalSize, 16, Allocator.Temp);
                UnsafeUtility.MemClear(collider, totalSize);

                // Initialize the collider
                collider->m_Header.Type = ColliderType.Terrain;
                collider->m_Header.CollisionType = (collisionMethod == CollisionMethod.Triangles) ? CollisionType.Composite : CollisionType.Terrain;
                collider->m_Header.Version = 1;
                collider->m_Header.Magic = 0xff;
                collider->m_Header.ForceUniqueBlobID = ~ColliderConstants.k_SharedBlobID;
                collider->m_Header.Filter = filter;
                collider->Material = material;
                collider->MemorySize = totalSize;
                collider->Terrain.Init(size, scale, (float*)heights.GetUnsafePtr());

                var blob = BlobAssetReference<Collider>.Create(collider, totalSize);
                UnsafeUtility.Free(collider, Allocator.Temp);
                return blob;
            }
        }

        #endregion

        #region ICompositeCollider

        /// <summary>   Gets the Collider Type. </summary>
        ///
        /// <value> Collider Type. </value>
        public ColliderType Type => m_Header.Type;

        /// <summary>   Gets the collision type </summary>
        ///
        /// <value> Collision type. </value>
        public CollisionType CollisionType => m_Header.CollisionType;

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

        internal bool RespondsToCollision => Material.CollisionResponse != CollisionResponsePolicy.None;

        /// <summary>   Gets the number of collider key bits. </summary>
        ///
        /// <value> The total number of collider key bits. </value>
        public uint NumColliderKeyBits => Terrain.NumColliderKeyBits;
        internal uint TotalNumColliderKeyBits => NumColliderKeyBits;

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties
        {
            get
            {
                // Rough approximation based on AABB
                float3 size = Terrain.Aabb.Extents;
                return new MassProperties
                {
                    MassDistribution = new MassDistribution
                    {
                        Transform = new RigidTransform(quaternion.identity, Terrain.Aabb.Center),
                        InertiaTensor = new float3(
                            (size.y * size.y + size.z * size.z) / 12.0f,
                            (size.x * size.x + size.z * size.z) / 12.0f,
                            (size.x * size.x + size.y * size.y) / 12.0f)
                    },
                    Volume = 0,
                    AngularExpansionFactor = math.length(Terrain.Aabb.Extents) * 0.5f
                };
            }
        }

        /// <summary>   Gets a child of this collider. </summary>
        ///
        /// <param name="key">   [in,out] The key that identifies the child. Gets modified by removing the
        /// bits associated with the child collider. </param>
        /// <param name="child">    [out] The child. </param>
        ///
        /// <returns>   True if there is a child with the specified key, false otherwise. </returns>
        public bool GetChild(ref ColliderKey key, out ChildCollider child)
        {
            if (key.PopSubKey(NumColliderKeyBits, out uint subKey))
            {
                var index = new int2(((int)subKey >> 1) & ((1 << (int)Terrain.NumXBits) - 1), (int)subKey >> ((int)Terrain.NumXBits + 1));
                int triangle = (int)subKey & 1;
                child = Terrain.GetTriangle(index, triangle, GetCollisionFilter(), Material);
                return true;
            }
            else
            {
                child = new ChildCollider();
                return false;
            }
        }

        /// <summary>   Gets a leaf collider. </summary>
        ///
        /// <param name="key">  The key representing the leaf collider. </param>
        /// <param name="leaf"> [out] The leaf. </param>
        ///
        /// <returns>   True if a leaf with the specified key exists, otherwise false. </returns>
        public bool GetLeaf(ColliderKey key, out ChildCollider leaf)
        {
            return GetChild(ref key, out leaf); // all children of TerrainCollider are leaves
        }

        /// <summary>   Gets the leaf colliders of this collider . </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="collector">    [in,out] The collector. </param>
        public void GetLeaves<T>([NoAlias] ref T collector) where T : struct, ILeafColliderCollector
        {
            for (int x = 0; x < Terrain.Size.x - 1; x++)
            {
                for (int y = 0; y < Terrain.Size.y - 1; y++)
                {
                    var index = new int2(x, y);
                    for (int iTriangle = 0; iTriangle < 2; iTriangle++)
                    {
                        ColliderKey key = Terrain.GetColliderKey(index, iTriangle);
                        ChildCollider leaf = Terrain.GetTriangle(index, iTriangle, GetCollisionFilter(), Material);
                        collector.AddLeaf(key, ref leaf);
                    }
                }
            }
        }

        /// <summary>   Calculates the aabb. </summary>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb()
        {
            return Terrain.Aabb;
        }

        /// <summary>   Calculates the aabb. </summary>
        ///
        /// <param name="transform">    The transform. </param>
        /// <param name="uniformScale"> (Optional) The uniform scale. </param>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb(RigidTransform transform, float uniformScale = 1.0f)
        {
            return Math.TransformAabb(Terrain.Aabb, transform, uniformScale);
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
                fixed(TerrainCollider* target = &this)
                {
                    return RaycastQueries.RayCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        internal void SetMaterialField(Material material, Material.MaterialField option)
        {
            m_Header.Version++;
            Material.SetMaterialField(material, option);
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
                fixed(TerrainCollider* target = &this)
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
                fixed(TerrainCollider* target = &this)
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
                fixed(TerrainCollider* target = &this)
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


    // Terrain data.
    // Heights are quantized to a signed 16 bit integer.
    // Four height samples at (i, j), (i + 1, j), (i, j + 1), (i + 1, j + 1) make a quad.
    // The quad is triangulated with an edge connecting (i + 1, j) to (i, j + 1).
    // Queries are accelerated using a tree whose leaves are quads and whose interior nodes each have four equal-sized children.
    // Warning: This is just the header, the terrain's variable sized data follows it in memory.
    // Therefore this struct must always be passed by reference, never by value.
    struct Terrain
    {
        // A quadtree node, storing the Y bounds of four children.
        // XZ bounds can be determined from the node's position.
        public unsafe struct Node
        {
            public fixed short Min[4]; // minimum height of each child
            public fixed short Max[4]; // maximum height of each child

            public int4 Min4
            {
                get => new int4(Min[0], Min[1], Min[2], Min[3]);
                set
                {
                    Min[0] = (short)value.x;
                    Min[1] = (short)value.y;
                    Min[2] = (short)value.z;
                    Min[3] = (short)value.w;
                }
            }

            public int4 Max4
            {
                get => new int4(Max[0], Max[1], Max[2], Max[3]);
                set
                {
                    Max[0] = (short)value.x;
                    Max[1] = (short)value.y;
                    Max[2] = (short)value.z;
                    Max[3] = (short)value.w;
                }
            }
        }

        // Descriptor for one level of the quadtree
        public struct Level
        {
            public int Base;    // Index of the first node
            public int Pitch;   // Number of nodes per row
            public float Scale; // Size of a child of a node in this level
        }

        public Aabb Aabb { get; private set; } // AABB of the terrain in local space
        public int2 Size { get; private set; } // Number of height samples in each dimension

        public float3 Scale { get; private set; } // Transform from quantized heightfield space to local space
        public float3 InverseScale { get; private set; } // 1/Scale

        public uint NumXBits { get; private set; } // Number of bits required to store an X coordinate
        public uint NumColliderKeyBits { get; private set; } // Number of bits required to store a collider key

        // Quantized heights in row-major order (0, 0), (1, 0), ..., (n, 0), (0, 1), (1, 1), ...
        private BlobArray m_HeightsBlob;
        public BlobArray.Accessor<short> Heights => new BlobArray.Accessor<short>(ref m_HeightsBlob);

        // Levels of the quadtree
        // The nodes in the last level each have a 2x2 block of quads as children, the level above that has 2x2 nodes (so 4x4 quads), etc.
        // Top level is 1x1, all other level dimensions are rounded up to a multiple of 2.
        private BlobArray m_LevelsBlob;
        public BlobArray.Accessor<Level> Levels => new BlobArray.Accessor<Level>(ref m_LevelsBlob);

        // Nodes
        // Contains the root node, then all of the nodes for the 2nd level, then all of the nodes for the 3rd level, etc.
        // Node order within each level is swizzled so that each node's children are contiguous in memory, eg. here is a 4x4 level layout
        // with (0, 0) in the top left and (3, 3) in the bottom right:
        //
        //      0  1  4  5
        //      2  3  6  7
        //      8  9  12 13
        //      10 11 14 15
        //
        // Because sizeof(Node) = 64 and nodes are cache aligned, each node is in the same cache line as all of its siblings.
        private BlobArray m_NodesBlob;
        public BlobArray.Accessor<Node> Nodes => new BlobArray.Accessor<Node>(ref m_NodesBlob);

        #region Construction

        // Calculate the number of quadtree nodes and levels required for the given heightfield size
        static internal void CalculateTreeInfo(
            int2 heightfieldSize,
            out int numNodes, out int numLevels,
            out int heightsSize, out int levelsSize, out int nodesSize)
        {
            numNodes = 0;
            numLevels = 0;
            {
                int2 levelSize = heightfieldSize & ~1; // size is number of samples, subtract one to get number of quads then round up to a multiple of two.
                while (true)
                {
                    numLevels++;

                    if (math.all(levelSize <= 2))
                    {
                        // Root level is one node
                        numNodes++;
                        break;
                    }

                    // Level dimensions must be even to fit the swizzling pattern.  Divide by 2, then round up to the next multiple of 2
                    levelSize = (levelSize / 2 + 1) & ~1;
                    numNodes += levelSize.x * levelSize.y;
                }
            }

            heightsSize = Math.NextMultipleOf(heightfieldSize.x * heightfieldSize.y * sizeof(short), 4); // Level needs 4B alignment
            levelsSize = numLevels * UnsafeUtility.SizeOf<Level>();
            nodesSize = numNodes * UnsafeUtility.SizeOf<Node>();
        }

        // Calculate the amount of extra memory needed to store the terrain data for the given heightfield size
        static internal int CalculateDataSize(int2 heightfieldSize)
        {
            CalculateTreeInfo(heightfieldSize, out int numNodes, out int numLevels,
                out int heightsSize, out int levelsSize, out int nodesSize);
            return heightsSize + levelsSize + nodesSize + 64; // + 64 to allow for aligning the nodes to 64 bytes
        }

        // Initialize the terrain.
        // The memory must have been allocated correctly beforehand.
        internal unsafe void Init(int2 size, float3 scale, float* heights)
        {
            int numSamples = size.x * size.y;
            CalculateTreeInfo(size, out int numNodes, out int numLevels,
                out int heightsSize, out int levelsSize, out int nodesSize);

            // Calculate the quantization scale for the heights
            float quantizationFactor;
            {
                float4 maxHeight4 = float4.zero;
                int numSamples4 = numSamples >> 2;
                for (int iHeight = 0; iHeight < numSamples4; iHeight++)
                {
                    float4 height4 = ((float4*)heights)[iHeight];
                    maxHeight4 = math.max(maxHeight4, math.abs(height4));
                }

                float maxHeight = math.cmax(maxHeight4);
                for (int iHeight = numSamples4 << 2; iHeight < numSamples; iHeight++)
                {
                    maxHeight = math.max(maxHeight, heights[iHeight]);
                }

                quantizationFactor = (short.MaxValue - 1) / maxHeight;
            }

            Size = size;
            Scale = new float3(scale.x, scale.y / quantizationFactor, scale.z);
            InverseScale = new float3(1.0f, quantizationFactor, 1.0f) * math.rcp(scale);

            // Calculate the number of shape key bits required to store quad z, x and triangle index.  Size - 2 is the highest possible quad index.
            // We reserve one extra value in the x field to guarantee that all-1s is not needed to refer to any triangle, since it is reserved for invalid.
            NumXBits = 32 - (uint)math.lzcnt((uint)size.x - 1);
            NumColliderKeyBits = 32 - (uint)math.lzcnt((uint)size.y - 2) + NumXBits + 1;

            // Quantize the height data
            byte* end = (byte*)UnsafeUtility.AddressOf(ref this) + sizeof(Terrain);
            short* quantizedHeights = (short*)end;
            for (int iHeight = 0; iHeight < numSamples; iHeight++)
            {
                quantizedHeights[iHeight] = (short)(heights[iHeight] * quantizationFactor + 0.5f);
            }
            m_HeightsBlob.Offset = UnsafeEx.CalculateOffset(end, ref m_HeightsBlob);
            m_HeightsBlob.Length = numSamples;
            end += heightsSize;

            // Allocate space for the quadtree
            m_LevelsBlob.Offset = UnsafeEx.CalculateOffset(end, ref m_LevelsBlob);
            m_LevelsBlob.Length = numLevels;
            end += levelsSize;
            Level* levelData = (Level*)end - 1;

            // Allocate space for nodes, align so that starting from the second level each level is cache aligned
            // Each level's node count is a multiple of four, so they do not need to be aligned individually.
            end = (byte*)Math.NextMultipleOf((ulong)(end + nodesSize), 64);
            Node* root = (Node*)(end - nodesSize);
            m_NodesBlob.Offset = UnsafeEx.CalculateOffset(root, ref m_NodesBlob);
            m_NodesBlob.Length = numNodes;

            // Build the quadtree
            {
                Node* level = (Node*)end;
                int2 levelSize = size & ~1;
                int levelIndex = 0;
                do
                {
                    // Save the last level description
                    int2 lastSize = levelSize;
                    Node* lastLevel = level;

                    // Calculate the current level description
                    if (math.all(levelSize <= 2))
                    {
                        levelSize = 1;
                    }
                    else
                    {
                        levelSize = (levelSize / 2 + 1) & ~1;
                    }
                    level -= levelSize.x * levelSize.y; // pointer to the beginning of the current level's nodes

#if DEVELOPMENT_BUILD
                    SafetyChecks.Check4ByteAlignmentAndThrow(levelData, nameof(levelData));
#endif
                    // Save the current level description for lookup in queries
                    levelData->Base = (int)(level - root);
                    levelData->Pitch = (int)levelSize.x;
                    levelData->Scale = (float)(1 << levelIndex);
                    levelData--;
                    levelIndex++;

                    // Build the level's nodes
                    int2 index = int2.zero;
                    Node* node = level;
                    int4 di = new int4(1, -1, 1, 1);    // swizzling pattern, order in 2x2 blocks
                    int4 dj = new int4(0, 1, 0, -1);
                    while (node != lastLevel)
                    {
                        // Find the min and max height for the node's children
                        int2 childIndex = index + index; // index of the first child in lastLevel
                        if (lastLevel == end)
                        {
                            // Leaf node
                            if (childIndex.x < size.x - 2 && childIndex.y < size.y - 2)
                            {
                                // Load 9 height samples that define the four quads
                                short* base0 = (short*)(quantizedHeights + childIndex.y * size.x + childIndex.x);
                                short* base1 = (short*)(quantizedHeights + (childIndex.y + 1) * size.x + childIndex.x);
                                short* base2 = (short*)(quantizedHeights + (childIndex.y + 2) * size.x + childIndex.x);
                                int3 heights0 = new int3(base0[0], base0[1], base0[2]);
                                int3 heights1 = new int3(base1[0], base1[1], base1[2]);
                                int3 heights2 = new int3(base2[0], base2[1], base2[2]);

                                // Reorder into the four corners of each quad
                                int4 corner0 = new int4(heights0.xy, heights1.xy);
                                int4 corner1 = new int4(heights0.yz, heights1.yz);
                                int4 corner2 = new int4(heights1.xy, heights2.xy);
                                int4 corner3 = new int4(heights1.yz, heights2.yz);

                                // Calculate the min and max height of each quad
                                node->Min4 = math.min(math.min(corner0, corner1), math.min(corner2, corner3));
                                node->Max4 = math.max(math.max(corner0, corner1), math.max(corner2, corner3));
                            }
                            else
                            {
                                // Edge case, slow loop to avoid reading out of bounds
                                node->Min4 = short.MaxValue;
                                node->Max4 = short.MinValue;
                                int leafIndex = 0;
                                for (int j = 0; j < math.min(size.y - childIndex.y - 1, 2); j++)
                                {
                                    for (int i = 0; i < 2; i++)
                                    {
                                        if (i + childIndex.x < size.x - 1)
                                        {
                                            int2 sampleIndex = childIndex + new int2(i, j);
                                            short height0 = quantizedHeights[sampleIndex.y * size.x + sampleIndex.x];
                                            short height1 = quantizedHeights[sampleIndex.y * size.x + sampleIndex.x + 1];
                                            short height2 = quantizedHeights[(sampleIndex.y + 1) * size.x + sampleIndex.x];
                                            short height3 = quantizedHeights[(sampleIndex.y + 1) * size.x + sampleIndex.x + 1];

                                            node->Min[leafIndex] = (short)math.min(math.min(height0, height1), math.min(height2, height3));
                                            node->Max[leafIndex] = (short)math.max(math.max(height0, height1), math.max(height2, height3));
                                        }
                                        leafIndex++;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Interior node
                            if (childIndex.x < lastSize.x && childIndex.y < lastSize.y)
                            {
                                Node* lastNode = lastLevel + index.y * lastSize.x * 2 + index.x * 4;
                                int4x4 minT = math.transpose(new int4x4(lastNode[0].Min4, lastNode[1].Min4, lastNode[2].Min4, lastNode[3].Min4));
                                int4x4 maxT = math.transpose(new int4x4(lastNode[0].Max4, lastNode[1].Max4, lastNode[2].Max4, lastNode[3].Max4));
                                node->Min4 = math.min(math.min(minT.c0, minT.c1), math.min(minT.c2, minT.c3));
                                node->Max4 = math.max(math.max(maxT.c0, maxT.c1), math.max(maxT.c2, maxT.c3));
                            }
                            else
                            {
                                // Padding node with no children on the last level
                                node->Min4 = short.MaxValue;
                                node->Max4 = short.MinValue;
                            }
                        }

                        // Move to the next node in the swizzled order
                        node++;
                        index += new int2(di.x, dj.x);
                        di = di.yzwx;
                        dj = dj.yzwx;
                        if (index.x == levelSize.x)
                        {
                            // End of the current row pair, move to the next row pair
                            index = new int2(0, index.y + 2);
                        }
                    }
                }
                while (math.any(levelSize > 1));
            }

            // Use min and max height of the whole heightfield to build the AABB
            Aabb = new Aabb
            {
                Min = new float3(0, math.cmin(root->Min4), 0) * Scale,
                Max = new float3(size.x - 1, math.cmax(root->Max4), size.y - 1) * Scale
            };
        }

        #endregion

        #region Queries

        // Returns a quantized height sample
        public short GetHeight(int2 index)
        {
            return Heights[index.y * Size.x + index.x];
        }

        // Takes x,z coordinates in local space.
        // If those coordinates are inside the height field, then the height and gradient at those coordinates are set and the method returns true.
        // Otherwise, the method returns false.
        public bool GetHeightAndGradient(float2 position, out float height, out float2 gradient)
        {
            if (math.any(position < float2.zero | position >= Aabb.Max.xz))
            {
                height = 0.0f;
                gradient = float2.zero;
                return false;
            }

            // Find the cell
            float2 coord = position * InverseScale.xz;
            int2 index = (int2)coord;
            float2 fraction0 = coord - (float2)index;
            float2 fraction1 = 1 - fraction0;

            // Get heights of the corners of the cell
            int baseIndex = index.y * Size.x + index.x;
            float4 heights = Scale.y * new float4(Heights[baseIndex], Heights[baseIndex + 1], Heights[baseIndex + Size.x], Heights[baseIndex + Size.x + 1]);

            // Get the height within the cell
            bool triangle0 = fraction0.x < fraction1.y; // Select the triangle within the cell
            float4 triangleHeights = math.select(heights.wwzy, heights.yzxx, triangle0);
            gradient = triangleHeights.zw - triangleHeights.xy; // -(dy/dx, dy/dz)
            float height0 = heights.x - math.dot(gradient, fraction0);
            float height1 = heights.w + math.dot(gradient, fraction1);
            gradient *= InverseScale.xz;
            height = math.select(height1, height0, triangle0);

            return true;
        }

        // Returns a subkey for a triangle
        public uint GetSubKey(int2 index, int triangle)
        {
            Assert.IsTrue(math.all((index >= 0) & (index < Size - 1)) && triangle >= 0 && triangle <= 2);
            return ((uint)index.y << ((int)NumXBits + 1)) | ((uint)index.x << 1) | (uint)triangle;
        }

        // Returns a ColliderKey for a triangle
        public ColliderKey GetColliderKey(int2 index, int triangle)
        {
            return new ColliderKey(NumColliderKeyBits, GetSubKey(index, triangle));
        }

        // Returns a triangle in a ChildCollider
        public ChildCollider GetTriangle(int2 index, int triangle, CollisionFilter filter, Material material)
        {
            int2 index0 = index + new int2(0, triangle);
            int2 index1 = index + new int2(1, 0);
            int2 index2 = index + new int2(triangle, 1);

            return new ChildCollider(
                new float3(index0.x, GetHeight(index0), index0.y) * Scale,
                new float3(index1.x, GetHeight(index1), index1.y) * Scale,
                new float3(index2.x, GetHeight(index2), index2.y) * Scale,
                filter, material);
        }

        // Helper for traversing the quad tree in query implementations
        public unsafe struct QuadTreeWalker
        {
            // Terrain being walked
            readonly Terrain* m_Terrain;

            // Stack of nodes to visit
            const int k_StackSize = 64; // More than enough room for a 65kx65k sample heightfield
            fixed int m_Stack[k_StackSize * 3]; // Levels, then x coordinates, then z coordinates
            int* m_Top; // Pointer to the top element of the levels stack; x and z can be reached by adding k_StackSize

            // Current node state
            int m_LevelIndex;
            int2 m_Index;
            int2 m_ChildIndex;
            public FourTransposedAabbs Bounds { get; private set; } // Child AABBs

            public bool IsLeaf => m_LevelIndex == m_Terrain->Levels.Length - 1;

            public QuadTreeWalker(Terrain* terrain, Aabb aabb)
            {
                m_Terrain = terrain;
                m_LevelIndex = 0;
                m_Index = 0;
                m_ChildIndex = 0;
                Bounds = new FourTransposedAabbs();

                // Initialize the stack with a single node
                fixed(int* stack = m_Stack)
                {
                    m_Top = stack;

                    // Clamp the query AABB to the terrain bounds
                    int2 min = math.clamp((int2)aabb.Min.xz, 0, terrain->Size - 2); // Size - 1 is number of quads, Size - 2 is the highest valid quad index
                    int2 max = math.clamp((int2)aabb.Max.xz, 0, terrain->Size - 2);

                    // Find the deepest node that fully contains the query AABB, by finding the highest bit in which the AABB min and max differ
                    // We can begin the query from that node instead of the root, and this is much faster than traversing the tree.
                    int level = 0;
                    int2 diffs = min ^ max;
                    int diff = diffs.x | diffs.y;
                    int bit = 1 << (terrain->Levels.Length - 1);
                    while ((diff & bit) == 0 && bit > 1)
                    {
                        level++;
                        bit = bit >> 1;
                    }
                    int2 coord = min >> (terrain->Levels.Length - level);

                    // Push the node onto the stack
                    *m_Top = level;
                    *(m_Top + k_StackSize) = coord.x;
                    *(m_Top + k_StackSize + k_StackSize) = coord.y;
                    m_Top++;
                }
            }

            public void Push(bool4 hitMask)
            {
                // Interior node, add hit child nodes to the stack
                int4 childX = m_ChildIndex.x + new int4(0, 1, 0, 1);
                int4 childZ = m_ChildIndex.y + new int4(0, 0, 1, 1);
                int4 hitChildX;
                int4 hitChildZ;
                int hitCount = math.compress((int*)(&hitChildX), 0, childX, hitMask);
                math.compress((int*)(&hitChildZ), 0, childZ, hitMask);
                *((int4*)m_Top) = new int4(m_LevelIndex + 1);
                *((int4*)(m_Top + k_StackSize)) = hitChildX;
                *((int4*)(m_Top + k_StackSize + k_StackSize)) = hitChildZ;
                m_Top += hitCount;
            }

            // If the stack is not empty, removes the top node and sets the current state from it, then returns true.
            // Otherwise returns false.
            public bool Pop()
            {
                fixed(int* stack = m_Stack)
                {
                    if (m_Top == stack)
                    {
                        return false; // stack is empty
                    }
                }

                m_Top--;
                m_LevelIndex = *m_Top;
                m_Index = new int2(*(m_Top + k_StackSize), *(m_Top + k_StackSize + k_StackSize));

                // Get the node
                Level level = m_Terrain->Levels[m_LevelIndex];
                int levelOffset = (m_Index.y & ~1) * level.Pitch + ((m_Index.y & 1) << 1) + ((m_Index.x & ~1) << 1) + (m_Index.x & 1); // swizzled, see comment on Nodes
                Node node = m_Terrain->Nodes[level.Base + levelOffset];

                // Calculate the AABBs of the node's children
                m_ChildIndex = m_Index + m_Index;
                float3 boundsX = (float3)(m_ChildIndex.x + new int3(0, 1, 2)) * level.Scale;
                float3 boundsZ = (float3)(m_ChildIndex.y + new int3(0, 1, 2)) * level.Scale;
                Bounds = new FourTransposedAabbs
                {
                    Lx = boundsX.xyxy,
                    Hx = boundsX.yzyz,
                    Lz = boundsZ.xxyy,
                    Hz = boundsZ.yyzz,
                    Ly = node.Min4,
                    Hy = node.Max4
                };

                return true;
            }

            // Returns the coordinates of one child of the current node, specified by childIndex.
            public int2 GetQuadIndex(int childIndex)
            {
                return m_ChildIndex + new int2(childIndex & 1, childIndex >> 1);
            }

            // Returns the coordinates and vertices of one child of the current node, specified by childIndex.
            // Should only be called if the current node is a leaf.
            public void GetQuad(int childIndex, out int2 quadIndex, out float3 a, out float3 b, out float3 c, out float3 d)
            {
                quadIndex = GetQuadIndex(childIndex);
                int height0 = m_Terrain->GetHeight(quadIndex);
                int height1 = m_Terrain->GetHeight(quadIndex + new int2(1, 0));
                int height2 = m_Terrain->GetHeight(quadIndex + new int2(0, 1));
                int height3 = m_Terrain->GetHeight(quadIndex + new int2(1, 1));
                float4 heights = new int4(height0, height1, height2, height3);
                a = new float3(Bounds.Lx[childIndex], heights[0], Bounds.Lz[childIndex]) * m_Terrain->Scale;
                b = new float3(Bounds.Hx[childIndex], heights[1], Bounds.Lz[childIndex]) * m_Terrain->Scale;
                c = new float3(Bounds.Lx[childIndex], heights[2], Bounds.Hz[childIndex]) * m_Terrain->Scale;
                d = new float3(Bounds.Hx[childIndex], heights[3], Bounds.Hz[childIndex]) * m_Terrain->Scale;
            }
        }

        #endregion
    }
}
