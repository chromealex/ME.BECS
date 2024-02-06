namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using Transforms;
    using Views;
    using Unity.Mathematics;
    using Unity.Jobs;
    using ME.BECS.Players;

    [RequiredDependencies(typeof(CreateSystem))]
    public struct CreateTextureSystem : IAwake, IDestroy {

        public View renderView;
        
        private ClassPtr<UnityEngine.Texture2D> texture;
        internal int textureWidth;
        internal int textureHeight;

        public bool IsCreated => this.textureWidth > 0;

        [WithoutBurst]
        public void OnAwake(ref SystemContext context) {
            
            var system = context.world.GetSystem<CreateSystem>();
            var props = system.heights.Read<FogOfWarStaticComponent>();
            var size = FogOfWarUtils.WorldToFogMapPosition(in props, system.mapSize.xyy);
            this.textureWidth = (int)math.ceilpow2(size.x);
            this.textureHeight = (int)math.ceilpow2(size.y);
            var tex = new UnityEngine.Texture2D(this.textureWidth, this.textureHeight, UnityEngine.TextureFormat.R8, false);
            tex.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            this.texture = new ClassPtr<UnityEngine.Texture2D>(tex);
            
            var render = Ent.New();
            var tr = render.GetOrCreateAspect<TransformAspect>();
            var pos = tr.position;
            pos.x = 0f;
            pos.z = 0f;
            tr.position = pos;
            tr.rotation = quaternion.identity;
            render.InstantiateView(this.renderView);

        }

        public Unity.Collections.NativeArray<UnityEngine.Color32> GetBuffer() => this.texture.Value.GetPixelData<UnityEngine.Color32>(0);

        public ClassPtr<UnityEngine.Texture2D> GetTexturePtr() => this.texture;

        public UnityEngine.Texture2D GetTexture() => this.texture.Value;

        [WithoutBurst]
        public void OnDestroy(ref SystemContext context) {
            UnityEngine.Object.DestroyImmediate(this.texture.Value);
            this.texture.Dispose();
        }

    }

}