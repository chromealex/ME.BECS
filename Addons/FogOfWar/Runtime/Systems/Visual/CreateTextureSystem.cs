
namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Transforms;
    using Views;
    using Unity.Mathematics;
    using static Cuts;

    [RequiredDependencies(typeof(CreateSystem))]
    public struct CreateTextureSystem : IAwake, IDestroy {

        public View renderView;
        
        private ClassPtr<UnityEngine.Texture2D> texture;

        public bool IsCreated => this.texture.IsValid;

        [WithoutBurst]
        public void OnAwake(ref SystemContext context) {
            
            var system = context.world.GetSystem<CreateSystem>();
            {
                var fowSize = math.max(32u, (uint2)(system.mapSize * system.resolution));
                var tex = new UnityEngine.Texture2D((int)fowSize.x, (int)fowSize.y, UnityEngine.TextureFormat.RGBA32, false);
                tex.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                FogOfWarUtils.CleanUpTexture(tex.GetPixelData<byte>(0));
                tex.Apply();
                this.texture = new ClassPtr<UnityEngine.Texture2D>(tex);
            }

            var render = Ent.New();
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