using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics.Aspects;

namespace Unity.Physics
{
    /// <summary>   Interface for objects that can be hit by physics queries. </summary>
    public interface ICollidable    // TODO: rename to Physics.IQueryable?
    {
        // Bounding box

        /// <summary>   Calculate an axis aligned bounding box around the object, in local space. </summary>
        ///
        /// <returns>   The calculated aabb. </returns>
        Aabb CalculateAabb();

        // Cast ray

        /// <summary>   Cast a ray against the object.</summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if it hits, otherwise false. </returns>
        bool CastRay(RaycastInput input);

        /// <summary>
        /// Cast a ray against the object.
        /// </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastRay(RaycastInput input, out RaycastHit closestHit);

        /// <summary>
        /// Cast a ray against the object. Return true if it hits, with details of every hit in "allHits".
        /// </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   Return true if it hits, with details of the closest hit in "closestHit". </returns>
        bool CastRay(RaycastInput input, ref NativeList<RaycastHit> allHits);

        /// <summary>
        /// Generic ray cast. Return true if it hits, with details stored in the collector implementation.
        /// </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastRay<T>(RaycastInput input, ref T collector) where T : struct, ICollector<RaycastHit>;

        // Cast collider

        /// <summary>   Cast a collider against the object. Return true if it hits. </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastCollider(ColliderCastInput input);

        /// <summary>
        /// Cast a collider against the object. Return true if it hits, with details of the closest hit
        /// in "closestHit".
        /// </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastCollider(ColliderCastInput input, out ColliderCastHit closestHit);

        /// <summary>
        /// Cast a collider against the object. Return true if it hits, with details of every hit in
        /// "allHits".
        /// </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastCollider(ColliderCastInput input, ref NativeList<ColliderCastHit> allHits);

        /// <summary>
        /// Generic collider cast. Return true if it hits, with details stored in the collector
        /// implementation.
        /// </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastCollider<T>(ColliderCastInput input, ref T collector) where T : struct, ICollector<ColliderCastHit>;

        // Point distance query

        /// <summary>
        /// Calculate the distance from a point to the object. Return true if there are any hits.
        /// </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance(PointDistanceInput input);

        /// <summary>
        /// Calculate the distance from a point to the object. Return true if there are any hits, with
        /// details of the closest hit in "closestHit".
        /// </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance(PointDistanceInput input, out DistanceHit closestHit);

        /// <summary>
        /// Calculate the distance from a point to the object. Return true if there are any hits, with
        /// details of every hit in "allHits".
        /// </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance(PointDistanceInput input, ref NativeList<DistanceHit> allHits);

        /// <summary>
        /// Calculate the distance from a point to the object. Return true if there are any hits, with
        /// details stored in the collector implementation.
        /// </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance<T>(PointDistanceInput input, ref T collector) where T : struct, ICollector<DistanceHit>;

        // Collider distance query

        /// <summary>
        /// Calculate the distance from a collider to the object. Return true if there are any hits.
        /// </summary>
        ///
        /// <param name="input">    The input. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance(ColliderDistanceInput input);

        /// <summary>
        /// Calculate the distance from a collider to the object. Return true if there are any hits, with
        /// details of the closest hit in "closestHit".
        /// </summary>
        ///
        /// <param name="input">        The input. </param>
        /// <param name="closestHit">   [out] The closest hit. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance(ColliderDistanceInput input, out DistanceHit closestHit);

        /// <summary>
        /// Calculate the distance from a collider to the object. Return true if there are any hits, with
        /// details of every hit in "allHits".
        /// </summary>
        ///
        /// <param name="input">    The input. </param>
        /// <param name="allHits">  [in,out] all hits. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance(ColliderDistanceInput input, ref NativeList<DistanceHit> allHits);

        /// <summary>
        /// Calculate the distance from a collider to the object. Return true if there are any hits, with
        /// details stored in the collector implementation.
        /// </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="input">        The input. </param>
        /// <param name="collector">    [in,out] The collector. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance<T>(ColliderDistanceInput input, ref T collector) where T : struct, ICollector<DistanceHit>;

        // Interfaces that look like the GameObject query interfaces.

        /// <summary>
        /// Checks if the provided sphere is overlapping with an ICollidable Return true if it is
        /// overlapping.
        /// </summary>
        ///
        /// <param name="position">         The position. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CheckSphere(float3 position, float radius, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Checks if the provided sphere is overlapping with an ICollidable Return true if there is at
        /// least one overlap, and all overlaps will be stored in provided list.
        /// </summary>
        ///
        /// <param name="position">         The position. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool OverlapSphere(float3 position, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Checks if the provided sphere is overlapping with an ICollidable Return true if there is at
        /// least one overlap, the passed collector is used for custom hit filtering if needed.
        /// </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="position">         The position. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool OverlapSphereCustom<T>(float3 position, float radius, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>;

        /// <summary>
        /// Checks if the provided capsule is overlapping with an ICollidable Return true if it is
        /// overlapping.
        /// </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CheckCapsule(float3 point1, float3 point2, float radius, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Checks if the provided capsule is overlapping with an ICollidable Return true if there is at
        /// least one overlap, and all overlaps will be stored in provided list.
        /// </summary>
        ///
        /// <param name="point1">           The first point in capsule definition. </param>
        /// <param name="point2">           The second point in capsule definition. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool OverlapCapsule(float3 point1, float3 point2, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Checks if the provided capsule is overlapping with an ICollidable Return true if there is at
        /// least one overlap, the passed collector is used for custom hit filtering if needed.
        /// </summary>
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
        bool OverlapCapsuleCustom<T>(float3 point1, float3 point2, float radius, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>;

        /// <summary>
        /// Checks if the provided box is overlapping with an ICollidable Return true if it is
        /// overlapping.
        /// </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CheckBox(float3 center, quaternion orientation, float3 halfExtents, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Checks if the provided box is overlapping with an ICollidable Return true if there is at
        /// least one overlap, and all overlaps will be stored in provided list.
        /// </summary>
        ///
        /// <param name="center">           The center. </param>
        /// <param name="orientation">      The orientation. </param>
        /// <param name="halfExtents">      Half extents of the box. </param>
        /// <param name="outHits">          [in,out] The out hits. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool OverlapBox(float3 center, quaternion orientation, float3 halfExtents, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Checks if the provided box is overlapping with an ICollidable Return true if there is at
        /// least one overlap, the passed collector is used for custom hit filtering if needed.
        /// </summary>
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
        bool OverlapBoxCustom<T>(float3 center, quaternion orientation, float3 halfExtents, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>;

        /// <summary>
        /// Casts a specified sphere along a ray specified with origin, direction, and maxDistance, and
        /// checks if it hits an ICollidable. Return true if there is at least one hit.
        /// </summary>
        ///
        /// <param name="origin">           The origin. </param>
        /// <param name="radius">           The radius. </param>
        /// <param name="direction">        The direction. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="filter">           Specifies the filter. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Casts a specified sphere along a ray specified with origin, direction, and maxDistance, and
        /// checks if it hits an ICollidable. Return true if a hit happened, the information about
        /// closest hit will be in hitInfo.
        /// </summary>
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
        bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Casts a specified sphere along a ray specified with origin, direction, and maxDistance, and
        /// checks if it hits an ICollidable. Return true if at least one hit happened, all hits will be
        /// stored in a provided list.
        /// </summary>
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
        bool SphereCastAll(float3 origin, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Casts a specified sphere along a ray specified with origin, direction, and maxDistance, and
        /// checks if it hits an ICollidable. Return true if at least one hit happened, the passed
        /// collector is used for custom hit filtering.
        /// </summary>
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
        bool SphereCastCustom<T>(float3 origin, float radius, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>;

        /// <summary>
        /// Casts a specified box along a ray specified with center, direction, and maxDistance, and
        /// checks if it hits an ICollidable. Return true if there is at least one hit.
        /// </summary>
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
        bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Casts a specified box along a ray specified with center, direction, and maxDistance, and
        /// checks if it hits an ICollidable. Return true if a hit happened, the information about
        /// closest hit will be in hitInfo.
        /// </summary>
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
        bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Casts a specified box along a ray specified with center, direction, and maxDistance, and
        /// checks if it hits an ICollidable. Return true if at least one hit happened, all hits will be
        /// stored in a provided list.
        /// </summary>
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
        bool BoxCastAll(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Casts a specified box along a ray specified with center, direction, and maxDistance, and
        /// checks if it hits an ICollidable. Return true if at least one hit happened, the passed
        /// collector is used for custom hit filtering.
        /// </summary>
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
        bool BoxCastCustom<T>(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>;

        /// <summary>
        /// Casts a capsule specified with two points along a ray specified with the center of the
        /// capsule, direction and maxDistance, and checks if it hits an ICollidable. Return true if
        /// there is at least one hit.
        /// </summary>
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
        bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Casts a capsule specified with two points along a ray specified with the center of the
        /// capsule, direction and maxDistance, and checks if it hits an ICollidable. Return true if a
        /// hit happened, the information about closest hit will be in hitInfo.
        /// </summary>
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
        bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Casts a capsule specified with two points along a ray specified with the center of the
        /// capsule, direction and maxDistance, and checks if it hits an ICollidable. Return true if at
        /// least one hit happened, all hits will be stored in a provided list.
        /// </summary>
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
        bool CapsuleCastAll(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>
        /// Casts a capsule specified with two points along a ray specified with the center of the
        /// capsule, direction and maxDistance, and checks if it hits an ICollidable. Return true if at
        /// least one hit happened, the passed collector is used for custom hit filtering.
        /// </summary>
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
        bool CapsuleCastCustom<T>(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>;
    }

    /// <summary>   Interface for objects that can be hit by aspect queries. </summary>
    public interface IAspectQueryable
    {
        /// <summary>   Cast the collider aspect against this <see cref="IAspectQueryable"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>   Cast the collider aspect against this <see cref="IAspectQueryable"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="closestHit">       [out] The closest hit. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, out ColliderCastHit closestHit, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>   Cast the collider aspect against this <see cref="IAspectQueryable"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="allHits">          [in,out] all hits. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastCollider(in ColliderAspect colliderAspect, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> allHits, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>   Cast the collider aspect against this <see cref="IAspectQueryable"/>. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="direction">        The direction of the aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CastCollider<T>(in ColliderAspect colliderAspect, float3 direction, float maxDistance, ref T collector, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<ColliderCastHit>;

        /// <summary>   Calculates the distance from the collider aspect to this <see cref="IAspectQueryable"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance(in ColliderAspect colliderAspect, float maxDistance, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>   Calculates the distance from the collider aspect to this <see cref="IAspectQueryable"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="closestHit">       [out] The closest hit. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance(in ColliderAspect colliderAspect, float maxDistance, out DistanceHit closestHit, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>   Calculates the distance from the collider aspect to this <see cref="IAspectQueryable"/>. </summary>
        ///
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="allHits">          [in,out] all hits. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance(in ColliderAspect colliderAspect, float maxDistance, ref NativeList<DistanceHit> allHits, QueryInteraction queryInteraction = QueryInteraction.Default);

        /// <summary>   Calculates the distance from the collider aspect to this <see cref="IAspectQueryable"/>. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        /// <param name="colliderAspect">   The collider aspect. </param>
        /// <param name="maxDistance">      The maximum distance. </param>
        /// <param name="collector">        [in,out] The collector. </param>
        /// <param name="queryInteraction"> (Optional) The query interaction. </param>
        ///
        /// <returns>   True if there is a hit, false otherwise. </returns>
        bool CalculateDistance<T>(in ColliderAspect colliderAspect, float maxDistance, ref T collector, QueryInteraction queryInteraction = QueryInteraction.Default) where T : struct, ICollector<DistanceHit>;
    }

    /// <summary>
    /// Used as a way to provide queries with more filtering options, without creating collectors At
    /// the moment, IgnoreTriggers is the only option that is supported.
    /// </summary>
    public enum QueryInteraction : byte
    {
        /// <summary>   An enum constant representing the default option. </summary>
        Default = 0,
        /// <summary>   An enum constant representing the ignore triggers option. </summary>
        IgnoreTriggers = 1 << 0
    }

    // Wrappers around generic ICollidable queries
    static class QueryWrappers
    {
        #region ICollidable

        #region Ray casts

        public static bool RayCast<T>(in T target, RaycastInput input) where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<RaycastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CastRay(input, ref collector);
                }
            }
        }

        public static bool RayCast<T>(in T target, RaycastInput input, out RaycastHit closestHit) where T : unmanaged, ICollidable
        {
            var collector = new ClosestHitCollector<RaycastHit>(1.0f);

            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (ptr->CastRay(input, ref collector))
                    {
                        closestHit = collector.ClosestHit; // TODO: would be nice to avoid this copy
                        return true;
                    }
                }
            }

            closestHit = new RaycastHit();
            return false;
        }

        public static bool RayCast<T>(in T target, RaycastInput input, ref NativeList<RaycastHit> allHits) where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<RaycastHit>(1.0f, ref allHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CastRay(input, ref collector);
                }
            }
        }

        #endregion

        #region Collider casts

        public static bool ColliderCast<T>(in T target, ColliderCastInput input) where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<ColliderCastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CastCollider(input, ref collector);
                }
            }
        }

        public static bool ColliderCast<T>(in T target, ColliderCastInput input, out ColliderCastHit result) where T : unmanaged, ICollidable
        {
            var collector = new ClosestHitCollector<ColliderCastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (ptr->CastCollider(input, ref collector))
                    {
                        result = collector.ClosestHit;  // TODO: would be nice to avoid this copy
                        return true;
                    }

                    result = new ColliderCastHit();
                    return false;
                }
            }
        }

        public static bool ColliderCast<T>(in T target, ColliderCastInput input, ref NativeList<ColliderCastHit> allHits) where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<ColliderCastHit>(1.0f, ref allHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CastCollider(input, ref collector);
                }
            }
        }

        #endregion

        #region Point distance queries

        public static bool CalculateDistance<T>(in T target, PointDistanceInput input) where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<DistanceHit>(input.MaxDistance);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CalculateDistance(input, ref collector);
                }
            }
        }

        public static bool CalculateDistance<T>(in T target, PointDistanceInput input, out DistanceHit result) where T : unmanaged, ICollidable
        {
            var collector = new ClosestHitCollector<DistanceHit>(input.MaxDistance);

            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (ptr->CalculateDistance(input, ref collector))
                    {
                        result = collector.ClosestHit;  // TODO: would be nice to avoid this copy
                        return true;
                    }
                }
            }

            result = new DistanceHit();
            return false;
        }

        public static bool CalculateDistance<T>(in T target, PointDistanceInput input, ref NativeList<DistanceHit> allHits) where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<DistanceHit>(input.MaxDistance, ref allHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CalculateDistance(input, ref collector);
                }
            }
        }

        #endregion

        #region Collider distance queries

        public static bool CalculateDistance<T>(in T target, ColliderDistanceInput input) where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<DistanceHit>(input.MaxDistance);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CalculateDistance(input, ref collector);
                }
            }
        }

        public static bool CalculateDistance<T>(in T target, ColliderDistanceInput input, out DistanceHit result) where T : unmanaged, ICollidable
        {
            var collector = new ClosestHitCollector<DistanceHit>(input.MaxDistance);

            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (ptr->CalculateDistance(input, ref collector))
                    {
                        result = collector.ClosestHit;  // TODO: would be nice to avoid this copy
                        return true;
                    }

                    result = new DistanceHit();
                    return false;
                }
            }
        }

        public static bool CalculateDistance<T>(in T target, ColliderDistanceInput input, ref NativeList<DistanceHit> allHits) where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<DistanceHit>(input.MaxDistance, ref allHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CalculateDistance(input, ref collector);
                }
            }
        }

        #endregion

        #region Existing GO API queries

        public static bool OverlapSphereCustom<T, C>(in T target, float3 position, float radius, ref C collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
            where C : struct, ICollector<DistanceHit>
        {
            PointDistanceInput input = new PointDistanceInput
            {
                Filter = filter,
                MaxDistance = radius,
                Position = position
            };
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (queryInteraction == QueryInteraction.Default)
                    {
                        return ptr->CalculateDistance(input, ref collector);
                    }
                    else
                    {
                        var interactionCollector = new QueryInteractionCollector<DistanceHit, C>(ref collector, true, Entities.Entity.Null);
                        return ptr->CalculateDistance(input, ref interactionCollector);
                    }
                }
            }
        }

        public static bool OverlapSphere<T>(in T target, float3 position, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<DistanceHit>(radius, ref outHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->OverlapSphereCustom(position, radius, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool CheckSphere<T>(in T target, float3 position, float radius, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<DistanceHit>(radius);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->OverlapSphereCustom(position, radius, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool OverlapCapsuleCustom<T, C>(in T target, float3 point1, float3 point2, float radius, ref C collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
            where C : struct, ICollector<DistanceHit>
        {
            Assert.IsTrue(collector.MaxFraction == 0);

            CapsuleCollider collider = default;
            float3 center = (point1 + point2) / 2;

            CapsuleGeometry geometry = new CapsuleGeometry
            {
                Radius = radius,
                Vertex0 = point1 - center,
                Vertex1 = point2 - center
            };

            collider.Initialize(geometry, filter, Material.Default);
            ColliderDistanceInput input;
            unsafe
            {
                input = new ColliderDistanceInput
                {
                    Collider = (Collider*)UnsafeUtility.AddressOf(ref collider),
                    MaxDistance = 0.0f,
                    Transform = new RigidTransform
                    {
                        pos = center,
                        rot = quaternion.identity
                    }
                };
            }

            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (queryInteraction == QueryInteraction.Default)
                    {
                        return ptr->CalculateDistance(input, ref collector);
                    }
                    else
                    {
                        var interactionCollector = new QueryInteractionCollector<DistanceHit, C>(ref collector, true, Entities.Entity.Null);
                        return ptr->CalculateDistance(input, ref interactionCollector);
                    }
                }
            }
        }

        public static bool OverlapCapsule<T>(in T target, float3 point1, float3 point2, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<DistanceHit>(0.0f, ref outHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->OverlapCapsuleCustom(point1, point2, radius, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool CheckCapsule<T>(in T target, float3 point1, float3 point2, float radius, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<DistanceHit>(0.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->OverlapCapsuleCustom(point1, point2, radius, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool OverlapBoxCustom<T, C>(in T target, float3 center, quaternion orientation, float3 halfExtents, ref C collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
            where C : struct, ICollector<DistanceHit>
        {
            Assert.IsTrue(collector.MaxFraction == 0);

            BoxCollider collider = default;
            BoxGeometry geometry = new BoxGeometry
            {
                BevelRadius = 0.0f,
                Center = float3.zero,
                Size = halfExtents * 2,
                Orientation = quaternion.identity
            };

            collider.Initialize(geometry, filter, Material.Default);

            ColliderDistanceInput input;
            unsafe
            {
                input = new ColliderDistanceInput
                {
                    Collider = (Collider*)UnsafeUtility.AddressOf(ref collider),
                    MaxDistance = 0.0f,
                    Transform = new RigidTransform
                    {
                        pos = center,
                        rot = orientation
                    }
                };
            }

            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (queryInteraction == QueryInteraction.Default)
                    {
                        return ptr->CalculateDistance(input, ref collector);
                    }
                    else
                    {
                        var interactionCollector = new QueryInteractionCollector<DistanceHit, C>(ref collector, true, Entities.Entity.Null);
                        return ptr->CalculateDistance(input, ref interactionCollector);
                    }
                }
            }
        }

        public static bool OverlapBox<T>(in T target, float3 center, quaternion orientation, float3 halfExtents, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<DistanceHit>(0.0f, ref outHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->OverlapBoxCustom(center, orientation, halfExtents, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool CheckBox<T>(in T target, float3 center, quaternion orientation, float3 halfExtents, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<DistanceHit>(0.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->OverlapBoxCustom(center, orientation, halfExtents, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool SphereCastCustom<T, C>(in T target, float3 origin, float radius, float3 direction, float maxDistance, ref C collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
            where C : struct, ICollector<ColliderCastHit>
        {
            SphereCollider collider = default;
            SphereGeometry geometry = new SphereGeometry
            {
                Center = 0,
                Radius = radius
            };

            collider.Initialize(geometry, filter, Material.Default);

            ColliderCastInput input;
            unsafe
            {
                input = new ColliderCastInput
                {
                    Collider = (Collider*)UnsafeUtility.AddressOf(ref collider),
                    Orientation = quaternion.identity,
                    Start = origin,
                    End = origin + direction * maxDistance
                };
            }

            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (queryInteraction == QueryInteraction.Default)
                    {
                        return ptr->CastCollider(input, ref collector);
                    }
                    else
                    {
                        var interactionCollector = new QueryInteractionCollector<ColliderCastHit, C>(ref collector, true, Entities.Entity.Null);
                        return ptr->CastCollider(input, ref interactionCollector);
                    }
                }
            }
        }

        public static bool SphereCastAll<T>(in T target, float3 origin, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<ColliderCastHit>(1.0f, ref outHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->SphereCastCustom(origin, radius, direction, maxDistance, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool SphereCast<T>(in T target, float3 origin, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new ClosestHitCollector<ColliderCastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    bool hasHit = ptr->SphereCastCustom(origin, radius, direction, maxDistance, ref collector, filter, queryInteraction);
                    hitInfo = collector.ClosestHit;

                    return hasHit;
                }
            }
        }

        public static bool SphereCast<T>(in T target, float3 origin, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<ColliderCastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->SphereCastCustom(origin, radius, direction, maxDistance, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool BoxCastCustom<T, C>(in T target, float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref C collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
            where C : struct, ICollector<ColliderCastHit>
        {
            BoxCollider collider = default;
            BoxGeometry boxGeometry = new BoxGeometry
            {
                BevelRadius = 0,
                Center = 0,
                Orientation = quaternion.identity,
                Size = halfExtents * 2
            };

            collider.Initialize(boxGeometry, filter, Material.Default);

            ColliderCastInput input;
            unsafe
            {
                input = new ColliderCastInput
                {
                    Collider = (Collider*)UnsafeUtility.AddressOf(ref collider),
                    Orientation = orientation,
                    Start = center,
                    End = center + direction * maxDistance
                };
            }
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (queryInteraction == QueryInteraction.Default)
                    {
                        return ptr->CastCollider(input, ref collector);
                    }
                    else
                    {
                        var interactionCollector = new QueryInteractionCollector<ColliderCastHit, C>(ref collector, true, Entities.Entity.Null);
                        return ptr->CastCollider(input, ref interactionCollector);
                    }
                }
            }
        }

        public static bool BoxCastAll<T>(in T target, float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<ColliderCastHit>(1.0f, ref outHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->BoxCastCustom(center, orientation, halfExtents, direction, maxDistance, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool BoxCast<T>(in T target, float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new ClosestHitCollector<ColliderCastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    bool hasHit = ptr->BoxCastCustom(center, orientation, halfExtents, direction, maxDistance, ref collector, filter, queryInteraction);
                    hitInfo = collector.ClosestHit;
                    return hasHit;
                }
            }
        }

        public static bool BoxCast<T>(in T target, float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<ColliderCastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->BoxCastCustom(center, orientation, halfExtents, direction, maxDistance, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool CapsuleCastCustom<T, C>(in T target, float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref C collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
            where C : struct, ICollector<ColliderCastHit>
        {
            CapsuleCollider collider = default;

            float3 center = (point1 + point2) / 2;

            CapsuleGeometry geometry = new CapsuleGeometry
            {
                Radius = radius,
                Vertex0 = point1 - center,
                Vertex1 = point2 - center
            };

            collider.Initialize(geometry, filter, Material.Default);

            ColliderCastInput input;
            unsafe
            {
                input = new ColliderCastInput
                {
                    Collider = (Collider*)UnsafeUtility.AddressOf(ref collider),
                    Orientation = quaternion.identity,
                    Start = center,
                    End = center + direction * maxDistance
                };
            }
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (queryInteraction == QueryInteraction.Default)
                    {
                        return ptr->CastCollider(input, ref collector);
                    }
                    else
                    {
                        var interactionCollector = new QueryInteractionCollector<ColliderCastHit, C>(ref collector, true, Entities.Entity.Null);
                        return ptr->CastCollider(input, ref interactionCollector);
                    }
                }
            }
        }

        public static bool CapsuleCastAll<T>(in T target, float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AllHitsCollector<ColliderCastHit>(1.0f, ref outHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CapsuleCastCustom(point1, point2, radius, direction, maxDistance, ref collector, filter, queryInteraction);
                }
            }
        }

        public static bool CapsuleCast<T>(in T target, float3 point1, float3 point2, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new ClosestHitCollector<ColliderCastHit>(1.0f);

            unsafe
            {
                fixed(T* ptr = &target)
                {
                    bool hasHit = ptr->CapsuleCastCustom(point1, point2, radius, direction, maxDistance, ref collector, filter, queryInteraction);

                    hitInfo = collector.ClosestHit;
                    return hasHit;
                }
            }
        }

        public static bool CapsuleCast<T>(in T target, float3 point1, float3 point2, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, ICollidable
        {
            var collector = new AnyHitCollector<ColliderCastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CapsuleCastCustom(point1, point2, radius, direction, maxDistance, ref collector, filter, queryInteraction);
                }
            }
        }

        #endregion

        #endregion

        #region IAspectQueryable

        #region Collider cast

        public static bool CastCollider<T>(in T target, ColliderAspect colliderAspect, float3 direction, float maxDistance, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, IAspectQueryable
        {
            AnyHitCollector<ColliderCastHit> anyHitCollector = new AnyHitCollector<ColliderCastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CastCollider(colliderAspect, direction, maxDistance, ref anyHitCollector, queryInteraction);
                }
            }
        }

        public static bool CastCollider<T>(in T target, ColliderAspect colliderAspect, float3 direction, float maxDistance, out ColliderCastHit closestHit, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, IAspectQueryable
        {
            ClosestHitCollector<ColliderCastHit> closestHitCollector = new ClosestHitCollector<ColliderCastHit>(1.0f);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (ptr->CastCollider(colliderAspect, direction, maxDistance, ref closestHitCollector, queryInteraction))
                    {
                        closestHit = closestHitCollector.ClosestHit;
                        return true;
                    }
                }
            }

            closestHit = new ColliderCastHit();
            return false;
        }

        public static bool CastCollider<T>(in T target, ColliderAspect colliderAspect, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> allHits, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, IAspectQueryable
        {
            AllHitsCollector<ColliderCastHit> allHitsCollector = new AllHitsCollector<ColliderCastHit>(1.0f, ref allHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CastCollider(colliderAspect, direction, maxDistance, ref allHitsCollector, queryInteraction);
                }
            }
        }

        #endregion

        #region Collider distance

        public static bool CalculateDistance<T>(in T target, ColliderAspect colliderAspect, float maxDistance, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, IAspectQueryable
        {
            AnyHitCollector<DistanceHit> anyHitCollector = new AnyHitCollector<DistanceHit>(maxDistance);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CalculateDistance(colliderAspect, maxDistance, ref anyHitCollector, queryInteraction);
                }
            }
        }

        public static bool CalculateDistance<T>(in T target, ColliderAspect colliderAspect, float maxDistance, out DistanceHit closestHit, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, IAspectQueryable
        {
            ClosestHitCollector<DistanceHit> closestHitCollector = new ClosestHitCollector<DistanceHit>(maxDistance);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    if (ptr->CalculateDistance(colliderAspect, maxDistance, ref closestHitCollector, queryInteraction))
                    {
                        closestHit = closestHitCollector.ClosestHit;
                        return true;
                    }
                }
            }

            closestHit = new DistanceHit();
            return false;
        }

        public static bool CalculateDistance<T>(in T target, ColliderAspect colliderAspect, float maxDistance, ref NativeList<DistanceHit> allHits, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : unmanaged, IAspectQueryable
        {
            AllHitsCollector<DistanceHit> allHitsCollector = new AllHitsCollector<DistanceHit>(maxDistance, ref allHits);
            unsafe
            {
                fixed(T* ptr = &target)
                {
                    return ptr->CalculateDistance(colliderAspect, maxDistance, ref allHitsCollector, queryInteraction);
                }
            }
        }

        #endregion

        #endregion
    }
}
