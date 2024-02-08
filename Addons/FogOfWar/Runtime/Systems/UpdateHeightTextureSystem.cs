namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using ME.BECS.Players;
    using Unity.Collections;

    //[BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(CreateTextureSystem), typeof(CreateSystem))]
    public struct UpdateHeightTextureSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct UpdateJob : IJobParallelFor {

            public FogOfWarStaticComponent props;
            public int textureWidth;
            public int textureHeight;
            [NativeDisableParallelForRestriction]
            public Unity.Collections.NativeArray<UnityEngine.Color32> currentBuffer;
            
            public void Execute(int index) {

                var w = this.textureWidth / 4;
                var h = this.textureHeight;
                {
                    var x = index % w;
                    var y = index / w;
                    (var fowX, var fowY) = FogOfWarUtils.GetPixelPosition(in this.props, x, y, w, h);
                    var height = FogOfWarUtils.GetHeight(in this.props, fowX, fowY);
                    var maxHeight = this.props.maxHeight;

                    var p = (byte)(255 * (height / (float)maxHeight));
                    this.currentBuffer[index] = new UnityEngine.Color32(p, p, p, p);
                    
                }
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var createTexture = context.world.GetSystem<CreateTextureSystem>();
            if (createTexture.IsCreated == false) return;
            
            var props = context.world.GetSystem<CreateSystem>().heights.Read<FogOfWarStaticComponent>();

            var buffer = createTexture.GetHeightBuffer();
            var handle = new UpdateJob() {
                props = props,
                textureWidth = createTexture.textureWidth,
                textureHeight = createTexture.textureHeight,
                currentBuffer = buffer,
            }.Schedule(buffer.Length, JobUtils.GetScheduleBatchCount(buffer.Length));
            createTexture.GetHeightTexture().Apply(false);
            context.SetDependency(handle);
            
        }

    }

}