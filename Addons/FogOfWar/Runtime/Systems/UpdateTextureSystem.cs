namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using ME.BECS.Players;
    using Unity.Collections;

    //[BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(CreateTextureSystem), typeof(CreateSystem), typeof(PlayersSystem))]
    public struct UpdateTextureSystem : IUpdate {

        public float fadeInSpeed;
        public float fadeOutSpeed;

        [BURST(CompileSynchronously = true)]
        public struct UpdateJob : IJobParallelFor {

            public float dt;
            public float fadeInSpeed;
            public float fadeOutSpeed;
            public FogOfWarStaticComponent props;
            public FogOfWarComponent fow;
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
                    if (FogOfWarUtils.IsVisible(in this.props, in this.fow, fowX, fowY) == true) {
                        var targetColor = new UnityEngine.Color32(255, 255, 255, 255);
                        this.Place(index, targetColor, this.dt * this.fadeInSpeed);
                        this.Place(index + 1, targetColor, this.dt * this.fadeInSpeed);
                        this.Place(index - 1, targetColor, this.dt * this.fadeInSpeed);
                        this.Place(index + w, targetColor, this.dt * this.fadeInSpeed);
                        this.Place(index - w, targetColor, this.dt * this.fadeInSpeed);
                    } else {
                        this.currentBuffer[index] = UnityEngine.Color32.Lerp(this.currentBuffer[index], new UnityEngine.Color32(0, 0, 0, 0), this.dt * this.fadeOutSpeed);
                    }
                }
                
            }

            private void Place(int i, UnityEngine.Color32 targetColor, float speed) {

                if (i >= this.currentBuffer.Length) return;
                if (i < 0) return;
                
                this.currentBuffer[i] = UnityEngine.Color32.Lerp(this.currentBuffer[i], targetColor, speed);
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var createTexture = context.world.GetSystem<CreateTextureSystem>();
            if (createTexture.IsCreated == false) return;
            
            var playersSystem = context.world.GetSystem<PlayersSystem>();
            var activePlayer = playersSystem.GetActivePlayer();
            var fow = activePlayer.team.Read<FogOfWarComponent>();
            var props = context.world.GetSystem<CreateSystem>().heights.Read<FogOfWarStaticComponent>();

            var buffer = createTexture.GetBuffer();
            var handle = new UpdateJob() {
                dt = context.deltaTime,
                fadeInSpeed = this.fadeInSpeed,
                fadeOutSpeed = this.fadeOutSpeed,
                props = props,
                fow = fow,
                textureWidth = createTexture.textureWidth,
                textureHeight = createTexture.textureHeight,
                currentBuffer = buffer,
            }.Schedule(buffer.Length, JobUtils.GetScheduleBatchCount(buffer.Length));
            createTexture.GetTexture().Apply(false);
            context.SetDependency(handle);
            
        }

    }

}