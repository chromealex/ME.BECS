#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    /// <summary>
    /// Defines graph mask grid scene entity state and operations.
    /// </summary>
    public class GraphMaskGridSceneEntity : SceneEntity {

        /// <summary>
        /// Grid size used by <c>GraphMaskGridSceneEntity</c>.
        /// </summary>
        public uint gridSize = 1u;
        /// <summary>
        /// Level limit used by <c>GraphMaskGridSceneEntity</c>.
        /// </summary>
        public tfloat levelLimit = 0f;
        /// <summary>
        /// Height samples used by the geometry or graph.
        /// </summary>
        public tfloat[] heights;
        /// <summary>
        /// Cost assigned to this entry by the associated calculation.
        /// </summary>
        public byte cost;
        /// <summary>
        /// Whether ignore graph radius behavior or state is selected.
        /// </summary>
        public bool ignoreGraphRadius;
        /// <summary>
        /// Obstacle channel used by <c>GraphMaskGridSceneEntity</c>.
        /// </summary>
        public ObstacleChannel obstacleChannel;
        /// <summary>
        /// Graph mask used to select the applicable bits or entries.
        /// </summary>
        public int graphMask = -1;
        
        /// <summary>
        /// Handles the create callback.
        /// </summary>
        protected override void OnCreate(in Ent ent) {

            var bounds = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.bounds;
            var size = new uint2((uint)(bounds.size.x + 0.5f), (uint)(bounds.size.z + 0.5f));

            var heights = new MemArrayAuto<tfloat>(in ent, (uint)this.heights.Length);
            NativeArrayUtils.Copy(this.heights, 0, heights, 0, (int)heights.Length);
            GraphUtils.CreateGraphMask(in ent, (float3)this.transform.position, (quaternion)this.transform.rotation, size, this.cost, this.obstacleChannel, this.ignoreGraphRadius, heights, this.gridSize, this.graphMask);

        }

        /// <summary>
        /// Refreshes or validates state after values change in the Unity Inspector.
        /// </summary>
        public void OnValidate() {
            this.CreateGrid((float3)this.transform.position, (quaternion)this.transform.rotation);
        }

        /// <summary>
        /// Creates grid.
        /// </summary>
        public void CreateGrid(float3 position, quaternion rotation) {

            var mesh = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
            var bounds = mesh.bounds;
            var xSize = math.max(this.gridSize, 1u);
            var vertices = mesh.vertices;

            this.heights = new tfloat[xSize * xSize];
            
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

        /// <summary>
        /// Draws grid.
        /// </summary>
        public void DrawGrid(float3 position, quaternion rotation, float3 scale) {

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
                    UnityEngine.Gizmos.matrix = UnityEngine.Matrix4x4.TRS((UnityEngine.Vector3)position, (UnityEngine.Quaternion)rotation, (UnityEngine.Vector3)scale);
                    var p = localOffset + new float3(0f, height, 0f);
                    var s = new float3(size.x, 0f, size.z);
                    UnityEngine.Gizmos.DrawCube((UnityEngine.Vector3)p, (UnityEngine.Vector3)s);
                    UnityEngine.Gizmos.DrawWireCube((UnityEngine.Vector3)p, (UnityEngine.Vector3)s);
                    c.a = 0.05f;
                    UnityEngine.Gizmos.color = c;
                    UnityEngine.Gizmos.matrix = UnityEngine.Matrix4x4.TRS((UnityEngine.Vector3)pos, (UnityEngine.Quaternion)rotation, (UnityEngine.Vector3)scale);
                    UnityEngine.Gizmos.DrawCube((UnityEngine.Vector3)float3.zero, (UnityEngine.Vector3)size);
                    UnityEngine.Gizmos.DrawWireCube((UnityEngine.Vector3)float3.zero, (UnityEngine.Vector3)size);
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
                        point = (float3)vert;
                    }

                }
                
            }

            return point;

        }

        /// <summary>
        /// Draws diagnostic geometry for the associated state.
        /// </summary>
        public void OnDrawGizmos() {

            var renderer = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
            var bounds = renderer.bounds;

            this.DrawGrid((float3)this.transform.position, (quaternion)this.transform.rotation, (float3)this.transform.lossyScale);
            /*if (this.test != null) {
                var rotation = this.transform.rotation;
                var testPos = this.test.transform.position;
                var localPos = math.mul(math.inverse(rotation), testPos - this.transform.position);
                var size = bounds.size;
                localPos.x += size.x * 0.5f;
                localPos.z += size.z * 0.5f;
                UnityEngine.Debug.Log(GraphUtils.GetObstacleHeight(localPos, this.heights, new float2(size.x, size.z), this.gridSize));
            }*/

            var oldMatrix = UnityEngine.Gizmos.matrix;
            UnityEngine.Gizmos.matrix = UnityEngine.Matrix4x4.TRS(this.transform.position, this.transform.rotation, this.transform.lossyScale);
            UnityEngine.Gizmos.color = UnityEngine.Color.yellow;
            UnityEngine.Gizmos.DrawWireCube(bounds.center, bounds.size);
            UnityEngine.Gizmos.matrix = oldMatrix;

        }

    }

}