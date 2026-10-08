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
    using Unity.Collections;
    using static Cuts;

    /// <summary>
    /// Coordinates create texture during the ECS system lifecycle.
    /// </summary>
    public partial struct CreateTextureSystem : IAwake, IDestroy {

        /// <summary>
        /// Render view used by <c>CreateTextureSystem</c>.
        /// </summary>
        public View renderView;
        
        private ClassPtr<UnityEngine.Texture2D> texture;
        private Ent camera;
        private Unity.Collections.NativeArray<byte> textureBuffer;

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.texture.IsValid;

        /// <summary>
        /// Sets camera.
        /// </summary>
        public void SetCamera(in ME.BECS.Views.CameraAspect camera) {
            this.camera = camera.ent;
        }
        
        /// <summary>
        /// Returns camera.
        /// </summary>
        public ME.BECS.Views.CameraAspect GetCamera() => this.camera.GetAspect<ME.BECS.Views.CameraAspect>();
        
        /// <summary>
        /// Initializes create texture system state from the supplied context.
        /// </summary>
        [WithoutBurst]
        public void OnAwake(ref SystemContext context) {

            context.dependsOn.Complete();
            
            var logicWorld = context.world.parent;
            E.IS_CREATED(logicWorld);
            
            var system = logicWorld.GetSystem<CreateSystem>();
            {
                var fowSize = math.max(8u, (uint2)(system.mapSize * system.resolution));
                var tex = new UnityEngine.Texture2D((int)fowSize.x, (int)fowSize.y, UnityEngine.TextureFormat.RGBA32, false);
                tex.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                FogOfWarUtils.CleanUpTexture(tex.GetPixelData<byte>(0));
                tex.Apply();
                this.texture = new ClassPtr<UnityEngine.Texture2D>(tex);
                var buffer = this.texture.Value.GetPixelData<byte>(0);
                this.textureBuffer = CollectionHelper.CreateNativeArray<byte>(buffer.Length, Constants.ALLOCATOR_PERSISTENT);
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

        /// <summary>
        /// Returns buffer.
        /// </summary>
        public Unity.Collections.NativeArray<byte> GetBuffer() => this.textureBuffer;

        /// <summary>
        /// Returns texture.
        /// </summary>
        public UnityEngine.Texture2D GetTexture() => this.texture.Value;

        /// <summary>
        /// Releases create texture system state at the end of its owning lifecycle.
        /// </summary>
        [WithoutBurst]
        public void OnDestroy(ref SystemContext context) {
            CollectionHelper.DisposeNativeArray(this.textureBuffer, Constants.ALLOCATOR_PERSISTENT);
            UnityEngine.Object.DestroyImmediate(this.texture.Value);
            this.texture.Dispose();
        }

    }

}