namespace ME.BECS.FogOfWar {
    
    using Views;
    using UnityEngine;
    using Unity.Mathematics;

    public class FogOfWarView : EntityView {

        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        
        protected override void OnInitialize(in EntRO ent) {

            var fowSystem = ent.World.GetSystem<CreateSystem>();
            var system = ent.World.GetSystem<CreateTextureSystem>();
            var texture = system.GetTexture();
            this.meshFilter.sharedMesh = CreateQuad(fowSystem.mapSize.x, fowSystem.mapSize.y);
            this.meshRenderer.sharedMaterial.mainTexture = texture;

        }

        private static Mesh CreateQuad(float width, float height) {

            Mesh mesh = new Mesh();

            var vertices = new Vector3[] {
                new Vector3(0, 0, 0),
                new Vector3(width, 0, 0),
                new Vector3(0, 0, height),
                new Vector3(width, 0, height),
            };
            mesh.vertices = vertices;

            int[] tris = new int[6] {
                // lower left triangle
                0, 2, 1,
                // upper right triangle
                2, 3, 1,
            };
            mesh.triangles = tris;

            var normals = new Vector3[] {
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
            };
            mesh.normals = normals;

            var uv = new Vector2[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            mesh.uv = uv;

            return mesh;
            
        }

    }

}