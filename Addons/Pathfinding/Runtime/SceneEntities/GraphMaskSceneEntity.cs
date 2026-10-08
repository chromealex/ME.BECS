#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    /// <summary>
    /// Defines graph mask scene entity state and operations.
    /// </summary>
    public class GraphMaskSceneEntity : SceneEntity {

        /// <summary>
        /// Cost assigned to this entry by the associated calculation.
        /// </summary>
        public byte cost;
        /// <summary>
        /// Obstacle channel used by <c>GraphMaskSceneEntity</c>.
        /// </summary>
        public ObstacleChannel obstacleChannel;
        /// <summary>
        /// Vertical extent used by the associated geometry or query.
        /// </summary>
        public tfloat height;
        /// <summary>
        /// Whether ignore graph radius behavior or state is selected.
        /// </summary>
        public bool ignoreGraphRadius;

        /// <summary>
        /// Handles the create callback.
        /// </summary>
        protected override void OnCreate(in Ent ent) {

            var bounds = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.bounds;
            var size = new uint2((uint)(bounds.size.x + 0.5f), (uint)(bounds.size.z + 0.5f));

            GraphUtils.CreateGraphMask(in ent, (float3)this.transform.position, (quaternion)this.transform.rotation, size, this.cost, this.height, this.obstacleChannel, this.ignoreGraphRadius);

        }

        /// <summary>
        /// Draws diagnostic geometry for the associated state.
        /// </summary>
        public void OnDrawGizmos() {

            var renderer = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
            var bounds = renderer.bounds;

            var oldMatrix = UnityEngine.Gizmos.matrix;
            UnityEngine.Gizmos.matrix = UnityEngine.Matrix4x4.TRS(this.transform.position, this.transform.rotation, this.transform.lossyScale);
            UnityEngine.Gizmos.color = UnityEngine.Color.yellow;
            UnityEngine.Gizmos.DrawWireCube(bounds.center, bounds.size);
            UnityEngine.Gizmos.matrix = oldMatrix;

        }

    }

}