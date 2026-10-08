namespace ME.BECS.FogOfWar {

    /// <summary>
    /// Defines built in render state and operations.
    /// </summary>
    public class BuiltInRender : UnityEngine.MonoBehaviour {

        /// <summary>
        /// Material used by the associated renderer.
        /// </summary>
        public UnityEngine.Material material;

        private void OnRenderImage(UnityEngine.RenderTexture source, UnityEngine.RenderTexture destination) {
            
            UnityEngine.Graphics.Blit(source, destination, this.material);
            
        }

    }

}