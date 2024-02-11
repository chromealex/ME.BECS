using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Assertions;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    /// <summary>   Interface for query results. </summary>
    public interface IQueryResult
    {
        /// <summary>
        /// For casts this is fraction of the query at which the hit occurred. For distance queries, this
        /// is a distance from the query object.
        /// </summary>
        ///
        /// <value> The fraction. </value>
        float Fraction { get; }

        /// <summary>   Index of the hit body in the CollisionWorld's rigid body array. </summary>
        ///
        /// <value> The rigid body index. </value>
        int RigidBodyIndex { get; }

        /// <summary>   ColliderKey of the hit leaf collider. </summary>
        ///
        /// <value> The collider key. </value>
        ColliderKey ColliderKey { get; }

        /// <summary>   Material of the hit leaf collider. </summary>
        ///
        /// <value> The material. </value>
        Material Material { get; }

        /// <summary>   Entity of the hit body. </summary>
        ///
        /// <value> The entity. </value>
        Entity Entity { get; }
    }

    struct QueryContext
    {
        public int RigidBodyIndex;
        public ColliderKey ColliderKey;
        public Entity Entity;
        public uint NumColliderKeyBits;
        public ScaledMTransform WorldFromLocalTransform;
        public bool IsInitialized;
        public float InvTargetScale;

        // Needed only in ColliderCast queries with non convex input,
        // where it is used to handle penetration cases properly.
        public bool IsFlipped;

        public float TargetScale => WorldFromLocalTransform.Scale;

        public void InitScale()
        {
            InvTargetScale = 1.0f;
            WorldFromLocalTransform.Scale = 1.0f;
        }

        public static QueryContext DefaultContext => new QueryContext
        {
            RigidBodyIndex = -1,
            ColliderKey = ColliderKey.Empty,
            Entity = Entity.Null,
            NumColliderKeyBits = 0,
            WorldFromLocalTransform = ScaledMTransform.Identity,
            InvTargetScale = 1.0f,
            IsInitialized = true,
            IsFlipped = false
        };

        public ColliderKey SetSubKey(uint childSubKeyNumOfBits, uint childSubKey)
        {
            var parentColliderKey = ColliderKey;
            parentColliderKey.PopSubKey(NumColliderKeyBits, out uint parentKey);

            var colliderKey = new ColliderKey(childSubKeyNumOfBits, childSubKey);
            colliderKey.PushSubKey(NumColliderKeyBits, parentKey);
            return colliderKey;
        }

        public ColliderKey PushSubKey(uint childSubKeyNumOfBits, uint childSubKey)
        {
            var colliderKey = SetSubKey(childSubKeyNumOfBits, childSubKey);
            NumColliderKeyBits += childSubKeyNumOfBits;
            return colliderKey;
        }
    }

    /// <summary>   Interface for collecting hits during a collision query. </summary>
    ///
    /// <typeparam name="T">    Generic type parameter. </typeparam>
    public interface ICollector<T> where T : struct, IQueryResult
    {
        /// <summary>   Whether to exit the query as soon as any hit has been accepted. </summary>
        ///
        /// <value> True if early out on first hit, false if not. </value>
        bool EarlyOutOnFirstHit { get; }

        /// <summary>
        /// The maximum fraction of the query within which to check for hits For casts, this is a
        /// fraction along the ray For distance queries, this is a distance from the query object.
        /// </summary>
        ///
        /// <value> The maximum fraction. </value>
        float MaxFraction { get; }

        /// <summary>   The number of hits that have been collected. </summary>
        ///
        /// <value> The total number of hits. </value>
        int NumHits { get; }

        /// <summary>
        /// Called when the query hits something.
        /// </summary>
        ///
        /// <param name="hit">  The hit. </param>
        ///
        /// <returns>   True to accept the hit, or false to ignore it. </returns>
        bool AddHit(T hit);
    }

    /// <summary>   A collector which exits the query as soon as any hit is detected. </summary>
    ///
    /// <typeparam name="T">    Generic type parameter. </typeparam>
    public struct AnyHitCollector<T> : ICollector<T> where T : struct, IQueryResult
    {
        /// <summary>   Gets a value indicating whether the early out on first hit. </summary>
        ///
        /// <value> True. </value>
        public bool EarlyOutOnFirstHit => true;

        /// <summary>   Gets the maximum fraction. </summary>
        ///
        /// <value> The maximum fraction. </value>
        public float MaxFraction { get; }

        /// <summary>   Gets the number of hits. </summary>
        ///
        /// <value> The total number of hits. </value>
        public int NumHits => 0;

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="maxFraction">  The maximum fraction. </param>
        public AnyHitCollector(float maxFraction)
        {
            MaxFraction = maxFraction;
        }

        #region ICollector

        /// <summary>   Adds a hit. </summary>
        ///
        /// <param name="hit">  The hit. </param>
        ///
        /// <returns>   True. </returns>
        public bool AddHit(T hit)
        {
            Assert.IsTrue(hit.Fraction <= MaxFraction);
            return true;
        }

        #endregion
    }

    /// <summary>   A collector which stores only the closest hit. </summary>
    ///
    /// <typeparam name="T">    Generic type parameter. </typeparam>
    public struct ClosestHitCollector<T> : ICollector<T> where T : struct, IQueryResult
    {
        /// <summary>   Gets a value indicating whether the early out on first hit. </summary>
        ///
        /// <value> False. </value>
        public bool EarlyOutOnFirstHit => false;

        /// <summary>   Gets or sets the maximum fraction. </summary>
        ///
        /// <value> The maximum fraction. </value>
        public float MaxFraction { get; private set; }

        /// <summary>   Gets  the number of hits. </summary>
        ///
        /// <value> The total number of hits (0 or 1). </value>
        public int NumHits { get; private set; }

        private T m_ClosestHit;

        /// <summary>   Gets the closest hit. </summary>
        ///
        /// <value> The closest hit. </value>
        public T ClosestHit => m_ClosestHit;

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="maxFraction">  The maximum fraction. </param>
        public ClosestHitCollector(float maxFraction)
        {
            MaxFraction = maxFraction;
            m_ClosestHit = default(T);
            NumHits = 0;
        }

        #region ICollector

        /// <summary>   Adds a hit. </summary>
        ///
        /// <param name="hit">  The hit. </param>
        ///
        /// <returns>   True. </returns>
        public bool AddHit(T hit)
        {
            Assert.IsTrue(hit.Fraction <= MaxFraction);
            MaxFraction = hit.Fraction;
            m_ClosestHit = hit;
            NumHits = 1;
            return true;
        }

        #endregion
    }

    /// <summary>   A collector which stores every hit. </summary>
    ///
    /// <typeparam name="T">    Generic type parameter. </typeparam>
    public struct AllHitsCollector<T> : ICollector<T> where T : unmanaged, IQueryResult
    {
        /// <summary>   Gets a value indicating whether the early out on first hit. </summary>
        ///
        /// <value> False. </value>
        public bool EarlyOutOnFirstHit => false;

        /// <summary>   Gets the maximum fraction. </summary>
        ///
        /// <value> The maximum fraction. </value>
        public float MaxFraction { get; }

        /// <summary>   Gets the number of hits. </summary>
        ///
        /// <value> The total number of hits. </value>
        public int NumHits => AllHits.Length;

        /// <summary>   All hits. </summary>
        public NativeList<T> AllHits;

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="maxFraction">  The maximum fraction. </param>
        /// <param name="allHits">      [in,out] all hits. </param>
        public AllHitsCollector(float maxFraction, ref NativeList<T> allHits)
        {
            MaxFraction = maxFraction;
            AllHits = allHits;
        }

        #region ICollector

        /// <summary>   Adds a hit. </summary>
        ///
        /// <param name="hit">  The hit. </param>
        ///
        /// <returns>   True. </returns>
        public bool AddHit(T hit)
        {
            Assert.IsTrue(hit.Fraction <= MaxFraction);

            AllHits.Add(hit);
            return true;
        }

        #endregion
    }

    // A collector used to provide filtering for QueryInteraction enum and a specified entity (usually used for self-hit filtering).
    // This is a wrapper of the user provided collector, which serves to enable
    // filtering based on the QueryInteraction parameter.
    internal unsafe struct QueryInteractionCollector<T, C> : ICollector<T>
        where T : struct, IQueryResult
        where C : struct, ICollector<T>
    {
        public bool EarlyOutOnFirstHit => Collector.EarlyOutOnFirstHit;
        public float MaxFraction => Collector.MaxFraction;
        public int NumHits => Collector.NumHits;

        // Todo: have a QueryInteraction field here, and filter differently based on it in AddHit()
        // at the moment, this collector will only get constructed if IgnoreTriggers interaction is selected
        public ref C Collector
        {
            get => ref UnsafeUtility.AsRef<C>(m_CollectorPtr);
        }

        // This must be a void ptr, since C# doesn't allow generic type pointers
        private void* m_CollectorPtr;
        private bool m_IgnoreTriggers;
        private Entity m_EntityToIgnore;

        public QueryInteractionCollector(ref C collector, bool ignoreTriggers, Entity entityToIgnore)
        {
            m_CollectorPtr = UnsafeUtility.AddressOf(ref collector);
            m_EntityToIgnore = entityToIgnore;
            m_IgnoreTriggers = ignoreTriggers;
        }

        public bool AddHit(T hit)
        {
            if ((m_IgnoreTriggers && (hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents)) || (hit.Entity == m_EntityToIgnore))
            {
                return false;
            }

            return Collector.AddHit(hit);
        }
    }

    // Collector used when flipping input and target of collider cast queries
    // It is just a wrapper around user provided collector (base collector)
    // All it does is restore the flipped query output to a non-flipped one
    // and passes the modified hit to the provided collector.
    internal unsafe struct FlippedColliderCastQueryCollector<C> : ICollector<ColliderCastHit>
        where C : struct, ICollector<ColliderCastHit>
    {
        public bool EarlyOutOnFirstHit => Collector.EarlyOutOnFirstHit;
        public float MaxFraction => Collector.MaxFraction;
        public int NumHits => Collector.NumHits;

        public ref C Collector
        {
            get => ref UnsafeUtility.AsRef<C>(m_CollectorPtr);
        }

        private ColliderKey m_TargetColliderKey;
        private Material m_TargetMaterial;
        private float3 m_CastDirectionWS;

        // This must be a void ptr, since C# doesn't allow generic type pointers
        private void* m_CollectorPtr;

        public FlippedColliderCastQueryCollector(ref C collector, float3 castDirectionWS, ColliderKey targetColliderKey, Material targetMaterial)
        {
            m_TargetColliderKey = targetColliderKey;
            m_TargetMaterial = targetMaterial;
            m_CastDirectionWS = castDirectionWS;
            m_CollectorPtr = UnsafeUtility.AddressOf(ref collector);
        }

        public bool AddHit(ColliderCastHit hit)
        {
            hit.Position = hit.Position + hit.Fraction * m_CastDirectionWS;
            hit.SurfaceNormal = -hit.SurfaceNormal;

            // Collider keys are in 'flipped' order, need to swap them.
            hit.QueryColliderKey = hit.ColliderKey;
            hit.ColliderKey = m_TargetColliderKey;

            // Material at this point represents the query collider material, which needs to be corrected to target collider material.
            hit.Material = m_TargetMaterial;

            return Collector.AddHit(hit);
        }
    }

    // Collector used when flipping input and target of collider distance queries
    // It is just a wrapper around user provided collector (base collector)
    // All it does is restore the flipped query output to a non-flipped one
    // and passes the modified hit to the provided collector.
    internal unsafe struct FlippedColliderDistanceQueryCollector<C> : ICollector<DistanceHit>
        where C : struct, ICollector<DistanceHit>
    {
        public bool EarlyOutOnFirstHit => Collector.EarlyOutOnFirstHit;
        public float MaxFraction => Collector.MaxFraction;
        public int NumHits => Collector.NumHits;

        public ref C Collector
        {
            get => ref UnsafeUtility.AsRef<C>(m_CollectorPtr);
        }

        private ColliderKey m_TargetColliderKey;
        private Material m_TargetMaterial;

        // This must be a void ptr, since C# doesn't allow generic type pointers
        private void* m_CollectorPtr;

        public FlippedColliderDistanceQueryCollector(ref C collector, ColliderKey targetColliderKey, Material targetMaterial)
        {
            m_TargetColliderKey = targetColliderKey;
            m_TargetMaterial = targetMaterial;
            m_CollectorPtr = UnsafeUtility.AddressOf(ref collector);
        }

        public bool AddHit(DistanceHit hit)
        {
            hit.Position = hit.Position + hit.SurfaceNormal * hit.Fraction;
            hit.SurfaceNormal = -hit.SurfaceNormal;

            // Collider keys are in 'flipped' order, need to swap them.
            hit.QueryColliderKey = hit.ColliderKey;
            hit.ColliderKey = m_TargetColliderKey;

            // Material at this point represents the query collider material, which needs to be corrected to target collider material.
            hit.Material = m_TargetMaterial;

            return Collector.AddHit(hit);
        }
    }
}
