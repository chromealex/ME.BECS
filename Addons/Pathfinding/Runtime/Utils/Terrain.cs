namespace ME.BECS.Pathfinding {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    public struct GraphHeights {

        [Unity.Collections.ReadOnlyAttribute]
        public Unity.Collections.NativeArray<float> heightMap;
        private int resolution;
        private Unity.Mathematics.float2 sampleSize;
        public UnityEngine.Bounds AABB { get; private set; }
        public readonly bool IsValid => this.heightMap.IsCreated;
        private readonly int QuadCount => this.resolution - 1;

        [INLINE(256)]
        public GraphHeights(float3 position, UnityEngine.TerrainData terrainData, Unity.Collections.Allocator alloc) {
            this.resolution = terrainData.heightmapResolution;
            this.sampleSize = new Unity.Mathematics.float2(terrainData.heightmapScale.x, terrainData.heightmapScale.z);
            this.AABB = GetTerrainAABB(position, terrainData);
            this.heightMap = GetHeightMap(terrainData, alloc);
        }

        /// <summary>
        /// Returns world height of terrain at x and z position values.
        /// </summary>
        [INLINE(256)]
        public readonly float SampleHeight(Unity.Mathematics.float3 worldPosition) {
            this.GetTriAtPosition(worldPosition, out var tri);
            return tri.SampleHeight(worldPosition);
        }

        /// <summary>
        /// Returns world height of terrain at x and z position values. Also outputs normalized normal vector of terrain at position.
        /// </summary>
        [INLINE(256)]
        public readonly float SampleHeight(Unity.Mathematics.float3 worldPosition, out Unity.Mathematics.float3 normal) {
            this.GetTriAtPosition(worldPosition, out var tri);
            normal = tri.Normal;
            return tri.SampleHeight(worldPosition);
        }

        [INLINE(256)]
        private readonly void GetTriAtPosition(Unity.Mathematics.float3 worldPosition, out Triangle tri) {
            if (!this.IsWithinBounds(worldPosition)) {
                throw new System.ArgumentException("Position given is outside of terrain x or z bounds.");
            }

            var localPos = new Unity.Mathematics.float2(
                worldPosition.x - this.AABB.min.x,
                worldPosition.z - this.AABB.min.z);
            var samplePos = localPos / this.sampleSize;
            var sampleFloor = (Unity.Mathematics.int2)Unity.Mathematics.math.floor(samplePos);
            var sampleDecimal = samplePos - sampleFloor;
            var upperLeftTri = sampleDecimal.y > sampleDecimal.x;
            var v1Offset = upperLeftTri ? new Unity.Mathematics.int2(0, 1) : new Unity.Mathematics.int2(1, 1);
            var v2Offset = upperLeftTri ? new Unity.Mathematics.int2(1, 1) : new Unity.Mathematics.int2(1, 0);
            var v0 = this.GetWorldVertex(sampleFloor);
            var v1 = this.GetWorldVertex(sampleFloor + v1Offset);
            var v2 = this.GetWorldVertex(sampleFloor + v2Offset);
            tri = new Triangle(v0, v1, v2);
        }

        [INLINE(256)]
        public void Dispose() {
            this.heightMap.Dispose();
        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle dependsOn) {
            return this.heightMap.Dispose(dependsOn);
        }

        [INLINE(256)]
        private readonly bool IsWithinBounds(Unity.Mathematics.float3 worldPos) {
            return
                worldPos.x >= this.AABB.min.x &&
                worldPos.z >= this.AABB.min.z &&
                worldPos.x <= this.AABB.max.x &&
                worldPos.z <= this.AABB.max.z;
        }

        [INLINE(256)]
        private readonly Unity.Mathematics.float3 GetWorldVertex(Unity.Mathematics.int2 heightMapCoords) {
            var i = heightMapCoords.x + heightMapCoords.y * this.resolution;
            var vertexPercentages = new Unity.Mathematics.float3((float)heightMapCoords.x / this.QuadCount, this.heightMap[i], (float)heightMapCoords.y / this.QuadCount);
            return (float3)this.AABB.min + this.AABB.size * vertexPercentages;
        }

        [INLINE(256)]
        private static UnityEngine.Bounds GetTerrainAABB(float3 position, UnityEngine.TerrainData terrainData) {
            var min = position;
            var max = min + (Unity.Mathematics.float3)terrainData.size;
            var extents = (max - min) / 2;
            return new UnityEngine.Bounds() { center = min + extents, extents = extents };
        }

        [INLINE(256)]
        private static Unity.Collections.NativeArray<float> GetHeightMap(UnityEngine.TerrainData terrainData, Unity.Collections.Allocator alloc) {
            var resolution = terrainData.heightmapResolution;
            var heightList = new Unity.Collections.NativeArray<float>(resolution * resolution, alloc);
            var map = terrainData.GetHeights(0, 0, resolution, resolution);
            for (var y = 0; y < resolution; y++) {
                for (var x = 0; x < resolution; x++) {
                    var i = y * resolution + x;
                    heightList[i] = map[y, x];
                }
            }

            return heightList;
        }

    }

    public readonly struct Triangle {

        public Unity.Mathematics.float3 V0 { get; }
        public Unity.Mathematics.float3 V1 { get; }
        public Unity.Mathematics.float3 V2 { get; }

        ///
        /// This is already normalized.
        ///

        public Unity.Mathematics.float3 Normal { get; }

        [INLINE(256)]
        public Triangle(Unity.Mathematics.float3 v0, Unity.Mathematics.float3 v1, Unity.Mathematics.float3 v2) {
            this.V0 = v0;
            this.V1 = v1;
            this.V2 = v2;
            this.Normal = Unity.Mathematics.math.normalize(Unity.Mathematics.math.cross(this.V1 - this.V0, this.V2 - this.V0));
        }

        [INLINE(256)]
        public float SampleHeight(Unity.Mathematics.float3 position) {
            // plane formula: a(x - x0) + b(y - y0) + c(z - z0) = 0
            // <a,b,c> is a normal vector for the plane
            // (x,y,z) and (x0,y0,z0) are any points on the plane
            return (-this.Normal.x * (position.x - this.V0.x) - this.Normal.z * (position.z - this.V0.z)) / this.Normal.y + this.V0.y;
        }

    }

}