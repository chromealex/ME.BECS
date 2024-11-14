using Unity.Collections.LowLevel.Unsafe;

namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using ME.BECS.Players;
    using Unity.Collections;
    using ME.BECS.Jobs;
    using Unity.Mathematics;
    using static Cuts;

    //[BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(CreateTextureSystem))]
    public unsafe struct UpdateTextureSystem : IUpdate {

        public float fadeInSpeed;
        public float fadeOutSpeed;

        private Ent lastActivePlayer;
        private Ent camera;
        private ulong lastTick;

        public void SetCamera(in ME.BECS.Views.CameraAspect camera) {
            this.camera = camera.ent;
        }
        
        public ME.BECS.Views.CameraAspect GetCamera() => this.camera.GetAspect<ME.BECS.Views.CameraAspect>();

        [BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, FloatPrecision = Unity.Burst.FloatPrecision.Low, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
        public struct UpdateJob : IJobParallelFor {

            public float dt;
            public float fadeInSpeed;
            public float fadeOutSpeed;
            public FogOfWarStaticComponent props;
            public FogOfWarComponent fow;
            public uint textureWidth;
            //public int textureHeight;
            [NativeDisableParallelForRestriction]
            [NativeDisableUnsafePtrRestriction]
            public UnityEngine.Color32* currentBuffer;
            
            public void Execute(int index) {

                var w = this.textureWidth;
                //var h = this.textureHeight;
                {
                    var fowX = (uint)(index % w);
                    var fowY = (uint)(index / w);
                    //(var fowX, var fowY) = FogOfWarUtils.GetPixelPosition(in this.props, x, y, w, h);
                    //var height = FogOfWarUtils.GetHeight(in this.props, fowX, fowY) / this.props.maxHeight;
                    ref var color = ref this.currentBuffer[index];
                    if (FogOfWarUtils.IsVisible(in this.props, in this.fow, fowX, fowY) == true) {
                        color.r = (byte)(color.r + (255 - color.r) * this.dt * this.fadeInSpeed);
                    } else {
                        color.r = (byte)(color.r + (0 - color.r) * this.dt * this.fadeOutSpeed);
                    }

                    if (FogOfWarUtils.IsExplored(in this.props, in this.fow, fowX, fowY) == true) {
                        color.g = (byte)(color.g + (255 - color.g) * this.dt * this.fadeInSpeed);
                    }

                    //color.b = (byte)math.lerp(0, 255, height);

                }
                
            }

        }

        [BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, FloatPrecision = Unity.Burst.FloatPrecision.Low, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
        public struct UpdateTextureJob : IJob {

            [NativeDisableParallelForRestriction]
            [NativeDisableUnsafePtrRestriction]
            public byte* currentBuffer;
            public FogOfWarComponent fow;
            
            public void Execute() {

                _memcpy(this.fow.nodes.GetUnsafePtr(), this.currentBuffer, this.fow.nodes.Length);

            }

        }

        public struct ApplyTextureJob : IJobMainThread {

            public CreateTextureSystem system;
            
            public void Execute() {
                
                this.system.GetTexture().Apply(false);
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var logicWorld = context.world.parent;
            E.IS_CREATED(logicWorld);
            
            var createTexture = context.world.GetSystem<CreateTextureSystem>();
            
            var playersSystem = logicWorld.GetSystem<PlayersSystem>();
            var activePlayer = playersSystem.GetActivePlayer();
            if (this.lastActivePlayer != activePlayer.ent || logicWorld.state->tick < this.lastTick) {
                // clean up textures because we need to rebuild them for current player
                FogOfWarUtils.CleanUpTexture(createTexture.GetBuffer());
            }
            this.lastActivePlayer = activePlayer.ent;
            var fow = activePlayer.readTeam.Read<FogOfWarComponent>();
            
            var buffer = createTexture.GetBuffer();
            this.lastTick = logicWorld.state->tick;
        
            var system = logicWorld.GetSystem<CreateSystem>();
            var props = system.heights.Read<FogOfWarStaticComponent>();
            var handle = new UpdateJob() {
                dt = context.deltaTime,
                fadeInSpeed = this.fadeInSpeed,
                fadeOutSpeed = this.fadeOutSpeed,
                props = props,
                fow = fow,
                textureWidth = props.size.x,
                //textureHeight = (int)system.mapSize.y,
                currentBuffer = (UnityEngine.Color32*)buffer.GetUnsafePtr(),
            }.Schedule(buffer.Length / 4, JobUtils.GetScheduleBatchCount(buffer.Length));
            
            handle = new ApplyTextureJob() {
                system = createTexture,
            }.Schedule(handle);
            context.SetDependency(handle);
            
        }

    }

}