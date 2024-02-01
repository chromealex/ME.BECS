namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using ME.BECS.Players;

    //[BURST(CompileSynchronously = true)]
    public struct UpdateTextureSystem : IUpdate {

        public float fadeInSpeed;
        public float fadeOutSpeed;

        [BURST(CompileSynchronously = true)]
        public struct UpdateJob : Unity.Jobs.IJob {

            public float dt;
            public float fadeInSpeed;
            public float fadeOutSpeed;
            public FogOfWarStaticComponent props;
            public FogOfWarComponent fow;
            public int textureWidth;
            public int textureHeight;
            public Unity.Collections.NativeArray<UnityEngine.Color32> currentBuffer;
            
            public void Execute() {

                var w = this.textureWidth / 4;
                var h = this.textureHeight;
                for (int i = 0; i < this.currentBuffer.Length; ++i) {
                    var pixelIndex = i;
                    var x = pixelIndex % w;
                    var y = pixelIndex / w;
                    (var fowX, var fowY) = FogOfWarUtils.GetPixelPosition(in this.props, x, y, w, h);
                    if (FogOfWarUtils.IsVisible(in this.props, in this.fow, fowX, fowY) == true) {
                        var targetColor = new UnityEngine.Color32(255, 255, 255, 255);
                        this.Place(i, targetColor, this.dt * this.fadeInSpeed);
                        this.Place(i + 1, targetColor, this.dt * this.fadeInSpeed);
                        this.Place(i - 1, targetColor, this.dt * this.fadeInSpeed);
                        this.Place(i + w, targetColor, this.dt * this.fadeInSpeed);
                        this.Place(i - w, targetColor, this.dt * this.fadeInSpeed);
                    } else {
                        this.currentBuffer[i] = UnityEngine.Color32.Lerp(this.currentBuffer[i], new UnityEngine.Color32(0, 0, 0, 0), this.dt * this.fadeOutSpeed);
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
            
            var handle = new UpdateJob() {
                dt = context.deltaTime,
                fadeInSpeed = this.fadeInSpeed,
                fadeOutSpeed = this.fadeOutSpeed,
                props = props,
                fow = fow,
                textureWidth = createTexture.textureWidth,
                textureHeight = createTexture.textureHeight,
                currentBuffer = createTexture.GetBuffer(),
            }.Schedule();
            handle.Complete();
            createTexture.GetTexture().Apply(false);
            context.SetDependency(handle);
            
        }

    }

}