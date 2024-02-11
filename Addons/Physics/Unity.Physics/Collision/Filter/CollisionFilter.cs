using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   Describes which other objects an object can collide with. </summary>
    [DebuggerDisplay("Group: {GroupIndex} BelongsTo: {BelongsTo} CollidesWith: {CollidesWith}")]
    public struct CollisionFilter : IEquatable<CollisionFilter>
    {
        /// <summary>   A bit mask describing which layers this object belongs to. </summary>
        public uint BelongsTo;

        /// <summary>   A bit mask describing which layers this object can collide with. </summary>
        public uint CollidesWith;

        /// <summary>
        /// An optional override for the bit mask checks. If the value in both objects is equal and
        /// positive, the objects always collide. If the value in both objects is equal and negative, the
        /// objects never collide.
        /// </summary>
        public int GroupIndex;

        /// <summary>
        /// Returns true if the filter cannot collide with anything, which likely means it was default
        /// constructed but not initialized.
        /// </summary>
        ///
        /// <value> True if this object is empty, false if not. </value>
        public bool IsEmpty => BelongsTo == 0 || CollidesWith == 0;

        /// <summary>   (Immutable) A collision filter which wants to collide with everything. </summary>
        public static readonly CollisionFilter Default = new CollisionFilter
        {
            BelongsTo = 0xffffffff,
            CollidesWith = 0xffffffff,
            GroupIndex = 0
        };

        /// <summary>
        /// (Immutable) A collision filter which never collides with against anything (including Default).
        /// </summary>
        public static readonly CollisionFilter Zero = new CollisionFilter
        {
            BelongsTo = 0,
            CollidesWith = 0,
            GroupIndex = 0
        };

        /// <summary>   Return true if the given pair of filters want to collide with each other. </summary>
        ///
        /// <param name="filterA">  The filter a. </param>
        /// <param name="filterB">  The filter b. </param>
        ///
        /// <returns>   True if the collision is enabled, false if not. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCollisionEnabled(CollisionFilter filterA, CollisionFilter filterB)
        {
            if (filterA.GroupIndex > 0 && filterA.GroupIndex == filterB.GroupIndex)
            {
                return true;
            }
            if (filterA.GroupIndex < 0 && filterA.GroupIndex == filterB.GroupIndex)
            {
                return false;
            }
            return
                (filterA.BelongsTo & filterB.CollidesWith) != 0 &&
                (filterB.BelongsTo & filterA.CollidesWith) != 0;
        }

        /// <summary>   Return a union of two filters. </summary>
        ///
        /// <param name="filterA">  The filter a. </param>
        /// <param name="filterB">  The filter b. </param>
        ///
        /// <returns>   The new union. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CollisionFilter CreateUnion(CollisionFilter filterA, CollisionFilter filterB)
        {
            return new CollisionFilter
            {
                BelongsTo = filterA.BelongsTo | filterB.BelongsTo,
                CollidesWith = filterA.CollidesWith | filterB.CollidesWith,
                GroupIndex = (filterA.GroupIndex == filterB.GroupIndex) ? filterA.GroupIndex : 0
            };
        }

        /// <summary>   Calculates a hash code for this object. </summary>
        ///
        /// <returns>   A hash code for this object. </returns>
        public override int GetHashCode()
        {
            return unchecked((int)math.hash(new uint3(
                BelongsTo,
                CollidesWith,
                unchecked((uint)GroupIndex)
            )));
        }

        /// <summary>   Tests if this CollisionFilter is considered equal to another. </summary>
        ///
        /// <param name="other">    The collision filter to compare to this object. </param>
        ///
        /// <returns>   True if the objects are considered equal, false if they are not. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CollisionFilter other)
        {
            return BelongsTo == other.BelongsTo && CollidesWith == other.CollidesWith && GroupIndex == other.GroupIndex;
        }
    }
}
