using Unity.Collections.LowLevel.Unsafe;

namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using ME.BECS.Players;
    using Unity.Collections;
    using ME.BECS.Jobs;
    using static Cuts;

    //[BURST]
    /// <summary>
    /// Coordinates update texture during the ECS system lifecycle.
    /// </summary>
    [RequiredDependencies(typeof(CreateTextureSystem))]
    public unsafe partial struct UpdateTextureSystem : IUpdate {

        /// <summary>
        /// Fade in speed controlling the associated calculation.
        /// </summary>
        public sfloat fadeInSpeed;
        /// <summary>
        /// Fade out speed controlling the associated calculation.
        /// </summary>
        public sfloat fadeOutSpeed;

        private Ent lastActivePlayer;
        private ulong lastTick;

        /// <summary>
        /// Executes update work through the job scheduler.
        /// </summary>
        [BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, FloatPrecision = Unity.Burst.FloatPrecision.Low, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
        public partial struct UpdateJob : IJobParallelFor {

            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            public sfloat dt;
            /// <summary>
            /// Fade in speed controlling the associated calculation.
            /// </summary>
            public sfloat fadeInSpeed;
            /// <summary>
            /// Fade out speed controlling the associated calculation.
            /// </summary>
            public sfloat fadeOutSpeed;
            /// <summary>
            /// Configuration values used by this operation.
            /// </summary>
            public FogOfWarStaticComponent props;
            /// <summary>
            /// Fog-of-war state used by this operation.
            /// </summary>
            public FogOfWarComponent fow;
            /// <summary>
            /// Texture width used by <c>UpdateTextureSystem.UpdateJob</c>.
            /// </summary>
            public uint textureWidth;
            /// <summary>
            /// Current buffer used by <c>UpdateTextureSystem.UpdateJob</c>.
            /// </summary>
            [NativeDisableParallelForRestriction]
            [NativeDisableUnsafePtrRestriction]
            public UnityEngine.Color32* currentBuffer;
            /// <summary>
            /// Use fade used by <c>UpdateTextureSystem.UpdateJob</c>.
            /// </summary>
            public byte useFade;

            /// <summary>
            /// Processes update using the supplied job inputs.
            /// </summary>
            public void Execute(int index) {

                var w = this.textureWidth;
                {
                    var fowX = (uint)(index % w);
                    var fowY = (uint)(index / w);
                    ref var color = ref this.currentBuffer[index];
                    if (this.useFade == 1) {
                        if (FogOfWarUtils.IsVisible(in this.props, in this.fow, fowX, fowY) == true) {
                            color.r = (byte)(int)(color.r + (255 - color.r) * this.dt * this.fadeInSpeed);
                        } else {
                            color.r = (byte)(int)(color.r + (0 - color.r) * this.dt * this.fadeOutSpeed);
                        }

                        if (FogOfWarUtils.IsExplored(in this.props, in this.fow, fowX, fowY) == true) {
                            color.g = (byte)(int)(color.g + (255 - color.g) * this.dt * this.fadeInSpeed);
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

        /// <summary>
        /// Executes update texture work through the job scheduler.
        /// </summary>
        [BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, FloatPrecision = Unity.Burst.FloatPrecision.Low, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
        public partial struct UpdateTextureJob : IJob {

            /// <summary>
            /// Current buffer used by <c>UpdateTextureSystem.UpdateTextureJob</c>.
            /// </summary>
            [NativeDisableParallelForRestriction]
            [NativeDisableUnsafePtrRestriction]
            public byte* currentBuffer;
            /// <summary>
            /// Fog-of-war state used by this operation.
            /// </summary>
            public FogOfWarComponent fow;
            
            /// <summary>
            /// Processes update texture using the supplied job inputs.
            /// </summary>
            public void Execute() {

                _memcpy(this.fow.nodes.GetUnsafePtr(), (safe_ptr)this.currentBuffer, this.fow.nodes.Length);

            }

        }

        /// <summary>
        /// Executes clear texture work through the job scheduler.
        /// </summary>
        [BURST(CompileSynchronously = true, FloatMode = Unity.Burst.FloatMode.Fast, FloatPrecision = Unity.Burst.FloatPrecision.Low, OptimizeFor = Unity.Burst.OptimizeFor.Performance)]
        public partial struct ClearTextureJob : IJob {

            /// <summary>
            /// Current buffer used by <c>UpdateTextureSystem.ClearTextureJob</c>.
            /// </summary>
            [NativeDisableParallelForRestriction]
            [NativeDisableUnsafePtrRestriction]
            public UnityEngine.Color32* currentBuffer;
            /// <summary>
            /// Number of elements in the associated storage.
            /// </summary>
            public uint length;
            
            /// <summary>
            /// Processes clear texture using the supplied job inputs.
            /// </summary>
            public void Execute() {
                
                FogOfWarUtils.CleanUpTexture(this.currentBuffer, this.length);
                
            }

        }

        /// <summary>
        /// Executes apply texture work through the job scheduler.
        /// </summary>
        public partial struct ApplyTextureJob : IJobMainThread {

            /// <summary>
            /// System instance used by the associated operation.
            /// </summary>
            public CreateTextureSystem system;
            /// <summary>
            /// Color buffer used by <c>UpdateTextureSystem.ApplyTextureJob</c>.
            /// </summary>
            public NativeArray<byte> colorBuffer;
            
            /// <summary>
            /// Processes apply texture using the supplied job inputs.
            /// </summary>
            public void Execute() {
                
                this.system.GetTexture().SetPixelData(this.colorBuffer, 0);
                this.system.GetTexture().Apply(false);
                
            }

        }

        /// <summary>
        /// Updates update texture system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var logicWorld = context.world.parent;
            E.IS_CREATED(logicWorld);
            
            var createTexture = context.world.GetSystem<CreateTextureSystem>();
            
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
            
            handle.Complete();
            new ApplyTextureJob() {
                system = createTexture,
                colorBuffer = buffer,
            }.Execute();
            context.SetDependency(handle);
            
        }

    }

}