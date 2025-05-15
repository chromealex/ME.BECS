#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.FogOfWar {
    
    using Views;
    using UnityEngine;

    public class FogOfWarView : EntityView {

        private static readonly int fogTex = Shader.PropertyToID("_FogTex");
        private static readonly int resolution = Shader.PropertyToID("_HeightResolution");
        
        private static readonly int inverseMvp = Shader.PropertyToID("_InverseMVP");
        private static readonly int pos = Shader.PropertyToID("_CamPos");
        private static readonly int @params = Shader.PropertyToID("_Params");
        private static readonly int padding = Shader.PropertyToID("_Padding");

        public Material material;
        public MeshRenderer meshRenderer;
        public Transform transformScale;
        [Tooltip("X - top, Y - right, Z - bottom, W - left")]
        public Vector4 paddingSize;
        private float2 worldSize;
        private Vector3 offset;

        protected override void OnInitialize(in EntRO ent) {
            
            var fowSystem = ent.World.parent.GetSystem<CreateSystem>();
            var system = ent.World.GetSystem<CreateTextureSystem>();
            var heightResolution = fowSystem.resolution;
            if (this.meshRenderer != null) {
                this.material = new Material(this.material);
                this.meshRenderer.sharedMaterial = this.material;
            }
            this.material.SetTexture(fogTex, system.GetTexture());
            this.material.SetFloat(resolution, (float)heightResolution);

            this.worldSize = fowSystem.mapSize;
            this.SetScale();
        }

        protected override void OnUpdate(in EntRO ent, float dt) {

            // this.SetScale();
            
            var createTextureSystem = ent.World.GetSystem<CreateTextureSystem>();
            var logicWorld = ent.World.parent;
            var fowSystem = logicWorld.GetSystem<CreateSystem>();
            var system = ent.World.GetSystem<CreateTextureSystem>();
            this.material.SetTexture(fogTex, system.GetTexture());

            var camera = createTextureSystem.GetCamera();
            var proj = (Matrix4x4)camera.projectionMatrix;
            var cam = (Matrix4x4)camera.worldToCameraMatrix;
            var inverseMVP = (proj * cam).inverse;
            //var inverseMVP = math.inverse(math.mul(camera.projectionMatrix, camera.worldToCameraMatrix));

            var invScaleX = 1f / this.worldSize.x;
            var invScaleY = 1f / this.worldSize.y;
            var x = this.offset.x - this.worldSize.x * 0.5f;
            var y = this.offset.z - this.worldSize.y * 0.5f;
            var camPos3d = camera.ent.GetAspect<ME.BECS.Transforms.TransformAspect>().position;
            var camPos = new float4(camPos3d.xyz, 0f);
            if (QualitySettings.antiAliasing > 0) {
                RuntimePlatform pl = Application.platform;
                if (pl == RuntimePlatform.WindowsEditor ||
                    pl == RuntimePlatform.WindowsPlayer ||
                    pl == RuntimePlatform.WebGLPlayer) {
                    camPos.w = 1f;
                }
            }
            
            var p = new float4(-x * invScaleX, -y * invScaleY, invScaleX, 0f);
            var heightResolution = fowSystem.resolution;
            this.material.SetFloat(resolution, (float)heightResolution);
            this.material.SetMatrix(inverseMvp, inverseMVP);
            this.material.SetVector(pos, (Vector4)camPos);
            this.material.SetVector(@params, (Vector4)p);
            
        }

        private void SetScale() {
            if (this.transformScale != null) {
                var horizontalPadding = this.paddingSize.y + this.paddingSize.w;
                var verticalPadding = this.paddingSize.x + this.paddingSize.z;
                this.transformScale.localScale = new Vector3((float)this.worldSize.x + horizontalPadding, 
                                                             1f, 
                                                             (float)this.worldSize.y + verticalPadding);
                
                this.transformScale.localPosition = new Vector3((float)this.worldSize.x * 0.5f - this.paddingSize.w * 0.5f + this.paddingSize.y * 0.5f, 
                                                                0f, 
                                                                (float)this.worldSize.y * 0.5f - this.paddingSize.z * 0.5f + this.paddingSize.x * 0.5f);
                
                this.material.SetVector(padding, new Vector4(
                                            this.paddingSize.x / this.transformScale.localScale.z,
                                            this.paddingSize.y / this.transformScale.localScale.x,
                                            this.paddingSize.z / this.transformScale.localScale.z,
                                            this.paddingSize.w / this.transformScale.localScale.x
                                        ));
                
            }

        }

    }
    
}