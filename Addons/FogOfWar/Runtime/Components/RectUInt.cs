#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.FogOfWar {

    using UnityEngine;
    using System;
    using System.Runtime.CompilerServices;

    [System.Serializable]
    public struct RectUInt : IEquatable<RectUInt>, IFormattable {

        public uint mXMin;
        public uint mYMin;
        public uint mWidth;
        public uint mHeight;

        /// <summary>
        ///   <para>Left coordinate of the rectangle.</para>
        /// </summary>
        public uint x {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.mXMin;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.mXMin = value;
        }

        /// <summary>
        ///   <para>Top coordinate of the rectangle.</para>
        /// </summary>
        public uint y {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.mYMin;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.mYMin = value;
        }

        /// <summary>
        ///   <para>Center coordinate of the rectangle.</para>
        /// </summary>
        public float2 center {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new((tfloat)this.x + (tfloat)this.mWidth / 2f, (tfloat)this.y + (tfloat)this.mHeight / 2f);
        }

        /// <summary>
        ///   <para>The lower left corner of the rectangle; which is the minimal position of the rectangle along the x- and y-axes, when it is aligned to both axes.</para>
        /// </summary>
        public uint2 min {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.xMin, this.yMin);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                this.xMin = value.x;
                this.yMin = value.y;
            }
        }

        /// <summary>
        ///   <para>The upper right corner of the rectangle; which is the maximal position of the rectangle along the x- and y-axes, when it is aligned to both axes.</para>
        /// </summary>
        public uint2 max {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.xMax, this.yMax);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                this.xMax = value.x;
                this.yMax = value.y;
            }
        }

        /// <summary>
        ///   <para>Width of the rectangle.</para>
        /// </summary>
        public uint width {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.mWidth;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.mWidth = value;
        }

        /// <summary>
        ///   <para>Height of the rectangle.</para>
        /// </summary>
        public uint height {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.mHeight;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.mHeight = value;
        }

        /// <summary>
        ///   <para>Shows the minimum X value of the RectUInt.</para>
        /// </summary>
        public uint xMin {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Math.Min(this.mXMin, this.mXMin + this.mWidth);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                var xMax = this.xMax;
                this.mXMin = value;
                this.mWidth = xMax - this.mXMin;
            }
        }

        /// <summary>
        ///   <para>Show the minimum Y value of the RectUInt.</para>
        /// </summary>
        public uint yMin {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Math.Min(this.mYMin, this.mYMin + this.mHeight);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                var yMax = this.yMax;
                this.mYMin = value;
                this.mHeight = yMax - this.mYMin;
            }
        }

        /// <summary>
        ///   <para>Shows the maximum X value of the RectUInt.</para>
        /// </summary>
        public uint xMax {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Math.Max(this.mXMin, this.mXMin + this.mWidth);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.mWidth = value - this.mXMin;
        }

        /// <summary>
        ///   <para>Shows the maximum Y value of the RectUInt.</para>
        /// </summary>
        public uint yMax {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Math.Max(this.mYMin, this.mYMin + this.mHeight);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.mHeight = value - this.mYMin;
        }

        /// <summary>
        ///   <para>Returns the position (x, y) of the RectUInt.</para>
        /// </summary>
        public uint2 position {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.mXMin, this.mYMin);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                this.mXMin = value.x;
                this.mYMin = value.y;
            }
        }

        /// <summary>
        ///   <para>Returns the width and height of the RectUInt.</para>
        /// </summary>
        public uint2 size {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.mWidth, this.mHeight);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                this.mWidth = value.x;
                this.mHeight = value.y;
            }
        }

        /// <summary>
        ///   <para>Sets the bounds to the min and max value of the rect.</para>
        /// </summary>
        /// <param name="minPosition"></param>
        /// <param name="maxPosition"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMinMax(uint2 minPosition, uint2 maxPosition) {
            this.min = minPosition;
            this.max = maxPosition;
        }

        /// <summary>
        ///   <para>Creates a new RectUInt.</para>
        /// </summary>
        /// <param name="xMin">The minimum X value of the RectUInt.</param>
        /// <param name="yMin">The minimum Y value of the RectUInt.</param>
        /// <param name="width">Width of the rectangle.</param>
        /// <param name="height">Height of the rectangle.</param>
        /// <param name="position">The position (x, y) of the rectangle.</param>
        /// <param name="size">The width (x) and height (y) of the rectangle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RectUInt(uint xMin, uint yMin, uint width, uint height) {
            this.mXMin = xMin;
            this.mYMin = yMin;
            this.mWidth = width;
            this.mHeight = height;
        }

        /// <summary>
        ///   <para>Creates a new RectUInt.</para>
        /// </summary>
        /// <param name="xMin">The minimum X value of the RectUInt.</param>
        /// <param name="yMin">The minimum Y value of the RectUInt.</param>
        /// <param name="width">Width of the rectangle.</param>
        /// <param name="height">Height of the rectangle.</param>
        /// <param name="position">The position (x, y) of the rectangle.</param>
        /// <param name="size">The width (x) and height (y) of the rectangle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RectUInt(uint2 position, uint2 size) {
            this.mXMin = position.x;
            this.mYMin = position.y;
            this.mWidth = size.x;
            this.mHeight = size.y;
        }

        /// <summary>
        ///   <para>Clamps the position and size of the RectUInt to the given bounds.</para>
        /// </summary>
        /// <param name="bounds">Bounds to clamp the RectUInt.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClampToBounds(RectUInt bounds) {
            this.position = new uint2(math.max(math.min(bounds.xMax, this.position.x), bounds.xMin), math.max(math.min(bounds.yMax, this.position.y), bounds.yMin));
            this.size = new uint2(math.min(bounds.xMax - this.position.x, this.size.x), math.min(bounds.yMax - this.position.y, this.size.y));
        }

        /// <summary>
        ///   <para>Returns true if the given position is within the RectUInt.</para>
        /// </summary>
        /// <param name="position">Position to check.</param>
        /// <returns>
        ///   <para>Whether the position is within the RectUInt.</para>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Vector2Int position) {
            return position.x >= this.xMin && position.y >= this.yMin && position.x < this.xMax && position.y < this.yMax;
        }

        /// <summary>
        ///   <para>RectUInts overlap if each RectUInt Contains a shared point.</para>
        /// </summary>
        /// <param name="other">Other rectangle to test overlapping with.</param>
        /// <returns>
        ///   <para>True if the other rectangle overlaps this one.</para>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(RectUInt other) {
            return other.xMin < this.xMax && other.xMax > this.xMin && other.yMin < this.yMax && other.yMax > this.yMin;
        }

        /// <summary>
        ///   <para>Returns the x, y, width and height of the RectUInt.</para>
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        /// <param name="formatProvider">An object that specifies culture-specific formatting.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() {
            return this.ToString((string)null, (IFormatProvider)null);
        }

        /// <summary>
        ///   <para>Returns the x, y, width and height of the RectUInt.</para>
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        /// <param name="formatProvider">An object that specifies culture-specific formatting.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format) {
            return this.ToString(format, (IFormatProvider)null);
        }

        /// <summary>
        ///   <para>Returns the x, y, width and height of the RectUInt.</para>
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        /// <param name="formatProvider">An object that specifies culture-specific formatting.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format, IFormatProvider formatProvider) {
            if (formatProvider == null) {
                formatProvider = (IFormatProvider)System.Globalization.CultureInfo.InvariantCulture.NumberFormat;
            }

            return string.Format("(x:{0}, y:{1}, width:{2}, height:{3})", (object)this.x.ToString(format, formatProvider), (object)this.y.ToString(format, formatProvider),
                                 (object)this.width.ToString(format, formatProvider), (object)this.height.ToString(format, formatProvider));
        }

        /// <summary>
        ///   <para>Returns true if the given RectUInt is equal to this RectUInt.</para>
        /// </summary>
        /// <param name="other"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RectUInt other) {
            return this.mXMin == other.mXMin && this.mYMin == other.mYMin && this.mWidth == other.mWidth && this.mHeight == other.mHeight;
        }

        /// <summary>
        ///   <para>A RectUInt.PositionCollection that contains all positions within the RectUInt.</para>
        /// </summary>
        public RectUInt.PositionEnumerator allPositionsWithin {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.min, this.max);
        }

        /// <summary>
        ///   <para>An iterator that allows you to iterate over all positions within the RectUInt.</para>
        /// </summary>
        public struct PositionEnumerator : System.Collections.Generic.IEnumerator<uint2>, System.Collections.IEnumerator, IDisposable {

            private readonly uint2 _min;
            private readonly uint2 _max;
            private uint2 _current;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public PositionEnumerator(uint2 min, uint2 max) {
                this._min = this._current = min;
                this._max = max;
                this.Reset();
            }

            /// <summary>
            ///   <para>Returns this as an iterator that allows you to iterate over all positions within the RectUInt.</para>
            /// </summary>
            /// <returns>
            ///   <para>This RectUInt.PositionEnumerator.</para>
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RectUInt.PositionEnumerator GetEnumerator() {
                return this;
            }

            /// <summary>
            ///   <para>Moves the enumerator to the next position.</para>
            /// </summary>
            /// <returns>
            ///   <para>Whether the enumerator has successfully moved to the next position.</para>
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                if (this._current.y >= this._max.y) {
                    return false;
                }

                ++this._current.x;
                var x1 = this._current.x;
                var vector2Int = this._max;
                var x2 = vector2Int.x;
                if (x1 >= x2) {
                    ref var local = ref this._current;
                    vector2Int = this._min;
                    var x3 = vector2Int.x;
                    local.x = x3;
                    var x4 = this._current.x;
                    vector2Int = this._max;
                    var x5 = vector2Int.x;
                    if (x4 >= x5) {
                        return false;
                    }

                    ++this._current.y;
                    var y1 = this._current.y;
                    vector2Int = this._max;
                    var y2 = vector2Int.y;
                    if (y1 >= y2) {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            ///   <para>Resets this enumerator to its starting state.</para>
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset() {
                this._current = this._min;
                --this._current.x;
            }

            /// <summary>
            ///   <para>Current position of the enumerator.</para>
            /// </summary>
            public uint2 Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => this._current;
            }

            object System.Collections.IEnumerator.Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (object)this.Current;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void IDisposable.Dispose() { }

        }

    }

}