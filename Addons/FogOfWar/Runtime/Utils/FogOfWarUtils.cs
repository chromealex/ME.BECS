
namespace ME.BECS.FogOfWar {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using ME.BECS.Units;
    using ME.BECS.Transforms;
    using Unity.Mathematics;
    using static Cuts;
    
    public readonly ref struct FowMathSector {

        private readonly float2 position;
        private readonly float2 lookDirection;
        private readonly float sector;
        private readonly bool checkSector;
        
        public FowMathSector(in FogOfWarStaticComponent props, in float3 position, in quaternion rotation, float sector) {
            this.position = position.xz;
            this.lookDirection = math.normalize(math.mul(rotation, math.forward())).xz;
            this.sector = math.radians(sector);
            this.checkSector = sector > 0f && sector < 360f;
        }

        public bool IsValid(in FogOfWarStaticComponent props, uint x, uint y) {
            if (this.checkSector == false) return true;
            var dir = math.normalize(FogOfWarUtils.FogMapToWorldPosition(in props, new uint2(x, y)).xz - this.position);
            var dot = math.clamp(math.dot(dir, this.lookDirection), -1f, 1f);
            var angle = math.acos(dot);
            return angle < this.sector * 0.5f;
        }
        
    }

    public static unsafe class FogOfWarUtils {

        public const int BYTES_PER_NODE = 1;
        
        [INLINE(256)]
        public static void Write(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect unitTr, in UnitAspect unit) {

            var pos = WorldToFogMapPosition(in props, unitTr.GetWorldMatrixPosition());
            var fowRange = WorldToFogMapValue(in props, math.sqrt(unit.readSightRangeSqr));
            var fowRangeMin = WorldToFogMapValue(in props, math.sqrt(unit.readMinSightRangeSqr));
            var height = unitTr.GetWorldMatrixPosition().y + unit.readHeight;
            var sector = new FowMathSector(in props, unitTr.GetWorldMatrixPosition(), unitTr.GetWorldMatrixRotation(), unit.readSector);
            SetVisibleRange(in props, in fow, (int)pos.x, (int)pos.y, fowRangeMin, fowRange, height, in sector);

        }

        [INLINE(256)]
        public static void WriteRange(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect tr, float height, float range, float rangeMin) {

            var worldPos = tr.GetWorldMatrixPosition();
            var fowPos = WorldToFogMapPosition(in props, worldPos);
            var fowRange = WorldToFogMapValue(in props, range);
            var fowRangeMin = WorldToFogMapValue(in props, rangeMin);
            var fowHeight = worldPos.y + height;
            SetVisibleRange(in props, in fow, (int)fowPos.x, (int)fowPos.y, fowRangeMin, fowRange, fowHeight, default);

        }

        [INLINE(256)]
        public static void WriteRect(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect tr, float height, float sizeX, float sizeY) {

            var worldPos = tr.GetWorldMatrixPosition();
            var fowPos = WorldToFogMapPosition(in props, worldPos);
            var fowRangeX = WorldToFogMapValue(in props, sizeX);
            var fowRangeY = WorldToFogMapValue(in props, sizeY);
            var fowHeight = worldPos.y + height;
            SetVisibleRect(in props, in fow, (int)fowPos.x - (int)math.ceil(fowRangeX * 0.5f), (int)fowPos.y - (int)math.ceil(fowRangeY * 0.5f), fowRangeX, fowRangeY, fowHeight);

        }

        [INLINE(256)]
        public static bool IsVisible(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y) {
            return IsSet(in fow.nodes, in props, x, y, offset: 0);
        }

        [INLINE(256)]
        public static bool IsVisible(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y, float radius) {
            return IsSet(in fow.nodes, in props, x, y, radius, offset: 0);
        }

        [INLINE(256)]
        public static bool IsExplored(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y) {
            return IsSet(in fow.explored, in props, x, y, offset: 0);
        }

        [INLINE(256)]
        public static bool IsExplored(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y, float radius) {
            return IsSet(in fow.explored, in props, x, y, radius, offset: 0);
        }

        [INLINE(256)]
        public static bool IsSet(in MemArrayAuto<byte> arr, in FogOfWarStaticComponent props, uint x, uint y, byte offset) {
            return arr[(y * props.size.x + x) * BYTES_PER_NODE + offset] > 0;
        }

        [INLINE(256)]
        public static bool IsSet(in MemArrayAuto<byte> arr, in FogOfWarStaticComponent props, uint x, uint y, float radius, byte offset) {
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
        public static float GetHeight(in FogOfWarStaticComponent props, uint x, uint y) {
            return props.heights[y * props.size.x + x];
        }

        [INLINE(256)]
        public static (uint pixelX, uint pixelY) GetPixelPosition(in FogOfWarStaticComponent props, int x, int y, int textureWidth, int textureHeight) {
            
            var xf = x / (float)textureWidth * props.size.x;
            var yf = y / (float)textureHeight * props.size.y;
            var pixelX = xf >= 0u ? (uint)(xf + 0.5f) : 0u;
            var pixelY = yf >= 0u ? (uint)(yf + 0.5f) : 0u;
            
            pixelX = math.clamp(pixelX, 0u, props.size.x - 1u);
            pixelY = math.clamp(pixelY, 0u, props.size.y - 1u);

            return (pixelX, pixelY);

        }

        [INLINE(256)]
        public static float3 FogMapToWorldPosition(in FogOfWarStaticComponent props, in uint2 position) {
            
            var xf = (position.x - props.mapPosition.x * 0.5f) / (float)props.size.x * props.worldSize.x;
            var yf = (position.y - props.mapPosition.y * 0.5f) / (float)props.size.y * props.worldSize.y;
            var h = props.heights[position.y * props.size.x + position.x];
            return new float3(xf, h, yf);

        }

        [INLINE(256)]
        public static uint2 WorldToFogMapPosition(in FogOfWarStaticComponent props, in float3 position) {
            
            var xf = (position.x - props.mapPosition.x * 0.5f) / props.worldSize.x * props.size.x;
            var yf = (position.z - props.mapPosition.y * 0.5f) / props.worldSize.y * props.size.y;

            //fast round to int (int)(x + 0.5f)
            var x = xf >= 0u ? (uint)(xf + 0.5f) : 0u;
            var y = yf >= 0u ? (uint)(yf + 0.5f) : 0u;

            if (x >= props.size.x) x = props.size.x - 1u;
            if (y >= props.size.y) y = props.size.y - 1u;

            return new uint2(x, y);
            
        }

        [INLINE(256)]
        public static int WorldToFogMapValue(in FogOfWarStaticComponent props, in float value) {
            
            var xf = value / props.worldSize.x * props.size.x;
            var x = xf >= 0u ? (uint)(xf + 0.5f) : 0u;
            if (x >= props.size.x) x = props.size.x - 1u;
            return (int)x;
            
        }

        [INLINE(256)]
        public static void SetVisibleRect(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int sizeX, int sizeY, float height) {

            for (var x = x0; x < x0 + sizeX; ++x) {
                if (x < 0 || x >= props.size.x) continue;
                for (var y = y0; y < y0 + sizeY; ++y) {
                    if (y < 0 || y >= props.size.y) continue;
                    var index = y * (int)props.size.x + x;
                    if (GetHeight(in props, (uint)x, (uint)y) > height) continue;
                    map.nodes[index * BYTES_PER_NODE] = 255;
                    map.explored[index * BYTES_PER_NODE] = 255;
                }
            }
            
        }

        [INLINE(256)]
        public static void SetVisibleRange(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int minRadius, int radius, float height, in FowMathSector sector) {

            var radiusMinSqr = minRadius * minRadius;
            var radiusSqr = radius * radius;
            for (var r = -radius; r < radius; ++r) {

                var hh = (int)math.sqrt(radiusSqr - r * r);
                var x = x0 + r;
                if (x < 0 || x >= props.size.x) continue;

                for (var y = y0 - hh; y < y0 + hh; ++y) {

                    if (y < 0 || y >= props.size.y) continue;
                    if (IsVisible(in props, in map, (uint)x, (uint)y) == true) {
                        continue;
                    }

                    var localY = y - y0;
                    if (r * r <= radiusMinSqr && localY * localY <= radiusMinSqr) continue;

                    if (sector.IsValid(in props, (uint)x, (uint)y) == false) continue;

                    if (Raycast(in props, x0, y0, x, y, height) == true) {
                        var index = y * (int)props.size.x + x;
                        map.nodes[index * BYTES_PER_NODE] = 255;
                        map.explored[index * BYTES_PER_NODE] = 255;
                    }

                }

            }
            
        }

        [INLINE(256)]
        private static bool Raycast(in FogOfWarStaticComponent props, int x0, int y0, int x1, int y1, float terrainHeight) {

            var steep = math.abs(y1 - y0) > math.abs(x1 - x0);
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
            var dy = math.abs(y1 - y0);
            var error = dx / 2;
            var ystep = y0 < y1 ? 1 : -1;
            var y = y0;

            for (var x = x0; x <= x1; ++x) {

                var px = (steep == true ? y : x);
                var py = (steep == true ? x : y);
                if (px < 0 || px >= props.size.x) return false;
                if (py < 0 || py >= props.size.y) return false;
            
                var height = GetHeight(in props, (uint)px, (uint)py);
                if (height > terrainHeight) {
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
            _memclear(data.GetUnsafePtr(), TSize<byte>.sizeInt * data.Length);
        }

        [INLINE(256)]
        public static Ent Reveal(in ME.BECS.Players.PlayerAspect owner, in float3 position, float range, float height, float lifetime = -1f, in JobInfo jobInfo = default) {
            var ent = Ent.New(in jobInfo);
            ME.BECS.Players.PlayerUtils.SetOwner(in ent, in owner);
            var entTr = ent.GetOrCreateAspect<TransformAspect>();
            entTr.position = position;
            entTr.rotation = quaternion.identity;
            ent.Set(new FogOfWarRevealerComponent() {
                type = (byte)RevealType.Range,
                range = range,
                height = height,
            });
            if (lifetime >= 0f) ent.Destroy(lifetime);
            return ent;
        }

        [INLINE(256)]
        public static Ent Reveal(in ME.BECS.Players.PlayerAspect owner, in float3 position, float sizeX, float sizeY, float height, float lifetime = -1f, in JobInfo jobInfo = default) {
            var ent = Ent.New(in jobInfo);
            ME.BECS.Players.PlayerUtils.SetOwner(in ent, in owner);
            var entTr = ent.GetOrCreateAspect<TransformAspect>();
            entTr.position = position;
            entTr.rotation = quaternion.identity;
            ent.Set(new FogOfWarRevealerComponent() {
                type = (byte)RevealType.Rect,
                range = sizeX,
                rangeY = sizeY,
                height = height,
            });
            if (lifetime >= 0f) ent.Destroy(lifetime);
            return ent;
        }

    }

}