namespace ME.BECS.FogOfWar {

    public class BuiltInRender : UnityEngine.MonoBehaviour {

        public UnityEngine.Material material;

        private void OnRenderImage(UnityEngine.RenderTexture source, UnityEngine.RenderTexture destination) {
            
            UnityEngine.Graphics.Blit(source, destination, this.material);
            
        }

    }

}