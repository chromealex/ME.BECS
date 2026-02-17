#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using static ME.BECS.FixedPoint.math;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace NativeTrees {

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

        bool OnVisit(in T obj, in AABB2D bounds);
        uint Capacity { get; }

    }

    public interface ISpatialRangeVisitor<T> {

        bool OnVisit(in T obj, in NativeTrees.AABB2D objBounds, in NativeTrees.AABB2D queryRange);

    }

    public interface ISpatialDistanceProvider<T> {

        tfloat DistanceSquared(in float2 point, in T obj, in AABB2D bounds);

    }

    public struct AABB2DSpatialDistanceSquaredProvider<T> : ISpatialDistanceProvider<T> {
        public tfloat DistanceSquared(in float2 point, in T obj, in NativeTrees.AABB2D bounds) => bounds.DistanceSquared(point);
    }
    
    public struct SpatialHashing {

        public readonly struct ObjWrapper : System.IComparable<ObjWrapper>, System.IEquatable<ObjWrapper> {

            public readonly NativeTrees.AABB2D bounds;
            public readonly ME.BECS.Ent obj;

            public ObjWrapper(ME.BECS.Ent obj, NativeTrees.AABB2D bounds) {
                this.obj = obj;
                this.bounds = bounds;
            }

            public int CompareTo(ObjWrapper other) {
                return this.obj.CompareTo(other.obj);
            }

            public bool Equals(ObjWrapper other) {
                return this.obj.Equals(other.obj);
            }

            public override bool Equals(object obj) {
                return obj is ObjWrapper other && this.Equals(other);
            }

            public override int GetHashCode() {
                return this.obj.GetHashCode();
            }

        }
        
        public NativeParallelMultiHashMap<uint, ObjWrapper> data;
        private Allocator allocator;
        private int cellSize;
        public NativeParallelList<ObjWrapper> tempObjects;

        public SpatialHashing(int capacity, int cellSize, Allocator allocator) {
            this.allocator = allocator;
            this.cellSize = cellSize;
            this.data = new NativeParallelMultiHashMap<uint, ObjWrapper>(capacity, allocator);
            this.tempObjects = new NativeParallelList<ObjWrapper>(capacity, allocator);
        }

        public void Dispose() {
            this.data.Dispose();
        }

        public uint GetHash(float2 pos) {
            var cx = (uint)math.floor(pos.x / this.cellSize);
            var cy = (uint)math.floor(pos.y / this.cellSize);
            var hash = cx * 73856093 ^ cy * 19349663;
            return hash;
        }

        public void Clear() {
            this.tempObjects.Clear();
            this.data.Clear();
        }
        
        public void Insert(ME.BECS.Ent obj, NativeTrees.AABB2D bounds) {
            var hash = this.GetHash(bounds.Center);
            this.data.Add(hash, new ObjWrapper(obj, bounds));
        }

        public void Add(ME.BECS.Ent obj, NativeTrees.AABB2D bounds) {
            this.tempObjects.Add(new ObjWrapper(obj, bounds));
        }

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
        
        public void Nearest<U, V>(float2 pos, tfloat minDistanceSqr, tfloat maxDistanceSqr, ref U visitor, ref V provider) where U : struct, ISpatialNearestVisitor<ME.BECS.Ent> where V : struct, ISpatialDistanceProvider<ME.BECS.Ent> {
            var rangeInt = (int)math.ceil(math.sqrt(maxDistanceSqr) / this.cellSize);
            for (float x = -rangeInt; x <= rangeInt; ++x) {
                for (float y = -rangeInt; y <= rangeInt; ++y) {
                    var p = new float2(pos.x + x, pos.y + y);
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
                    e.Dispose();
                }
            }
        }

        public void Range<U>(AABB2D range, ref U visitor) where U : struct, ISpatialRangeVisitor<ME.BECS.Ent> {
            var rangeIntX = (int)math.ceil(range.Size.x * 0.5f / this.cellSize);
            var rangeIntY = (int)math.ceil(range.Size.y * 0.5f / this.cellSize);
            for (float x = -rangeIntX; x <= rangeIntX; ++x) {
                for (float y = -rangeIntY; y <= rangeIntY; ++y) {
                    var p = new float2(range.Center.x + x, range.Center.y + y);
                    var hash = this.GetHash(p);
                    var e = this.data.GetValuesForKey(hash);
                    while (e.MoveNext() == true) {
                        var item = e.Current;
                        if (visitor.OnVisit(in item.obj, in item.bounds, range) == false) {
                            e.Dispose();
                            return;
                        }
                    }
                    e.Dispose();
                }
            }
        }

        public int2 GetCoord(float2 position) {
            return new int2((int)math.round(position.x / this.cellSize) * this.cellSize, (int)math.round(position.y / this.cellSize) * this.cellSize);
        }

        public bool RaycastAABB(UnityEngine.Ray2D ray, out SpatialRaycastHit raycastHit, sfloat distance) {

            raycastHit = default;

            var precomputedRay2D = new PrecomputedRay2D(ray);
            var position = (float2)ray.origin;
            var dir = math.normalizesafe((float2)ray.direction);
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

                var hash = this.GetHash(new float2(px * this.cellSize, py * this.cellSize));
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

            var rendered = new UnsafeHashSet<uint>(this.data.Count(), Allocator.Temp);
            foreach (var kv in this.data) {
                var item = kv.Value;
                var hash = this.GetHash(item.bounds.Center);
                if (this.data.ContainsKey(hash) == true && rendered.Add(hash) == true) {
                    var p = new UnityEngine.Vector3((float)item.bounds.Center.x, 0f, (float)item.bounds.Center.y);
                    p.x = UnityEngine.Mathf.RoundToInt(p.x / this.cellSize) * this.cellSize;
                    p.z = UnityEngine.Mathf.RoundToInt(p.z / this.cellSize) * this.cellSize;
                    UnityEngine.Gizmos.DrawWireCube(p, (UnityEngine.Vector3)new float3(1f, 1f, 1f) * this.cellSize);
                }
            }

        }

    }

}