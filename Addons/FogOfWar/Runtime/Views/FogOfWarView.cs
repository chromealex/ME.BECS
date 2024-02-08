namespace ME.BECS.FogOfWar {
    
    using Views;
    using UnityEngine;
    using Unity.Mathematics;

    public class FogOfWarView : EntityView {

        public Material material;
        
        protected override void OnInitialize(in EntRO ent) {

            var fowSystem = ent.World.GetSystem<CreateSystem>();
            var system = ent.World.GetSystem<CreateTextureSystem>();
            var heightResolution = fowSystem.resolution;
            this.material.SetTexture("_FogTex", system.GetTexture());
            this.material.SetFloat("_HeightResolution", heightResolution);

            var comp = Camera.main.gameObject.AddComponent<CameraFogOfWarTexture>();
            comp.material = this.material;
            comp.worldSize = fowSystem.mapSize.x;
            comp.offset = new Vector3(0f, 0f, 0f);

        }

    }

    public class CameraFogOfWarTexture : MonoBehaviour {

        public Material material;
        public float2 worldSize;
        public Vector3 offset;
        private Camera objCamera;

        public void Awake() {
            this.objCamera = this.GetComponent<Camera>();
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dest) {
            
            var inverseMVP = (this.objCamera.projectionMatrix * this.objCamera.worldToCameraMatrix).inverse;

            float invScaleX = 1f / this.worldSize.x;
            float invScaleY = 1f / this.worldSize.y;
            float x = this.offset.x - this.worldSize.x * 0.5f;
            float y = this.offset.z - this.worldSize.y * 0.5f;
            Vector4 camPos = this.objCamera.transform.position;
            if (QualitySettings.antiAliasing > 0) {
                RuntimePlatform pl = Application.platform;
                if (pl == RuntimePlatform.WindowsEditor ||
                    pl == RuntimePlatform.WindowsPlayer ||
                    pl == RuntimePlatform.WebGLPlayer) {
                    camPos.w = 1f;
                }
            }
            
            Vector4 p = new Vector4(-x * invScaleX, -y * invScaleY, invScaleX, 0f);
            this.material.SetMatrix("_InverseMVP", inverseMVP);
            this.material.SetVector("_CamPos", camPos);
            this.material.SetVector("_Params", p);
            
            Graphics.Blit(src, dest, this.material);
        }

    }
    
}