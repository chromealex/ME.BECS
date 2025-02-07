#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    public class GraphMaskSceneEntity : SceneEntity {

        public byte cost;
        public ObstacleChannel obstacleChannel;
        public tfloat height;
        public bool ignoreGraphRadius;

        protected override void OnCreate(in Ent ent) {

            var bounds = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.bounds;
            var size = new uint2((uint)(bounds.size.x + 0.5f), (uint)(bounds.size.z + 0.5f));

            GraphUtils.CreateGraphMask(in ent, (float3)this.transform.position, (quaternion)this.transform.rotation, size, this.cost, this.height, this.obstacleChannel, this.ignoreGraphRadius);

        }

        public void OnDrawGizmos() {

            var renderer = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
            var bounds = renderer.bounds;

            var oldMatrix = UnityEngine.Gizmos.matrix;
            UnityEngine.Gizmos.matrix = UnityEngine.Matrix4x4.TRS(this.transform.position, this.transform.rotation, UnityEngine.Vector3.one);
            UnityEngine.Gizmos.color = UnityEngine.Color.yellow;
            UnityEngine.Gizmos.DrawWireCube(bounds.center, bounds.size);
            UnityEngine.Gizmos.matrix = oldMatrix;

        }

    }

}