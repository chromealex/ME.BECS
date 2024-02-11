using Unity.Burst;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   The input to AABB overlap queries. </summary>
    public struct OverlapAabbInput
    {
        /// <summary>   The aabb. </summary>
        public Aabb Aabb;
        /// <summary>   Specifies the filter. </summary>
        public CollisionFilter Filter;
    }

    /// <summary>   A hit from an overlap query. </summary>
    public struct OverlapAabbHit
    {
        /// <summary>   Zero-based index of the rigid body. </summary>
        public int RigidBodyIndex;
        /// <summary>   The collider key. </summary>
        public ColliderKey ColliderKey;
    }

    /// <summary>   Interface for collecting hits from overlap queries. </summary>
    public interface IOverlapCollector
    {
        /// <summary>   Adds rigid body indices to the collector. </summary>
        ///
        /// <param name="indices">  [in,out] If non-null, the rigid body indices. </param>
        /// <param name="count">    Number of indices. </param>
        unsafe void AddRigidBodyIndices(int* indices, int count);

        /// <summary>   Adds a collider keys to 'count'. </summary>
        ///
        /// <param name="keys">     [in,out] If non-null, the collider keys. </param>
        /// <param name="count">    Number of collider keys. </param>
        unsafe void AddColliderKeys(ColliderKey* keys, int count);

        /// <summary>   Pushes a composite collider. </summary>
        ///
        /// <param name="compositeKey"> The composite key. </param>
        void PushCompositeCollider(ColliderKeyPath compositeKey);

        /// <summary>   Pops the composite collider described by numCompositeKeyBits. </summary>
        ///
        /// <param name="numCompositeKeyBits">  Number of composite key bits. </param>
        void PopCompositeCollider(uint numCompositeKeyBits);
    }

    // Overlap query implementations
    static class OverlapQueries
    {
        #region AABB vs colliders

        public static unsafe void AabbCollider<T>(OverlapAabbInput input, [NoAlias] Collider* collider, [NoAlias] ref T collector)
            where T : struct, IOverlapCollector
        {
            if (!CollisionFilter.IsCollisionEnabled(input.Filter, collider->GetCollisionFilter()))
            {
                return;
            }

            switch (collider->Type)
            {
                case ColliderType.Mesh:
                    AabbMesh(input, (MeshCollider*)collider, ref collector);
                    break;
                case ColliderType.Compound:
                    AabbCompound(input, (CompoundCollider*)collider, ref collector);
                    break;
                case ColliderType.Terrain:
                    AabbTerrain(input, (TerrainCollider*)collider, ref collector);
                    break;
                default:
                    SafetyChecks.ThrowNotImplementedException();
                    return;
            }
        }

        // Mesh
        internal unsafe struct MeshLeafProcessor : BoundingVolumeHierarchy.IAabbOverlapLeafProcessor
        {
            readonly Mesh* m_Mesh;
            readonly uint m_NumColliderKeyBits;

            const int k_MaxKeys = 512;
            fixed uint m_Keys[k_MaxKeys];  // actually ColliderKeys, but C# doesn't allow fixed arrays of structs
            int m_NumKeys;

            public MeshLeafProcessor(MeshCollider* mesh)
            {
                m_Mesh = &mesh->Mesh;
                m_NumColliderKeyBits = mesh->NumColliderKeyBits;
                m_NumKeys = 0;
            }

            public void AabbLeaf<T>(OverlapAabbInput input, int primitiveKey, [NoAlias] ref T collector) where T : struct, IOverlapCollector
            {
                fixed(uint* keys = m_Keys)
                {
                    keys[m_NumKeys++] = new ColliderKey(m_NumColliderKeyBits, (uint)(primitiveKey << 1)).Value;

                    Mesh.PrimitiveFlags flags = m_Mesh->GetPrimitiveFlags(primitiveKey);
                    if (Mesh.IsPrimitiveFlagSet(flags, Mesh.PrimitiveFlags.IsTrianglePair) &&
                        !Mesh.IsPrimitiveFlagSet(flags, Mesh.PrimitiveFlags.IsQuad))
                    {
                        keys[m_NumKeys++] = new ColliderKey(m_NumColliderKeyBits, (uint)(primitiveKey << 1) | 1).Value;
                    }
                }

                if (m_NumKeys > k_MaxKeys - 8)
                {
                    Flush(ref collector);
                }
            }

            // Flush keys to collector
            internal void Flush<T>([NoAlias] ref T collector) where T : struct, IOverlapCollector
            {
                fixed(uint* keys = m_Keys)
                {
                    collector.AddColliderKeys((ColliderKey*)keys, m_NumKeys);
                }
                m_NumKeys = 0;
            }
        }

        private static unsafe void AabbMesh<T>(OverlapAabbInput input, [NoAlias] MeshCollider* mesh, [NoAlias] ref T collector)
            where T : struct, IOverlapCollector
        {
            var leafProcessor = new MeshLeafProcessor(mesh);
            mesh->Mesh.BoundingVolumeHierarchy.AabbOverlap(input, ref leafProcessor, ref collector);
            leafProcessor.Flush(ref collector);
        }

        // Compound
        internal unsafe struct CompoundLeafProcessor : BoundingVolumeHierarchy.IAabbOverlapLeafProcessor
        {
            readonly CompoundCollider* m_CompoundCollider;
            readonly uint m_NumColliderKeyBits;

            const int k_MaxKeys = 512;
            fixed uint m_Keys[k_MaxKeys];  // actually ColliderKeys, but C# doesn't allow fixed arrays of structs
            int m_NumKeys;

            public CompoundLeafProcessor(CompoundCollider* compound)
            {
                m_CompoundCollider = compound;
                m_NumColliderKeyBits = compound->NumColliderKeyBits;
                m_NumKeys = 0;
            }

            public void AabbLeaf<T>(OverlapAabbInput input, int childIndex, [NoAlias] ref T collector) where T : struct, IOverlapCollector
            {
                ColliderKey childKey = new ColliderKey(m_NumColliderKeyBits, (uint)(childIndex));

                // Recurse if child is a composite
                ref CompoundCollider.Child child = ref m_CompoundCollider->Children[childIndex];
                if (child.Collider->CollisionType == CollisionType.Composite)
                {
                    OverlapAabbInput childInput = input;
                    childInput.Aabb = Math.TransformAabb(input.Aabb, math.inverse(child.CompoundFromChild));

                    collector.PushCompositeCollider(new ColliderKeyPath(childKey, m_NumColliderKeyBits));
                    AabbCollider(childInput, child.Collider, ref collector);
                    collector.PopCompositeCollider(m_NumColliderKeyBits);
                }
                else
                {
                    m_Keys[m_NumKeys++] = childKey.Value;
                    if (m_NumKeys > k_MaxKeys - 8)
                    {
                        Flush(ref collector);
                    }
                }
            }

            // Flush keys to collector
            internal void Flush<T>(ref T collector) where T : struct, IOverlapCollector
            {
                fixed(uint* keys = m_Keys)
                {
                    collector.AddColliderKeys((ColliderKey*)keys, m_NumKeys);
                }
                m_NumKeys = 0;
            }
        }

        private static unsafe void AabbCompound<T>(OverlapAabbInput input, [NoAlias] CompoundCollider* compound, [NoAlias] ref T collector)
            where T : struct, IOverlapCollector
        {
            var leafProcessor = new CompoundLeafProcessor(compound);
            compound->BoundingVolumeHierarchy.AabbOverlap(input, ref leafProcessor, ref collector);
            leafProcessor.Flush(ref collector);
        }

        private static unsafe void AabbTerrain<T>(OverlapAabbInput input, [NoAlias] TerrainCollider* terrainCollider, [NoAlias] ref T collector)
            where T : struct, IOverlapCollector
        {
            ref var terrain = ref terrainCollider->Terrain;

            // Get the collider AABB in heightfield space
            var aabbT = new FourTransposedAabbs();
            Terrain.QuadTreeWalker walker;
            {
                Aabb aabb = new Aabb
                {
                    Min = input.Aabb.Min * terrain.InverseScale,
                    Max = input.Aabb.Max * terrain.InverseScale
                };
                aabbT.SetAllAabbs(aabb);
                walker = new Terrain.QuadTreeWalker(&terrainCollider->Terrain, aabb);
            }

            // Traverse the tree
            ColliderKey* keys = stackalloc ColliderKey[8];
            while (walker.Pop())
            {
                bool4 hitMask = walker.Bounds.Overlap1Vs4(ref aabbT);
                hitMask &= (walker.Bounds.Ly <= walker.Bounds.Hy); // Mask off empty children
                if (walker.IsLeaf)
                {
                    // Leaf node, distance test against hit child quads
                    if (math.any(hitMask))
                    {
                        int4 hitIndex;
                        int hitCount = math.compress((int*)(&hitIndex), 0, new int4(0, 1, 2, 3), hitMask);
                        int numKeys = 0;
                        for (int iHit = 0; iHit < hitCount; iHit++)
                        {
                            int2 quadIndex = walker.GetQuadIndex(hitIndex[iHit]);
                            keys[numKeys++] = terrain.GetColliderKey(quadIndex, 0);
                            keys[numKeys++] = terrain.GetColliderKey(quadIndex, 1);
                        }
                        collector.AddColliderKeys(keys, numKeys);
                    }
                }
                else
                {
                    // Interior node, add hit child nodes to the stack
                    walker.Push(hitMask);
                }
            }
        }

        #endregion
    }
}
