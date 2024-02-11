using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>
    /// An axis-aligned bounding box, or AABB for short, is a box aligned with coordinate axes and
    /// fully enclosing some object.
    /// </summary>
    [DebuggerDisplay("{Min} - {Max}")]
    [Serializable]
    public struct Aabb
    {
        /// <summary>   The minimum point. </summary>
        public float3 Min;
        /// <summary>   The maximum point. </summary>
        public float3 Max;

        /// <summary>   Gets the extents. </summary>
        ///
        /// <value> The extents. </value>
        public float3 Extents => Max - Min;

        /// <summary>   Gets the center. </summary>
        ///
        /// <value> The center. </value>
        public float3 Center => (Max + Min) * 0.5f;

        /// <summary>   Gets a value indicating whether this aabb is valid. </summary>
        ///
        /// <value> True if this aabb is valid, false if not. </value>
        public bool IsValid => math.all(Min <= Max);

        /// <summary>  Create an empty, invalid AABB. </summary>
        public static readonly Aabb Empty = new Aabb { Min = Math.Constants.Max3F, Max = Math.Constants.Min3F };

        /// <summary>   Gets the surface area. </summary>
        ///
        /// <value> The surface area. </value>
        public float SurfaceArea
        {
            get
            {
                float3 diff = Max - Min;
                return 2 * math.dot(diff, diff.yzx);
            }
        }

        /// <summary>   Create a union of two aabbs. </summary>
        ///
        /// <param name="a">    An Aabb to process. </param>
        /// <param name="b">    An Aabb to process. </param>
        ///
        /// <returns>   The union of a and b. </returns>
        public static Aabb Union(Aabb a, Aabb b)
        {
            a.Include(b);
            return a;
        }

        /// <summary>   Intersects this aabb and another one. </summary>
        ///
        /// <param name="aabb"> The aabb to intersect with. </param>
        [DebuggerStepThrough]
        public void Intersect(Aabb aabb)
        {
            Min = math.max(Min, aabb.Min);
            Max = math.min(Max, aabb.Max);
        }

        /// <summary>   Includes the given point in the aabb. </summary>
        ///
        /// <param name="point">    The point. </param>
        [DebuggerStepThrough]
        public void Include(float3 point)
        {
            Min = math.min(Min, point);
            Max = math.max(Max, point);
        }

        /// <summary>   Includes the given aabb into this aabb. </summary>
        ///
        /// <param name="aabb"> The aabb to include. </param>
        [DebuggerStepThrough]
        public void Include(Aabb aabb)
        {
            Min = math.min(Min, aabb.Min);
            Max = math.max(Max, aabb.Max);
        }

        /// <summary>   Query if this aabb contains the given point. </summary>
        ///
        /// <param name="point">    The point to test with. </param>
        ///
        /// <returns>   True if the aabb contains the point, false if not. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float3 point) => math.all(point >= Min & point <= Max);

        /// <summary>   Query if this aabb contains the given aabb. </summary>
        ///
        /// <param name="aabb"> The Aabb to test with. </param>
        ///
        /// <returns>  True if this aabb contains given aabb, false if not. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Aabb aabb) => math.all((Min <= aabb.Min) & (Max >= aabb.Max));

        /// <summary>   Expands the aabb by the provided distance. </summary>
        ///
        /// <param name="distance"> The distance. </param>
        public void Expand(float distance)
        {
            Min -= distance;
            Max += distance;
        }

        /// <summary>   Creates aabb from points. </summary>
        ///
        /// <param name="points">   The points. </param>
        ///
        /// <returns>   The new aabb constructed from points. </returns>
        internal static Aabb CreateFromPoints(float3x4 points)
        {
            Aabb aabb;
            aabb.Min = points.c0;
            aabb.Max = aabb.Min;

            aabb.Min = math.min(aabb.Min, points.c1);
            aabb.Max = math.max(aabb.Max, points.c1);

            aabb.Min = math.min(aabb.Min, points.c2);
            aabb.Max = math.max(aabb.Max, points.c2);

            aabb.Min = math.min(aabb.Min, points.c3);
            aabb.Max = math.max(aabb.Max, points.c3);

            return aabb;
        }

        /// <summary>   Query if this aabb overlaps the given other aabb. </summary>
        ///
        /// <param name="other">    The other aabb. </param>
        ///
        /// <returns>   True if there is an overlap, false otherwise. </returns>
        public bool Overlaps(Aabb other)
        {
            return math.all(Max >= other.Min & Min <= other.Max);
        }

        /// <summary>
        /// Returns the closest point on the bounds of the AABB to the specified position.
        /// </summary>
        ///
        /// <param name="position"> A target point in space. </param>
        ///
        /// <returns>   The closest point. </returns>
        public float3 ClosestPoint(float3 position)
        {
            return math.min(Max, math.max(Min, position));
        }
    }

    /// <summary>   Helper functions. </summary>
    public static partial class Math
    {
        /// <summary>   Transform an AABB into another space, expanding it as needed. </summary>
        ///
        /// <param name="aabb">         The aabb. </param>
        /// <param name="transform">    The transform. </param>
        /// <param name="uniformScale"> (Optional) The uniform scale. </param>
        ///
        /// <returns>   An Aabb. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Aabb TransformAabb(Aabb aabb, RigidTransform transform, float uniformScale = 1.0f)
        {
            // Transforming an empty AABB results in NaNs!
            if (!aabb.IsValid)
            {
                return aabb;
            }

            float3 halfExtentsInA = aabb.Extents * 0.5f * uniformScale;
            float3 x = math.rotate(transform.rot, new float3(halfExtentsInA.x, 0, 0));
            float3 y = math.rotate(transform.rot, new float3(0, halfExtentsInA.y, 0));
            float3 z = math.rotate(transform.rot, new float3(0, 0, halfExtentsInA.z));

            float3 halfExtentsInB = math.abs(x) + math.abs(y) + math.abs(z);
            float3 centerInB = math.transform(transform, aabb.Center * uniformScale);

            return new Aabb
            {
                Min = centerInB - halfExtentsInB,
                Max = centerInB + halfExtentsInB
            };
        }

        /// <summary>   Transform an AABB into another space, expanding it as needed. </summary>
        ///
        /// <param name="aabb">         The aabb. </param>
        /// <param name="transform">    The transform. </param>
        /// <param name="uniformScale"> (Optional) The uniform scale. </param>
        ///
        /// <returns>   An Aabb. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Aabb TransformAabb(Aabb aabb, MTransform transform, float uniformScale = 1.0f)
        {
            // Transforming an empty AABB results in NaNs!
            if (!aabb.IsValid)
            {
                return aabb;
            }

            float3 halfExtentsInA = aabb.Extents * 0.5f * uniformScale;

            float3 transformedX = math.abs(transform.Rotation.c0 * halfExtentsInA.x);
            float3 transformedY = math.abs(transform.Rotation.c1 * halfExtentsInA.y);
            float3 transformedZ = math.abs(transform.Rotation.c2 * halfExtentsInA.z);

            float3 halfExtentsInB = transformedX + transformedY + transformedZ;
            float3 centerInB = Mul(transform, aabb.Center * uniformScale);

            return new Aabb
            {
                Min = centerInB - halfExtentsInB,
                Max = centerInB + halfExtentsInB
            };
        }
    }
}
