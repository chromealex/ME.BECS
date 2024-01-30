namespace ME.BECS.Pathfinding {
    
    using Unity.Mathematics;

    public class GraphMaskSceneEntity : SceneEntity {

        public byte cost;
        public float height;

        protected override void OnCreate(in Ent ent) {

            var bounds = this.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.bounds;
            var size = bounds.size;

            GraphUtils.CreateGraphMask(in ent, this.transform.position, this.transform.rotation, new float2(size.x, size.z), this.cost, this.height);

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