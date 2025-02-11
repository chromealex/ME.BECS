using tfloat = sfloat;

namespace ME.BECS.FixedPoint {

    using System;
    using System.Globalization;
    using System.Runtime.CompilerServices;

    /// <summary>
    ///   <para>A 2D Rectangle defined by X and Y position, width and height.</para>
    /// </summary>
    [System.Serializable]
    public struct Rect : IEquatable<Rect>, IFormattable {

        [UnityEngine.SerializeField]
        private tfloat m_XMin;
        [UnityEngine.SerializeField]
        private tfloat m_YMin;
        [UnityEngine.SerializeField]
        private tfloat m_Width;
        [UnityEngine.SerializeField]
        private tfloat m_Height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator UnityEngine.Rect(Rect rect) {
            return UnityEngine.Rect.MinMaxRect((float)rect.min.x, (float)rect.min.y, (float)rect.max.x, (float)rect.max.y);
        }
        
        /// <summary>
        ///   <para>Creates a new rectangle.</para>
        /// </summary>
        /// <param name="x">The X value the rect is measured from.</param>
        /// <param name="y">The Y value the rect is measured from.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rect(tfloat x, tfloat y, tfloat width, tfloat height) {
            this.m_XMin = x;
            this.m_YMin = y;
            this.m_Width = width;
            this.m_Height = height;
        }

        /// <summary>
        ///   <para>Creates a rectangle given a size and position.</para>
        /// </summary>
        /// <param name="position">The position of the minimum corner of the rect.</param>
        /// <param name="size">The width and height of the rect.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rect(float2 position, float2 size) {
            this.m_XMin = position.x;
            this.m_YMin = position.y;
            this.m_Width = size.x;
            this.m_Height = size.y;
        }

        /// <summary>
        ///   <para></para>
        /// </summary>
        /// <param name="source"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rect(Rect source) {
            this.m_XMin = source.m_XMin;
            this.m_YMin = source.m_YMin;
            this.m_Width = source.m_Width;
            this.m_Height = source.m_Height;
        }

        /// <summary>
        ///   <para>Shorthand for writing new Rect(0,0,0,0).</para>
        /// </summary>
        public static Rect zero => new(0.0f, 0.0f, 0.0f, 0.0f);

        /// <summary>
        ///   <para>Creates a rectangle from min/max coordinate values.</para>
        /// </summary>
        /// <param name="xmin">The minimum X coordinate.</param>
        /// <param name="ymin">The minimum Y coordinate.</param>
        /// <param name="xmax">The maximum X coordinate.</param>
        /// <param name="ymax">The maximum Y coordinate.</param>
        /// <returns>
        ///   <para>A rectangle matching the specified coordinates.</para>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect MinMaxRect(tfloat xmin, tfloat ymin, tfloat xmax, tfloat ymax) {
            return new Rect(xmin, ymin, xmax - xmin, ymax - ymin);
        }

        /// <summary>
        ///   <para>Set components of an existing Rect.</para>
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(tfloat x, tfloat y, tfloat width, tfloat height) {
            this.m_XMin = x;
            this.m_YMin = y;
            this.m_Width = width;
            this.m_Height = height;
        }

        /// <summary>
        ///   <para>The X coordinate of the rectangle.</para>
        /// </summary>
        public tfloat x {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.m_XMin;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.m_XMin = value;
        }

        /// <summary>
        ///   <para>The Y coordinate of the rectangle.</para>
        /// </summary>
        public tfloat y {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.m_YMin;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.m_YMin = value;
        }

        /// <summary>
        ///   <para>The X and Y position of the rectangle.</para>
        /// </summary>
        public float2 position {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.m_XMin, this.m_YMin);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                this.m_XMin = value.x;
                this.m_YMin = value.y;
            }
        }

        /// <summary>
        ///   <para>The position of the center of the rectangle.</para>
        /// </summary>
        public float2 center {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.x + this.m_Width / 2f, this.y + this.m_Height / 2f);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                this.m_XMin = value.x - this.m_Width / 2f;
                this.m_YMin = value.y - this.m_Height / 2f;
            }
        }

        /// <summary>
        ///   <para>The position of the minimum corner of the rectangle.</para>
        /// </summary>
        public float2 min {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.xMin, this.yMin);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                this.xMin = value.x;
                this.yMin = value.y;
            }
        }

        /// <summary>
        ///   <para>The position of the maximum corner of the rectangle.</para>
        /// </summary>
        public float2 max {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.xMax, this.yMax);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                this.xMax = value.x;
                this.yMax = value.y;
            }
        }

        /// <summary>
        ///   <para>The width of the rectangle, measured from the X position.</para>
        /// </summary>
        public tfloat width {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.m_Width;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.m_Width = value;
        }

        /// <summary>
        ///   <para>The height of the rectangle, measured from the Y position.</para>
        /// </summary>
        public tfloat height {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.m_Height;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.m_Height = value;
        }

        /// <summary>
        ///   <para>The width and height of the rectangle.</para>
        /// </summary>
        public float2 size {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.m_Width, this.m_Height);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                this.m_Width = value.x;
                this.m_Height = value.y;
            }
        }

        /// <summary>
        ///   <para>The minimum X coordinate of the rectangle.</para>
        /// </summary>
        public tfloat xMin {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.m_XMin;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                var xMax = this.xMax;
                this.m_XMin = value;
                this.m_Width = xMax - this.m_XMin;
            }
        }

        /// <summary>
        ///   <para>The minimum Y coordinate of the rectangle.</para>
        /// </summary>
        public tfloat yMin {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.m_YMin;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                var yMax = this.yMax;
                this.m_YMin = value;
                this.m_Height = yMax - this.m_YMin;
            }
        }

        /// <summary>
        ///   <para>The maximum X coordinate of the rectangle.</para>
        /// </summary>
        public tfloat xMax {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.m_Width + this.m_XMin;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.m_Width = value - this.m_XMin;
        }

        /// <summary>
        ///   <para>The maximum Y coordinate of the rectangle.</para>
        /// </summary>
        public tfloat yMax {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.m_Height + this.m_YMin;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.m_Height = value - this.m_YMin;
        }

        /// <summary>
        ///   <para>Returns true if the x and y components of point is a point inside this rectangle. If allowInverse is present and true, the width and height of the Rect are allowed to take negative values (ie, the min value is greater than the max), and the test will still work.</para>
        /// </summary>
        /// <param name="point">Point to test.</param>
        /// <param name="allowInverse">Does the test allow the Rect's width and height to be negative?</param>
        /// <returns>
        ///   <para>True if the point lies within the specified rectangle.</para>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float2 point) {
            return (tfloat)point.x >= (tfloat)this.xMin && (tfloat)point.x < (tfloat)this.xMax && (tfloat)point.y >= (tfloat)this.yMin && (tfloat)point.y < (tfloat)this.yMax;
        }

        /// <summary>
        ///   <para>Returns true if the x and y components of point is a point inside this rectangle. If allowInverse is present and true, the width and height of the Rect are allowed to take negative values (ie, the min value is greater than the max), and the test will still work.</para>
        /// </summary>
        /// <param name="point">Point to test.</param>
        /// <param name="allowInverse">Does the test allow the Rect's width and height to be negative?</param>
        /// <returns>
        ///   <para>True if the point lies within the specified rectangle.</para>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float3 point) {
            return (tfloat)point.x >= (tfloat)this.xMin && (tfloat)point.x < (tfloat)this.xMax && (tfloat)point.y >= (tfloat)this.yMin && (tfloat)point.y < (tfloat)this.yMax;
        }

        /// <summary>
        ///   <para>Returns true if the x and y components of point is a point inside this rectangle. If allowInverse is present and true, the width and height of the Rect are allowed to take negative values (ie, the min value is greater than the max), and the test will still work.</para>
        /// </summary>
        /// <param name="point">Point to test.</param>
        /// <param name="allowInverse">Does the test allow the Rect's width and height to be negative?</param>
        /// <returns>
        ///   <para>True if the point lies within the specified rectangle.</para>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float2 point, bool allowInverse) {
            return !allowInverse
                       ? this.Contains(point)
                       : (((tfloat)this.width < 0.0f && (tfloat)point.x <= (tfloat)this.xMin && (tfloat)point.x > (tfloat)this.xMax) ||
                          ((tfloat)this.width >= 0.0f && (tfloat)point.x >= (tfloat)this.xMin && (tfloat)point.x < (tfloat)this.xMax)) &
                         (((tfloat)this.height < 0.0f && (tfloat)point.y <= (tfloat)this.yMin && (tfloat)point.y > (tfloat)this.yMax) ||
                          ((tfloat)this.height >= 0.0f && (tfloat)point.y >= (tfloat)this.yMin && (tfloat)point.y < (tfloat)this.yMax));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float3 point, bool allowInverse) {
            return !allowInverse
                       ? this.Contains(point)
                       : (((tfloat)this.width < 0.0f && (tfloat)point.x <= (tfloat)this.xMin && (tfloat)point.x > (tfloat)this.xMax) ||
                          ((tfloat)this.width >= 0.0f && (tfloat)point.x >= (tfloat)this.xMin && (tfloat)point.x < (tfloat)this.xMax)) &
                         (((tfloat)this.height < 0.0f && (tfloat)point.y <= (tfloat)this.yMin && (tfloat)point.y > (tfloat)this.yMax) ||
                          ((tfloat)this.height >= 0.0f && (tfloat)point.y >= (tfloat)this.yMin && (tfloat)point.y < (tfloat)this.yMax));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Rect OrderMinMax(Rect rect) {
            if ((tfloat)rect.xMin > (tfloat)rect.xMax) {
                var xMin = rect.xMin;
                rect.xMin = rect.xMax;
                rect.xMax = xMin;
            }

            if ((tfloat)rect.yMin > (tfloat)rect.yMax) {
                var yMin = rect.yMin;
                rect.yMin = rect.yMax;
                rect.yMax = yMin;
            }

            return rect;
        }

        /// <summary>
        ///   <para>Returns true if the other rectangle overlaps this one. If allowInverse is present and true, the widths and heights of the Rects are allowed to take negative values (ie, the min value is greater than the max), and the test will still work.</para>
        /// </summary>
        /// <param name="other">Other rectangle to test overlapping with.</param>
        /// <param name="allowInverse">Does the test allow the widths and heights of the Rects to be negative?</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(Rect other) {
            return (tfloat)other.xMax > (tfloat)this.xMin && (tfloat)other.xMin < (tfloat)this.xMax && (tfloat)other.yMax > (tfloat)this.yMin &&
                   (tfloat)other.yMin < (tfloat)this.yMax;
        }

        /// <summary>
        ///   <para>Returns true if the other rectangle overlaps this one. If allowInverse is present and true, the widths and heights of the Rects are allowed to take negative values (ie, the min value is greater than the max), and the test will still work.</para>
        /// </summary>
        /// <param name="other">Other rectangle to test overlapping with.</param>
        /// <param name="allowInverse">Does the test allow the widths and heights of the Rects to be negative?</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(Rect other, bool allowInverse) {
            var rect = this;
            if (allowInverse) {
                rect = OrderMinMax(rect);
                other = OrderMinMax(other);
            }

            return rect.Overlaps(other);
        }

        /// <summary>
        ///   <para>Returns a point inside a rectangle, given normalized coordinates.</para>
        /// </summary>
        /// <param name="rectangle">Rectangle to get a point inside.</param>
        /// <param name="normalizedRectCoordinates">Normalized coordinates to get a point for.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 NormalizedToPoint(Rect rectangle, float2 normalizedRectCoordinates) {
            return new float2(math.lerp(rectangle.x, rectangle.xMax, normalizedRectCoordinates.x), math.lerp(rectangle.y, rectangle.yMax, normalizedRectCoordinates.y));
        }

        /// <summary>
        ///   <para>Returns the normalized coordinates cooresponding the the point.</para>
        /// </summary>
        /// <param name="rectangle">Rectangle to get normalized coordinates inside.</param>
        /// <param name="point">A point inside the rectangle to get normalized coordinates for.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 PointToNormalized(Rect rectangle, float2 point) {
            return new float2(math.unlerp(rectangle.x, rectangle.xMax, point.x), math.unlerp(rectangle.y, rectangle.yMax, point.y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Rect lhs, Rect rhs) {
            return !(lhs == rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Rect lhs, Rect rhs) {
            return (tfloat)lhs.x == (tfloat)rhs.x && (tfloat)lhs.y == (tfloat)rhs.y && (tfloat)lhs.width == (tfloat)rhs.width && (tfloat)lhs.height == (tfloat)rhs.height;
        }

        public override int GetHashCode() {
            var num1 = this.x;
            var hashCode = num1.GetHashCode();
            num1 = this.width;
            var num2 = num1.GetHashCode() << 2;
            var num3 = hashCode ^ num2;
            num1 = this.y;
            var num4 = num1.GetHashCode() >> 2;
            var num5 = num3 ^ num4;
            num1 = this.height;
            var num6 = num1.GetHashCode() >> 1;
            return num5 ^ num6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object other) {
            return other is Rect other1 && this.Equals(other1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rect other) {
            int num1;
            if (this.x.Equals(other.x)) {
                var num2 = this.y;
                if (num2.Equals(other.y)) {
                    num2 = this.width;
                    if (num2.Equals(other.width)) {
                        num2 = this.height;
                        num1 = num2.Equals(other.height) ? 1 : 0;
                        goto label_5;
                    }
                }
            }

            num1 = 0;
            label_5:
            return num1 != 0;
        }

        /// <summary>
        ///   <para>Returns a formatted string for this Rect.</para>
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        /// <param name="formatProvider">An object that specifies culture-specific formatting.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() {
            return this.ToString((string)null, (IFormatProvider)null);
        }

        /// <summary>
        ///   <para>Returns a formatted string for this Rect.</para>
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        /// <param name="formatProvider">An object that specifies culture-specific formatting.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format) {
            return this.ToString(format, (IFormatProvider)null);
        }

        /// <summary>
        ///   <para>Returns a formatted string for this Rect.</para>
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        /// <param name="formatProvider">An object that specifies culture-specific formatting.</param>
        public string ToString(string format, IFormatProvider formatProvider) {
            if (string.IsNullOrEmpty(format)) {
                format = "F2";
            }

            if (formatProvider == null) {
                formatProvider = (IFormatProvider)CultureInfo.InvariantCulture.NumberFormat;
            }

            var objArray = new object[4] {
                (object)this.x.ToString(format, formatProvider),
                null,
                null,
                null,
            };
            var num = this.y;
            objArray[1] = (object)num.ToString(format, formatProvider);
            num = this.width;
            objArray[2] = (object)num.ToString(format, formatProvider);
            num = this.height;
            objArray[3] = (object)num.ToString(format, formatProvider);
            return string.Format("(x:{0}, y:{1}, width:{2}, height:{3})", objArray);
        }

        [Obsolete("use xMin")]
        public tfloat left => this.m_XMin;

        [Obsolete("use xMax")]
        public tfloat right => this.m_XMin + this.m_Width;

        [Obsolete("use yMin")]
        public tfloat top => this.m_YMin;

        [Obsolete("use yMax")]
        public tfloat bottom => this.m_YMin + this.m_Height;

    }

}