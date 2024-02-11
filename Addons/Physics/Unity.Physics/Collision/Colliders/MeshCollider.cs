using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace Unity.Physics
{
    /// <summary>
    /// A collider representing a mesh comprised of triangles and quads. Warning: This is just the
    /// header, it is followed by variable sized data in memory. Therefore this struct must always be
    /// passed by reference, never by value.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MeshCollider : ICompositeCollider
    {
        internal ColliderHeader m_Header;
        Aabb m_Aabb;
        int m_MemorySize;
        public Mesh Mesh; // Note: Mesh must be the last member. It is immediately followed by the mesh data (see Mesh.Init()).

        // followed by variable sized mesh data

        #region Construction

        /// <summary>   Create a mesh collider asset from a set of triangles. </summary>
        ///
        /// <param name="vertices">     The vertices. </param>
        /// <param name="triangles">    The triangles. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(NativeArray<float3> vertices, NativeArray<int3> triangles) =>
            CreateInternal(vertices, triangles, CollisionFilter.Default, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="vertices">     The vertices. </param>
        /// <param name="triangles">    The triangles. </param>
        /// <param name="filter">       Specifies the filter. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(NativeArray<float3> vertices, NativeArray<int3> triangles, CollisionFilter filter) =>
            CreateInternal(vertices, triangles, filter, Material.Default);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="vertices">     The vertices. </param>
        /// <param name="triangles">    The triangles. </param>
        /// <param name="filter">       Specifies the filter. </param>
        /// <param name="material">     The material. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(NativeArray<float3> vertices, NativeArray<int3> triangles, CollisionFilter filter, Material material) =>
            CreateInternal(vertices, triangles, filter, material);

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="mesh">         The UnityEngine.Mesh. </param>
        /// <param name="filter">       Specifies the filter. </param>
        /// <param name="material">     The material. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        [GenerateTestsForBurstCompatibility]
        public static BlobAssetReference<Collider> Create(UnityEngine.Mesh mesh, CollisionFilter filter, Material material)
        {
            // Get fast zero-copy access to raw mesh data
            using var meshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(mesh);

            //meshDataArray[0] always since we aren't doing an entity query. [MeshArrayIndex = 0]
            // bool trianglesNeeded = true because we are always getting a mesh. [!physicsMeshData.Convex]
            MeshUtilities.AppendMeshPropertiesToNativeBuffers(meshDataArray[0], true, out var vertices, out var triangles);

            return CreateInternal(vertices, triangles, filter, material);
        }

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="meshData">     The UnityEngine.Mesh.MeshData. </param>
        /// <param name="filter">       Specifies the filter. </param>
        /// <param name="material">     The material. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        [GenerateTestsForBurstCompatibility]
        public static BlobAssetReference<Collider> Create(UnityEngine.Mesh.MeshData meshData, CollisionFilter filter,
            Material material)
        {
            MeshUtilities.AppendMeshPropertiesToNativeBuffers(meshData, true, out var vertices, out var triangles);
            return CreateInternal(vertices, triangles, filter, material);
        }

        /// <summary>   Creates a new BlobAssetReference&lt;Collider&gt; </summary>
        ///
        /// <param name="meshDataArray">     The UnityEngine.Mesh.MeshDataArray. </param>
        /// <param name="filter">       Specifies the filter. </param>
        /// <param name="material">     The material. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        [GenerateTestsForBurstCompatibility]
        public static BlobAssetReference<Collider> Create(UnityEngine.Mesh.MeshDataArray meshDataArray, CollisionFilter filter,
            Material material)
        {
            MeshUtilities.AppendMeshPropertiesToNativeBuffers(meshDataArray[0], true, out var vertices, out var triangles);
            return CreateInternal(vertices, triangles, filter, material);
        }

        #endregion

        #region Internal Construction

        internal static BlobAssetReference<Collider> CreateInternal(NativeArray<float3> vertices, NativeArray<int3> triangles, CollisionFilter filter, Material material, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            unsafe
            {
                SafetyChecks.CheckTriangleIndicesInRangeAndThrow(triangles, vertices.Length, nameof(triangles));

                // Copy vertices
                var tempVertices = new NativeArray<float3>(vertices, Allocator.Temp);

                // Triangle indices - needed for WeldVertices
                var tempIndices = new NativeArray<int>(triangles.Reinterpret<int>(UnsafeUtility.SizeOf<int3>()), Allocator.Temp);

                // Build connectivity and primitives

                NativeList<float3> uniqueVertices = MeshConnectivityBuilder.WeldVertices(tempIndices, tempVertices);

                var tempTriangleIndices = new NativeArray<int3>(triangles.Length, Allocator.Temp);
                UnsafeUtility.MemCpy(tempTriangleIndices.GetUnsafePtr(), tempIndices.GetUnsafePtr(), tempIndices.Length * UnsafeUtility.SizeOf<int>());

                var connectivity = new MeshConnectivityBuilder(tempTriangleIndices, uniqueVertices.AsArray());
                NativeList<MeshConnectivityBuilder.Primitive> primitives = connectivity.EnumerateQuadDominantGeometry(tempTriangleIndices, uniqueVertices);

                // Build bounding volume hierarchy
                int nodeCount = math.max(primitives.Length * 2 + 1, 2); // We need at least two nodes - an "invalid" node and a root node.
                var nodes = new NativeArray<BoundingVolumeHierarchy.Node>(nodeCount, Allocator.Temp);
                int numNodes = 0;

                {
                    // Prepare data for BVH
                    var points = new NativeList<BoundingVolumeHierarchy.PointAndIndex>(primitives.Length, Allocator.Temp);
                    var aabbs = new NativeArray<Aabb>(primitives.Length, Allocator.Temp);

                    for (int i = 0; i < primitives.Length; i++)
                    {
                        MeshConnectivityBuilder.Primitive p = primitives[i];

                        // Skip degenerate triangles
                        if (MeshConnectivityBuilder.IsTriangleDegenerate(p.Vertices[0], p.Vertices[1], p.Vertices[2]))
                        {
                            continue;
                        }

                        aabbs[i] = Aabb.CreateFromPoints(p.Vertices);
                        points.Add(new BoundingVolumeHierarchy.PointAndIndex
                        {
                            Position = aabbs[i].Center,
                            Index = i
                        });
                    }

                    var bvh = new BoundingVolumeHierarchy(nodes);

                    bvh.Build(points.AsArray(), aabbs, out numNodes, useSah: true);
                }

                // Build mesh sections
                BoundingVolumeHierarchy.Node* nodesPtr = (BoundingVolumeHierarchy.Node*)nodes.GetUnsafePtr();
                MeshBuilder.TempSection sections = MeshBuilder.BuildSections(nodesPtr, numNodes, primitives);

                // Allocate collider
                int meshDataSize = Mesh.CalculateMeshDataSize(numNodes, sections.Ranges);
                int totalColliderSize = Math.NextMultipleOf(sizeof(MeshCollider), 16) + meshDataSize;

                MeshCollider* meshCollider = (MeshCollider*)UnsafeUtility.Malloc(totalColliderSize, 16, Allocator.Temp);

                // Initialize it
                {
                    UnsafeUtility.MemClear(meshCollider, totalColliderSize);
                    meshCollider->m_MemorySize = totalColliderSize;
                    meshCollider->m_Header.Type = ColliderType.Mesh;
                    meshCollider->m_Header.CollisionType = CollisionType.Composite;
                    meshCollider->m_Header.Version = 0;
                    meshCollider->m_Header.Magic = 0xff;
                    meshCollider->m_Header.ForceUniqueBlobID = forceUniqueBlobID;

                    ref var mesh = ref meshCollider->Mesh;

                    mesh.Init(nodesPtr, numNodes, sections, filter, material);
                    mesh.UpdateCachedBoundingRadius();

                    // Calculate combined filter
                    meshCollider->m_Header.Filter = mesh.Sections.Length > 0 ? mesh.Sections[0].Filters[0] : CollisionFilter.Default;
                    for (int i = 0; i < mesh.Sections.Length; ++i)
                    {
                        for (var j = 0; j < mesh.Sections[i].Filters.Length; ++j)
                        {
                            var f = mesh.Sections[i].Filters[j];
                            meshCollider->m_Header.Filter = CollisionFilter.CreateUnion(meshCollider->m_Header.Filter, f);
                        }
                    }

                    meshCollider->m_Aabb = meshCollider->Mesh.BoundingVolumeHierarchy.Domain;
                    meshCollider->NumColliderKeyBits = meshCollider->Mesh.NumColliderKeyBits;
                }

                // Copy collider into blob
                var blob = BlobAssetReference<Collider>.Create(meshCollider, totalColliderSize);
                UnsafeUtility.Free(meshCollider, Allocator.Temp);
                return blob;
            }
        }

        #endregion

        #region ICompositeCollider

        /// <summary>   Gets the collider type. </summary>
        ///
        /// <value> The collider type. </value>
        public ColliderType Type => m_Header.Type;

        /// <summary>   Gets the collision type. </summary>
        ///
        /// <value> Collision Type. </value>
        public CollisionType CollisionType => m_Header.CollisionType;

        /// <summary>   The total size of the collider in memory. </summary>
        ///
        /// <value> The size of the memory. </value>
        public int MemorySize => m_MemorySize;

        /// <summary>   Gets the collision filter. </summary>
        ///
        /// <returns>   The collision filter. </returns>
        public CollisionFilter GetCollisionFilter() => m_Header.Filter;

        /// <summary>   Sets the collision filter. </summary>
        ///
        /// <param name="filter">   Specifies the filter. </param>
        public void SetCollisionFilter(CollisionFilter filter)
        {
            m_Header.Version++;
            m_Header.Filter = filter;

            for (int i = 0; i < Mesh.Sections.Length; i++)
            {
                var filters = Mesh.Sections[i].Filters;
                for (var j = 0; j < filters.Length; j++)
                {
                    filters[j] = filter;
                }
            }
        }

        internal bool RespondsToCollision
        {
            get
            {
                for (int i = 0; i < Mesh.Sections.Length; i++)
                {
                    var materials = Mesh.Sections[i].Materials;
                    for (var j = 0; j < materials.Length; j++)
                    {
                        if (materials[j].CollisionResponse != CollisionResponsePolicy.None)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Gets the mass properties calculated based on the AABB of the mesh. This is a rough approximation,
        /// but only makes a difference in the case of dynamic meshes, which shouldn't be used a lot for
        /// performance reasons.
        /// </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties
        {
            get
            {
                // Rough approximation based on AABB
                float3 size = m_Aabb.Extents;
                return new MassProperties
                {
                    MassDistribution = new MassDistribution
                    {
                        Transform = new RigidTransform(quaternion.identity, m_Aabb.Center),
                        InertiaTensor = new float3(
                            (size.y * size.y + size.z * size.z) / 12.0f,
                            (size.x * size.x + size.z * size.z) / 12.0f,
                            (size.x * size.x + size.y * size.y) / 12.0f)
                    },
                    Volume = size.x * size.y * size.z,
                    AngularExpansionFactor = math.length(m_Aabb.Extents) * 0.5f
                };
            }
        }

        internal float CalculateBoundingRadius(float3 pivot)
        {
            return math.distance(pivot, Mesh.BoundingVolumeHierarchy.Domain.Center) + Mesh.m_BoundingRadius;
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
            Aabb outAabb = Math.TransformAabb(m_Aabb, transform, uniformScale);
            float3 center = outAabb.Center;
            float scaledBoundingRadius = Mesh.m_BoundingRadius * math.abs(uniformScale);
            Aabb sphereAabb = new Aabb
            {
                Min = new float3(center - scaledBoundingRadius),
                Max = new float3(center + scaledBoundingRadius)
            };
            outAabb.Intersect(sphereAabb);

            return outAabb;
        }

        /// <summary>   Cast a ray against this collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input) => QueryWrappers.RayCast(in this, input);

        /// <summary>   Cast a ray against this collider. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input, out RaycastHit closestHit) => QueryWrappers.RayCast(in this, input, out closestHit);

        /// <summary>   Cast a ray against this collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input, ref NativeList<RaycastHit> allHits) => QueryWrappers.RayCast(in this, input, ref allHits);

        /// <summary>   Cast a ray against this collider. </summary>
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
                fixed(MeshCollider* target = &this)
                {
                    return RaycastQueries.RayCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        // Change when HAVOK-274 is implemented
        internal Material GetMaterial() => Mesh.Sections[0].Materials[0];

        // Change when HAVOK-274 is implemented
        internal void SetMaterialField(Material material, Material.MaterialField option)
        {
            m_Header.Version++;
            for (int i = 0; i < Mesh.Sections.Length; i++)
            {
                Mesh.Sections[i].Materials[0].SetMaterialField(material, option);
            }
        }

        /// <summary>   Cast another collider against this one. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input) => QueryWrappers.ColliderCast(in this, input);

        /// <summary>   Cast another collider against this one. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input, out ColliderCastHit closestHit) => QueryWrappers.ColliderCast(in this, input, out closestHit);

        /// <summary>   Cast another collider against this one. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input, ref NativeList<ColliderCastHit> allHits) => QueryWrappers.ColliderCast(in this, input, ref allHits);

        /// <summary>   Cast another collider against this one. </summary>
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
                fixed(MeshCollider* target = &this)
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

        /// <summary>   Calculate the distance from a point to this collider. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input, out DistanceHit closestHit) => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        /// <summary>   Calculate the distance from a point to this collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input, ref NativeList<DistanceHit> allHits) => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        /// <summary>   Calculate the distance from a point to this collider. </summary>
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
                fixed(MeshCollider* target = &this)
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

        /// <summary>   Calculate the distance from a point to this collider. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input, out DistanceHit closestHit) => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        /// <summary>   Calculate the distance from a point to this collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input, ref NativeList<DistanceHit> allHits) => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        /// <summary>   Calculate the distance from a point to this collider. </summary>
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
                fixed(MeshCollider* target = &this)
                {
                    return DistanceQueries.ColliderCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        #region GO API Queries

        /// <summary> Checks if a sphere overlaps this collider. </summary>
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

        /// <summary>   Gets the number of collider key bits. </summary>
        ///
        /// <value> The total number of collider key bits. </value>
        public uint NumColliderKeyBits { get; private set; }

        internal uint TotalNumColliderKeyBits => NumColliderKeyBits;

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
                int primitiveKey = (int)(subKey >> 1);
                int polygonIndex = (int)(subKey & 1);

                Mesh.GetPrimitive(primitiveKey, out float3x4 vertices, out Mesh.PrimitiveFlags flags, out CollisionFilter filter, out Material material);

                if (Mesh.IsPrimitiveFlagSet(flags, Mesh.PrimitiveFlags.IsQuad))
                {
                    child = new ChildCollider(vertices[0], vertices[1], vertices[2], vertices[3], filter, material);
                }
                else
                {
                    child = new ChildCollider(vertices[0], vertices[1 + polygonIndex], vertices[2 + polygonIndex], filter, material);
                }

                return true;
            }

            child = new ChildCollider();
            return false;
        }

        /// <summary>   Gets a leaf collider. </summary>
        ///
        /// <param name="key">  The key representing the leaf collider. </param>
        /// <param name="leaf"> [out] The leaf. </param>
        ///
        /// <returns>   True if a leaf with the specified key exists, otherwise false. </returns>
        public bool GetLeaf(ColliderKey key, out ChildCollider leaf)
        {
            return GetChild(ref key, out leaf);
        }

        /// <summary>   Gets the leaf colliders of this collider . </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="collector">    [in,out] The collector. </param>
        public void GetLeaves<T>([NoAlias] ref T collector) where T : struct, ILeafColliderCollector
        {
            unsafe
            {
                var polygon = new PolygonCollider();
                polygon.InitNoVertices(CollisionFilter.Default, Material.Default);
                if (Mesh.GetFirstPolygon(out uint meshKey, ref polygon))
                {
                    do
                    {
                        var leaf = new ChildCollider((Collider*)&polygon, RigidTransform.identity);
                        collector.AddLeaf(new ColliderKey(NumColliderKeyBits, meshKey), ref leaf);
                    }
                    while (Mesh.GetNextPolygon(meshKey, out meshKey, ref polygon));
                }
            }
        }

        #endregion
    }
}
