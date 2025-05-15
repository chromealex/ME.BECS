using Unity.Collections.LowLevel.Unsafe;

namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using ME.BECS.Players;
    using Unity.Collections;
    using ME.BECS.Jobs;
    using static Cuts;

    //[BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(CreateTextureSystem))]
    public unsafe struct UpdateTextureSystem : IUpdate {

        public sfloat fadeInSpeed;
        public sfloat fadeOutSpeed;

        private Ent lastActivePlayer;
        private ulong lastTick;

        [BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, FloatPrecision = Unity.Burst.FloatPrecision.Low, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
        public struct UpdateJob : IJobParallelFor {

            public sfloat dt;
            public sfloat fadeInSpeed;
            public sfloat fadeOutSpeed;
            public FogOfWarStaticComponent props;
            public FogOfWarComponent fow;
            public uint textureWidth;
            [NativeDisableParallelForRestriction]
            [NativeDisableUnsafePtrRestriction]
            public UnityEngine.Color32* currentBuffer;
            public byte useFade;

            public void Execute(int index) {

                var w = this.textureWidth;
                {
                    var fowX = (uint)(index % w);
                    var fowY = (uint)(index / w);
                    ref var color = ref this.currentBuffer[index];
                    if (this.useFade == 1) {
                        if (FogOfWarUtils.IsVisible(in this.props, in this.fow, fowX, fowY) == true) {
                            color.r = (byte)(color.r + (255 - color.r) * this.dt * this.fadeInSpeed);
                        } else {
                            color.r = (byte)(color.r + (0 - color.r) * this.dt * this.fadeOutSpeed);
                        }

                        if (FogOfWarUtils.IsExplored(in this.props, in this.fow, fowX, fowY) == true) {
                            color.g = (byte)(color.g + (255 - color.g) * this.dt * this.fadeInSpeed);
                        }
                    } else {
                        if (FogOfWarUtils.IsVisible(in this.props, in this.fow, fowX, fowY) == true) {
                            color.r = 255;
                        } else {
                            color.r = 0;
                        }

                        if (FogOfWarUtils.IsExplored(in this.props, in this.fow, fowX, fowY) == true) {
                            color.g = 255;
                        }
                    }
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

                _memcpy(this.fow.nodes.GetUnsafePtr(), (safe_ptr)this.currentBuffer, this.fow.nodes.Length);

            }

        }

        [BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, FloatPrecision = Unity.Burst.FloatPrecision.Low, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
        public struct ClearTextureJob : IJob {

            [NativeDisableParallelForRestriction]
            [NativeDisableUnsafePtrRestriction]
            public UnityEngine.Color32* currentBuffer;
            public uint length;
            
            public void Execute() {
                
                FogOfWarUtils.CleanUpTexture(this.currentBuffer, this.length);
                
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
            
            context.dependsOn.Complete();
            var buffer = createTexture.GetBuffer();
            var bufferPtr = (UnityEngine.Color32*)buffer.GetUnsafePtr();
            var playersSystem = logicWorld.GetSystem<PlayersSystem>();
            var activePlayer = playersSystem.GetActivePlayer();
            var useFade = true;
            if (this.lastActivePlayer != activePlayer.ent) {
                // clean up textures because we need to rebuild them for current player
                context.SetDependency(new ClearTextureJob() {
                    currentBuffer = bufferPtr,
                    length = (uint)buffer.Length,
                }.Schedule(context.dependsOn));
                useFade = false;
            }
            this.lastActivePlayer = activePlayer.ent;
            var fow = activePlayer.readTeam.Read<FogOfWarComponent>();
            
            this.lastTick = logicWorld.CurrentTick;
        
            var system = logicWorld.GetSystem<CreateSystem>();
            var props = system.heights.Read<FogOfWarStaticComponent>();
            var handle = new UpdateJob() {
                dt = context.deltaTime,
                fadeInSpeed = this.fadeInSpeed,
                fadeOutSpeed = this.fadeOutSpeed,
                props = props,
                fow = fow,
                textureWidth = props.size.x,
                useFade = (byte)(useFade == true ? 1 : 0),
                currentBuffer = bufferPtr,
            }.Schedule(buffer.Length / 4, JobUtils.GetScheduleBatchCount(buffer.Length), context.dependsOn);
            
            handle = new ApplyTextureJob() {
                system = createTexture,
            }.Schedule(handle);
            context.SetDependency(handle);
            
        }

    }

}