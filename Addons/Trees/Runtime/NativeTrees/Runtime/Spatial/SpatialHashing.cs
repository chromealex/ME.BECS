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

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
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
        bool OnVisit(in T obj, in AABB2D bounds, tfloat distanceSqr);
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
    
    public unsafe struct SpatialHashing {

        public readonly struct ObjWrapper : System.IComparable<ObjWrapper>, System.IEquatable<ObjWrapper> {

            public readonly NativeTrees.AABB2D bounds;
            public readonly ME.BECS.Ent obj;
            public readonly int2 minCell;

            [INLINE(256)]
            public ObjWrapper(ME.BECS.Ent obj, NativeTrees.AABB2D bounds) {
                this.obj = obj;
                this.bounds = bounds;
                this.minCell = default;
            }

            [INLINE(256)]
            public ObjWrapper(ME.BECS.Ent obj, NativeTrees.AABB2D bounds, int2 minCell) {
                this.obj = obj;
                this.bounds = bounds;
                this.minCell = minCell;
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
        
        public NativeParallelMultiHashMap<long, ObjWrapper> data;
        private Allocator allocator;
        private int cellSize;
        private tfloat invCellSize;
        public NativeParallelList<ObjWrapper> tempObjects;
        private UnsafeList<ObjWrapper> objects;

        public SpatialHashing(int capacity, int cellSize, Allocator allocator) {
            this.allocator = allocator;
            this.cellSize = cellSize;
            this.invCellSize = 1f / cellSize;
            this.data = new NativeParallelMultiHashMap<long, ObjWrapper>(capacity, allocator);
            this.tempObjects = new NativeParallelList<ObjWrapper>(capacity, allocator);
            this.objects = new UnsafeList<ObjWrapper>(capacity, allocator);
        }

        public void Dispose() {
            this.data.Dispose();
            this.tempObjects.Dispose();
            this.objects.Dispose();
        }

        [INLINE(256)]
        public readonly UnsafeList<ObjWrapper> GetObjects() {
            return this.objects;
        }

        [INLINE(256)]
        public long GetHash(float2 pos) {
            var cx = (int)math.floor(pos.x * this.invCellSize);
            var cy = (int)math.floor(pos.y * this.invCellSize);
            var hash = GetHash(cx, cy);
            return hash;
        }

        [INLINE(256)]
        public static long GetHash(int cx, int cy) {
            return ((long)cx << 32) | (uint)cy;
        }

        [INLINE(256)]
        public void Clear() {
            this.tempObjects.Clear();
            this.data.Clear();
            this.objects.Clear();
        }
        
        [INLINE(256)]
        public void Insert(ME.BECS.Ent obj, NativeTrees.AABB2D bounds) {
            var minX = (int)math.floor(bounds.min.x * this.invCellSize);
            var minY = (int)math.floor(bounds.min.y * this.invCellSize);
            var maxX = (int)math.floor(bounds.max.x * this.invCellSize);
            var maxY = (int)math.floor(bounds.max.y * this.invCellSize);
            var item = new ObjWrapper(obj, bounds, new int2(minX, minY));
            for (int x = minX; x <= maxX; ++x) {
                for (int y = minY; y <= maxY; ++y) {
                    var hash = GetHash(x, y);
                    this.data.Add(hash, item);
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
            this.objects.Clear();
            this.objects.AddRange(temp.Ptr, temp.Length);
            var marker = new Unity.Profiling.ProfilerMarker("Insert");
            marker.Begin();
            foreach (var obj in temp) {
                this.Insert(obj.obj, obj.bounds);
            }
            marker.End();
                
        }
        
        [INLINE(256)]
        public void NearestFirst<U, V>(float2 pos, tfloat minDistanceSqr, tfloat maxDistanceSqr, ref U visitor, ref V provider, bool ignoreSorting) where U : struct, ISpatialNearestVisitor<ME.BECS.Ent> where V : struct, ISpatialDistanceProvider<ME.BECS.Ent> {
            if (this.objects.Length == 0) return;
            var range = math.sqrt(maxDistanceSqr);
            var min = this.GetCell(pos - range);
            var max = this.GetCell(pos + range);
            var dist = tfloat.MaxValue;
            var nearest = default(ME.BECS.Ent);
            var hasNearest = false;
            if (this.ShouldUseLinear(in min, in max) == true) {
                for (int i = 0; i < this.objects.Length; ++i) {
                    var item = this.objects[i];
                    var d = provider.DistanceSquared(in pos, in item.obj, in item.bounds);
                    if ((minDistanceSqr <= 0f || d > minDistanceSqr) && d <= maxDistanceSqr && (d < dist || (d == dist && hasNearest == true && item.obj.CompareTo(nearest) < 0))) {
                        if (visitor.OnVisit(in item.obj, in item.bounds, d) == false) {
                            dist = d;
                            nearest = item.obj;
                            hasNearest = true;
                            if (ignoreSorting == true) return;
                        }
                    }
                }
                return;
            }
            for (int x = min.x; x <= max.x; ++x) {
                for (int y = min.y; y <= max.y; ++y) {
                    var hash = GetHash(x, y);
                    var e = this.data.GetValuesForKey(hash);
                    while (e.MoveNext() == true) {
                        var item = e.Current;
                        if (IsCanonicalCell(in item, in min, x, y) == false) continue;
                        var d = provider.DistanceSquared(in pos, in item.obj, in item.bounds);
                        if ((minDistanceSqr <= 0f || d > minDistanceSqr) && d <= maxDistanceSqr && (d < dist || (d == dist && hasNearest == true && item.obj.CompareTo(nearest) < 0))) {
                            if (visitor.OnVisit(in item.obj, in item.bounds, d) == false) {
                                dist = d;
                                nearest = item.obj;
                                hasNearest = true;
                                if (ignoreSorting == true) return;
                            }
                        }
                    }
                }
            }
        }

        [INLINE(256)]
        public void Nearest<U, V>(float2 pos, tfloat minDistanceSqr, tfloat maxDistanceSqr, ref U visitor, ref V provider) where U : struct, ISpatialNearestVisitor<ME.BECS.Ent> where V : struct, ISpatialDistanceProvider<ME.BECS.Ent> {
            if (this.objects.Length == 0) return;
            var range = math.sqrt(maxDistanceSqr);
            var min = this.GetCell(pos - range);
            var max = this.GetCell(pos + range);
            if (this.ShouldUseLinear(in min, in max) == true) {
                for (int i = 0; i < this.objects.Length; ++i) {
                    var item = this.objects[i];
                    var d = provider.DistanceSquared(in pos, in item.obj, in item.bounds);
                    if ((minDistanceSqr <= 0f || d > minDistanceSqr) && d <= maxDistanceSqr && visitor.OnVisit(in item.obj, in item.bounds, d) == false) return;
                }
                return;
            }
            for (int x = min.x; x <= max.x; ++x) {
                for (int y = min.y; y <= max.y; ++y) {
                    var hash = GetHash(x, y);
                    var e = this.data.GetValuesForKey(hash);
                    while (e.MoveNext() == true) {
                        var item = e.Current;
                        if (IsCanonicalCell(in item, in min, x, y) == false) continue;
                        var d = provider.DistanceSquared(in pos, in item.obj, in item.bounds);
                        if ((minDistanceSqr <= 0f || d > minDistanceSqr) && d <= maxDistanceSqr) {
                            if (visitor.OnVisit(in item.obj, in item.bounds, d) == false) {
                                return;
                            }
                        }
                    }
                }
            }
        }

        [INLINE(256)]
        public void Range<U>(AABB2D range, ref U visitor) where U : struct, ISpatialRangeVisitor<ME.BECS.Ent> {
            if (this.objects.Length == 0) return;
            var min = this.GetCell(range.min);
            var max = this.GetCell(range.max);
            if (this.ShouldUseLinear(in min, in max) == true) {
                for (int i = 0; i < this.objects.Length; ++i) {
                    var item = this.objects[i];
                    if (visitor.OnVisit(in item.obj, in item.bounds, range) == false) return;
                }
                return;
            }
            for (int x = min.x; x <= max.x; ++x) {
                for (int y = min.y; y <= max.y; ++y) {
                    var hash = GetHash(x, y);
                    var e = this.data.GetValuesForKey(hash);
                    while (e.MoveNext() == true) {
                        var item = e.Current;
                        if (IsCanonicalCell(in item, in min, x, y) == false) continue;
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
            return this.GetCell(position);
        }

        [INLINE(256)]
        private int2 GetCell(float2 position) {
            return (int2)math.floor(position * this.invCellSize);
        }

        [INLINE(256)]
        private bool ShouldUseLinear(in int2 min, in int2 max) {
            var width = (long)max.x - min.x + 1L;
            var height = (long)max.y - min.y + 1L;
            return width * height >= this.objects.Length;
        }

        [INLINE(256)]
        private static bool IsCanonicalCell(in ObjWrapper item, in int2 queryMin, int cellX, int cellY) {
            return cellX == math.max(item.minCell.x, queryMin.x) && cellY == math.max(item.minCell.y, queryMin.y);
        }

        [INLINE(256)]
        public bool RaycastAABB(Ray2D ray, out SpatialRaycastHit raycastHit, tfloat distance) {
            raycastHit = default;
            if (this.objects.Length == 0) return false;

            var precomputedRay2D = new PrecomputedRay2D(ray);
            var position = (float2)ray.origin;
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
            var distanceSq = distance * distance;

            for (var x = x0; x <= x1; ++x) {

                var px = (steep == true ? y : x);
                var py = (steep == true ? x : y);

                var hash = GetHash(px, py);
                var e = this.data.GetValuesForKey(hash);
                while (e.MoveNext() == true) {
                    var item = e.Current;
                    if (item.bounds.IntersectsRay(precomputedRay2D, out var point) == true && math.distancesq(precomputedRay2D.origin, point) <= distanceSq) {
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

        private static readonly UnityEngine.Vector3[] gizmosPoints = new UnityEngine.Vector3[4];
        public void DrawGizmos() {

            var rendered = new UnsafeHashSet<long>(this.data.Count(), Allocator.Temp);
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
                            var c = UnityEngine.Gizmos.color;
                            c.a = 0.05f;
                            #if UNITY_EDITOR
                            var rect = new UnityEngine.Rect(p.x - this.cellSize * 0.5f, p.z - this.cellSize * 0.5f, this.cellSize, this.cellSize);
                            gizmosPoints[0] = new UnityEngine.Vector3(rect.xMin, 0f, rect.yMin);
                            gizmosPoints[1] = new UnityEngine.Vector3(rect.xMin, 0f, rect.yMax);
                            gizmosPoints[2] = new UnityEngine.Vector3(rect.xMax, 0f, rect.yMax);
                            gizmosPoints[3] = new UnityEngine.Vector3(rect.xMax, 0f, rect.yMin);
                            UnityEditor.Handles.DrawSolidRectangleWithOutline(gizmosPoints, c, UnityEngine.Gizmos.color);
                            //UnityEngine.Gizmos.DrawWireCube(p, (UnityEngine.Vector3)new float3(1f, 1f, 1f) * this.cellSize);
                            #endif
                        }
                    }
                }
            }

        }

    }

}
