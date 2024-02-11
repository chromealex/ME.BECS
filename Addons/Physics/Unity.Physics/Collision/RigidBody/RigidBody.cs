using System.Runtime.CompilerServices;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Physics.Math;
using Unity.Physics.Aspects;

namespace Unity.Physics
{
    /// <summary>   An instance of a collider in a physics world. </summary>
    /// Using StructLayout to handle for an issue where there can be a mismatch between struct sizes for RigidBody in separate jobs
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Pack = 1, Size = 56)]
    public struct RigidBody : ICollidable, IAspectQueryable
    {
        /// <summary>   The rigid body's collider (allowed to be null) </summary>
        [System.Runtime.InteropServices.FieldOffset(0)]
        public BlobAssetReference<Collider> Collider;

        /// <summary>   The rigid body's transform in world space. </summary>
        [System.Runtime.InteropServices.FieldOffset(8)]
        public RigidTransform WorldFromBody;

        /// <summary>   The entity that rigid body represents. </summary>
        [System.Runtime.InteropServices.FieldOffset(36)]
        public Entity Entity;

        /// <summary>
        /// Arbitrary custom tags. These get copied into contact manifolds and can be used to inform
        /// contact modifiers.
        /// </summary>
        [System.Runtime.InteropServices.FieldOffset(44)]
        public byte CustomTags;

        /// <summary>   The rigid body's scale in world space. </summary>
        [System.Runtime.InteropServices.FieldOffset(48)]
        public float Scale;

        /// <summary>   Rigid body that thas identity transform, null entity, and a null collider. </summary>
        public static readonly RigidBody Zero = new RigidBody
        {
            WorldFromBody = RigidTransform.identity,
            Collider = default,
            Entity = Entity.Null,
            CustomTags = 0,
            Scale = 1.0f
        };

        #region ICollidable implementation

        /// <summary>   Calculates the aabb. </summary>
        ///
        /// <returns>   The calculated aabb. </returns>
        public Aabb CalculateAabb()
        {
            return RigidBodyUtil.CalculateAabb(Collider, WorldFromBody, Scale);
        }

        /// <summary>   Cast ray. </summary>
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
            return RigidBodyUtil.CastRay(Collider, Entity, input, ref collector, WorldFromBody, Scale);
        }

        internal static class RigidBodyUtil
        {
            public static Aabb CalculateAabb(BlobAssetReference<Collider> bodyCollider, RigidTransform worldFromBody, float uniformScale = 1)
            {
                if (bodyCollider.IsCreated)
                {
                    return bodyCollider.Value.CalculateAabb(worldFromBody, uniformScale);
                }
                return new Aabb { Min = worldFromBody.pos, Max = worldFromBody.pos };
            }

            public static bool CastRay<T>(BlobAssetReference<Collider> bodyCollider, Entity rbEntity, RaycastInput input, ref T collector, RigidTransform worldFromBody, float unifromScale = 1.0f)
                where T : struct, ICollector<RaycastHit>
            {
                // Transform the ray into body space
                var worldFromBodyScaled = new ScaledMTransform(worldFromBody, unifromScale);
                var bodyFromWorldScaled = Inverse(worldFromBodyScaled);

                input.Ray.Origin = Mul(bodyFromWorldScaled, input.Ray.Origin);
                input.Ray.Displacement = math.mul(bodyFromWorldScaled.Rotation, input.Ray.Displacement * bodyFromWorldScaled.Scale);

                SetQueryContextParameters(ref input.QueryContext, worldFromBodyScaled, bodyFromWorldScaled.Scale, rbEntity);

                return bodyCollider.IsCreated && bodyCollider.Value.CastRay(input, ref collector);
            }

            public static bool CastCollider<T>(BlobAssetReference<Collider> bodyCollider, Entity rbEntity, ColliderCastInput input, ref T collector, RigidTransform worldFromBody, float uniformScale = 1.0f)
                where T : struct, ICollector<ColliderCastHit>
            {
                // Transform the input into body space

                var worldFromBodyScaled = new ScaledMTransform(worldFromBody, uniformScale);
                var bodyFromWorldScaled = Inverse(worldFromBodyScaled);

                input.Orientation = math.mul(math.inverse(worldFromBody.rot), input.Orientation);
                input.Ray.Origin = Mul(bodyFromWorldScaled, input.Ray.Origin);
                input.Ray.Displacement = math.mul(bodyFromWorldScaled.Rotation, input.Ray.Displacement * bodyFromWorldScaled.Scale);

                SetQueryContextParameters(ref input.QueryContext, worldFromBodyScaled, bodyFromWorldScaled.Scale, rbEntity);

                return bodyCollider.IsCreated && bodyCollider.Value.CastCollider(input, ref collector);
            }

            public static bool CalculateDistance<T>(BlobAssetReference<Collider> bodyCollider, Entity rbEntity, PointDistanceInput input, ref T collector, RigidTransform worldFromBody, float uniformScale = 1.0f)
                where T : struct, ICollector<DistanceHit>
            {
                // Transform the input into body space

                var worldFromBodyScaled = new ScaledMTransform(worldFromBody, uniformScale);
                var bodyFromWorldScaled = Inverse(worldFromBodyScaled);

                input.Position = Mul(bodyFromWorldScaled, input.Position);

                SetQueryContextParameters(ref input.QueryContext, worldFromBodyScaled, bodyFromWorldScaled.Scale, rbEntity);

                return bodyCollider.IsCreated && bodyCollider.Value.CalculateDistance(input, ref collector);
            }

            public static bool CalculateDistance<T>(BlobAssetReference<Collider> bodyCollider, Entity rbEntity, ColliderDistanceInput input, ref T collector, RigidTransform worldFromBody, float uniformScale = 1.0f)
                where T : struct, ICollector<DistanceHit>
            {
                // Transform the input into body space

                var worldFromBodyScaled = new ScaledMTransform(worldFromBody, uniformScale);
                var bodyFromWorldScaled = Inverse(worldFromBodyScaled);

                input.Transform = new RigidTransform(
                    math.mul(math.inverse(worldFromBody.rot), input.Transform.rot),
                    Mul(bodyFromWorldScaled, input.Transform.pos));

                SetQueryContextParameters(ref input.QueryContext, worldFromBodyScaled, bodyFromWorldScaled.Scale, rbEntity);

                return bodyCollider.IsCreated && bodyCollider.Value.CalculateDistance(input, ref collector);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static void SetQueryContextParameters(ref QueryContext context, in ScaledMTransform worldFromBody, float invTargetUniformScale, Entity rigidBodyEntity)
            {
                // QueryContext.WorldFromLocalTransform is not expected to be initialized at this point
                // and should have default value (zeros in all fields)
                Assert.IsTrue(context.WorldFromLocalTransform.Translation.Equals(float3.zero));
                Assert.IsTrue(context.WorldFromLocalTransform.Rotation.Equals(float3x3.zero));

                context.ColliderKey = ColliderKey.Empty;
                context.Entity = rigidBodyEntity;
                context.WorldFromLocalTransform = worldFromBody;

                context.InvTargetScale = invTargetUniformScale;

                if (!context.IsInitialized)
                {
                    context.RigidBodyIndex = -1;
                    context.IsInitialized = true;
                }
            }
        }

        /// <summary>   Cast collider. </summary>
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
            return RigidBodyUtil.CastCollider(Collider, Entity, input, ref collector, WorldFromBody, Scale);
        }

        /// <summary>   Calculates the distance. </summary>
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
            return RigidBodyUtil.CalculateDistance(Collider, Entity, input, ref collector, WorldFromBody, Scale);
        }

        /// <summary>   Calculates the distance. </summary>
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
            return RigidBodyUtil.CalculateDistance(Collider, Entity, input, ref collector, WorldFromBody, Scale);
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

        #region IAspectQueryable implementation

        /// <summary>   Cast another collider aspect against this <see cref="RigidBody"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the input aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        public bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CastCollider(in this, colliderAspect, direction, maxDistance, queryInteraction);

        /// <summary>   Cast another collider aspect against this <see cref="RigidBody"/>. </summary>
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

        /// <summary>   Cast another collider aspect against this <see cref="RigidBody"/>. </summary>
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

        #region private

        #endregion
    }

    /// <summary>   A pair of rigid body indices. </summary>
    public struct BodyIndexPair
    {
        // B before A
        /// <summary>   The body index b. </summary>
        public int BodyIndexB;
        /// <summary>   The body index a. </summary>
        public int BodyIndexA;

        /// <summary>   Gets a value indicating whether this object is valid. </summary>
        ///
        /// <value> True if this object is valid, false if not. </value>
        public bool IsValid => BodyIndexB != -1 && BodyIndexA != -1;

        /// <summary>   Gets the invalid BodyIndexPair. </summary>
        ///
        /// <value> The invalid BodyIndexPair. </value>
        public static BodyIndexPair Invalid => new BodyIndexPair { BodyIndexB = -1, BodyIndexA = -1 };
    }

    // A pair of entities
    internal struct EntityPair
    {
        // B before A for consistency with other pairs
        public Entity EntityB;
        public Entity EntityA;
    }

    // A pair of custom rigid body tags
    internal struct CustomTagsPair
    {
        // B before A for consistency with other pairs
        public byte CustomTagsB;
        public byte CustomTagsA;
    }
}
