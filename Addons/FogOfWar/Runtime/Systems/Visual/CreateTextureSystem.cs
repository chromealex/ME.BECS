#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Transforms;
    using Views;
    using static Cuts;

    public struct CreateTextureSystem : IAwake, IDestroy {

        public View renderView;
        
        private ClassPtr<UnityEngine.Texture2D> texture;
        private Ent camera;

        public bool IsCreated => this.texture.IsValid;

        public void SetCamera(in ME.BECS.Views.CameraAspect camera) {
            this.camera = camera.ent;
        }
        
        public ME.BECS.Views.CameraAspect GetCamera() => this.camera.GetAspect<ME.BECS.Views.CameraAspect>();
        
        [WithoutBurst]
        public void OnAwake(ref SystemContext context) {

            context.dependsOn.Complete();
            
            var logicWorld = context.world.parent;
            E.IS_CREATED(logicWorld);
            
            var system = logicWorld.GetSystem<CreateSystem>();
            {
                var fowSize = math.max(32u, (uint2)(system.mapSize * system.resolution));
                var tex = new UnityEngine.Texture2D((int)fowSize.x, (int)fowSize.y, UnityEngine.TextureFormat.RGBA32, false);
                tex.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                FogOfWarUtils.CleanUpTexture(tex.GetPixelData<byte>(0));
                tex.Apply();
                this.texture = new ClassPtr<UnityEngine.Texture2D>(tex);
            }

            var render = Ent.New(in context, editorName: "FOW Renderer");
            var tr = render.GetOrCreateAspect<TransformAspect>();
            var pos = tr.position;
            pos.x = 0f;
            pos.z = 0f;
            tr.position = pos;
            tr.rotation = quaternion.identity;
            render.InstantiateView(this.renderView);

        }

        public Unity.Collections.NativeArray<byte> GetBuffer() => this.texture.Value.GetPixelData<byte>(0);

        public UnityEngine.Texture2D GetTexture() => this.texture.Value;

        [WithoutBurst]
        public void OnDestroy(ref SystemContext context) {
            UnityEngine.Object.DestroyImmediate(this.texture.Value);
            this.texture.Dispose();
        }

    }

}