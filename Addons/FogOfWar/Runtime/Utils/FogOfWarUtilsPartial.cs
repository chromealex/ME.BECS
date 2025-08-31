#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
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

    public class FogOfWarUtilsPartial {

        [INLINE(256)]
        public static void SetVisibleRangePartial0(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int minRadius, int radius, tfloat height, in FowMathSector sector) {
            
            var propSizeX = (int)props.size.x;
            var propSizeY = (int)props.size.y;
            var radiusMinSqr = minRadius * minRadius;
            var radiusSqr = radius * radius;
            int rMin = 0;
            int rMax = 0;
            /*
             * + -
             * + -
             */
            rMin = um::math.min(radius, x0);
            rMax = 0;
            
            var nodesPtr = (safe_ptr<byte>)map.nodes.GetUnsafePtr();
            if (sector.checkSector == false && height < 0f && radiusMinSqr <= 0) {
                for (var r = -rMin; r < rMax; ++r) {

                    var x = x0 + r;
                    if (x < 0 || x >= props.size.x) continue;

                    var r2 = r * r;
                    var hh = Math.SqrtInt(radiusSqr - r2);
                    var yMin = um::math.max(0, y0 - hh);
                    var yMax = um::math.min(propSizeY, y0 + hh);
                    
                    /*
                     * - -
                     * + -
                     */
                    yMax = (yMax + yMin) / 2;
                    
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
                    
                    /*
                     * - -
                     * + -
                     */
                    yMax = (yMax + yMin) / 2;
                    
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

                        if (height < 0f || FogOfWarUtils.Raycast(in props, x0, y0, x, y, height) == true) {
                            map.nodes[index] = 255;
                            map.explored[index] = 255;
                        }

                    }

                }
            }

        }

        [INLINE(256)]
        public static void SetVisibleRangePartial1(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int minRadius, int radius, tfloat height, in FowMathSector sector) {
            
            var propSizeX = (int)props.size.x;
            var propSizeY = (int)props.size.y;
            var radiusMinSqr = minRadius * minRadius;
            var radiusSqr = radius * radius;
            int rMin = 0;
            int rMax = 0;
            /*
             * - +
             * - +
             */
            rMin = 0;
            rMax = um::math.min(radius, propSizeX);
            
            var nodesPtr = (safe_ptr<byte>)map.nodes.GetUnsafePtr();
            if (sector.checkSector == false && height < 0f && radiusMinSqr <= 0) {
                for (var r = -rMin; r < rMax; ++r) {

                    var x = x0 + r;
                    if (x < 0 || x >= props.size.x) continue;

                    var r2 = r * r;
                    var hh = Math.SqrtInt(radiusSqr - r2);
                    var yMin = um::math.max(0, y0 - hh);
                    var yMax = um::math.min(propSizeY, y0 + hh);
                    
                    /*
                     * - -
                     * - +
                     */
                    yMax = (yMax + yMin) / 2;
                    
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
                    
                    /*
                     * - -
                     * - +
                     */
                    yMax = (yMax + yMin) / 2;
                    
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

                        if (height < 0f || FogOfWarUtils.Raycast(in props, x0, y0, x, y, height) == true) {
                            map.nodes[index] = 255;
                            map.explored[index] = 255;
                        }

                    }

                }
            }

        }

        [INLINE(256)]
        public static void SetVisibleRangePartial2(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int minRadius, int radius, tfloat height, in FowMathSector sector) {
            
            var propSizeX = (int)props.size.x;
            var propSizeY = (int)props.size.y;
            var radiusMinSqr = minRadius * minRadius;
            var radiusSqr = radius * radius;
            int rMin = 0;
            int rMax = 0;
            /*
             * + -
             * + -
             */
            rMin = um::math.min(radius, x0);
            rMax = 0;
            
            var nodesPtr = (safe_ptr<byte>)map.nodes.GetUnsafePtr();
            if (sector.checkSector == false && height < 0f && radiusMinSqr <= 0) {
                for (var r = -rMin; r < rMax; ++r) {

                    var x = x0 + r;
                    if (x < 0 || x >= props.size.x) continue;

                    var r2 = r * r;
                    var hh = Math.SqrtInt(radiusSqr - r2);
                    var yMin = um::math.max(0, y0 - hh);
                    var yMax = um::math.min(propSizeY, y0 + hh);
                    
                    /*
                     * + -
                     * - -
                     */
                    yMin = (yMax + yMin) / 2;
                    
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
                    
                    /*
                     * + -
                     * - -
                     */
                    yMin = (yMax + yMin) / 2;
                    
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

                        if (height < 0f || FogOfWarUtils.Raycast(in props, x0, y0, x, y, height) == true) {
                            map.nodes[index] = 255;
                            map.explored[index] = 255;
                        }

                    }

                }
            }

        }

        [INLINE(256)]
        public static void SetVisibleRangePartial3(in FogOfWarStaticComponent props, in FogOfWarComponent map, int x0, int y0, int minRadius, int radius, tfloat height, in FowMathSector sector) {
            
            var propSizeX = (int)props.size.x;
            var propSizeY = (int)props.size.y;
            var radiusMinSqr = minRadius * minRadius;
            var radiusSqr = radius * radius;
            int rMin = 0;
            int rMax = 0;
            /*
             * - +
             * - +
             */
            rMin = 0;
            rMax = um::math.min(radius, propSizeX);
            
            var nodesPtr = (safe_ptr<byte>)map.nodes.GetUnsafePtr();
            if (sector.checkSector == false && height < 0f && radiusMinSqr <= 0) {
                for (var r = -rMin; r < rMax; ++r) {

                    var x = x0 + r;
                    if (x < 0 || x >= props.size.x) continue;

                    var r2 = r * r;
                    var hh = Math.SqrtInt(radiusSqr - r2);
                    var yMin = um::math.max(0, y0 - hh);
                    var yMax = um::math.min(propSizeY, y0 + hh);
                    
                    /*
                     * - +
                     * - -
                     */
                    yMin = (yMax + yMin) / 2;
                    
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
                    
                    /*
                     * - +
                     * - -
                     */
                    yMin = (yMax + yMin) / 2;
                    
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

                        if (height < 0f || FogOfWarUtils.Raycast(in props, x0, y0, x, y, height) == true) {
                            map.nodes[index] = 255;
                            map.explored[index] = 255;
                        }

                    }

                }
            }

        }

    }

}