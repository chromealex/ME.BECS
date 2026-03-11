#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using static ME.BECS.FixedPoint.math;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
using Ray2D = ME.BECS.FixedPoint.Ray2D;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
using Ray2D = UnityEngine.Ray2D;
#endif

namespace NativeTrees {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using ME.BECS.NativeCollections;

    public struct SpatialRaycastHit {
        
        public float2 point;
        public Ent obj;

    }
    
    public struct SpatialRaycastHitMinNode : ME.BECS.NativeCollections.IMinHeapNode {

        public SpatialRaycastHit data;
        public tfloat cost;

        public tfloat ExpectedCost => this.cost;
        public int Next { get; set; }

    }

    public interface ISpatialNearestVisitor<T> {

        [INLINE(256)]
        bool OnVisit(in T obj, in AABB2D bounds);
        uint Capacity { get; }

    }

    public interface ISpatialRangeVisitor<T> {

        [INLINE(256)]
        bool OnVisit(in T obj, in NativeTrees.AABB2D objBounds, in NativeTrees.AABB2D queryRange);

    }

    public interface ISpatialDistanceProvider<T> {

        [INLINE(256)]
        tfloat DistanceSquared(in float2 point, in T obj, in AABB2D bounds);

    }

    public struct AABB2DSpatialDistanceSquaredProvider<T> : ISpatialDistanceProvider<T> {
        [INLINE(256)]
        public tfloat DistanceSquared(in float2 point, in T obj, in NativeTrees.AABB2D bounds) => bounds.DistanceSquared(point);
    }
    
    public struct SpatialHashing {

        public readonly struct ObjWrapper : System.IComparable<ObjWrapper>, System.IEquatable<ObjWrapper> {

            public readonly NativeTrees.AABB2D bounds;
            public readonly ME.BECS.Ent obj;

            [INLINE(256)]
            public ObjWrapper(ME.BECS.Ent obj, NativeTrees.AABB2D bounds) {
                this.obj = obj;
                this.bounds = bounds;
            }

            [INLINE(256)]
            public int CompareTo(ObjWrapper other) {
                return this.obj.CompareTo(other.obj);
            }

            [INLINE(256)]
            public bool Equals(ObjWrapper other) {
                return this.obj.Equals(other.obj);
            }

            [INLINE(256)]
            public override bool Equals(object obj) {
                return obj is ObjWrapper other && this.Equals(other);
            }

            [INLINE(256)]
            public override int GetHashCode() {
                return this.obj.GetHashCode();
            }

        }
        
        public NativeParallelMultiHashMap<int, ObjWrapper> data;
        private Allocator allocator;
        private int cellSize;
        private tfloat invCellSize;
        public NativeParallelList<ObjWrapper> tempObjects;

        public SpatialHashing(int capacity, int cellSize, Allocator allocator) {
            this.allocator = allocator;
            this.cellSize = cellSize;
            this.invCellSize = 1f / cellSize;
            this.data = new NativeParallelMultiHashMap<int, ObjWrapper>(capacity, allocator);
            this.tempObjects = new NativeParallelList<ObjWrapper>(capacity, allocator);
        }

        public void Dispose() {
            this.data.Dispose();
        }

        [INLINE(256)]
        public int GetHash(float2 pos) {
            var cx = (int)math.floor(pos.x * this.invCellSize);
            var cy = (int)math.floor(pos.y * this.invCellSize);
            var hash = GetHash(cx, cy);
            return hash;
        }

        [INLINE(256)]
        public static int GetHash(int cx, int cy) {
            return cx * 73856093 ^ cy * 19349663;
        }

        [INLINE(256)]
        public void Clear() {
            this.tempObjects.Clear();
            this.data.Clear();
        }
        
        [INLINE(256)]
        public void Insert(ME.BECS.Ent obj, NativeTrees.AABB2D bounds) {
            var minX = (int)math.floor(bounds.min.x * this.invCellSize);
            var minY = (int)math.floor(bounds.min.y * this.invCellSize);
            var maxX = (int)math.floor(bounds.max.x * this.invCellSize);
            var maxY = (int)math.floor(bounds.max.y * this.invCellSize);
            for (int x = minX; x <= maxX; ++x) {
                for (int y = minY; y <= maxY; ++y) {
                    var hash = GetHash(x, y);
                    this.data.Add(hash, new ObjWrapper(obj, bounds));
                }
            }
        }

        [INLINE(256)]
        public void Add(ME.BECS.Ent obj, NativeTrees.AABB2D bounds) {
            this.tempObjects.Add(new ObjWrapper(obj, bounds));
        }

        [INLINE(256)]
        public void Rebuild() {

            var temp = this.tempObjects.ToList(Allocator.Temp);
            // [!] Must be sorted because we add elements in threads 
            temp.Sort();
            var marker = new Unity.Profiling.ProfilerMarker("Insert");
            foreach (var obj in temp) {
                marker.Begin();
                this.Insert(obj.obj, obj.bounds);
                marker.End();
            }
                
        }
        
        [INLINE(256)]
        public void Nearest<U, V>(float2 pos, tfloat minDistanceSqr, tfloat maxDistanceSqr, ref U visitor, ref V provider) where U : struct, ISpatialNearestVisitor<ME.BECS.Ent> where V : struct, ISpatialDistanceProvider<ME.BECS.Ent> {
            if (this.tempObjects.Count == 0) return;
            var rangeInt = (int)math.ceil(math.sqrt(maxDistanceSqr) * this.invCellSize);
            for (int x = -rangeInt; x <= rangeInt; ++x) {
                for (int y = -rangeInt; y <= rangeInt; ++y) {
                    var p = new float2(pos.x + x * this.cellSize, pos.y + y * this.cellSize);
                    var hash = this.GetHash(p);
                    var e = this.data.GetValuesForKey(hash);
                    while (e.MoveNext() == true) {
                        var item = e.Current;
                        var d = provider.DistanceSquared(in pos, in item.obj, in item.bounds);
                        if ((minDistanceSqr <= 0f || d > minDistanceSqr) && d <= maxDistanceSqr) {
                            if (visitor.OnVisit(in item.obj, in item.bounds) == false) {
                                return;
                            }
                        }
                    }
                }
            }
        }

        [INLINE(256)]
        public void Range<U>(AABB2D range, ref U visitor) where U : struct, ISpatialRangeVisitor<ME.BECS.Ent> {
            if (this.tempObjects.Count == 0) return;
            var rangeIntX = (int)math.ceil(range.Size.x * 0.5f * this.invCellSize);
            var rangeIntY = (int)math.ceil(range.Size.y * 0.5f * this.invCellSize);
            for (int x = -rangeIntX; x <= rangeIntX; ++x) {
                for (int y = -rangeIntY; y <= rangeIntY; ++y) {
                    var p = new float2(range.Center.x + x * this.cellSize, range.Center.y + y * this.cellSize);
                    var hash = this.GetHash(p);
                    var e = this.data.GetValuesForKey(hash);
                    while (e.MoveNext() == true) {
                        var item = e.Current;
                        if (visitor.OnVisit(in item.obj, in item.bounds, range) == false) {
                            e.Dispose();
                            return;
                        }
                    }
                }
            }
        }

        [INLINE(256)]
        public int2 GetCoord(float2 position) {
            return new int2((int)math.round(position.x * this.invCellSize), (int)math.round(position.y * this.invCellSize));
        }

        [INLINE(256)]
        public bool RaycastAABB(Ray2D ray, out SpatialRaycastHit raycastHit, tfloat distance) {
            raycastHit = default;
            if (this.tempObjects.Count == 0) return false;

            var precomputedRay2D = new PrecomputedRay2D(ray);
            var position = ray.origin;
            var dir = math.normalizesafe(ray.direction);
            var cell = this.GetCoord(position);
            var targetCell = this.GetCoord(position + dir * distance);

            var x0 = cell.x;
            var x1 = targetCell.x;
            
            var y0 = cell.y;
            var y1 = targetCell.y;
            
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

            raycastHit.point = default;
            for (var x = x0; x <= x1; ++x) {

                var px = (steep == true ? y : x);
                var py = (steep == true ? x : y);

                var hash = GetHash(px, py);
                var e = this.data.GetValuesForKey(hash);
                while (e.MoveNext() == true) {
                    var item = e.Current;
                    if (item.bounds.IntersectsRay(precomputedRay2D, out var point) == true) {
                        raycastHit.point = point;
                        return true;
                    }
                }
                e.Dispose();

                error -= dy;

                if (error < 0) {
                    y += ystep;
                    error += dx;
                }
            }
            
            return false;

        }
        
        public void DrawGizmos() {

            var rendered = new UnsafeHashSet<int>(this.data.Count(), Allocator.Temp);
            foreach (var kv in this.data) {
                var item = kv.Value;
                var bounds = item.bounds;
                var minX = (int)math.floor(bounds.min.x * this.invCellSize);
                var minY = (int)math.floor(bounds.min.y * this.invCellSize);
                var maxX = (int)math.floor(bounds.max.x * this.invCellSize);
                var maxY = (int)math.floor(bounds.max.y * this.invCellSize);
                for (int x = minX; x <= maxX; ++x) {
                    for (int y = minY; y <= maxY; ++y) {
                        var hash = GetHash(x, y);
                        if (this.data.ContainsKey(hash) == true && rendered.Add(hash) == true) {
                            float worldX = (x + 0.5f) * this.cellSize;
                            float worldY = (y + 0.5f) * this.cellSize;
                            var p = new UnityEngine.Vector3(worldX, 0f, worldY);
                            UnityEngine.Gizmos.DrawWireCube(p, (UnityEngine.Vector3)new float3(1f, 1f, 1f) * this.cellSize);
                        }
                    }
                }
            }

        }

    }

}