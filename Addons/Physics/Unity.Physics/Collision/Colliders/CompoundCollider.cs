using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    /// <summary>   A collider containing instances of other colliders. </summary>
    public struct CompoundCollider : ICompositeCollider
    {
        internal ColliderHeader m_Header;

        /// <summary>
        /// A child collider, within the same blob as the compound collider. Warning: This references the
        /// collider via a relative offset, so must always be passed by reference.
        /// </summary>
        public struct Child
        {
            /// <summary>   The child transform relative to the compound collider. </summary>
            public RigidTransform CompoundFromChild;
            internal int m_ColliderOffset;

            /// <summary>
            /// <para>
            ///     The <see cref="Unity.Entities.Entity"/> that this Child is associated with. Default value is <see cref="Entity.Null"/>.
            /// </para>
            /// <para>
            ///     If creating a <see cref="CompoundCollider"/> manually using <see cref="CompoundCollider.Create"/>,
            ///     you can set this field to any value you want and interpret it the way you want to.
            /// </para>
            /// </summary>
            public Entity Entity;

            /// <summary>   Gets the child collider pointer. </summary>
            ///
            /// <value> The child collider pointer. </value>
            public unsafe Collider* Collider
            {
                get
                {
                    fixed(int* offsetPtr = &m_ColliderOffset)
                    {
                        return (Collider*)((byte*)offsetPtr + *offsetPtr);
                    }
                }
            }
        }

        internal float m_BoundingRadius;

        // The array of child colliders
        private BlobArray m_ChildrenBlob;

        /// <summary>
        /// Get the root level filter that can be used for a quick dismiss of the collision/query.
        /// Individual children can have their own collision filters for further filtering. This filter
        /// is a union of all child filters.
        /// </summary>
        ///
        /// <returns>   The root collision filter. </returns>
        public CollisionFilter GetCollisionFilter() => m_Header.Filter;


        /// <summary>   Gets the collision filter of the child specified by the collider key. </summary>
        ///
        /// <param name="colliderKey">  The child collider key. </param>
        ///
        /// <returns>
        /// The collision filter of the child specified by the collider key. If the key is empty, it will
        /// return root filter. If the key is invalid, it will throw an exception.
        /// </returns>
        public CollisionFilter GetCollisionFilter(ColliderKey colliderKey)
        {
            // Root filter
            if (colliderKey == ColliderKey.Empty)
            {
                return m_Header.Filter;
            }

            unsafe
            {
                // Get the child filter
                if (GetChild(ref colliderKey, out ChildCollider child))
                {
                    return child.Collider->GetCollisionFilter(colliderKey);
                }
            }
            SafetyChecks.ThrowInvalidOperationException("Calling GetCollisionFilter() on a CompoundCollider requires a valid or empty collider key!");
            return CollisionFilter.Default;
        }

        /// <summary>   Sets the root collision filter. This also sets the filter of all children to the input value.</summary>
        ///
        /// <param name="filter">   Specifies the filter. </param>
        public void SetCollisionFilter(CollisionFilter filter)
        {
            m_Header.Version++;
            m_Header.Filter = filter;

            for (int childIndex = 0; childIndex < Children.Length; childIndex++)
            {
                ref Child c = ref Children[childIndex];
                unsafe
                {
                    c.Collider->SetCollisionFilter(filter);
                }
            }
        }

        /// <summary>
        /// Sets collision filter of the child specified by collider key. If the provided key is empty,
        /// it will set the root filter and the filter of all children to the provided value. If the
        /// filter is invalid, it will throw an exception. If refreshCompoundFilter is set to false, this
        /// function will not call <see cref="RefreshCollisionFilter"/>, and should be used in cases of
        /// updating multiple child colliders at once, where you will need to call <see cref="RefreshCollisionFilter"/>
        /// manually after the last child filter is updated.
        /// </summary>
        ///
        /// <param name="filter">                   Specifies the filter. </param>
        /// <param name="childKey">                 The child key. </param>
        /// <param name="refreshCompoundFilter">    True to refresh compound filter. </param>
        public void SetCollisionFilter(CollisionFilter filter, ColliderKey childKey, bool refreshCompoundFilter)
        {
            if (childKey == ColliderKey.Empty)
            {
                SetCollisionFilter(filter);
                return;
            }
            unsafe
            {
                if (GetChild(ref childKey, out ChildCollider child))
                {
                    child.Collider->SetCollisionFilter(filter, childKey);
                    if (refreshCompoundFilter)
                    {
                        RefreshCollisionFilter();
                    }
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("Calling SetCollisionFilter() on a CompoundCollider requires a valid or empty collider key!");
                }
            }
        }

        /// <summary>
        /// Sets collision filter of the child specified by collider key. If the provided key is empty,
        /// it will set the root filter and the filter of all children to the provided value. If the
        /// filter is invalid, it will throw an exception.
        /// After setting the child filter, this function will also call <see cref="RefreshCollisionFilter"/> to update the root filter based on the new child filter value.
        /// </summary>
        ///
        /// <param name="filter">       Specifies the filter. </param>
        /// <param name="colliderKey">  The collider key. </param>
        public void SetCollisionFilter(CollisionFilter filter, ColliderKey colliderKey)
        {
            // Root filter
            if (colliderKey == ColliderKey.Empty)
            {
                SetCollisionFilter(filter);
                return;
            }
            unsafe
            {
                if (GetChild(ref colliderKey, out ChildCollider child))
                {
                    child.Collider->SetCollisionFilter(filter, colliderKey);
                    RefreshCollisionFilter();
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("Calling SetCollisionFilter() on a CompoundCollider requires a valid or empty collider key!");
                }
            }
        }

        /// <summary>   Gets the number of children. </summary>
        ///
        /// <value> The total number of children. </value>
        public int NumChildren => m_ChildrenBlob.Length;

        /// <summary>   Gets the children. </summary>
        ///
        /// <value> The children. </value>
        public BlobArray.Accessor<Child> Children => new BlobArray.Accessor<Child>(ref m_ChildrenBlob);

        /// <summary>  An utility method that converts child index to collider key. </summary>
        ///
        /// <param name="childIndex">   Zero-based index of the child. </param>
        ///
        /// <returns>   The child converted index to collider key. </returns>
        public ColliderKey ConvertChildIndexToColliderKey(int childIndex)
        {
            SafetyChecks.CheckInRangeAndThrow(childIndex, new int2(0, NumChildren - 1), "childIndex");
            return new ColliderKey(NumColliderKeyBits, (uint)childIndex);
        }

        /// <summary>
        /// Fills the passed in map with <see cref="ColliderKey"/> - <see cref="ChildCollider"/> pairs of
        /// this compound collider.
        /// </summary>
        ///
        /// <param name="colliderKeyToChildrenMapping"> [in,out] The collider key to children mapping. </param>
        public void GetColliderKeyToChildrenMapping(ref NativeHashMap<ColliderKey, ChildCollider> colliderKeyToChildrenMapping)
        {
            unsafe
            {
                for (int childIndex = 0; childIndex < NumChildren; childIndex++)
                {
                    ref Child child = ref Children[childIndex];
                    ChildCollider childCollider = new ChildCollider(child.Collider, child.CompoundFromChild, child.Entity);
                    colliderKeyToChildrenMapping.Add(new ColliderKey(NumColliderKeyBits, (uint)childIndex), childCollider);
                }
            }
        }

        internal Material GetMaterial(ColliderKey colliderKey)
        {
            unsafe
            {
                // If we get ColliderKey.Empty, return the material of the first child
                if (colliderKey == ColliderKey.Empty)
                {
                    return Children[0].Collider->GetMaterial(ColliderKey.Empty);
                }
                else
                {
                    if (GetChild(ref colliderKey, out ChildCollider child))
                    {
                        return child.Collider->GetMaterial(colliderKey);
                    }
                }
                SafetyChecks.ThrowInvalidOperationException("Invalid ColliderKey");
                return Material.Default;
            }
        }

        internal void SetMaterialField(Material material, ColliderKey colliderKey, Material.MaterialField option)
        {
            unsafe
            {
                if (colliderKey == ColliderKey.Empty)
                {
                    m_Header.Version++;
                    for (int i = 0; i < NumChildren; i++)
                    {
                        Children[i].Collider->SetMaterialField(material, colliderKey, option);
                    }
                }
                else if (GetChild(ref colliderKey, out ChildCollider child))
                {
                    m_Header.Version++;
                    child.Collider->SetMaterialField(material, colliderKey, option);
                }
                else
                {
                    SafetyChecks.ThrowInvalidOperationException("Invalid collider key");
                }
            }
        }

        // The bounding volume hierarchy
        // TODO: Store node filters array too, for filtering queries within the BVH
        private BlobArray m_BvhNodesBlob;

        internal BlobArray.Accessor<BoundingVolumeHierarchy.Node> BvhNodes => new BlobArray.Accessor<BoundingVolumeHierarchy.Node>(ref m_BvhNodesBlob);
        internal unsafe BoundingVolumeHierarchy BoundingVolumeHierarchy
        {
            get
            {
                fixed(BlobArray* blob = &m_BvhNodesBlob)
                {
                    var firstNode = (BoundingVolumeHierarchy.Node*)((byte*)&(blob->Offset) + blob->Offset);
                    return new BoundingVolumeHierarchy(firstNode, nodeFilters: null);
                }
            }
        }

        #region Construction

        /// <summary>
        ///     Input to the compound collider creation function <see cref="CompoundCollider.Create"/>.
        ///     Represents one child of the compound collider and its local transformation
        ///     relative to the compound collider.
        /// </summary>
        public struct ColliderBlobInstance
        {
            /// <summary>   The child transform relative to the compound collider. </summary>
            public RigidTransform CompoundFromChild;
            /// <summary>   The child collider. </summary>
            public BlobAssetReference<Collider> Collider;

            /// <summary>
            /// <para>
            ///     The <see cref="Unity.Entities.Entity"/> that this Child is associated with. Default value is <see cref="Entity.Null"/>.
            /// </para>
            /// <para>
            ///     If creating a <see cref="CompoundCollider"/> manually using <see cref="CompoundCollider.Create"/>,
            ///     you can set this field to any value you want and interpret it the way you want to.
            /// </para>
            /// </summary>
            public Entity Entity;
        }

        /// <summary>
        /// Create a compound collider containing an array of other colliders. The source colliders are
        /// copied into the compound, so that it becomes one blob.
        /// </summary>
        ///
        /// <param name="children"> The children. </param>
        ///
        /// <returns>   A BlobAssetReference&lt;Collider&gt; </returns>
        public static BlobAssetReference<Collider> Create(NativeArray<ColliderBlobInstance> children)
            => CreateInternal(children);

        internal static BlobAssetReference<Collider> CreateInternal(NativeArray<ColliderBlobInstance> children, uint forceUniqueBlobID = ~ColliderConstants.k_SharedBlobID)
        {
            unsafe
            {
                SafetyChecks.CheckNotEmptyAndThrow(children, nameof(children));

                // Get the total required memory size for the compound plus all its children,
                // and the combined filter of all children
                // TODO: Verify that the size is enough
                int totalSize = Math.NextMultipleOf16(UnsafeUtility.SizeOf<CompoundCollider>());
                CollisionFilter filter = children[0].Collider.Value.GetCollisionFilter();
                var srcToDestInstanceAddrs = new NativeParallelHashMap<long, long>(children.Length, Allocator.Temp);
                for (var childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    var child = children[childIndex];
                    var instanceKey = (long)child.Collider.GetUnsafePtr();
                    if (srcToDestInstanceAddrs.ContainsKey(instanceKey))
                        continue;
                    totalSize += Math.NextMultipleOf16(child.Collider.Value.MemorySize);
                    filter = CollisionFilter.CreateUnion(filter, child.Collider.Value.GetCollisionFilter());
                    srcToDestInstanceAddrs.Add(instanceKey, 0L);
                }
                totalSize += (children.Length + BoundingVolumeHierarchy.Constants.MaxNumTreeBranches) * UnsafeUtility.SizeOf<BoundingVolumeHierarchy.Node>();

                // Allocate the collider
                var compoundCollider = (CompoundCollider*)UnsafeUtility.Malloc(totalSize, 16, Allocator.Temp);
                UnsafeUtility.MemClear(compoundCollider, totalSize);
                compoundCollider->m_Header.Type = ColliderType.Compound;
                compoundCollider->m_Header.CollisionType = CollisionType.Composite;
                compoundCollider->m_Header.Version = 1;
                compoundCollider->m_Header.Magic = 0xff;
                compoundCollider->m_Header.ForceUniqueBlobID = forceUniqueBlobID;
                compoundCollider->m_Header.Filter = filter;

                // Initialize children array
                Child* childrenPtr = (Child*)((byte*)compoundCollider + UnsafeUtility.SizeOf<CompoundCollider>());
                compoundCollider->m_ChildrenBlob.Offset = (int)((byte*)childrenPtr - (byte*)(&compoundCollider->m_ChildrenBlob.Offset));
                compoundCollider->m_ChildrenBlob.Length = children.Length;
                byte* end = (byte*)childrenPtr + UnsafeUtility.SizeOf<Child>() * children.Length;
                end = (byte*)Math.NextMultipleOf16((ulong)end);

                uint maxTotalNumColliderKeyBits = 0;

                // Copy children
                for (int i = 0; i < children.Length; i++)
                {
                    Collider* collider = (Collider*)children[i].Collider.GetUnsafePtr();
                    var srcInstanceKey = (long)collider;
                    var dstAddr = srcToDestInstanceAddrs[srcInstanceKey];
                    if (dstAddr == 0L)
                    {
                        dstAddr = (long)end;
                        srcToDestInstanceAddrs[srcInstanceKey] = dstAddr;
                        UnsafeUtility.MemCpy(end, collider, collider->MemorySize);
                        end += Math.NextMultipleOf16(collider->MemorySize);
                    }
                    childrenPtr[i].m_ColliderOffset = (int)((byte*)dstAddr - (byte*)(&childrenPtr[i].m_ColliderOffset));
                    childrenPtr[i].CompoundFromChild = children[i].CompoundFromChild;
                    childrenPtr[i].Entity = children[i].Entity;

                    maxTotalNumColliderKeyBits = math.max(maxTotalNumColliderKeyBits, collider->TotalNumColliderKeyBits);
                }

                // Build mass properties
                compoundCollider->MassProperties = compoundCollider->BuildMassProperties();

                // Build bounding volume
                int numNodes = compoundCollider->BuildBoundingVolume(out NativeArray<BoundingVolumeHierarchy.Node> nodes);
                int bvhSize = numNodes * UnsafeUtility.SizeOf<BoundingVolumeHierarchy.Node>();
                compoundCollider->m_BvhNodesBlob.Offset = (int)(end - (byte*)(&compoundCollider->m_BvhNodesBlob.Offset));
                compoundCollider->m_BvhNodesBlob.Length = numNodes;
                UnsafeUtility.MemCpy(end, nodes.GetUnsafeReadOnlyPtr(), bvhSize);
                compoundCollider->UpdateCachedBoundingRadius();
                end += bvhSize;

                // Validate nesting level of composite colliders.
                compoundCollider->TotalNumColliderKeyBits = maxTotalNumColliderKeyBits + compoundCollider->NumColliderKeyBits;

                // If TotalNumColliderKeyBits is greater than 32, it means maximum nesting level of composite colliders has been breached.
                // ColliderKey has 32 bits so it can't handle infinite nesting of composite colliders.
                if (compoundCollider->TotalNumColliderKeyBits > 32)
                {
                    SafetyChecks.ThrowArgumentException(nameof(children), "Composite collider exceeded maximum level of nesting!");
                }

                // Copy to blob asset
                int usedSize = (int)(end - (byte*)compoundCollider);
                UnityEngine.Assertions.Assert.IsTrue(usedSize < totalSize);
                compoundCollider->MemorySize = usedSize;
                var blob = BlobAssetReference<Collider>.Create(compoundCollider, usedSize);
                UnsafeUtility.Free(compoundCollider, Allocator.Temp);

                return blob;
            }
        }

        private unsafe int BuildBoundingVolume(out NativeArray<BoundingVolumeHierarchy.Node> nodes)
        {
            // Create inputs
            var points = new NativeArray<BoundingVolumeHierarchy.PointAndIndex>(NumChildren, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var aabbs = new NativeArray<Aabb>(NumChildren, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < NumChildren; ++i)
            {
                points[i] = new BoundingVolumeHierarchy.PointAndIndex { Position = Children[i].CompoundFromChild.pos, Index = i };
                aabbs[i] = Children[i].Collider->CalculateAabb(Children[i].CompoundFromChild);
            }

            // Build BVH
            // Todo: cleanup, better size of nodes array
            nodes = new NativeArray<BoundingVolumeHierarchy.Node>(2 + NumChildren, Allocator.Temp, NativeArrayOptions.UninitializedMemory)
            {
                [0] = BoundingVolumeHierarchy.Node.Empty,
                [1] = BoundingVolumeHierarchy.Node.Empty
            };

            var bvh = new BoundingVolumeHierarchy(nodes);
            bvh.Build(points, aabbs, out int numNodes);

            return numNodes;
        }

        private unsafe void UpdateCachedBoundingRadius()
        {
            m_BoundingRadius = 0;
            float3 center = BoundingVolumeHierarchy.Domain.Center;

            for (int i = 0; i < NumChildren; i++)
            {
                ref Child child = ref Children[i];

                float3 childFromCenter = math.transform(math.inverse(child.CompoundFromChild), center);
                float radius = 0;

                switch (child.Collider->Type)
                {
                    case ColliderType.Sphere:
                    case ColliderType.Box:
                    case ColliderType.Capsule:
                    case ColliderType.Quad:
                    case ColliderType.Triangle:
                    case ColliderType.Cylinder:
                    case ColliderType.Convex:
                        radius = ((ConvexCollider*)child.Collider)->CalculateBoundingRadius(childFromCenter);
                        break;
                    case ColliderType.Compound:
                        radius = ((CompoundCollider*)child.Collider)->CalculateBoundingRadius(childFromCenter);
                        break;
                    case ColliderType.Mesh:
                        radius = ((MeshCollider*)child.Collider)->CalculateBoundingRadius(childFromCenter);
                        break;
                    case ColliderType.Terrain:
                        Aabb terrainAabb = ((TerrainCollider*)child.Collider)->CalculateAabb();
                        radius = math.length(math.max(math.abs(terrainAabb.Max - childFromCenter), math.abs(terrainAabb.Min - childFromCenter)));
                        break;
                    default:
                        SafetyChecks.ThrowNotImplementedException();
                        break;
                }
                m_BoundingRadius = math.max(m_BoundingRadius, radius);
            }
        }

        // Build mass properties representing a union of all the child collider mass properties.
        // This assumes a uniform density for all children, and returns a mass properties for a compound of unit mass.
        private unsafe MassProperties BuildMassProperties()
        {
            BlobArray.Accessor<Child> children = Children;

            // Check if all children are triggers or have collisions disabled.
            // If so, we'll include them in mass properties of the compound collider.
            // This is mostly targeted for single collider compounds, as they should behave
            // the same as when there is no compound.
            bool skipTriggersAndDisabledColliders = false;
            {
                for (int i = 0; i < NumChildren; ++i)
                {
                    ref Child child = ref children[i];
                    var convexChildCollider = (ConvexCollider*)child.Collider;
                    if (child.Collider->CollisionType == CollisionType.Convex &&
                        (convexChildCollider->Material.CollisionResponse != CollisionResponsePolicy.RaiseTriggerEvents &&
                         convexChildCollider->Material.CollisionResponse != CollisionResponsePolicy.None))
                    {
                        // If there are children with regular collisions, all triggers and disabled colliders should be skipped
                        skipTriggersAndDisabledColliders = true;
                        break;
                    }
                }
            }

            // Calculate combined center of mass
            float3 combinedCenterOfMass = float3.zero;
            float combinedVolume = 0.0f;
            for (int i = 0; i < NumChildren; ++i)
            {
                ref Child child = ref children[i];
                var convexChildCollider = (ConvexCollider*)child.Collider;
                if (skipTriggersAndDisabledColliders && child.Collider->CollisionType == CollisionType.Convex &&
                    (convexChildCollider->Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents ||
                     convexChildCollider->Material.CollisionResponse == CollisionResponsePolicy.None))
                    continue;

                var mp = child.Collider->MassProperties;

                // weight this contribution by its volume (=mass)
                combinedCenterOfMass += math.transform(child.CompoundFromChild, mp.MassDistribution.Transform.pos) * mp.Volume;
                combinedVolume += mp.Volume;
            }
            if (combinedVolume > 0.0f)
            {
                combinedCenterOfMass /= combinedVolume;
            }

            // Calculate combined inertia, relative to new center of mass
            float3x3 combinedOrientation;
            float3 combinedInertiaTensor;
            {
                float3x3 combinedInertiaMatrix = float3x3.zero;
                for (int i = 0; i < NumChildren; ++i)
                {
                    ref Child child = ref children[i];
                    var convexChildCollider = (ConvexCollider*)child.Collider;
                    if (skipTriggersAndDisabledColliders && child.Collider->CollisionType == CollisionType.Convex &&
                        (convexChildCollider->Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents ||
                         convexChildCollider->Material.CollisionResponse == CollisionResponsePolicy.None))
                        continue;

                    var mp = child.Collider->MassProperties;

                    // rotate inertia into compound space
                    float3x3 temp = math.mul(mp.MassDistribution.InertiaMatrix, new float3x3(math.inverse(child.CompoundFromChild.rot)));
                    float3x3 inertiaMatrix = math.mul(new float3x3(child.CompoundFromChild.rot), temp);

                    // shift it to be relative to the new center of mass
                    float3 shift = math.transform(child.CompoundFromChild, mp.MassDistribution.Transform.pos) - combinedCenterOfMass;
                    float3 shiftSq = shift * shift;
                    var diag = new float3(shiftSq.y + shiftSq.z, shiftSq.x + shiftSq.z, shiftSq.x + shiftSq.y);
                    var offDiag = new float3(shift.x * shift.y, shift.y * shift.z, shift.z * shift.x) * -1.0f;
                    inertiaMatrix.c0 += new float3(diag.x, offDiag.x, offDiag.z);
                    inertiaMatrix.c1 += new float3(offDiag.x, diag.y, offDiag.y);
                    inertiaMatrix.c2 += new float3(offDiag.z, offDiag.y, diag.z);

                    // weight by its proportional volume (=mass)
                    inertiaMatrix *= mp.Volume / (combinedVolume + float.Epsilon);

                    combinedInertiaMatrix += inertiaMatrix;
                }

                // convert to box inertia
                Math.DiagonalizeSymmetricApproximation(
                    combinedInertiaMatrix, out combinedOrientation, out combinedInertiaTensor);
            }

            // Calculate combined angular expansion factor, relative to new center of mass
            float combinedAngularExpansionFactor = 0.0f;
            for (int i = 0; i < NumChildren; ++i)
            {
                ref Child child = ref children[i];
                var mp = child.Collider->MassProperties;

                float3 shift = math.transform(child.CompoundFromChild, mp.MassDistribution.Transform.pos) - combinedCenterOfMass;
                float expansionFactor = mp.AngularExpansionFactor + math.length(shift);
                combinedAngularExpansionFactor = math.max(combinedAngularExpansionFactor, expansionFactor);
            }

            return new MassProperties
            {
                MassDistribution = new MassDistribution
                {
                    Transform = new RigidTransform(combinedOrientation, combinedCenterOfMass),
                    InertiaTensor = combinedInertiaTensor
                },
                Volume = combinedVolume,
                AngularExpansionFactor = combinedAngularExpansionFactor
            };
        }

        #endregion

        /// <summary>
        /// Refreshes combined collision filter of all children. Should be called when child collision
        /// filter changes.
        /// </summary>
        public void RefreshCollisionFilter()
        {
            unsafe
            {
                CollisionFilter filter = CollisionFilter.Zero;
                for (int childIndex = 0; childIndex < Children.Length; childIndex++)
                {
                    ref Child c = ref Children[childIndex];

                    // If a child is also a compound, refresh its collision filter first
                    if (c.Collider->Type == ColliderType.Compound)
                    {
                        ((CompoundCollider*)c.Collider)->RefreshCollisionFilter();
                    }

                    filter = CollisionFilter.CreateUnion(filter, c.Collider->GetCollisionFilter());
                }
                m_Header.Version++;
                m_Header.Filter = filter;
            }
        }

        #region ICompositeCollider

        /// <summary>   Gets the type. </summary>
        ///
        /// <value> The type. </value>
        public ColliderType Type => m_Header.Type;

        /// <summary>   Gets the collision type. </summary>
        ///
        /// <value> Collision type. </value>
        public CollisionType CollisionType => m_Header.CollisionType;

        /// <summary>   Gets the memory size (including children). </summary>
        ///
        /// <value> The size of the memory. </value>
        public int MemorySize { get; private set; }

        internal unsafe bool RespondsToCollision
        {
            get
            {
                for (int childIndex = 0; childIndex < Children.Length; childIndex++)
                {
                    ref Child c = ref Children[childIndex];
                    if (c.Collider->RespondsToCollision)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties { get; private set; }

        internal float CalculateBoundingRadius(float3 pivot)
        {
            return math.distance(pivot, BoundingVolumeHierarchy.Domain.Center) + m_BoundingRadius;
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
            unsafe
            {
                Aabb outAabb = Math.TransformAabb(BoundingVolumeHierarchy.Domain, transform, uniformScale);
                float3 center = outAabb.Center;
                float scaledBoundingRadius = m_BoundingRadius * math.abs(uniformScale);
                Aabb sphereAabb = new Aabb
                {
                    Min = new float3(center - scaledBoundingRadius),
                    Max = new float3(center + scaledBoundingRadius)
                };
                outAabb.Intersect(sphereAabb);

                return outAabb;
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
                fixed(CompoundCollider* target = &this)
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
                fixed(CompoundCollider* target = &this)
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
                fixed(CompoundCollider* target = &this)
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
                fixed(CompoundCollider* target = &this)
                {
                    return DistanceQueries.ColliderCollider(input, (Collider*)target, ref collector);
                }
            }
        }

        #region GO API Queries

        /// <summary>   Check if the sphere is overlapping this collider. </summary>
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
        public uint NumColliderKeyBits => (uint)(32 - math.lzcnt(NumChildren));

        internal uint TotalNumColliderKeyBits { get; private set; }

        /// <summary>   Gets a child of this collider. </summary>
        ///
        /// <param name="key">   [in,out] The key that identifies the child. Gets modified by removing the
        /// bits associated with the child collider. </param>
        /// <param name="child">    [out] The child. </param>
        ///
        /// <returns>   True if there is a child with the specified key, false otherwise. </returns>
        public bool GetChild(ref ColliderKey key, out ChildCollider child)
        {
            unsafe
            {
                if (key.PopSubKey(NumColliderKeyBits, out uint childIndex))
                {
                    ref Child c = ref Children[(int)childIndex];
                    child = new ChildCollider(c.Collider, c.CompoundFromChild, c.Entity);
                    return true;
                }

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
            unsafe
            {
                fixed(CompoundCollider* root = &this)
                {
                    return Collider.GetLeafCollider(out leaf, (Collider*)root, key, RigidTransform.identity);
                }
            }
        }

        /// <summary>   Gets the leaf colliders of this collider . </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="collector">    [in,out] The collector. </param>
        public void GetLeaves<T>([NoAlias] ref T collector) where T : struct, ILeafColliderCollector
        {
            unsafe
            {
                for (uint i = 0; i < NumChildren; i++)
                {
                    ref Child c = ref Children[(int)i];
                    ColliderKey childKey = new ColliderKey(NumColliderKeyBits, i);
                    if (c.Collider->CollisionType == CollisionType.Composite)
                    {
                        collector.PushCompositeCollider(new ColliderKeyPath(childKey, NumColliderKeyBits), new MTransform(c.CompoundFromChild), out MTransform worldFromCompound);
                        c.Collider->GetLeaves(ref collector);
                        collector.PopCompositeCollider(NumColliderKeyBits, worldFromCompound);
                    }
                    else
                    {
                        var child = new ChildCollider(c.Collider, c.CompoundFromChild, c.Entity);
                        collector.AddLeaf(childKey, ref child);
                    }
                }
            }
        }

        #endregion
    }
}
