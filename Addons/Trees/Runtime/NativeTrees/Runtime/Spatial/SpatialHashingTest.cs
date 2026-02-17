using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class SpatialHashingTest : MonoBehaviour {

    public struct Item : System.IEquatable<Item> {

        public ME.BECS.Ent ent;
        public int data;
        public float2 position;

        public bool Equals(Item other) {
            return this.ent.Equals(other.ent) && this.data == other.data;
        }

        public override bool Equals(object obj) {
            return obj is Item other && this.Equals(other);
        }

        public override int GetHashCode() {
            return System.HashCode.Combine(this.ent, this.data);
        }

    }
    
    public NativeTrees.SpatialHashing data;
    
    public int count = 100_000;
    public float spawnRange = 10f;
    public int cellSize = 2;
    public float range = 1f;

    private NativeArray<Item> items;
    private NativeArray<ME.BECS.SpatialNearestAABBVisitor<ME.BECS.Ent, ME.BECS.AlwaysTrueSpatialSubFilter>> results;

    public void Start() {

        this.data = new NativeTrees.SpatialHashing(this.count, this.cellSize, Allocator.Domain);

        this.items = new NativeArray<Item>(this.count, Allocator.Domain);
        this.results = new NativeArray<ME.BECS.SpatialNearestAABBVisitor<ME.BECS.Ent, ME.BECS.AlwaysTrueSpatialSubFilter>>(this.count, Allocator.Domain);
        for (int i = 0; i < this.count; i++) {
            var pos = UnityEngine.Random.insideUnitCircle * this.spawnRange;
            this.items[i] = new Item() {
                data = i + 1,
                ent = new ME.BECS.Ent((ulong)i),
                position = pos,
            };
        }
        
    }

    public void OnDestroy() {
        this.items.Dispose();
        this.results.Dispose();
        this.data.Dispose();
    }

    public unsafe void Update() {

        var dt = Time.deltaTime;
        var marker = new Unity.Profiling.ProfilerMarker("Clear");
        marker.Begin();
        this.data.Clear();
        marker.End();
        
        marker = new Unity.Profiling.ProfilerMarker("Add");
        marker.Begin();
        for (int i = 0; i < this.count; ++i) {
            ref var item = ref Unity.Collections.LowLevel.Unsafe.UnsafeUtility.ArrayElementAsRef<Item>(this.items.GetUnsafePtr(), i);
            item.position += (float2)UnityEngine.Random.insideUnitCircle * dt;
            this.data.Add(new ME.BECS.Ent((ulong)i), new NativeTrees.AABB2D(item.position, item.position));
        }
        marker.End();

        var handle = new RebuildJob() {
            data = this.data,
        }.Schedule();

        handle = new Job() {
            data = this.data,
            items = this.items,
            results = this.results,
            range = this.range,
        }.Schedule(this.items.Length, 64, handle);
        handle.Complete();
        
    }

    [Unity.Burst.BurstCompileAttribute]
    public struct RebuildJob : IJob {
        
        public NativeTrees.SpatialHashing data;
        
        public void Execute() {
            this.data.Rebuild();
        }

    }

    [Unity.Burst.BurstCompileAttribute]
    public struct Job : IJobParallelFor {

        [ReadOnly]
        public NativeArray<Item> items;
        public NativeArray<ME.BECS.SpatialNearestAABBVisitor<ME.BECS.Ent, ME.BECS.AlwaysTrueSpatialSubFilter>> results;
        public float range;
        [ReadOnly]
        public NativeTrees.SpatialHashing data;

        public void Execute(int index) {
            var item = this.items[index];
            var d = new NativeTrees.AABB2DSpatialDistanceSquaredProvider<ME.BECS.Ent>();
            var visitor = new ME.BECS.SpatialNearestAABBVisitor<ME.BECS.Ent, ME.BECS.AlwaysTrueSpatialSubFilter>() {
                subFilter = default,
                sector = default,
                ignoreSelf = true,
                ignore = item.ent,
            };
            this.data.Nearest(item.position, 0f, this.range * this.range, ref visitor, ref d);
            this.results[index] = visitor;
        }

    }

    public void OnDrawGizmos() {

        for (var i = 0; i < this.items.Length; ++i) {
            var item = this.items[i];
            Gizmos.color = new Color(1f, 1f, 1f, 1f);
            var pos = item.position;
            Gizmos.DrawCube(new Vector3(pos.x, 0f, pos.y), new float3(1f, 1f, 1f) * 0.1f);

            var visitor = this.results[i];
            if (visitor.found == true) {
                var nearest = this.items[(int)visitor.nearest.pack];
                var p = new float3(item.position.x, 0f, item.position.y);
                var p2 = new float3(nearest.position.x, 0f, nearest.position.y);
                Gizmos.DrawLine(p + (new float3(0f, 1f, 0f) * item.data) / this.count * 0.1f - new float3(0f, 1f, 0f) * 0.05f,
                                p2 + (new float3(0f, 1f, 0f) * item.data) / this.count * 0.1f - new float3(0f, 1f, 0f) * 0.05f);
            }

            this.data.DrawGizmos();
        }

    }

}
