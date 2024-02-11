using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics.Extensions;

namespace Unity.Physics.Aspects
{
/// <summary>   A collider aspect. Contains transform data and a collider. </summary>
    public readonly partial struct ColliderAspect : IAspect, IAspectQueryable, ICollidable
    {
        internal readonly RefRW<LocalTransform> m_Transform;
        internal readonly RefRW<PhysicsCollider> m_Collider;

        /// <summary>   The entity of this aspect. </summary>
        public readonly Entity Entity;

        /// <summary>   Gets or sets the world transform of a collider aspect. </summary>
        ///
        /// <value> The world space transform. </value>
        public LocalTransform WorldFromCollider
        {
            get => m_Transform.ValueRO;
            set => m_Transform.ValueRW = value;
        }

        /// <summary>   Gets or sets the world space position. </summary>
        ///
        /// <value> The world space position. </value>
        public float3 Position
        {
            get => m_Transform.ValueRO.Position;
            set => m_Transform.ValueRW.Position = value;
        }

        /// <summary>   Gets or sets the world space rotation. </summary>
        ///
        /// <value> The world space rotation. </value>
        public quaternion Rotation
        {
            get => m_Transform.ValueRO.Rotation;
            set => m_Transform.ValueRW.Rotation = value;
        }

        /// <summary>   Gets or sets the uniform scale. </summary>
        ///
        /// <value> The scale. </value>
        public float Scale
        {
            get => m_Transform.ValueRO.Scale;
            set => m_Transform.ValueRW.Scale = value;
        }

        /// <summary>   Gets the collider type. </summary>
        ///
        /// <value> The colldier type. </value>
        public ColliderType Type => m_Collider.ValueRO.Value.Value.Type;

        /// <summary>   Gets the collision type. </summary>
        ///
        /// <value> The collision type. </value>
        public CollisionType CollisionType => m_Collider.ValueRO.Value.Value.CollisionType;

        /// <summary>   Gets or sets the collider. </summary>
        ///
        /// <value> The collider. </value>
        public BlobAssetReference<Collider> Collider
        {
            get => m_Collider.ValueRO.Value;
            set => m_Collider.ValueRW.Value = value;
        }

        /// <summary>
        /// Gets the friction. In case of a <see cref="CompoundCollider"/>, this behaves
        /// as <see cref="GetFriction(ColliderKey)"/> with ColliderKey.Zero passed in.
        /// </summary>
        ///
        /// <returns>The friction. </returns>
        public float GetFriction() => m_Collider.ValueRO.Value.As<Collider>().GetFriction();

        /// <summary>
        /// Gets the friction of a child specified by the collider key. Collider key is only read in case
        /// of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="GetFriction()"/>
        /// . In case of <see cref="CompoundCollider"/> if the colider key is empty, it will return the friction of the first child.
        /// </summary>
        ///
        /// <param name="colliderKey">  The collider key. </param>
        ///
        /// <returns>   The friction. </returns>
        public float GetFriction(ColliderKey colliderKey) => m_Collider.ValueRO.Value.As<Collider>().GetFriction(colliderKey);

        /// <summary>
        /// Sets the friction. In case of a <see cref="CompoundCollider"/>, this behaves
        /// as <see cref="SetFriction(System.Single,Unity.Physics.ColliderKey)"/> with ColliderKey.Zero passed in.
        /// </summary>
        ///
        /// <param name="friction"> The friction. </param>
        public void SetFriction(float friction) => m_Collider.ValueRW.Value.As<Collider>().SetFriction(friction);

        /// <summary>
        /// Sets the friction of a child specified by the collider key. Collider key is only read in case
        /// of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="SetFriction(float)()"/>
        /// . In case of <see cref="CompoundCollider"/> if the collider key is empty, it will set the
        /// friction of the all children.
        /// </summary>
        ///
        /// <param name="friction">     The friction. </param>
        /// <param name="colliderKey">  The collider key. </param>
        public void SetFriction(float friction, ColliderKey colliderKey) => m_Collider.ValueRW.Value.As<Collider>().SetFriction(friction, colliderKey);

        /// <summary> Gets the collision response. In case of a <see cref="CompoundCollider"/>,
        /// this behaves as <see cref="GetCollisionResponse(ColliderKey)"/> with ColliderKey.Zero passed in.</summary>
        ///
        /// <returns>   The collision response. </returns>
        public CollisionResponsePolicy GetCollisionResponse() => m_Collider.ValueRO.Value.As<Collider>().GetCollisionResponse();

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
        public CollisionResponsePolicy GetCollisionResponse(ColliderKey colliderKey) => m_Collider.ValueRO.Value.As<Collider>().GetCollisionResponse(colliderKey);

        /// <summary>Sets collision response. In case of a <see cref="CompoundCollider"/>,
        /// this behaves as <see cref="SetCollisionResponse(CollisionResponsePolicy, ColliderKey)"/>
        /// with ColliderKey.Zero passed in.</summary>
        ///
        /// <param name="collisionResponse">    The collision response. </param>
        public void SetCollisionResponse(CollisionResponsePolicy collisionResponse) => m_Collider.ValueRW.Value.As<Collider>().SetCollisionResponse(collisionResponse);

        /// <summary>   Sets collision response of a child specified by the collider key. Collider key is only read in
        /// case of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="SetCollisionResponse(CollisionResponsePolicy)"/>
        /// . In case of <see cref="CompoundCollider"/> if the collider key is empty, it will set the
        /// collision response of the all children. </summary>
        ///
        /// <param name="collisionResponse">    The collision response. </param>
        /// <param name="colliderKey">          The collider key. </param>
        public void SetCollisionResponse(CollisionResponsePolicy collisionResponse, ColliderKey colliderKey) => m_Collider.ValueRW.Value.As<Collider>().SetCollisionResponse(collisionResponse, colliderKey);

        /// <summary>Gets the restitution. In case of a <see cref="CompoundCollider"/>,
        /// this behaves as <see cref="GetRestitution(ColliderKey)"/> with ColliderKey.Zero passed in.</summary>
        ///
        /// <returns>   The restitution. </returns>
        public float GetRestitution() => m_Collider.ValueRO.Value.As<Collider>().GetRestitution();

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
        public float GetRestitution(ColliderKey colliderKey) => m_Collider.ValueRO.Value.As<Collider>().GetRestitution(colliderKey);

        /// <summary>
        /// Sets the restitution. In case of a <see cref="CompoundCollider"/>, this
        /// behaves as <see cref="SetRestitution(System.Single,Unity.Physics.ColliderKey)"/> with ColliderKey.Zero passed in.
        /// </summary>
        ///
        /// <param name="restitution">  The restitution. </param>
        public void SetRestitution(float restitution) => m_Collider.ValueRW.Value.As<Collider>().SetRestitution(restitution);

        /// <summary>
        /// Sets the restitution of a child specified by the collider key. Collider key is only read in
        /// case of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="SetRestitution(float)()"/>
        /// . In case of <see cref="CompoundCollider"/> if the collider key is empty, it will set the
        /// restitution of the all children.
        /// </summary>
        ///
        /// <param name="restitution">  The restitution. </param>
        /// <param name="colliderKey">  The collider key. </param>
        public void SetRestitution(float restitution, ColliderKey colliderKey) => m_Collider.ValueRW.Value.As<Collider>().SetRestitution(restitution, colliderKey);

        /// <summary>
        /// Get the collision filter. In case of a <see cref="CompoundCollider"/>, returns the root
        /// filter.
        /// </summary>
        ///
        /// <returns>   The collision filter. </returns>
        public CollisionFilter GetCollisionFilter() => m_Collider.ValueRO.Value.As<Collider>().GetCollisionFilter();

        /// <summary>
        /// Gets the collision filter specified by the collider key. The key is only read in case the
        /// collider is <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="GetCollisionFilter()"/>.
        /// </summary>
        ///
        /// <param name="colliderKey">  The collider key. </param>
        ///
        /// <returns>   The collision filter. </returns>
        public CollisionFilter GetCollisionFilter(ColliderKey colliderKey) => m_Collider.ValueRO.Value.As<Collider>().GetCollisionFilter(colliderKey);

        /// <summary>   Sets the collision filter. In case of a <see cref="CompoundCollider"/> sets the root filter. </summary>
        ///
        /// <param name="filter">   Specifies the filter. </param>
        public void SetCollisionFilter(CollisionFilter filter) => m_Collider.ValueRW.Value.As<Collider>().SetCollisionFilter(filter);

        /// <summary>
        /// Sets the collision filter of a child specified by the collider key. Collider key is only read
        /// in case of <see cref="CompoundCollider"/>, otherwise it is ignored and will behave as <see cref="SetCollisionFilter(CollisionFilter)"/>
        /// </summary>
        ///
        /// <param name="filter">       Specifies the filter. </param>
        /// <param name="colliderKey">  The collider key. </param>
        public void SetCollisionFilter(CollisionFilter filter, ColliderKey colliderKey) => m_Collider.ValueRW.Value.As<Collider>().SetCollisionFilter(filter, colliderKey);

        /// <summary>   Gets the children of this collider aspect. </summary>
        ///
        /// <returns>   The number of children. If the collider is not a <see cref="CompoundCollider"/>, returns 0. </returns>
        public int GetNumberOfChildren()
        {
            if (Type != ColliderType.Compound)
                return 0;
            return m_Collider.ValueRO.Value.As<CompoundCollider>().NumChildren;
        }

        /// <summary>   Convert child index to collider key. </summary>
        ///
        /// <param name="childIndex">   Zero-based index of the child. </param>
        ///
        /// <returns>   The child converted index to collider key. If the collider is not a <see cref="CompoundCollider"/>, returns <see cref="ColliderKey.Empty"/>. </returns>
        public ColliderKey ConvertChildIndexToColliderKey(int childIndex)
        {
            if (Type != ColliderType.Compound)
                return ColliderKey.Empty;
            return m_Collider.ValueRO.Value.As<CompoundCollider>().ConvertChildIndexToColliderKey(childIndex);
        }

        /// <summary>
        /// Has effect only if the collider is a <see cref="CompoundCollider"/>.
        /// Fills the passed in map with <see cref="ColliderKey"/> - <see cref="ChildCollider"/> pairs of
        /// the compound collider.
        /// </summary>
        ///
        /// <param name="colliderKeyToChildrenMapping"> [in,out] The collider key to children mapping. </param>
        public unsafe void GetColliderKeyToChildrenMapping(ref NativeHashMap<ColliderKey, ChildCollider> colliderKeyToChildrenMapping)
        {
            if (Type != ColliderType.Compound)
                return;
            m_Collider.ValueRO.Value.As<CompoundCollider>().GetColliderKeyToChildrenMapping(ref colliderKeyToChildrenMapping);
        }

        /// <summary>   Calculates the aabb of this aspect in world space. </summary>
        ///
        /// <returns>   The calculated aabb in world space. </returns>
        public Aabb CalculateAabb()
        {
            var transformFromCollider = WorldFromCollider;
            return RigidBody.RigidBodyUtil.CalculateAabb(m_Collider.ValueRO.Value, new RigidTransform(transformFromCollider.Rotation, transformFromCollider.Position), transformFromCollider.Scale);
        }

        #region ICollidable

        /// <summary>   Cast ray. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input)
            => QueryWrappers.RayCast(in this, input);

        /// <summary>   Cast ray. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input, out RaycastHit closestHit)
            => QueryWrappers.RayCast(in this, input, out closestHit);

        /// <summary>   Cast ray. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay(RaycastInput input, ref NativeList<RaycastHit> allHits)
            => QueryWrappers.RayCast(in this, input, ref allHits);

        /// <summary>   Cast ray. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastRay<T>(RaycastInput input, ref T collector)
            where T : struct, ICollector<RaycastHit>
        {
            var transformFromCollider = m_Transform.ValueRO;
            return RigidBody.RigidBodyUtil.CastRay(m_Collider.ValueRO.Value, Entity, input, ref collector, new RigidTransform(transformFromCollider.Rotation, transformFromCollider.Position), transformFromCollider.Scale);
        }

        /// <summary>   Cast collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input
        ) => QueryWrappers.ColliderCast(in this, input);

        /// <summary>   Cast collider. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input, out ColliderCastHit closestHit)
            => QueryWrappers.ColliderCast(in this, input, out closestHit);

        /// <summary>   Cast collider. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(ColliderCastInput input, ref NativeList<ColliderCastHit> allHits)
            => QueryWrappers.ColliderCast(in this, input, ref allHits);

        /// <summary>   Cast collider. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider<T>(ColliderCastInput input, ref T collector)
            where T : struct, ICollector<ColliderCastHit>
        {
            var transformFromCollider = m_Transform.ValueRO;
            return RigidBody.RigidBodyUtil.CastCollider(m_Collider.ValueRO.Value, Entity, input, ref collector, new RigidTransform(transformFromCollider.Rotation, transformFromCollider.Position), transformFromCollider.Scale);
        }

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input)
            => QueryWrappers.CalculateDistance(in this, input);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input, out DistanceHit closestHit)
            => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(PointDistanceInput input, ref NativeList<DistanceHit> allHits)
            => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance<T>(PointDistanceInput input, ref T collector)
            where T : struct, ICollector<DistanceHit>
        {
            var transformFromCollider = m_Transform.ValueRO;
            return RigidBody.RigidBodyUtil.CalculateDistance(m_Collider.ValueRO.Value, Entity, input, ref collector, new RigidTransform(transformFromCollider.Rotation, transformFromCollider.Position), transformFromCollider.Scale);
        }

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input)
            => QueryWrappers.CalculateDistance(in this, input);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input, out DistanceHit closestHit)
            => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(ColliderDistanceInput input, ref NativeList<DistanceHit> allHits)
            => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        /// <summary>   Calculates the distance. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance<T>(ColliderDistanceInput input, ref T collector)
            where T : struct, ICollector<DistanceHit>
        {
            var transformFromCollider = m_Transform.ValueRO;
            return RigidBody.RigidBodyUtil.CalculateDistance(m_Collider.ValueRO.Value, Entity, input, ref collector, new RigidTransform(transformFromCollider.Rotation, transformFromCollider.Position), transformFromCollider.Scale);
        }

        #region GO API Queries

        /// <summary>   Checks if a sphere overlaps with this body. </summary>
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

        #region IAspectQueryable

        /// <summary>   Cast another collider aspect against this one. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the input aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CastCollider(in this, colliderAspect, direction, maxDistance, queryInteraction);

        /// <summary>   Cast another collider aspect against this one. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the input aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="closestHit">       [out] The closest hit. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, out ColliderCastHit closestHit, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CastCollider(in this, colliderAspect, direction, maxDistance, out closestHit, queryInteraction);

        /// <summary>   Cast another collider aspect against this one. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the input aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="allHits">          [in,out] all hits. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> allHits, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CastCollider(in this, colliderAspect, direction, maxDistance, ref allHits, queryInteraction);

        /// <summary>   Cast another collider aspect against this one. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the input aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider<T>(in ColliderAspect colliderAspect, float3 direction, float maxDistance, ref T collector, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>
        {
            ColliderCastInput input = new ColliderCastInput(colliderAspect.m_Collider.ValueRO.Value, colliderAspect.Position, colliderAspect.Position + direction * maxDistance, colliderAspect.Rotation, colliderAspect.Scale);
            if (queryInteraction == QueryInteraction.IgnoreTriggers)
            {
                QueryInteractionCollector<ColliderCastHit, T> interactionCollector = new QueryInteractionCollector<ColliderCastHit, T>(ref collector, true, Entity.Null);
                return CastCollider(input, ref interactionCollector);
            }
            else
            {
                return CastCollider(input, ref collector);
            }
        }

        /// <summary>   Calculates the distance from the input aspect. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(in ColliderAspect colliderAspect, float maxDistance, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CalculateDistance(in this, colliderAspect, maxDistance, queryInteraction);

        /// <summary>   Calculates the distance from the input aspect. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="closestHit">       [out] The closest hit. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(in ColliderAspect colliderAspect, float maxDistance, out DistanceHit closestHit, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CalculateDistance(in this, colliderAspect, maxDistance, out closestHit, queryInteraction);

        /// <summary>   Calculates the distance from the input aspect. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="allHits">          [in,out] all hits. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance(in ColliderAspect colliderAspect, float maxDistance, ref NativeList<DistanceHit> allHits, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CalculateDistance(in this, colliderAspect, maxDistance, ref allHits, queryInteraction);

        /// <summary>   Calculates the distance from the input aspect. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CalculateDistance<T>(in ColliderAspect colliderAspect, float maxDistance, ref T collector, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>
        {
            ColliderDistanceInput input = new ColliderDistanceInput(colliderAspect.m_Collider.ValueRO.Value, maxDistance, new RigidTransform(colliderAspect.Rotation, colliderAspect.Position), colliderAspect.Scale);
            if (queryInteraction == QueryInteraction.IgnoreTriggers)
            {
                QueryInteractionCollector<DistanceHit, T> interactionCollector = new QueryInteractionCollector<DistanceHit, T>(ref collector, true, Entity.Null);
                return CalculateDistance(input, ref interactionCollector);
            }
            else
            {
                return CalculateDistance(input, ref collector);
            }
        }

        #endregion
    }
}
