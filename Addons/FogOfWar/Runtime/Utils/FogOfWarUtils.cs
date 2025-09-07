#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using Unity.Mathematics;
using tfloat = System.Single;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif
using um = Unity.Mathematics;

namespace ME.BECS.FogOfWar {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using ME.BECS.Units;
    using ME.BECS.Transforms;
    using static Cuts;
    
    public readonly ref struct FowMathSector {

        private readonly float2 position;
        private readonly float2 lookDirection;
        private readonly tfloat sector;
        public readonly bool checkSector;
        
        [INLINE(256)]
        public FowMathSector(in float3 position, in quaternion rotation, tfloat sector) {
            this.checkSector = sector > 0 && sector < 360;
            if (this.checkSector == false) {
                this.sector = default;
                this.position = default;
                this.lookDirection = default;
                return;
            }
            this.sector = math.radians(sector);
            this.position = position.xz;
            this.lookDirection = math.normalize(math.mul(rotation, math.forward())).xz;
        }

        [INLINE(256)]
        public bool IsValid(in FogOfWarStaticComponent props, uint x, uint y) {
            if (this.checkSector == false) return true;
            var dir = math.normalize(FogOfWarUtils.FogMapToWorldPosition(in props, new uint2(x, y)).xz - this.position);
            var dot = math.clamp(math.dot(dir, this.lookDirection), -1f, 1f);
            var angle = math.acos(dot);
            return angle < this.sector * 0.5f;
        }
        
    }

    public class FogOfWarData {
        
        public static readonly Unity.Burst.SharedStatic<Internal.Array<byte>> fill255 = Unity.Burst.SharedStatic<Internal.Array<byte>>.GetOrCreate<FogOfWarData>();

        [INLINE(256)]
        public static void Initialize(uint sizeX) {
            FogOfWarData.fill255.Data.Resize(sizeX);
            for (uint i = 0u; i < sizeX * FogOfWarUtils.BYTES_PER_NODE; ++i) {
                FogOfWarData.fill255.Data.Get(i) = 255;
            }
        }

    }

    public static unsafe class FogOfWarUtils {

        public const int BYTES_PER_NODE = 1;
        
        [INLINE(256)]
        public static void Write(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect unitTr, in UnitAspect unit) {

            var worldPos = unitTr.GetWorldMatrixPosition();
            var pos = WorldToFogMapPosition(in props, worldPos);
            var fowRange = WorldToFogMapValue(in props, math.sqrt(unit.readSightRangeSqr));
            var fowRangeMin = WorldToFogMapValue(in props, math.sqrt(unit.readMinSightRangeSqr));
            var height = worldPos.y + unit.readHeight;
            var sector = new FowMathSector(worldPos, unitTr.GetWorldMatrixRotation(), unit.readSector);
            SetVisibleRange(in props, in fow, (int)pos.x, (int)pos.y, fowRangeMin, fowRange, height, in sector);

        }

        [INLINE(256)]
        public static void WriteRange(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in float3 position, tfloat range, tfloat rangeMin) {

            var fowPos = WorldToFogMapPosition(in props, position);
            var fowRange = WorldToFogMapValue(in props, range);
            var fowRangeMin = WorldToFogMapValue(in props, rangeMin);
            SetVisibleRange(in props, in fow, (int)fowPos.x, (int)fowPos.y, fowRangeMin, fowRange, -1f, default);

        }

        [INLINE(256)]
        public static void WriteRange(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in float3 position, tfloat height, tfloat range, tfloat rangeMin) {

            var fowPos = WorldToFogMapPosition(in props, position);
            var fowRange = WorldToFogMapValue(in props, range);
            var fowRangeMin = WorldToFogMapValue(in props, rangeMin);
            var fowHeight = position.y + height;
            SetVisibleRange(in props, in fow, (int)fowPos.x, (int)fowPos.y, fowRangeMin, fowRange, fowHeight, default);

        }

        [INLINE(256)]
        public static void WriteRange(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect tr, tfloat height, tfloat range, tfloat rangeMin) {

            var worldPos = tr.GetWorldMatrixPosition();
            WriteRange(in props, in fow, in worldPos, height, range, rangeMin);

        }

        [INLINE(256)]
        public static void WriteRange(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in float3 position, tfloat height, uint fowRange, uint fowRangeMin, in FowMathSector sector = default) {

            var fowPos = WorldToFogMapPosition(in props, position);
            var fowHeight = position.y + height;
            SetVisibleRange(in props, in fow, (int)fowPos.x, (int)fowPos.y, (int)fowRangeMin, (int)fowRange, fowHeight, in sector);

        }

        [INLINE(256)]
        public static void WriteRange(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect tr, tfloat height, uint range, uint rangeMin, in FowMathSector sector = default) {

            var worldPos = tr.GetWorldMatrixPosition();
            WriteRange(in props, in fow, in worldPos, height, range, rangeMin, in sector);

        }

        [INLINE(256)]
        public static void WriteRange(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in float3 position, tfloat height, uint fowRange, uint fowRangeMin, byte part, in FowMathSector sector = default) {

            var fowPos = WorldToFogMapPosition(in props, position);
            var fowHeight = position.y + height;
            if (part == 0) FogOfWarUtilsPartial.SetVisibleRangePartial0(in props, in fow, (int)fowPos.x, (int)fowPos.y, (int)fowRangeMin, (int)fowRange, fowHeight, in sector);
            if (part == 1) FogOfWarUtilsPartial.SetVisibleRangePartial1(in props, in fow, (int)fowPos.x, (int)fowPos.y, (int)fowRangeMin, (int)fowRange, fowHeight, in sector);
            if (part == 2) FogOfWarUtilsPartial.SetVisibleRangePartial2(in props, in fow, (int)fowPos.x, (int)fowPos.y, (int)fowRangeMin, (int)fowRange, fowHeight, in sector);
            if (part == 3) FogOfWarUtilsPartial.SetVisibleRangePartial3(in props, in fow, (int)fowPos.x, (int)fowPos.y, (int)fowRangeMin, (int)fowRange, fowHeight, in sector);

        }

        [INLINE(256)]
        public static void WriteRange(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect tr, tfloat height, uint range, uint rangeMin, byte part, in FowMathSector sector = default) {

            var worldPos = tr.GetWorldMatrixPosition();
            WriteRange(in props, in fow, in worldPos, height, range, rangeMin, part, in sector);

        }

        [INLINE(256)]
        public static void WriteRect(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect tr, tfloat height, tfloat sizeX, tfloat sizeY) {

            var worldPos = tr.GetWorldMatrixPosition();
            var fowPos = WorldToFogMapPosition(in props, worldPos);
            var fowRangeX = WorldToFogMapValue(in props, sizeX);
            var fowRangeY = WorldToFogMapValue(in props, sizeY);
            var fowHeight = worldPos.y + height;
            SetVisibleRect(in props, in fow, (int)fowPos.x - (int)math.ceil(fowRangeX * 0.5f), (int)fowPos.y - (int)math.ceil(fowRangeY * 0.5f), fowRangeX, fowRangeY, fowHeight);

        }

        [INLINE(256)]
        public static void WriteRect(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect tr, tfloat height, uint fowRangeX, uint fowRangeY) {

            var worldPos = tr.GetWorldMatrixPosition();
            var fowPos = WorldToFogMapPosition(in props, worldPos);
            var fowHeight = worldPos.y + height;
            SetVisibleRect(in props, in fow, (int)fowPos.x - (int)math.ceil(fowRangeX * 0.5f), (int)fowPos.y - (int)math.ceil(fowRangeY * 0.5f), (int)fowRangeX, (int)fowRangeY, fowHeight);

        }

        [INLINE(256)]
        public static void WriteRect(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect tr, tfloat height, uint fowRangeX, uint fowRangeY, byte part) {

            var worldPos = tr.GetWorldMatrixPosition();
            var fowPos = WorldToFogMapPosition(in props, worldPos);
            var fowHeight = worldPos.y + height;
            SetVisibleRectPartial(in props, in fow, (int)fowPos.x - (int)math.ceil(fowRangeX * 0.5f), (int)fowPos.y - (int)math.ceil(fowRangeY * 0.5f), (int)fowRangeX, (int)fowRangeY, fowHeight, part);

        }

        [INLINE(256)]
        public static bool IsVisible(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y) {
            return IsSet(in fow.nodes, in props, x, y, offset: 0);
        }

        [INLINE(256)]
        public static bool IsVisible(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y, float2 size) {
            return IsSet(in fow.nodes, in props, x, y, size, offset: 0);
        }

        [INLINE(256)]
        public static bool IsVisible(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y, tfloat radius) {
            return IsSet(in fow.nodes, in props, x, y, radius, offset: 0);
        }

        [INLINE(256)]
        public static bool IsExplored(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y) {
            return IsSet(in fow.explored, in props, x, y, offset: 0);
        }

        [INLINE(256)]
        public static bool IsExplored(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y, tfloat radius) {
            return IsSet(in fow.explored, in props, x, y, radius, offset: 0);
        }

        [INLINE(256)]
        public static bool IsExplored(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y, float2 size) {
            return IsSet(in fow.explored, in props, x, y, size, offset: 0);
        }

        [INLINE(256)]
        public static bool IsSet(in MemArrayAuto<byte> arr, in FogOfWarStaticComponent props, uint x, uint y, byte offset) {
            return arr[(y * props.size.x + x) * BYTES_PER_NODE + offset] > 0;
        }

        [INLINE(256)]
        public static bool IsSet(in MemArrayAuto<byte> arr, in FogOfWarStaticComponent props, uint x, uint y, float2 size, byte offset) {
            if (IsSet(in arr, in props, x, y, offset) == true) return true;
            if (size.x == 0u && size.y == 0u) return false;
            
            var stepsX = WorldToFogMapValue(in props, size.x * 0.5f);
            var stepsY = WorldToFogMapValue(in props, size.y * 0.5f);
            {
                var py = y * props.size.x;
                for (int px = -stepsX; px < stepsX; ++px) {
                    var of = (x + px);
                    if (of < 0 || of >= props.size.x) continue;
                    var idx = py + (uint)of;
                    if (arr[idx * BYTES_PER_NODE + offset] > 0) return true;
                }
            }

            {
                for (int py = -stepsY; py < stepsY; ++py) {
                    var of = ((y + py) * props.size.x);
                    if (of < 0 || of >= props.size.y) continue;
                    of += x;
                    var idx = (uint)of;
                    if (arr[idx * BYTES_PER_NODE + offset] > 0) return true;
                }
            }

            return false;
        }

        [INLINE(256)]
        public static bool IsSet(in MemArrayAuto<byte> arr, in FogOfWarStaticComponent props, uint x, uint y, tfloat radius, byte offset) {
            if (IsSet(in arr, in props, x, y, offset) == true) return true;
            if (radius <= 0f) return false;
            
            var steps = WorldToFogMapValue(in props, radius);
            {
                var py = y * props.size.x;
                for (int px = -steps; px < steps; ++px) {
                    var of = (x + px);
                    if (of < 0 || of >= props.size.x) continue;
                    var idx = py + (uint)of;
                    if (arr[idx * BYTES_PER_NODE + offset] > 0) return true;
                }
            }

            {
                for (int py = -steps; py < steps; ++py) {
                    var of = ((y + py) * props.size.x);
                    if (of < 0 || of >= props.size.y) continue;
                    of += x;
                    var idx = (uint)of;
                    if (arr[idx * BYTES_PER_NODE + offset] > 0) return true;
                }
            }

            return false;
        }

        [INLINE(256)]
        public static tfloat GetHeight(in FogOfWarStaticComponent props, uint x, uint y) {
            return props.heights[um::math.mad(y, props.size.x, x)];
        }

        [INLINE(256)]
        public static (uint pixelX, uint pixelY) GetPixelPosition(in FogOfWarStaticComponent props, int x, int y, int textureWidth, int textureHeight) {
            
            var xf = x / (tfloat)textureWidth * props.size.x;
            var yf = y / (tfloat)textureHeight * props.size.y;
            var pixelX = xf >= 0u ? (uint)(xf + 0.5f) : 0u;
            var pixelY = yf >= 0u ? (uint)(yf + 0.5f) : 0u;
            
            pixelX = um::math.clamp(pixelX, 0u, props.size.x - 1u);
            pixelY = um::math.clamp(pixelY, 0u, props.size.y - 1u);

            return (pixelX, pixelY);

        }

        [INLINE(256)]
        public static float3 FogMapToWorldPosition(in FogOfWarStaticComponent props, in uint2 position) {

            var xf = position.x * props.nodeSize + props.mapPosition.x;
            var yf = position.y * props.nodeSize + props.mapPosition.y;
            var h = props.heights[um::math.mad(position.y, props.size.x, position.x)];
            return new float3(xf, h, yf);

        }

        [INLINE(256)]
        public static uint2 WorldToFogMapPosition(in FogOfWarStaticComponent props, in float3 position) {

            var xf = (position.x - props.mapPosition.x) / props.nodeSize;
            var yf = (position.z - props.mapPosition.y) / props.nodeSize;
            
            //fast round to int (int)(x + 0.5f)
            var x = xf >= 0u ? (uint)(xf + 0.5f) : 0u;
            var y = yf >= 0u ? (uint)(yf + 0.5f) : 0u;

            if (x >= props.size.x) x = props.size.x - 1u;
            if (y >= props.size.y) y = props.size.y - 1u;

            return new uint2(x, y);
            
        }

        [INLINE(256)]
        public static int WorldToFogMapValue(in FogOfWarStaticComponent props, in tfloat value) {
            
            var xf = value / props.nodeSize;
            return xf >= 0u ? (int)(xf + 0.5f) : 0;
            
        }

        [INLINE(256)]
        public static uint WorldToFogMapUValue(in FogOfWarStaticComponent props, in tfloat value) {
            
            var xf = value / props.nodeSize;
            var x = xf >= 0u ? (uint)(xf + 0.5f) : 0u;
            if (x >= props.size.x) x = props.size.x - 1u;
            return x;
            
        }

        [INLINE(256)]
        public static void SetVisibleRect(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int sizeX, int sizeY, tfloat height) {

            if (sizeX == 1 && sizeY == 1 && FogOfWarUtils.BYTES_PER_NODE == 1) {
                var idx = um::math.mad(y0, (int)props.size.x, x0);
                if (height >= 0f && props.heights[idx] > height) return;
                map.nodes[idx] = 255;
                map.explored[idx] = 255;
                return;
            }
            var propSizeX = (int)props.size.x;
            var propSizeY = (int)props.size.y;
            var rMin = um::math.max(0, x0);
            var rMax = um::math.min(x0 + sizeX, propSizeX);
            var yMin = um::math.max(0, y0);
            var yMax = um::math.min(y0 + sizeY, propSizeY);
            var src = FogOfWarData.fill255.Data.GetPtr();
            var nodesPtr = (safe_ptr<byte>)map.nodes.GetUnsafePtr();
            var exploredPtr = (safe_ptr<byte>)map.explored.GetUnsafePtr();
            if (height > 0f) {
                for (var y = yMin; y < yMax; ++y) {
                    var fromIdx = um::math.mad(y, propSizeX, rMin);
                    var toIdx = um::math.mad(y, propSizeX, rMax);
                    var checkHeight = true;
                    for (int h = fromIdx; h < toIdx; ++h) {
                        if (height > 0f && props.heights[h] > height) {
                            checkHeight = false;
                            break;
                        }
                    }
                    if (checkHeight == false) continue;
                    var count = (toIdx - fromIdx) * BYTES_PER_NODE;
                    _memmove(src, nodesPtr + fromIdx, count);
                    _memmove(src, exploredPtr + fromIdx, count);
                }
            } else {
                for (var y = yMin; y < yMax; ++y) {
                    var fromIdx = um::math.mad(y, propSizeX, rMin);
                    var toIdx = um::math.mad(y, propSizeX, rMax);
                    var count = (toIdx - fromIdx) * BYTES_PER_NODE;
                    _memmove(src, nodesPtr + fromIdx, count);
                    _memmove(src, exploredPtr + fromIdx, count);
                }
            }
            
        }

        [INLINE(256)]
        public static void SetVisibleRectPartial(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int sizeX, int sizeY, tfloat height, byte part) {

            var propSizeX = (int)props.size.x;
            var propSizeY = (int)props.size.y;
            var rMin = um::math.max(0, x0);
            var rMax = um::math.min(x0 + sizeX, propSizeX);
            var yMin = um::math.max(0, y0);
            var yMax = um::math.min(y0 + sizeY, propSizeY);
            if (part == 0) {
                /*
                 * + -
                 * - -
                 */
                rMax = (rMax - rMin) / 2 + rMin;
                yMax = (yMax - yMin) / 2 + yMin;
            } else if (part == 1) {
                /*
                 * - +
                 * - -
                 */
                rMin = (rMax - rMin) / 2 + rMin;
                yMax = (yMax - yMin) / 2 + yMin;
            } else if (part == 2) {
                /*
                 * - -
                 * + -
                 */
                rMax = (rMax - rMin) / 2 + rMin;
                yMin = (yMax - yMin) / 2 + yMin;
            } else if (part == 3) {
                /*
                 * - -
                 * - +
                 */
                rMin = (rMax - rMin) / 2 + rMin;
                yMin = (yMax - yMin) / 2 + yMin;
            }
            var src = FogOfWarData.fill255.Data.GetPtr();
            var nodesPtr = (safe_ptr<byte>)map.nodes.GetUnsafePtr();
            var exploredPtr = (safe_ptr<byte>)map.explored.GetUnsafePtr();
            if (height > 0f) {
                for (var y = yMin; y < yMax; ++y) {
                    var s = y * propSizeX;
                    var fromIdx = s + rMin;
                    var toIdx = s + rMax;
                    var checkHeight = true;
                    for (int h = fromIdx; h < toIdx; ++h) {
                        if (height > 0f && props.heights[h] > height) {
                            checkHeight = false;
                            break;
                        }
                    }
                    if (checkHeight == false) continue;
                    var count = (toIdx - fromIdx) * BYTES_PER_NODE;
                    _memmove(src, nodesPtr + fromIdx, count);
                    _memmove(src, exploredPtr + fromIdx, count);
                }
            } else {
                for (var y = yMin; y < yMax; ++y) {
                    var s = y * propSizeX;
                    var fromIdx = s + rMin;
                    var toIdx = s + rMax;
                    var count = (toIdx - fromIdx) * BYTES_PER_NODE;
                    _memmove(src, nodesPtr + fromIdx, count);
                    _memmove(src, exploredPtr + fromIdx, count);
                }
            }
            
        }

        [INLINE(256)]
        public static void SetVisibleRange(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int minRadius, int radius, tfloat height, in FowMathSector sector) {

            var radiusSqr = radius * radius;
            
            var propSizeX = (int)props.size.x;
            var propSizeY = (int)props.size.y;
            var yMin = um::math.max(0, y0 - radius);
            var yMax = um::math.min(y0 + radius, propSizeY);
            var nodesPtr = (safe_ptr<byte>)map.nodes.GetUnsafePtr();
            var exploredPtr = (safe_ptr<byte>)map.explored.GetUnsafePtr();
            if (sector.checkSector == false && height < 0f && minRadius <= 0) {
                var fillPtr = FogOfWarData.fill255.Data.GetPtr();
                for (var y = yMin; y < yMax; ++y) {

                    var localY = y - y0;
                    var hh = Math.SqrtInt(radiusSqr - localY * localY);
                    var xMin = um::math.max(0, x0 - hh);
                    var xMax = um::math.min(propSizeX, x0 + hh);
                    if (xMax <= xMin) continue;
                    var fromIdx = um::math.mad(y, propSizeX, xMin);
                    var toIdx = um::math.mad(y, propSizeX, xMax);
                    var count = (toIdx - fromIdx) * BYTES_PER_NODE;
                    _memmove(fillPtr, nodesPtr + fromIdx, count);
                    _memmove(fillPtr, exploredPtr + fromIdx, count);

                }
            } else if (sector.checkSector == false && height >= 0f && minRadius <= 0) {
                var fillPtr = FogOfWarData.fill255.Data.GetPtr();
                for (var y = yMin; y < yMax; ++y) {

                    var localY = y - y0;
                    var hh = Math.SqrtInt(radiusSqr - localY * localY);
                    var xMin = um::math.max(0, x0 - hh);
                    var xMax = um::math.min(propSizeX, x0 + hh);
                    if (xMax <= xMin) continue;
                    var fromIdx = um::math.mad(y, propSizeX, xMin);
                    var toIdx = um::math.mad(y, propSizeX, xMax);
                    var checkHeight = false;
                    for (int h = fromIdx; h < toIdx; ++h) {
                        if (height > 0f && props.heights[h] > height) {
                            checkHeight = true;
                            break;
                        }
                    }

                    if (checkHeight == false) {
                        var count = (toIdx - fromIdx) * BYTES_PER_NODE;
                        _memmove(fillPtr, nodesPtr + fromIdx, count);
                        _memmove(fillPtr, exploredPtr + fromIdx, count);
                    } else {
                        for (var index = fromIdx; index < toIdx; ++index) {

                            if (nodesPtr[index] > 0) continue;
                            if (height < 0f || Raycast(in props, x0, y0, xMin + (index - fromIdx), y, height) == true) {
                                nodesPtr[index] = 255;
                                exploredPtr[index] = 255;
                            }

                        }
                    }

                }
            } else {
                var radiusMinSqr = minRadius * minRadius;
                for (var y = yMin; y < yMax; ++y) {

                    var localY = y - y0;
                    var r2 = localY * localY;
                    var hh = Math.SqrtInt(radiusSqr - r2);
                    var xMin = um::math.max(0, x0 - hh);
                    var xMax = um::math.min(propSizeX, x0 + hh);
                    for (var x = xMin; x < xMax; ++x) {

                        var localX = x - x0;
                        var index = um::math.mad(y, propSizeX, x);
                        if (nodesPtr[index] > 0) continue;
                        if (radiusMinSqr > 0 && r2 <= radiusMinSqr && localX * localX <= radiusMinSqr) continue;
                        if (sector.IsValid(in props, (uint)x, (uint)y) == false) continue;
                        if (height < 0f || Raycast(in props, x0, y0, x, y, height) == true) {
                            nodesPtr[index] = 255;
                            exploredPtr[index] = 255;
                        }

                    }

                }
            }

            /*
            for (var r = -rMin; r < rMax; ++r) {

                var x = x0 + r;
                if (x < 0 || x >= props.size.x) continue;

                var hh = Math.SqrtInt(radiusSqr - r * r);
                var yMin = um::math.max(0, y0 - hh);
                var yMax = um::math.min(propSizeY, y0 + hh);
                for (var y = yMin; y < yMax; ++y) {

                    if (y < 0 || y >= propSizeY) continue;
                    // IsVisible
                    var index = um::math.mad(y, propSizeX, x);
                    if (nodesPtr[index] > 0) {
                        continue;
                    }

                    var localY = y - y0;
                    if (radiusMinSqr > 0 && r * r <= radiusMinSqr && localY * localY <= radiusMinSqr) continue;

                    if (sector.IsValid(in props, (uint)x, (uint)y) == false) continue;

                    if (height < 0f || Raycast(in props, x0, y0, x, y, height) == true) {
                        map.nodes[index] = 255;
                        map.explored[index] = 255;
                    }

                }

            }*/
            
        }
        
        [INLINE(256)]
        public static void SetVisibleRangePartial(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int minRadius, int radius, tfloat height, in FowMathSector sector, byte part) {
            
            var propSizeX = (int)props.size.x;
            var propSizeY = (int)props.size.y;
            var radiusMinSqr = minRadius * minRadius;
            var radiusSqr = radius * radius;
            int rMin = 0;
            int rMax = 0;
            if (part == 0 || part == 2) {
                /*
                 * + -
                 * + -
                 */
                rMin = um::math.min(radius, x0);
                rMax = 0;
            } else if (part == 1 || part == 3) {
                /*
                 * - +
                 * - +
                 */
                rMin = 0;
                rMax = um::math.min(radius, propSizeX);
            }

            var nodesPtr = (safe_ptr<byte>)map.nodes.GetUnsafePtr();
            if (sector.checkSector == false && height < 0f && radiusMinSqr <= 0) {
                for (var r = -rMin; r < rMax; ++r) {

                    var x = x0 + r;
                    if (x < 0 || x >= props.size.x) continue;

                    var r2 = r * r;
                    var hh = Math.SqrtInt(radiusSqr - r2);
                    var yMin = um::math.max(0, y0 - hh);
                    var yMax = um::math.min(propSizeY, y0 + hh);
                    if (part == 0) {
                        /*
                         * - -
                         * + -
                         */
                        yMax = (yMax + yMin) / 2;
                    } else if (part == 2) {
                        /*
                         * + -
                         * - -
                         */
                        yMin = (yMax + yMin) / 2;
                    } else if (part == 1) {
                        /*
                         * - -
                         * - +
                         */
                        yMax = (yMax + yMin) / 2;
                    } else if (part == 3) {
                        /*
                         * - +
                         * - -
                         */
                        yMin = (yMax + yMin) / 2;
                    }

                    yMin = um::math.max(yMin, 0);
                    yMax = um::math.min(yMax, propSizeX);
                    for (int y = yMin; y < yMax; ++y) {

                        // IsVisible
                        var index = um::math.mad(y, propSizeX, x);
                        if (nodesPtr[index] > 0) {
                            continue;
                        }

                        map.nodes[index] = 255;
                        map.explored[index] = 255;

                    }

                }
            } else {
                for (var r = -rMin; r < rMax; ++r) {

                    var x = x0 + r;
                    if (x < 0 || x >= props.size.x) continue;

                    var hh = Math.SqrtInt(radiusSqr - r * r);
                    var yMin = um::math.max(0, y0 - hh);
                    var yMax = um::math.min(propSizeY, y0 + hh);
                    if (part == 0) {
                        /*
                         * - -
                         * + -
                         */
                        yMax = (yMax + yMin) / 2;
                    } else if (part == 2) {
                        /*
                         * + -
                         * - -
                         */
                        yMin = (yMax + yMin) / 2;
                    } else if (part == 1) {
                        /*
                         * - -
                         * - +
                         */
                        yMax = (yMax + yMin) / 2;
                    } else if (part == 3) {
                        /*
                         * - +
                         * - -
                         */
                        yMin = (yMax + yMin) / 2;
                    }

                    yMin = um::math.max(yMin, 0);
                    yMax = um::math.min(yMax, propSizeX);
                    for (var y = yMin; y < yMax; ++y) {

                        // IsVisible
                        var index = um::math.mad(y, propSizeX, x);
                        if (nodesPtr[index] > 0) {
                            continue;
                        }

                        var localY = y - y0;
                        if (radiusMinSqr > 0 && r * r <= radiusMinSqr && localY * localY <= radiusMinSqr) continue;

                        if (sector.IsValid(in props, (uint)x, (uint)y) == false) continue;

                        if (height < 0f || Raycast(in props, x0, y0, x, y, height) == true) {
                            map.nodes[index] = 255;
                            map.explored[index] = 255;
                        }

                    }

                }
            }

        }

        [INLINE(256)]
        internal static bool Raycast(in FogOfWarStaticComponent props, int x0, int y0, int x1, int y1, tfloat terrainHeight) {

            var steep = um::math.abs(y1 - y0) > um::math.abs(x1 - x0);
            if (steep == true) {
                var t = x0;
                x0 = y0;
                y0 = t;
                t = x1;
                x1 = y1;
                y1 = t;
            }

            if (x0 > x1) {
                var t = x0;
                x0 = x1;
                x1 = t;
                t = y0;
                y0 = y1;
                y1 = t;
            }

            var dx = x1 - x0;
            var dy = um::math.abs(y1 - y0);
            var error = dx / 2;
            var ystep = y0 < y1 ? 1 : -1;
            var y = y0;

            for (var x = x0; x <= x1; ++x) {

                var px = (steep == true ? y : x);
                var py = (steep == true ? x : y);
                if (px < 0 || px >= props.size.x) return false;
                if (py < 0 || py >= props.size.y) return false;

                if (GetHeight(in props, (uint)px, (uint)py) > terrainHeight) {
                    return false;
                }

                error -= dy;

                if (error < 0) {
                    y += ystep;
                    error += dx;
                }
            }
            
            return true;
        }

        [INLINE(256)]
        public static void CleanUpTexture(Unity.Collections.NativeArray<byte> data) {
            CleanUpTexture((UnityEngine.Color32*)data.GetUnsafePtr(), (uint)data.Length);
        }

        [INLINE(256)]
        public static void CleanUpTexture(UnityEngine.Color32* data, uint length) {
            _memclear((safe_ptr)data, TSize<byte>.sizeInt * length);
        }

        [INLINE(256)]
        public static Ent CreateObserver(in FogOfWarStaticComponent props, in ME.BECS.Players.PlayerAspect owner, in float3 position, tfloat range, tfloat? height = null, tfloat? lifetime = null, in JobInfo jobInfo = default) {
            var ent = Ent.New(in jobInfo, editorName: "FOW Observer");
            ME.BECS.Players.PlayerUtils.SetOwner(in ent, in owner);
            ent.Set(new FogOfWarRevealerComponent() {
                range = FogOfWarUtils.WorldToFogMapUValue(in props, range),
                height = height != null ? height.Value : (tfloat)(-1f),
            });
            var entTr = ent.GetOrCreateAspect<TransformAspect>();
            entTr.IsStaticLocal = true;
            entTr.position = position;
            entTr.rotation = quaternion.identity;
            ent.Set(new FogOfWarRevealerIsRangeTag());
            //ent.Set(new FogOfWarRevealerIsPartialTag());
            //CreatePart(in jobInfo, in ent, in owner, 0);
            //CreatePart(in jobInfo, in ent, in owner, 1);
            //CreatePart(in jobInfo, in ent, in owner, 2);
            //CreatePart(in jobInfo, in ent, in owner, 3);
            if (lifetime != null) ent.Destroy(lifetime.Value);
            return ent;
            
            [INLINE(256)]
            static void CreatePart(in JobInfo jobInfo, in Ent ent, in ME.BECS.Players.PlayerAspect owner, byte partIndex) {
                var part = Ent.New(in jobInfo, editorName: "FOW Observer Part");
                part.SetParent(ent);
                var tr = part.Set<TransformAspect>();
                tr.IsStaticLocal = true;
                ME.BECS.Players.PlayerUtils.SetOwner(in part, in owner);
                part.Set(new FogOfWarRevealerIsRangeTag());
                part.Set(new FogOfWarRevealerPartialComponent() {
                    part = partIndex,
                });
            }
        }

        [INLINE(256)]
        public static Ent CreateObserver(in FogOfWarStaticComponent props, in ME.BECS.Players.PlayerAspect owner, in float3 position, tfloat range, tfloat? height, Sector sector, tfloat? lifetime = null, in JobInfo jobInfo = default) {
            var ent = Ent.New(in jobInfo, editorName: "FOW Observer");
            ME.BECS.Players.PlayerUtils.SetOwner(in ent, in owner);
            ent.Set(new FogOfWarRevealerComponent() {
                range = FogOfWarUtils.WorldToFogMapUValue(in props, range),
                height = height != null ? height.Value : (tfloat)(-1f),
            });
            ent.Set(new FogOfWarSectorRevealerComponent() {
                value = sector.sector,
            });
            var entTr = ent.GetOrCreateAspect<TransformAspect>();
            entTr.IsStaticLocal = true;
            entTr.position = position;
            entTr.rotation = quaternion.identity;
            ent.Set(new FogOfWarRevealerIsRangeTag());
            ent.Set(new FogOfWarRevealerIsSectorTag());
            //ent.Set(new FogOfWarRevealerIsPartialTag());
            //CreatePart(in jobInfo, in ent, in owner, 0);
            //CreatePart(in jobInfo, in ent, in owner, 1);
            //CreatePart(in jobInfo, in ent, in owner, 2);
            //CreatePart(in jobInfo, in ent, in owner, 3);
            if (lifetime != null) ent.Destroy(lifetime.Value);
            return ent;
            
            [INLINE(256)]
            static void CreatePart(in JobInfo jobInfo, in Ent ent, in ME.BECS.Players.PlayerAspect owner, byte partIndex) {
                var part = Ent.New(in jobInfo, editorName: "FOW Observer Part");
                part.SetParent(ent);
                var tr = part.Set<TransformAspect>();
                tr.IsStaticLocal = true;
                ME.BECS.Players.PlayerUtils.SetOwner(in part, in owner);
                part.Set(new FogOfWarRevealerIsRangeTag());
                part.Set(new FogOfWarRevealerPartialComponent() {
                    part = partIndex,
                });
                part.Set(new FogOfWarRevealerIsSectorTag());
            }
        }

        [INLINE(256)]
        public static Ent CreateObserver(in FogOfWarStaticComponent props, in ME.BECS.Players.PlayerAspect owner, in float3 position, tfloat sizeX, tfloat sizeY, tfloat? height, tfloat? lifetime = null, in JobInfo jobInfo = default) {
            var ent = Ent.New(in jobInfo, editorName: "FOW Observer");
            ME.BECS.Players.PlayerUtils.SetOwner(in ent, in owner);
            ent.Set(new FogOfWarRevealerComponent() {
                range = FogOfWarUtils.WorldToFogMapUValue(in props, sizeX),
                rangeY = FogOfWarUtils.WorldToFogMapUValue(in props, sizeY),
                height = height != null ? height.Value : (tfloat)(-1f),
            });
            var entTr = ent.GetOrCreateAspect<TransformAspect>();
            entTr.IsStaticLocal = true;
            entTr.position = position;
            entTr.rotation = quaternion.identity;
            ent.Set(new FogOfWarRevealerIsRectTag());
            //ent.Set(new FogOfWarRevealerIsPartialTag());
            //CreatePart(in jobInfo, in ent, in owner, 0);
            //CreatePart(in jobInfo, in ent, in owner, 1);
            //CreatePart(in jobInfo, in ent, in owner, 2);
            //CreatePart(in jobInfo, in ent, in owner, 3);
            if (lifetime != null) ent.Destroy(lifetime.Value);
            return ent;
            
            [INLINE(256)]
            static void CreatePart(in JobInfo jobInfo, in Ent ent, in ME.BECS.Players.PlayerAspect owner, byte partIndex) {
                var part = Ent.New(in jobInfo, editorName: "FOW Observer Part");
                part.SetParent(ent);
                var tr = part.Set<TransformAspect>();
                tr.IsStaticLocal = true;
                ME.BECS.Players.PlayerUtils.SetOwner(in part, in owner);
                part.Set(new FogOfWarRevealerIsRectTag());
                part.Set(new FogOfWarRevealerPartialComponent() {
                    part = partIndex,
                });
            }
        }

        [INLINE(256)]
        public static Ent CreateObserver(in FogOfWarStaticComponent props, in ME.BECS.Players.PlayerAspect owner, in Rect rect, tfloat? height, tfloat? lifetime = null, in JobInfo jobInfo = default) {
            var ent = Ent.New(in jobInfo, editorName: "FOW Observer");
            ent.Set(new FogOfWarRevealerComponent() {
                range = FogOfWarUtils.WorldToFogMapUValue(in props, rect.width),
                rangeY = FogOfWarUtils.WorldToFogMapUValue(in props, rect.height),
                height = height != null ? height.Value : (tfloat)(-1f),
            });
            ME.BECS.Players.PlayerUtils.SetOwner(in ent, in owner);
            var entTr = ent.GetOrCreateAspect<TransformAspect>();
            entTr.IsStaticLocal = true;
            entTr.position = new float3(rect.center.x, 0f, rect.center.y);
            entTr.rotation = quaternion.identity;
            ent.Set(new FogOfWarRevealerIsRectTag());
            //ent.Set(new FogOfWarRevealerIsPartialTag());
            //CreatePart(in jobInfo, in ent, in owner, in props, in rect, height, 0);
            //CreatePart(in jobInfo, in ent, in owner, in props, in rect, height, 1);
            //CreatePart(in jobInfo, in ent, in owner, in props, in rect, height, 2);
            //CreatePart(in jobInfo, in ent, in owner, in props, in rect, height, 3);
            if (lifetime != null) ent.Destroy(lifetime.Value);
            return ent;

            [INLINE(256)]
            static void CreatePart(in JobInfo jobInfo, in Ent ent, in ME.BECS.Players.PlayerAspect owner, in FogOfWarStaticComponent props, in Rect rect, tfloat? height, byte partIndex) {
                var part = Ent.New(in jobInfo, editorName: "FOW Observer Part");
                part.SetParent(ent);
                var tr = part.Set<TransformAspect>();
                tr.IsStaticLocal = true;
                ME.BECS.Players.PlayerUtils.SetOwner(in part, in owner);
                part.Set(new FogOfWarRevealerComponent() {
                    range = FogOfWarUtils.WorldToFogMapUValue(in props, rect.width),
                    rangeY = FogOfWarUtils.WorldToFogMapUValue(in props, rect.height),
                    height = height != null ? height.Value : (tfloat)(-1f),
                });
                part.Set(new FogOfWarRevealerIsRectTag());
                part.Set(new FogOfWarRevealerPartialComponent() {
                    part = partIndex,
                });
            }
        }

        [INLINE(256)]
        public static Ent CreateObserver(in FogOfWarStaticComponent props, in ME.BECS.Players.PlayerAspect owner, in RectUInt rect, tfloat? height, tfloat? lifetime = null, in JobInfo jobInfo = default) {
            var ent = Ent.New(in jobInfo, editorName: "FOW Observer");
            ME.BECS.Players.PlayerUtils.SetOwner(in ent, in owner);
            ent.Set(new FogOfWarRevealerComponent() {
                range = rect.width,
                rangeY = rect.height,
                height = height != null ? height.Value : (tfloat)(-1f),
            });
            var entTr = ent.GetOrCreateAspect<TransformAspect>();
            entTr.IsStaticLocal = true;
            var pos = FogOfWarUtils.FogMapToWorldPosition(in props, rect.position);
            var size = FogOfWarUtils.FogMapToWorldPosition(in props, rect.size);
            entTr.position = new float3(pos.x, 0f, pos.z) + new float3(size.x, 0f, size.z) * 0.5f;
            entTr.rotation = quaternion.identity;
            ent.Set(new FogOfWarRevealerIsRectTag());
            //ent.Set(new FogOfWarRevealerIsPartialTag());
            //CreatePart(in jobInfo, in ent, in owner, 0);
            //CreatePart(in jobInfo, in ent, in owner, 1);
            //CreatePart(in jobInfo, in ent, in owner, 2);
            //CreatePart(in jobInfo, in ent, in owner, 3);
            if (lifetime != null) ent.Destroy(lifetime.Value);
            return ent;

            [INLINE(256)]
            static void CreatePart(in JobInfo jobInfo, in Ent ent, in ME.BECS.Players.PlayerAspect owner,  byte partIndex) {
                var part = Ent.New(in jobInfo, editorName: "FOW Observer Part");
                part.SetParent(ent);
                var tr = part.Set<TransformAspect>();
                tr.IsStaticLocal = true;
                ME.BECS.Players.PlayerUtils.SetOwner(in part, in owner);
                part.Set(new FogOfWarRevealerIsRectTag());
                part.Set(new FogOfWarRevealerPartialComponent() {
                    part = partIndex,
                });
            }
        }

        [INLINE(256)]
        public static void ClearQuadTree(in Ent ent) {
            
            var queue = new UnsafeQueue<Ent>(Constants.ALLOCATOR_TEMP);
            queue.Enqueue(ent);
            while (queue.Count > 0u) {
                var e = queue.Dequeue();
                //if (e.Has<QuadTreeElement>() == true) e.Remove<QuadTreeElement>();
                if (e.Has<QuadTreeResult>() == true) e.Remove<QuadTreeResult>();
                ref readonly var children = ref e.Read<ChildrenComponent>().list;
                if (children.Count > 0u) {
                    for (uint i = 0u; i < children.Count; ++i) {
                        var child = children[i];
                        queue.Enqueue(child);
                    }
                }
            }
            
        }

    }

}