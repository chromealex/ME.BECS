namespace ME.BECS.FogOfWar {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Units;
    using ME.BECS.Transforms;
    using Unity.Mathematics;

    public static class FogOfWarUtils {
        
        [INLINE(256)]
        public static void Write(in FogOfWarStaticComponent props, in FogOfWarComponent fow, in TransformAspect unitTr, in UnitAspect unit) {

            var pos = WorldToFogMapPosition(in props, unitTr.GetWorldMatrixPosition());
            var range = WorldToFogMapValue(in props, math.sqrt(unit.sightRangeSqr));
            var height = WorldToFogMapValue(in props, unitTr.GetWorldMatrixPosition().y + unit.height);
            SetVisibleRange(in props, in fow, (int)pos.x, (int)pos.y, range, height);

        }

        [INLINE(256)]
        public static bool IsVisible(in FogOfWarStaticComponent props, in FogOfWarComponent fow, uint x, uint y) {
            return fow.nodes[y * props.size.x + x] > 0;
        }

        [INLINE(256)]
        public static int GetHeight(in FogOfWarStaticComponent props, uint x, uint y) {
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
            
            var xf = position.x / (float)props.size.x * props.worldSize.x;
            var yf = position.y / (float)props.size.y * props.worldSize.y;
            var h = props.heights[position.y * props.size.x + position.x] / (float)props.size.x * props.worldSize.x;
            return new float3(xf, h, yf);

        }

        [INLINE(256)]
        public static uint2 WorldToFogMapPosition(in FogOfWarStaticComponent props, in float3 position) {
            
            var xf = position.x / props.worldSize.x * props.size.x;
            var yf = position.z / props.worldSize.y * props.size.y;

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
        public static void SetVisibleRange(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int radius, int height) {

            var radiusSqr = radius * radius;
            for (var r = -radius; r < radius; r++) {

                var hh = (int) math.sqrt(radiusSqr - r * r);
                var x = x0 + r;
                if (x < 0 || x >= props.size.x) continue;

                var ph = y0 + hh;
                for (var y = y0 - hh; y < ph; y++) {

                    if (y < 0 || y >= props.size.y) continue;
                    if (IsVisible(in props, in map, (uint)x, (uint)y) == true) {
                        continue;
                    }

                    if (Raycast(in props, x0, y0, x, y, height) == true) {
                        var index = y * (int)props.size.x + x; 
                        map.nodes[index] = 1;
                    }

                }

            }
            
            /*
            for (var y = -radius; y <= radius; ++y) {

                var py = y0 + y;
                if (py < 0 || py >= map.size.y) continue;

                var index = py * (int)map.size.x;
                index += x0 - radius - 1;
                for (var x = -radius; x <= radius; ++x) {

                    var px = x0 + x;
                    ++index;

                    if (px < 0 || px >= map.size.x || x * x + y * y > radiusSqr) continue;

                    if (Raycast(in map, x0, y0, x, y, height) == true) {

                        JobUtils.SetIfGreater(ref map.nodes[index], height);

                    }
                    
                }

            }*/

        }

        [INLINE(256)]
        private static int GetTerrainHeight(in FogOfWarStaticComponent props,  uint x, uint y) {
            return props.heights[y * props.size.x + x];
        }

        [INLINE(256)]
        private static bool Raycast(in FogOfWarStaticComponent props, int x0, int y0, int x1, int y1, int terrainHeight) {

            var steep = math.abs(y1 - y0) > math.abs(x1 - x0);

            if (steep) {
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

            for (var x = x0; x <= x1; x++) {

                var px = x;
                var py = y;
                if (px < 0 || px >= props.size.x) return false;
                if (py < 0 || py >= props.size.y) return false;
                
                var height = steep == true ? GetTerrainHeight(in props, (uint)y, (uint)x) : GetTerrainHeight(in props, (uint)x, (uint)y);
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

    }

}