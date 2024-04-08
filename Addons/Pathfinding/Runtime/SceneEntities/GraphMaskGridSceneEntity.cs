namespace ME.BECS.Pathfinding {
    
    using Unity.Mathematics;

    public class GraphMaskGridSceneEntity : SceneEntity {

        public uint gridSize = 1u;
        public float levelLimit = 0f;
        public float[] heights;
        public byte cost;
        public bool ignoreGraphRadius;
        public ObstacleChannel obstacleChannel;
        
        public UnityEngine.Transform test;

        protected override void OnCreate(in Ent ent) {

            var bounds = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.bounds;
            var size = new uint2((uint)(bounds.size.x + 0.5f), (uint)(bounds.size.z + 0.5f));

            var heights = new MemArrayAuto<float>(in ent, (uint)this.heights.Length);
            NativeArrayUtils.Copy(this.heights, 0, heights, 0, (int)heights.Length);
            GraphUtils.CreateGraphMask(in ent, this.transform.position, this.transform.rotation, size, this.cost, this.obstacleChannel, this.ignoreGraphRadius, heights, this.gridSize);

        }

        public void OnValidate() {
            this.CreateGrid(this.transform.position, this.transform.rotation);
        }

        public void CreateGrid(float3 position, quaternion rotation) {

            var mesh = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
            var bounds = mesh.bounds;
            var xSize = math.max(this.gridSize, 1u);
            var vertices = mesh.vertices;

            this.heights = new float[xSize * xSize];
            
            var offset = (float3)bounds.center - new float3(bounds.size.x * 0.5f, bounds.size.y * 0.5f, bounds.size.z * 0.5f);
            for (uint x = 0u; x < xSize; ++x) {
                for (uint z = 0u; z < xSize; ++z) {
                    var size = new float3(bounds.size.x / xSize, bounds.size.y, bounds.size.z / xSize);
                    var localPos = new float3(x * size.x, 0f, z * size.z);
                    var highestPosition = this.GetHighestPosition(localPos + offset + new float3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f), size.xz, vertices);
                    var isValid = (highestPosition.y > this.levelLimit);
                    var index = (int)(z * xSize + x);
                    if (isValid == true) {
                        this.heights[index] = highestPosition.y;
                    } else {
                        this.heights[index] = -1f;
                    }
                }
            }

        }

        public void DrawGrid(float3 position, quaternion rotation) {

            var mesh = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
            var bounds = mesh.bounds;
            var xSize = math.max(this.gridSize, 1u);

            var oldMatrix = UnityEngine.Gizmos.matrix;
            var offset = (float3)bounds.center - new float3(bounds.size.x * 0.5f, bounds.size.y * 0.5f, bounds.size.z * 0.5f);
            for (uint x = 0u; x < xSize; ++x) {
                for (uint z = 0u; z < xSize; ++z) {
                    var size = new float3(bounds.size.x / xSize, bounds.size.y, bounds.size.z / xSize);
                    var localPos = new float3(x * size.x, 0f, z * size.z);
                    var localOffset = localPos + offset + new float3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
                    var pos = math.mul(rotation, localOffset) + position;
                    var index = (int)(z * xSize + x);
                    var height = this.heights[index];
                    var isValid = height >= this.levelLimit;
                    UnityEngine.Gizmos.color = UnityEngine.Color.green;
                    if (isValid == false) {
                        continue;
                    }
                    var c = UnityEngine.Gizmos.color;
                    c.a = 0.2f;
                    UnityEngine.Gizmos.color = c;
                    UnityEngine.Gizmos.matrix = UnityEngine.Matrix4x4.TRS(position, rotation, new float3(1f));
                    var p = localOffset + new float3(0f, height, 0f);
                    var s = new float3(size.x, 0f, size.z);
                    UnityEngine.Gizmos.DrawCube(p, s);
                    UnityEngine.Gizmos.DrawWireCube(p, s);
                    c.a = 0.05f;
                    UnityEngine.Gizmos.color = c;
                    UnityEngine.Gizmos.matrix = UnityEngine.Matrix4x4.TRS(pos, rotation, new float3(1f));
                    UnityEngine.Gizmos.DrawCube(float3.zero, size);
                    UnityEngine.Gizmos.DrawWireCube(float3.zero, size);
                }
            }
            UnityEngine.Gizmos.matrix = oldMatrix;

        }

        private float3 GetHighestPosition(float3 position, float2 size, UnityEngine.Vector3[] vertices) {

            var minX = position.x - size.x * 0.5f;
            var maxX = position.x + size.x * 0.5f;
            var minY = position.z - size.y * 0.5f;
            var maxY = position.z + size.y * 0.5f;
            var height = float.MinValue;
            var point = new float3(position.x, this.levelLimit, position.z);
            foreach (var vert in vertices) {

                if (vert.x >= minX && vert.x <= maxX &&
                    vert.z >= minY && vert.z <= maxY) {

                    var h = vert.y;
                    if (h > height) {
                        height = h;
                        point = vert;
                    }

                }
                
            }

            return point;

        }

        public void OnDrawGizmos() {

            var renderer = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
            var bounds = renderer.bounds;

            this.DrawGrid(this.transform.position, this.transform.rotation);
            if (this.test != null) {
                var rotation = this.transform.rotation;
                var testPos = this.test.transform.position;
                var localPos = math.mul(math.inverse(rotation), testPos - this.transform.position);
                var size = bounds.size;
                localPos.x += size.x * 0.5f;
                localPos.z += size.z * 0.5f;
                UnityEngine.Debug.Log(GraphUtils.GetObstacleHeight(localPos, this.heights, new float2(size.x, size.z), this.gridSize));
            }

            var oldMatrix = UnityEngine.Gizmos.matrix;
            UnityEngine.Gizmos.matrix = UnityEngine.Matrix4x4.TRS(this.transform.position, this.transform.rotation, UnityEngine.Vector3.one);
            UnityEngine.Gizmos.color = UnityEngine.Color.yellow;
            UnityEngine.Gizmos.DrawWireCube(bounds.center, bounds.size);
            UnityEngine.Gizmos.matrix = oldMatrix;

        }

    }

}