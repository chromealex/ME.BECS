using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    /// <summary>   The concrete type of a collider. </summary>
    public enum ColliderType : byte
    {
        // Convex types
        /// <summary>   An enum constant representing the convex collider type. </summary>
        Convex = 0,
        /// <summary>   An enum constant representing the sphere collider type. </summary>
        Sphere = 1,
        /// <summary>   An enum constant representing the capsule collider type. </summary>
        Capsule = 2,
        /// <summary>   An enum constant representing the triangle collider type. </summary>
        Triangle = 3,
        /// <summary>   An enum constant representing the quad collider type. </summary>
        Quad = 4,
        /// <summary>   An enum constant representing the box collider type. </summary>
        Box = 5,
        /// <summary>   An enum constant representing the cylinder collider type. </summary>
        Cylinder = 6,

        // Composite types
        /// <summary>   An enum constant representing the mesh collider type. </summary>
        Mesh = 7,
        /// <summary>   An enum constant representing the compound collider type. </summary>
        Compound = 8,

        // Terrain types
        /// <summary>   An enum constant representing the terrain collider type. </summary>
        Terrain = 9
    }

    /// <summary>   The base type of a collider. </summary>
    public enum CollisionType : byte
    {
        /// <summary>   An enum constant representing the convex collision type. </summary>
        Convex = 0,
        /// <summary>   An enum constant representing the composite collision type. </summary>
        Composite = 1,
        /// <summary>   An enum constant representing the collision type. </summary>
        Terrain = 2
    }

    /// <summary>   Interface for colliders. </summary>
    public interface ICollider : ICollidable
    {
        /// <summary>   Gets the collider type. </summary>
        ///
        /// <value> Collider type. </value>
        ColliderType Type { get; }

        /// <summary>   Gets the collision type. </summary>
        ///
        /// <value> Collision Type. </value>
        CollisionType CollisionType { get; }

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        MassProperties MassProperties { get; }

        /// <summary>   The total size of the collider in memory. </summary>
        ///
        /// <value> The size of the memory. </value>
        int MemorySize { get; }

        /// <summary>   Gets the collision filter. </summary>
        ///
        /// <returns>   The collision filter. </returns>
        CollisionFilter GetCollisionFilter();

        /// <summary>   Sets the collision filter. </summary>
        ///
        /// <param name="filter">   Specifies the filter. </param>
        void SetCollisionFilter(CollisionFilter filter);
    }

    // Interface for convex colliders
    internal interface IConvexCollider : ICollider
    {
        Material Material { get; set; }
    }

    // Interface for composite colliders
    internal interface ICompositeCollider : ICollider
    {
        // The maximum number of bits needed to identify a child of this collider.
        uint NumColliderKeyBits { get; }

        // Get a child of this collider.
        // Return false if the key is not valid.
        bool GetChild(ref ColliderKey key, out ChildCollider child);

        // Get a leaf of this collider.
        // Return false if the key is not valid.
        bool GetLeaf(ColliderKey key, out ChildCollider leaf);

        // Get all the leaves of this collider.
        void GetLeaves<T>(ref T collector) where T : struct, ILeafColliderCollector;
    }

    /// <summary>   Interface for collecting leaf colliders. </summary>
    public interface ILeafColliderCollector
    {
        /// <summary>   Adds a leaf to the collector. </summary>
        ///
        /// <param name="key">  The collider key specifying the leaf. </param>
        /// <param name="leaf"> [in,out] The leaf collider. </param>
        void AddLeaf(ColliderKey key, ref ChildCollider leaf);

        /// <summary>   Pushes a composite collider to the collector. </summary>
        ///
        /// <param name="compositeKey">         The composite key. </param>
        /// <param name="parentFromComposite">  The parent from composite transform. </param>
        /// <param name="worldFromParent">      [out] The world from parent transform. </param>
        void PushCompositeCollider(ColliderKeyPath compositeKey, MTransform parentFromComposite, out MTransform worldFromParent);

        /// <summary>   Pops the composite collider from the collector. </summary>
        ///
        /// <param name="numCompositeKeyBits">  Number of composite key bits. </param>
        /// <param name="worldFromParent">      The world from parent transform. </param>
        void PopCompositeCollider(uint numCompositeKeyBits, MTransform worldFromParent);
    }

    /// <summary>
    /// Base struct common to all colliders. Dispatches the interface methods to appropriate
    /// implementations for the collider type.
    /// </summary>
    public struct Collider : ICompositeCollider
    {
        private ColliderHeader m_Header;

        /// <summary>   Indicates whether this collider is unique, i.e., not shared between rigid bodies. </summary>
        ///
        /// <value> True if this collider is unique, false if not. </value>
        public bool IsUnique => m_Header.ForceUniqueBlobID != ColliderConstants.k_SharedBlobID;

        #region ICollider

        /// <summary>   Gets the collider type. </summary>
        ///
        /// <value> Collider type. </value>
        public ColliderType Type => m_Header.Type;

        /// <summary>   Gets the type of the collision. </summary>
        ///
        /// <value> The type of the collision. </value>
        public CollisionType CollisionType => m_Header.CollisionType;

        /// <summary>   Gets the memory size. </summary>
        ///
        /// <value> The size of the memory. </value>
        public int MemorySize
        {
            get
            {
                unsafe
                {
                    fixed(Collider* collider = &this)
                    {
                        switch (collider->Type)
                        {
                            case ColliderType.Convex:
                                return ((ConvexCollider*)collider)->MemorySize;
                            case ColliderType.Sphere:
                                return ((SphereCollider*)collider)->MemorySize;
                            case ColliderType.Capsule:
                                return ((CapsuleCollider*)collider)->MemorySize;
                            case ColliderType.Triangle:
                            case ColliderType.Quad:
                                return ((PolygonCollider*)collider)->MemorySize;
                            case ColliderType.Box:
                                return ((BoxCollider*)collider)->MemorySize;
                            case ColliderType.Cylinder:
                                return ((CylinderCollider*)collider)->MemorySize;
                            case ColliderType.Mesh:
                                return ((MeshCollider*)collider)->MemorySize;
                            case ColliderType.Compound:
                                return ((CompoundCollider*)collider)->MemorySize;
                            case ColliderType.Terrain:
                                return ((TerrainCollider*)collider)->MemorySize;
                            default:
                                //Assert.IsTrue(Enum.IsDefined(typeof(ColliderType), collider->Type));
                                return 0;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the collision filter. In case of a <see cref="CompoundCollider"/>, returns the root
        /// filter.
        /// </summary>
        ///
        /// <returns>   The collision filter. </returns>
        public CollisionFilter GetCollisionFilter() => GetCollisionFilter(ColliderKey.Empty);

        /// <summary>
        /// Gets the collision filter specified by the collider key. The key is only read in case the
        /// collider is <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="GetCollisionFilter()"/>.
        /// </summary>
        ///
        /// <param name="colliderKey">  The collider key. </param>
        ///
        /// <returns>   The collision filter. </returns>
        public CollisionFilter GetCollisionFilter(ColliderKey colliderKey)
        {
            unsafe
            {
                fixed(Collider* collider = &this)
                {
                    switch (collider->Type)
                    {
                        case ColliderType.Convex:
                            return ((ConvexCollider*)collider)->GetCollisionFilter();
                        case ColliderType.Box:
                            return ((BoxCollider*)collider)->GetCollisionFilter();
                        case ColliderType.Capsule:
                            return ((CapsuleCollider*)collider)->GetCollisionFilter();
                        case ColliderType.Cylinder:
                            return ((CylinderCollider*)collider)->GetCollisionFilter();
                        case ColliderType.Quad:
                        case ColliderType.Triangle:
                            return ((PolygonCollider*)collider)->GetCollisionFilter();
                        case ColliderType.Sphere:
                            return ((SphereCollider*)collider)->GetCollisionFilter();
                        case ColliderType.Terrain:
                            return ((TerrainCollider*)collider)->GetCollisionFilter();
                        case ColliderType.Mesh:
                            return ((MeshCollider*)collider)->GetCollisionFilter();
                        case ColliderType.Compound:
                            return ((CompoundCollider*)collider)->GetCollisionFilter(colliderKey);
                        default:
                            return CollisionFilter.Default;
                    }
                }
            }
        }

        /// <summary>   Sets the collision filter. In case of a <see cref="CompoundCollider"/> sets the root filter. </summary>
        ///
        /// <param name="filter">   Specifies the filter. </param>
        public void SetCollisionFilter(CollisionFilter filter) => SetCollisionFilter(filter, ColliderKey.Empty);

        /// <summary>
        /// Sets the collision filter of a child specified by the collider key. Collider key is only read
        /// in case of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="SetCollisionFilter(CollisionFilter)"/>
        /// </summary>
        ///
        /// <param name="filter">       Specifies the filter. </param>
        /// <param name="colliderKey">  The collider key. </param>
        public void SetCollisionFilter(CollisionFilter filter, ColliderKey colliderKey)
        {
            unsafe
            {
                fixed(Collider* collider = &this)
                {
                    switch (collider->Type)
                    {
                        case ColliderType.Convex:
                            ((ConvexCollider*)collider)->SetCollisionFilter(filter);
                            break;
                        case ColliderType.Box:
                            ((BoxCollider*)collider)->SetCollisionFilter(filter);
                            break;
                        case ColliderType.Capsule:
                            ((CapsuleCollider*)collider)->SetCollisionFilter(filter);
                            break;
                        case ColliderType.Cylinder:
                            ((CylinderCollider*)collider)->SetCollisionFilter(filter);
                            break;
                        case ColliderType.Quad:
                        case ColliderType.Triangle:
                            ((PolygonCollider*)collider)->SetCollisionFilter(filter);
                            break;
                        case ColliderType.Sphere:
                            ((SphereCollider*)collider)->SetCollisionFilter(filter);
                            break;
                        case ColliderType.Terrain:
                            ((TerrainCollider*)collider)->SetCollisionFilter(filter);
                            break;
                        case ColliderType.Mesh:
                            ((MeshCollider*)collider)->SetCollisionFilter(filter);
                            break;
                        case ColliderType.Compound:
                            ((CompoundCollider*)collider)->SetCollisionFilter(filter, colliderKey);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the friction. In case of a <see cref="CompoundCollider"/>, this
        /// behaves as <see cref="GetFriction(ColliderKey)"/> with ColliderKey.Zero passed in.
        /// </summary>
        ///
        /// <returns>   The friction. </returns>
        public float GetFriction() => GetFriction(ColliderKey.Empty);

        /// <summary>
        /// Gets the friction of a child specified by the collider key. Collider key is only read in case
        /// of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="GetFriction()"/>
        /// . In case of <see cref="CompoundCollider"/> if the colider key is empty, it will return the friction of the first child.
        /// </summary>
        ///
        /// <param name="colliderKey">  The collider key. </param>
        ///
        /// <returns>   The friction. </returns>
        public float GetFriction(ColliderKey colliderKey)
        {
            SafetyChecks.CheckMaterialGetterValid(Type, colliderKey, "GetFriction");
            return GetMaterial(colliderKey).Friction;
        }

        /// <summary>
        /// Sets the friction. In case of a <see cref="CompoundCollider"/>, this
        /// behaves as <see cref="SetFriction(System.Single,Unity.Physics.ColliderKey)"/>
        /// with ColliderKey.Zero passed in.
        /// </summary>
        ///
        /// <param name="friction"> The friction. </param>
        public void SetFriction(float friction) => SetFriction(friction, ColliderKey.Empty);

        /// <summary>
        /// Sets the friction of a child specified by the collider key. Collider key is only read in case
        /// of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="SetFriction(float)()"/>
        /// . In case of <see cref="CompoundCollider"/> if the collider key is empty, it will set the
        /// friction of the all children.
        /// </summary>
        ///
        /// <param name="friction">     The friction. </param>
        /// <param name="colliderKey">  The collider key. </param>
        public void SetFriction(float friction, ColliderKey colliderKey)
        {
            Material material = GetMaterial(colliderKey);
            material.Friction = friction;
            SetMaterialField(material, colliderKey, Material.MaterialField.Friction);
        }

        /// <summary>Gets the restitution. In case of a <see cref="CompoundCollider"/>,
        /// this behaves as <see cref="GetRestitution(ColliderKey)"/> with ColliderKey.Zero
        /// passed in</summary>
        ///
        /// <returns>   The restitution. </returns>
        public float GetRestitution() => GetRestitution(ColliderKey.Empty);

        /// <summary>
        /// Gets a restitution of a child specified by the collider key. Collider key is only read in
        /// case of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="GetRestitution()"/>
        /// . In case of <see cref="CompoundCollider"/> if the colider key is empty, it will return the
        /// restitution of the first child.
        /// </summary>
        ///
        /// <param name="colliderKey">  The collider key. </param>
        ///
        /// <returns>   The restitution. </returns>
        public float GetRestitution(ColliderKey colliderKey)
        {
            SafetyChecks.CheckMaterialGetterValid(Type, colliderKey, "GetRestitution");
            return GetMaterial(colliderKey).Restitution;
        }

        /// <summary>
        /// Sets the restitution. In case of a <see cref="CompoundCollider"/>,
        /// this behaves as <see cref="SetRestitution(System.Single,Unity.Physics.ColliderKey)"/>
        /// with ColliderKey.Zero passed in.
        /// </summary>
        ///
        /// <param name="restitution">  The restitution. </param>
        public void SetRestitution(float restitution) => SetRestitution(restitution, ColliderKey.Empty);

        /// <summary>
        /// Sets the restitution of a child specified by the collider key. Collider key is only read in
        /// case of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="SetRestitution(float)()"/>
        /// . In case of <see cref="CompoundCollider"/> if the collider key is empty, it will set the
        /// restitution of the all children.
        /// </summary>
        ///
        /// <param name="restitution">  The restitution. </param>
        /// <param name="colliderKey">  The collider key. </param>
        public void SetRestitution(float restitution, ColliderKey colliderKey)
        {
            Material material = GetMaterial(colliderKey);
            material.Restitution = restitution;
            SetMaterialField(material, colliderKey, Material.MaterialField.Restitution);
        }

        /// <summary>Gets the collision response. In case of a <see cref="CompoundCollider"/>,
        /// this behaves as <see cref="GetCollisionResponse(ColliderKey)"/> with
        /// ColliderKey.Zero passed in.</summary>
        ///
        /// <returns>   The collision response. </returns>
        public CollisionResponsePolicy GetCollisionResponse() => GetCollisionResponse(ColliderKey.Empty);

        /// <summary>
        /// Gets collision response of a child specified by the collider key. Collider key is only read
        /// in case of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="GetCollisionResponse()"/>
        /// . In case of <see cref="CompoundCollider"/> if the colider key is empty, it will return the
        /// collision response of the first child.
        /// </summary>
        ///
        /// <param name="colliderKey">  The collider key. </param>
        ///
        /// <returns>   The collision response. </returns>
        public CollisionResponsePolicy GetCollisionResponse(ColliderKey colliderKey)
        {
            SafetyChecks.CheckMaterialGetterValid(Type, colliderKey, "GetCollisionResponse");
            return GetMaterial(colliderKey).CollisionResponse;
        }

        /// <summary>Sets collision response. In case of a <see cref="CompoundCollider"/>,
        /// this behaves as <see cref="SetCollisionResponse(CollisionResponsePolicy, ColliderKey)"/>
        /// with ColliderKey.Zero passed in.</summary>
        ///
        /// <param name="collisionResponse">    The collision response. </param>
        public void SetCollisionResponse(CollisionResponsePolicy collisionResponse) => SetCollisionResponse(collisionResponse, ColliderKey.Empty);

        /// <summary>   Sets collision response of a child specified by the collider key. Collider key is only read in
        /// case of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="SetCollisionResponse(CollisionResponsePolicy)"/>
        /// . In case of <see cref="CompoundCollider"/> if the collider key is empty, it will set the
        /// collision response of the all children. </summary>
        ///
        /// <param name="collisionResponse">    The collision response. </param>
        /// <param name="colliderKey">          The collider key. </param>
        public void SetCollisionResponse(CollisionResponsePolicy collisionResponse, ColliderKey colliderKey)
        {
            Material material = GetMaterial(colliderKey);
            material.CollisionResponse = collisionResponse;
            SetMaterialField(material, colliderKey, Material.MaterialField.CollisionResponsePolicy);
        }

        internal Material GetMaterial(ColliderKey colliderKey)
        {
            unsafe
            {
                fixed(Collider* ptr = &this)
                {
                    switch (CollisionType)
                    {
                        case CollisionType.Convex:
                        {
                            ConvexCollider* cvxPtr = (ConvexCollider*)ptr;
                            return cvxPtr->Material;
                        }
                        case CollisionType.Terrain:
                        case CollisionType.Composite:
                        {
                            switch (Type)
                            {
                                case ColliderType.Terrain:
                                {
                                    TerrainCollider* terrainPtr = (TerrainCollider*)ptr;
                                    return terrainPtr->Material;
                                }
                                case ColliderType.Mesh:
                                {
                                    MeshCollider* meshPtr = (MeshCollider*)ptr;
                                    return meshPtr->GetMaterial();
                                }
                                case ColliderType.Compound:
                                {
                                    CompoundCollider* compoundPtr = (CompoundCollider*)ptr;
                                    return compoundPtr->GetMaterial(colliderKey);
                                }
                                default:
                                    SafetyChecks.ThrowInvalidOperationException("Invalid ColliderType");
                                    break;
                            }
                            break;
                        }
                        default:
                            SafetyChecks.ThrowInvalidOperationException("Invalid CollisionType");
                            break;
                    }
                }

                return Material.Default;
            }
        }

        internal void SetMaterialField(Material material, ColliderKey colliderKey, Material.MaterialField option)
        {
            unsafe
            {
                fixed(Collider* ptr = &this)
                {
                    switch (ptr->CollisionType)
                    {
                        case CollisionType.Convex:
                        {
                            ConvexCollider* cvxPtr = (ConvexCollider*)ptr;
                            cvxPtr->SetMaterialField(material, option);
                        }
                        break;
                        case CollisionType.Terrain:
                        case CollisionType.Composite:
                            switch (ptr->Type)
                            {
                                case ColliderType.Mesh:
                                {
                                    // Mesh, ignore collider key
                                    MeshCollider* meshCollider = (MeshCollider*)ptr;
                                    meshCollider->SetMaterialField(material, option);
                                    break;
                                }
                                case ColliderType.Compound:
                                {
                                    CompoundCollider* compoundCollider = (CompoundCollider*)ptr;
                                    compoundCollider->SetMaterialField(material, colliderKey, option);
                                    break;
                                }
                                case ColliderType.Terrain:
                                {
                                    TerrainCollider* terrainPtr = (TerrainCollider*)ptr;
                                    terrainPtr->SetMaterialField(material, option);
                                    break;
                                }
                                default:
                                    SafetyChecks.ThrowInvalidOperationException("Invalid ColliderType");
                                    break;
                            }
                            break;
                        default:
                            SafetyChecks.ThrowInvalidOperationException("Invalid CollisionType");
                            break;
                    }
                }
            }
        }

        // Indicates whether collider should collide normally with others,
        // or skip collision, but still move and intercept queries
        internal bool RespondsToCollision
        {
            get
            {
                unsafe
                {
                    fixed(Collider* collider = &this)
                    {
                        switch (collider->Type)
                        {
                            case ColliderType.Convex:
                                return ((ConvexCollider*)collider)->RespondsToCollision;
                            case ColliderType.Sphere:
                                return ((SphereCollider*)collider)->RespondsToCollision;
                            case ColliderType.Capsule:
                                return ((CapsuleCollider*)collider)->RespondsToCollision;
                            case ColliderType.Triangle:
                            case ColliderType.Quad:
                                return ((PolygonCollider*)collider)->RespondsToCollision;
                            case ColliderType.Box:
                                return ((BoxCollider*)collider)->RespondsToCollision;
                            case ColliderType.Cylinder:
                                return ((CylinderCollider*)collider)->RespondsToCollision;
                            case ColliderType.Mesh:
                                return ((MeshCollider*)collider)->RespondsToCollision;
                            case ColliderType.Compound:
                                return ((CompoundCollider*)collider)->RespondsToCollision;
                            case ColliderType.Terrain:
                                return ((TerrainCollider*)collider)->RespondsToCollision;
                            default:
                                //Assert.IsTrue(Enum.IsDefined(typeof(ColliderType), collider->Type));
                                return false;
                        }
                    }
                }
            }
        }

        /// <summary>   Gets the mass properties. </summary>
        ///
        /// <value> The mass properties. </value>
        public MassProperties MassProperties
        {
            get
            {
                unsafe
                {
                    fixed(Collider* collider = &this)
                    {
                        switch (collider->Type)
                        {
                            case ColliderType.Convex:
                                return ((ConvexCollider*)collider)->MassProperties;
                            case ColliderType.Sphere:
                                return ((SphereCollider*)collider)->MassProperties;
                            case ColliderType.Capsule:
                                return ((CapsuleCollider*)collider)->MassProperties;
                            case ColliderType.Triangle:
                            case ColliderType.Quad:
                                return ((PolygonCollider*)collider)->MassProperties;
                            case ColliderType.Box:
                                return ((BoxCollider*)collider)->MassProperties;
                            case ColliderType.Cylinder:
                                return ((CylinderCollider*)collider)->MassProperties;
                            case ColliderType.Mesh:
                                return ((MeshCollider*)collider)->MassProperties;
                            case ColliderType.Compound:
                                return ((CompoundCollider*)collider)->MassProperties;
                            case ColliderType.Terrain:
                                return ((TerrainCollider*)collider)->MassProperties;
                            default:
                                //Assert.IsTrue(Enum.IsDefined(typeof(ColliderType), collider->Type));
                                return MassProperties.UnitSphere;
                        }
                    }
                }
            }
        }

        #endregion

        #region ICompositeCollider

        /// <summary>   Gets the number of collider key bits. </summary>
        ///
        /// <value> The total number of collider key bits. </value>
        public uint NumColliderKeyBits
        {
            get
            {
                unsafe
                {
                    fixed(Collider* collider = &this)
                    {
                        switch (collider->Type)
                        {
                            case ColliderType.Mesh:
                                return ((MeshCollider*)collider)->NumColliderKeyBits;
                            case ColliderType.Compound:
                                return ((CompoundCollider*)collider)->NumColliderKeyBits;
                            case ColliderType.Terrain:
                                return ((TerrainCollider*)collider)->NumColliderKeyBits;
                            default:
                                if (collider->CollisionType != CollisionType.Convex)
                                {
                                    SafetyChecks.ThrowNotImplementedException();
                                }
                                return 0;
                        }
                    }
                }
            }
        }

        /// <summary>   Gets the total number of number collider key bits. </summary>
        ///
        /// <value> The total number of number collider key bits. </value>
        public uint TotalNumColliderKeyBits
        {
            get
            {
                unsafe
                {
                    fixed(Collider* collider = &this)
                    {
                        switch (collider->Type)
                        {
                            case ColliderType.Mesh:
                                return ((MeshCollider*)collider)->TotalNumColliderKeyBits;
                            case ColliderType.Compound:
                                return ((CompoundCollider*)collider)->TotalNumColliderKeyBits;
                            case ColliderType.Terrain:
                                return ((TerrainCollider*)collider)->TotalNumColliderKeyBits;
                            default:
                                if (collider->CollisionType != CollisionType.Convex)
                                {
                                    SafetyChecks.ThrowNotImplementedException();
                                }
                                return 0;
                        }
                    }
                }
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
            unsafe
            {
                fixed(Collider* collider = &this)
                {
                    switch (collider->Type)
                    {
                        case ColliderType.Mesh:
                            return ((MeshCollider*)collider)->GetChild(ref key, out child);
                        case ColliderType.Compound:
                            return ((CompoundCollider*)collider)->GetChild(ref key, out child);
                        case ColliderType.Terrain:
                            return ((TerrainCollider*)collider)->GetChild(ref key, out child);
                        default:
                            //Assert.IsTrue(Enum.IsDefined(typeof(ColliderType), collider->Type));
                            child = new ChildCollider();
                            return false;
                    }
                }
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
                fixed(Collider* collider = &this)
                {
                    return GetLeafCollider(out leaf, collider, key, RigidTransform.identity);
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
                fixed(Collider* collider = &this)
                {
                    switch (collider->Type)
                    {
                        case ColliderType.Mesh:
                            ((MeshCollider*)collider)->GetLeaves(ref collector);
                            break;
                        case ColliderType.Compound:
                            ((CompoundCollider*)collider)->GetLeaves(ref collector);
                            break;
                        case ColliderType.Terrain:
                            ((TerrainCollider*)collider)->GetLeaves(ref collector);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Get a leaf of a collider hierarchy. Return false if the key is not valid for the collider.
        /// </summary>
        ///
        /// <param name="leaf">             [out] The leaf collider in world space. </param>
        /// <param name="root">             [in,out] If non-null, the root collider. </param>
        /// <param name="key">              The key identifying the leaf collider. </param>
        /// <param name="rootTransform">    The root transform in world space. </param>
        /// <param name="rootUniformScale"> (Optional) The root uniform scale. </param>
        ///
        /// <returns>   True if there is a leaf with the specified collider key, false otherwise. </returns>
        public static unsafe bool GetLeafCollider(out ChildCollider leaf, Collider* root, ColliderKey key, RigidTransform rootTransform, float rootUniformScale = 1.0f)
        {
            leaf = new ChildCollider(root, RigidTransform.identity);
            while (leaf.Collider != null)
            {
                if (!leaf.Collider->GetChild(ref key, out ChildCollider child))
                {
                    break;
                }
                leaf = new ChildCollider(leaf, child);
            }

            var worldFromChild = new RigidTransform
            {
                rot = math.mul(rootTransform.rot, leaf.TransformFromChild.rot),
                pos = math.mul(rootTransform.rot, leaf.TransformFromChild.pos * rootUniformScale) + rootTransform.pos
            };

            leaf.TransformFromChild = worldFromChild;

            return (leaf.Collider == null || leaf.Collider->CollisionType == CollisionType.Convex);
        }

        #endregion

        #region ICollidable

        /// <summary>   Calculate a bounding box around this collider. </summary>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb()
        {
            return CalculateAabb(RigidTransform.identity);
        }

        /// <summary>   Calculate a bounding box around this collider, at the given transform. </summary>
        ///
        /// <param name="transform">    The transform. </param>
        /// <param name="uniformScale"> (Optional) The uniform scale. </param>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb(RigidTransform transform, float uniformScale = 1.0f)
        {
            unsafe
            {
                fixed(Collider* collider = &this)
                {
                    switch (collider->Type)
                    {
                        case ColliderType.Convex:
                            return ((ConvexCollider*)collider)->CalculateAabb(transform, uniformScale);
                        case ColliderType.Sphere:
                            return ((SphereCollider*)collider)->CalculateAabb(transform, uniformScale);
                        case ColliderType.Capsule:
                            return ((CapsuleCollider*)collider)->CalculateAabb(transform, uniformScale);
                        case ColliderType.Triangle:
                        case ColliderType.Quad:
                            return ((PolygonCollider*)collider)->CalculateAabb(transform, uniformScale);
                        case ColliderType.Box:
                            return ((BoxCollider*)collider)->CalculateAabb(transform, uniformScale);
                        case ColliderType.Cylinder:
                            return ((CylinderCollider*)collider)->CalculateAabb(transform, uniformScale);
                        case ColliderType.Mesh:
                            return ((MeshCollider*)collider)->CalculateAabb(transform, uniformScale);
                        case ColliderType.Compound:
                            return ((CompoundCollider*)collider)->CalculateAabb(transform, uniformScale);
                        case ColliderType.Terrain:
                            return ((TerrainCollider*)collider)->CalculateAabb(transform, uniformScale);
                        default:
                            //Assert.IsTrue(Enum.IsDefined(typeof(ColliderType), collider->Type));
                            return Aabb.Empty;
                    }
                }
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
                fixed(Collider* target = &this)
                {
                    return RaycastQueries.RayCollider(input, target, ref collector);
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
                fixed(Collider* target = &this)
                {
                    return ColliderCastQueries.ColliderCollider(input, target, ref collector);
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
                fixed(Collider* target = &this)
                {
                    return DistanceQueries.PointCollider(input, target, ref collector);
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
                fixed(Collider* target = &this)
                {
                    return DistanceQueries.ColliderCollider(input, target, ref collector);
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

        /// <summary>
        /// This function clones the Collider and wraps it in a BlobAssetReference. The caller is
        /// responsible for appropriately calling `Dispose` on the result.
        /// Returned collider is guaranteed to be <see cref="Collider.IsUnique">unique</see>.
        /// </summary>
        ///
        /// <returns>   A clone of the Collider wrapped in a BlobAssetReference. </returns>
        public BlobAssetReference<Collider> Clone()
        {
            unsafe
            {
                var clone = BlobAssetReference<Collider>.Create(UnsafeUtility.AddressOf(ref this), MemorySize);
                // reset the version
                ((Collider*)clone.GetUnsafePtr())->m_Header.Version = 1;
                // flag the blob as unique
                ((Collider*)clone.GetUnsafePtr())->m_Header.ForceUniqueBlobID = ~ColliderConstants.k_SharedBlobID;

                return clone;
            }
        }

        internal void SetForceUniqueID(uint id) => m_Header.ForceUniqueBlobID = id;
    }

    internal struct ColliderConstants
    {
        /// Indicates that a collider blob can be shared across multiple PhysicsCollider components.
        /// Used during collider baking.
        public const uint k_SharedBlobID = 0u;
    }

    /// <summary>   Header common to all colliders. </summary>
    internal struct ColliderHeader
    {
        public ColliderType Type;
        public CollisionType CollisionType;

        public byte Version;    // increment whenever the collider data has changed
        public byte Magic;      // always = 0xff (for validation)

        public uint ForceUniqueBlobID;  // ID used to force a unique collider blob in entities.
                                        // When a collider is manually created or cloned, the ID is set to ~ColliderConstants.k_SharedBlobID
                                        // marking the collider as unique.
                                        // When a collider is created during baking through the blob asset store, the ID is set to
                                        // ColliderConstants.k_SharedBlobID in order to enable sharing of identical collider blobs among entities.
                                        // The sharing is disabled by the baking systems through setting of this value to a unique value when the user
                                        // requests unique collider blobs via the force unique authoring option.

        public CollisionFilter Filter;
    }

    /// <summary>   Header common to all convex colliders. </summary>
    internal struct ConvexColliderHeader
    {
        public ColliderType Type;
        public CollisionType CollisionType;

        public byte Version;
        public byte Magic;

        public uint ForceUniqueBlobID;  // ID used to force a unique collider blob in entities.
                                        // When a collider is manually created or cloned, the ID is set to ~ColliderConstants.k_SharedBlobID
                                        // marking the collider as unique.
                                        // When a collider is created during baking through the blob asset store, the ID is set to
                                        // ColliderConstants.k_SharedBlobID in order to enable sharing of identical collider blobs among entities.
                                        // The sharing is disabled by the baking systems through setting of this value to a unique value when the user
                                        // requests unique collider blobs via the force unique authoring option.

        public CollisionFilter Filter;
        public Material Material;
    }

    /// <summary>
    /// An opaque key which packs a path to a specific leaf of a collider hierarchy into a single
    /// integer.
    /// </summary>
    public struct ColliderKey : IEquatable<ColliderKey>, IComparable<ColliderKey>
    {
        /// <summary>   Gets or sets the value. </summary>
        ///
        /// <value> The value. </value>
        public uint Value { get; internal set; }

        /// <summary>   Empty collider key. </summary>
        public static readonly ColliderKey Empty = new ColliderKey { Value = uint.MaxValue };

        /// <summary>   Implicit cast that converts the given ColliderKey to an uint. </summary>
        ///
        /// <param name="key">  The key. </param>
        ///
        /// <returns>   The result of the operation. </returns>
        public static implicit operator uint(ColliderKey key) => key.Value;

        /// <summary>   Implicit cast that converts the given uint to a ColliderKey. </summary>
        ///
        /// <param name="key">  The key. </param>
        ///
        /// <returns>   The result of the operation. </returns>
        public static implicit operator ColliderKey(uint key) => new ColliderKey { Value = key};

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="numSubKeyBits">    Number of sub key bits. </param>
        /// <param name="subKey">           The sub key. </param>
        public ColliderKey(uint numSubKeyBits, uint subKey)
        {
            Value = uint.MaxValue;
            PushSubKey(numSubKeyBits, subKey);
        }

        /// <summary>   Tests if this ColliderKey is considered equal to another. </summary>
        ///
        /// <param name="other">    The collider key to compare to this object. </param>
        ///
        /// <returns>   True if the objects are considered equal, false if they are not. </returns>
        public bool Equals(ColliderKey other)
        {
            return Value == other.Value;
        }

        /// <summary>
        /// Compares this ColliderKey object to another to determine their relative ordering.
        /// </summary>
        ///
        /// <param name="other">    Another instance to compare. </param>
        ///
        /// <returns>
        /// Negative if this object is less than the other, 0 if they are equal, or positive if this is
        /// greater.
        /// </returns>
        public int CompareTo(ColliderKey other)
        {
            return (int)(Value - other.Value);
        }

        /// <summary>
        /// Append a sub key to the front of the path "numSubKeyBits" is the maximum number of bits
        /// required to store any value for this sub key.
        /// </summary>
        ///
        /// <param name="numSubKeyBits">    Number of sub key bits. </param>
        /// <param name="subKey">           The sub key. </param>
        public void PushSubKey(uint numSubKeyBits, uint subKey)
        {
            uint parentPart = (uint)((ulong)subKey << 32 - (int)numSubKeyBits);
            uint childPart = Value >> (int)numSubKeyBits;
            Value = parentPart | childPart;
        }

        /// <summary>
        /// Extract a sub key from the front of the path. "numSubKeyBits" is the maximum number of bits
        /// required to store any value for this sub key. Returns false if the key is empty.
        /// </summary>
        ///
        /// <param name="numSubKeyBits">    Number of sub key bits. </param>
        /// <param name="subKey">           [out] The sub key. </param>
        ///
        /// <returns>   False if the key is empty, true otherwise. </returns>
        public bool PopSubKey(uint numSubKeyBits, out uint subKey)
        {
            if (Value != uint.MaxValue)
            {
                subKey = Value >> (32 - (int)numSubKeyBits);
                Value = ((1 + Value) << (int)numSubKeyBits) - 1;
                return true;
            }

            subKey = uint.MaxValue;
            return false;
        }

        /// <summary>   Convert this object into a string representation. </summary>
        ///
        /// <returns>   A string that represents this object. </returns>
        public override string ToString() => $"ColliderKey {{ {nameof(Value)} = {Value} }}";
    }

    /// <summary>
    /// Stores a ColliderKey along with the number of bits in it that are used. This is useful for
    /// building keys from root to leaf, the bit count shows where to place the child key bits.
    /// </summary>
    public struct ColliderKeyPath
    {
        private ColliderKey m_Key;
        private uint m_NumKeyBits;

        /// <summary>   Gets the collider key. </summary>
        ///
        /// <value> The collider key. </value>
        public ColliderKey Key => m_Key;

        /// <summary>   Gets the empty <see cref="ColliderKeyPath"/>. </summary>
        ///
        /// <value> The empty <see cref="ColliderKeyPath"/>. </value>
        public static ColliderKeyPath Empty => new ColliderKeyPath(ColliderKey.Empty, 0);

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="key">          The key. </param>
        /// <param name="numKeyBits">   Number of key bits. </param>
        public ColliderKeyPath(ColliderKey key, uint numKeyBits)
        {
            m_Key = key;
            m_NumKeyBits = numKeyBits;
        }

        /// <summary>   Append the local key for a child of the shape referenced by this path. </summary>
        ///
        /// <param name="child">    The child. </param>
        public void PushChildKey(ColliderKeyPath child)
        {
            m_Key.Value &= (uint)(child.m_Key.Value >> (int)m_NumKeyBits | (ulong)0xffffffff << (int)(32 - m_NumKeyBits));
            m_NumKeyBits += child.m_NumKeyBits;
        }

        /// <summary>   Remove the most leafward shape's key from this path. </summary>
        ///
        /// <param name="numChildKeyBits">  Number of child key bits. </param>
        public void PopChildKey(uint numChildKeyBits)
        {
            m_NumKeyBits -= numChildKeyBits;
            m_Key.Value |= (uint)((ulong)0xffffffff >> (int)m_NumKeyBits);
        }

        /// <summary>
        /// Get the collider key for a leaf shape that is a child of the shape referenced by this path.
        /// </summary>
        ///
        /// <param name="leafKeyLocal"> The local leaf key. </param>
        ///
        /// <returns>   The local leaf key. </returns>
        public ColliderKey GetLeafKey(ColliderKey leafKeyLocal)
        {
            ColliderKeyPath leafPath = this;
            leafPath.PushChildKey(new ColliderKeyPath(leafKeyLocal, 0));
            return leafPath.Key;
        }
    }

    /// <summary>   A pair of collider keys. </summary>
    public struct ColliderKeyPair
    {
        // B before A for consistency with other pairs.
        ///  <summary>   The collider key b. </summary>
        public ColliderKey ColliderKeyB;
        /// <summary>   The collider key a. </summary>
        public ColliderKey ColliderKeyA;

        /// <summary>   Gets the empty <see cref="ColliderKeyPair"/>. </summary>
        ///
        /// <value> The empty <see cref="ColliderKeyPair"/>. </value>
        public static ColliderKeyPair Empty => new ColliderKeyPair { ColliderKeyB = ColliderKey.Empty, ColliderKeyA = ColliderKey.Empty };
    }

    /// <summary>   A child/leaf collider. </summary>
    public unsafe struct ChildCollider
    {
        private readonly Collider* m_Collider; // if null, the result is in "Polygon" instead
        private PolygonCollider m_Polygon;

        /// <summary>   The transform of the child collider in whatever space it was queried from. </summary>
        ///
        /// <value> The transform from child. </value>
        public RigidTransform TransformFromChild { get; internal set; }

        /// <summary>   The original Entity from a hierarchy that Child is associated with. </summary>
        public Entity Entity;

        /// <summary>   Gets the collider. </summary>
        ///
        /// <value> The collider. </value>
        public unsafe Collider* Collider
        {
            get
            {
                //Assert.IsTrue(m_Collider != null || m_Polygon.Vertices.Length > 0, "Accessing uninitialized Collider");
                fixed(ChildCollider* self = &this)
                {
                    return (self->m_Collider != null) ? self->m_Collider : (Collider*)&self->m_Polygon;
                }
            }
        }

        /// <summary>   Create from collider. </summary>
        ///
        /// <param name="collider"> [in] If non-null, the collider. </param>
        public ChildCollider(Collider* collider)
        {
            m_Collider = collider;
            m_Polygon = new PolygonCollider();
            TransformFromChild = new RigidTransform(quaternion.identity, float3.zero);
            Entity = Entity.Null;
        }

        /// <summary>   Create from body. </summary>
        ///
        /// <param name="collider">     [in] If non-null, the collider. </param>
        /// <param name="transform">    The transform. </param>
        public ChildCollider(Collider* collider, RigidTransform transform)
        {
            m_Collider = collider;
            m_Polygon = new PolygonCollider();
            TransformFromChild = transform;
            Entity = Entity.Null;
        }

        /// <summary>   Create from body with Entity indirection. </summary>
        ///
        /// <param name="collider">     [in] If non-null, the collider. </param>
        /// <param name="transform">    The transform. </param>
        /// <param name="entity">       The entity. </param>
        public ChildCollider(Collider* collider, RigidTransform transform, Entity entity)
        {
            m_Collider = collider;
            m_Polygon = new PolygonCollider();
            TransformFromChild = transform;
            Entity = entity;
        }

        /// <summary>   Create as triangle, from 3 vertices. </summary>
        ///
        /// <param name="a">        Vertex a. </param>
        /// <param name="b">        Vertex b. </param>
        /// <param name="c">        Vertex c. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        public ChildCollider(float3 a, float3 b, float3 c, CollisionFilter filter, Material material)
        {
            m_Collider = null;
            m_Polygon = new PolygonCollider();
            m_Polygon.InitAsTriangle(a, b, c, filter, material);
            TransformFromChild = new RigidTransform(quaternion.identity, float3.zero);
            Entity = Entity.Null;
        }

        /// <summary>   Create as quad, from 4 coplanar vertices. </summary>
        ///
        /// <param name="a">        Vertex a. </param>
        /// <param name="b">        Vertex b. </param>
        /// <param name="c">        Vertex c. </param>
        /// <param name="d">        Vertex d. </param>
        /// <param name="filter">   Specifies the filter. </param>
        /// <param name="material"> The material. </param>
        public ChildCollider(float3 a, float3 b, float3 c, float3 d, CollisionFilter filter, Material material)
        {
            m_Collider = null;
            m_Polygon = new PolygonCollider();
            m_Polygon.InitAsQuad(a, b, c, d, filter, material);
            TransformFromChild = new RigidTransform(quaternion.identity, float3.zero);
            Entity = Entity.Null;
        }

        /// <summary>
        /// Combine a parent ChildCollider with another ChildCollider describing one of its children.
        /// </summary>
        ///
        /// <param name="parent">   The parent. </param>
        /// <param name="child">    The child. </param>
        public ChildCollider(ChildCollider parent, ChildCollider child)
        {
            m_Collider = child.m_Collider;
            m_Polygon = child.m_Polygon;
            TransformFromChild = math.mul(parent.TransformFromChild, child.TransformFromChild);
            // TODO: Only a CompoundCollider setup in code is likely to have a Entity associated with a PolygonCollider.
            // So if we have a PolygonCollider should we really return the parent's associated Entity instead of the child's?
            // for example: Entity = m_Polygon.Vertices.Length == 0 ? child.Entity : parent.Entity;
            Entity = child.Entity;
        }
    }
}
