#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif
using Unity.Collections.LowLevel.Unsafe;

namespace ME.BECS.FogOfWar {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Players;
    using Unity.Collections;

    [RequiredDependencies(typeof(CreateTextureSystem))]
    public unsafe struct UpdateTextureSystemGPU : IAwake, IUpdate, IDestroy {

        private const int THREAD_GROUPS = 4;

        private static readonly int texSp = UnityEngine.Shader.PropertyToID("_Tex");
        private static readonly int widthSp = UnityEngine.Shader.PropertyToID("_Width");
        private static readonly int outSpeedSp = UnityEngine.Shader.PropertyToID("_FadeOutSpeed");
        private static readonly int inSpeedSp = UnityEngine.Shader.PropertyToID("_FadeInSpeed");
        private static readonly int bytesPerNodeSp = UnityEngine.Shader.PropertyToID("_BytesPerNode");
        private static readonly int nodesSp = UnityEngine.Shader.PropertyToID("_Nodes");
        private static readonly int exploredSp = UnityEngine.Shader.PropertyToID("_Explored");
        private static readonly int deltaTimeSp = UnityEngine.Shader.PropertyToID("_DeltaTime");
        private static readonly int fadeSp = UnityEngine.Shader.PropertyToID("_UseFade");

        public sfloat fadeInSpeed;
        public sfloat fadeOutSpeed;
        public ObjectReference<UnityEngine.ComputeShader> shader;
        private ClassPtr<UnityEngine.ComputeBuffer> nodesBuffer;
        private ClassPtr<UnityEngine.ComputeBuffer> exploredBuffer;
        private ClassPtr<UnityEngine.RenderTexture> renderTexture;

        private Ent lastActivePlayer;
        private ulong lastTick;
        private int intsCount;


        public void OnAwake(ref SystemContext context) {

            context.dependsOn.Complete();

            var fowSize = context.world.parent.GetSystem<CreateSystem>().GetHeights().Read<FogOfWarStaticComponent>().size;

            this.renderTexture = new ClassPtr<UnityEngine.RenderTexture>(new UnityEngine.RenderTexture((int)fowSize.x, (int)fowSize.y, 32) {
                enableRandomWrite = true,
            });

            uint bytesCount = fowSize.x * fowSize.y * FogOfWarUtils.BYTES_PER_NODE;
            this.intsCount = (int)math.ceil(bytesCount / 4f);
            this.nodesBuffer = new ClassPtr<UnityEngine.ComputeBuffer>(new UnityEngine.ComputeBuffer(this.intsCount, sizeof(int), UnityEngine.ComputeBufferType.Default));
            this.exploredBuffer = new ClassPtr<UnityEngine.ComputeBuffer>(new UnityEngine.ComputeBuffer(this.intsCount, sizeof(int), UnityEngine.ComputeBufferType.Default));
            this.InitShader(fowSize);

        }

        private void InitShader(uint2 fowSize) {

            this.shader.Value.SetTexture(0, texSp, this.renderTexture.Value);
            this.shader.Value.SetInt(widthSp, (int)fowSize.x);
            this.shader.Value.SetFloat(outSpeedSp, (float)this.fadeOutSpeed);
            this.shader.Value.SetFloat(inSpeedSp, (float)this.fadeInSpeed);
            this.shader.Value.SetInt(bytesPerNodeSp, FogOfWarUtils.BYTES_PER_NODE);
            this.shader.Value.SetTexture(1, texSp, this.renderTexture.Value);
            this.shader.Value.SetBuffer(1, nodesSp, this.nodesBuffer.Value);
            this.shader.Value.SetBuffer(1, exploredSp, this.exploredBuffer.Value);

        }

        private void UpdateShader(tfloat dt, bool useFade) {

            this.shader.Value.SetFloat(deltaTimeSp, (float)dt);
            this.shader.Value.SetFloat(fadeSp, useFade == true ? 1f : 0f);

        }

        public void OnUpdate(ref SystemContext context) {

            context.dependsOn.Complete();
            
            {
                //there is no need to init shader every frame, this is here cos of debug shader purposes
                // var mapSize = context.world.parent.GetSystem<CreateSystem>().mapSize;
                // var resolution = context.world.parent.GetSystem<CreateSystem>().resolution;
                // var fowSize = math.max(32u, (uint2)(mapSize * resolution));
                // this.InitShader(fowSize);
            }

            var logicWorld = context.world.parent;
            E.IS_CREATED(logicWorld);

            var playersSystem = logicWorld.GetSystem<PlayersSystem>();
            var activePlayer = playersSystem.GetActivePlayer();
            var useFade = true;
            if (this.lastActivePlayer != activePlayer.ent) {
                // clean up textures because we need to rebuild them for current player
                this.shader.Value.Dispatch(0, this.renderTexture.Value.width / THREAD_GROUPS, this.renderTexture.Value.height / THREAD_GROUPS, 1);
                useFade = false;
            }
            this.lastActivePlayer = activePlayer.ent;
            var fow = activePlayer.readTeam.Read<FogOfWarComponent>();
            this.UpdateShader(context.deltaTime, useFade);

            {
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                var nodesSafety = CollectionHelper.CreateSafetyHandle(Constants.ALLOCATOR_TEMP);
                #endif
                var nodesInt = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(fow.nodes.GetUnsafePtr().ptr, this.intsCount, Allocator.None);
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nodesInt, nodesSafety);
                #endif
                this.nodesBuffer.Value.SetData(nodesInt);
            }

            {
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                var expSafety = CollectionHelper.CreateSafetyHandle(Constants.ALLOCATOR_TEMP);
                #endif
                var expInt = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(fow.explored.GetUnsafePtr().ptr, this.intsCount, Allocator.None);
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref expInt, expSafety);
                #endif
                this.exploredBuffer.Value.SetData(expInt);
            }

            this.shader.Value.Dispatch(1, this.renderTexture.Value.width / THREAD_GROUPS, this.renderTexture.Value.height / THREAD_GROUPS, 1);
            UnityEngine.Graphics.CopyTexture(this.renderTexture.Value, context.world.GetSystem<CreateTextureSystem>().GetTexture());
            context.SetDependency(context.dependsOn);

        }

        public void OnDestroy(ref SystemContext context) {

            this.nodesBuffer.Value.Release();
            this.exploredBuffer.Value.Release();
            this.renderTexture.Value.Release();
            this.nodesBuffer.Dispose();
            this.exploredBuffer.Dispose();
            this.renderTexture.Dispose();

        }

    }

}