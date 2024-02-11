using System;
using Unity.Mathematics;

namespace Unity.Physics
{
    /// <summary>   A mathematics utility class. </summary>
    public static partial class Math
    {
        /// <summary>   Range of possible values for some constrained parameter. </summary>
        public struct FloatRange : IEquatable<FloatRange>
        {
            /// <summary>   The minimum. </summary>
            public float Min;

            /// <summary>   The maximum. </summary>
            public float Max;

            /// <summary>   Constructor. </summary>
            ///
            /// <param name="min">  The minimum. </param>
            /// <param name="max">  The maximum. </param>

            public FloatRange(float min, float max)
            {
                Min = min;
                Max = max;
            }

            /// <summary>   Gets the middle. </summary>
            ///
            /// <value> The middle. </value>

            public float Mid => math.lerp(Min, Max, 0.5f);

            /// <summary>   Tests if this FloatRange is considered equal to another. </summary>
            ///
            /// <param name="other">    The float range to compare to this object. </param>
            ///
            /// <returns>   True if the objects are considered equal, false if they are not. </returns>

            public bool Equals(FloatRange other) => Min.Equals(other.Min) && Max.Equals(other.Max);

            /// <summary>   Tests if this object is considered equal to another. </summary>
            ///
            /// <param name="obj">  The object to compare to this object. </param>
            ///
            /// <returns>   True if the objects are considered equal, false if they are not. </returns>

            public override bool Equals(object obj) => obj is FloatRange other && Equals(other);

            /// <summary>   Calculates a hash code for this object. </summary>
            ///
            /// <returns>   A hash code for this object. </returns>

            public override int GetHashCode() => unchecked((int)math.hash(new float2(Min, Max)));

            /// <summary>   Implicit cast that converts the given FloatRange to a float2. </summary>
            ///
            /// <param name="range">    The range. </param>
            ///
            /// <returns>   The result of the operation. </returns>

            public static implicit operator float2(FloatRange range) => new float2(range.Min, range.Max);

            /// <summary>   Implicit cast that converts the given float2 to a FloatRange. </summary>
            ///
            /// <param name="f">    A float2 to process. </param>
            ///
            /// <returns>   The result of the operation. </returns>

            public static implicit operator FloatRange(float2 f) => new FloatRange { Min = f.x, Max = f.y };

            /// <summary>   Convert this object into a string representation. </summary>
            ///
            /// <returns>   A string that represents this object. </returns>

            public override string ToString() => $"FloatRange {{ Min = {Min}, Max = {Max} }}";

            /// <summary>   Returns a sorted copy of this instance. </summary>
            ///
            /// <returns>
            /// A copy of this instance, where <see cref="Min"/> is the lesser of <see cref="Min"/> and <see cref="Max"/>
            /// , and <see cref="Max"/> is the greater of the two.
            /// </returns>

            public FloatRange Sorted() => math.select(this, ((float2)this).yx, Min > Max);
        }
    }
}
