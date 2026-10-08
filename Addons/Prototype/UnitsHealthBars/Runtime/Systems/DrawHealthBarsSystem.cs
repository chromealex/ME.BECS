#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.UnitsHealthBars {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Views;
    using ME.BECS.Transforms;
    using ME.BECS.Units;
    using ME.BECS.FogOfWar;
    using ME.BECS.Players;
    
    /// <summary>
    /// Coordinates draw health bars during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [UnityEngine.Tooltip("Drawing health bars via GL API")]
    public partial struct DrawHealthBarsSystem : IAwake, IUpdate, IDestroy {

        private const int MAX_SECTIONS = 20;
        private const int MIN_SECTIONS = 4;
        private const int MAX_HEALTH = 500;
        
        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static DrawHealthBarsSystem Default => new DrawHealthBarsSystem() {
            barSettings = new BarSettings() {
                sectionWidth = 8f,
                height = 8f,
            },
        };

        /// <summary>
        /// Stores bar item for <c>DrawHealthBarsSystem</c>.
        /// </summary>
        public struct BarItem {

            /// <summary>
            /// Settings used by <c>DrawHealthBarsSystem.BarItem</c>.
            /// </summary>
            public BarSettings settings;
            /// <summary>
            /// Position in the coordinate space used by the containing API.
            /// </summary>
            public float2 position;
            /// <summary>
            /// Height position used by the associated spatial operation.
            /// </summary>
            public float2 heightPosition;
            /// <summary>
            /// Bar lerp value used by <c>DrawHealthBarsSystem.BarItem</c>.
            /// </summary>
            public tfloat barLerpValue;
            /// <summary>
            /// Health percent used by <c>DrawHealthBarsSystem.BarItem</c>.
            /// </summary>
            public tfloat healthPercent;
            /// <summary>
            /// Bar lerp index used to locate the associated entry.
            /// </summary>
            public byte barLerpIndex;

        }
        
        /// <summary>
        /// Configures bar behavior and storage.
        /// </summary>
        [System.Serializable]
        public struct BarSettings {

            /// <summary>
            /// Sections used by <c>DrawHealthBarsSystem.BarSettings</c>.
            /// </summary>
            public int Sections => math.min(MAX_SECTIONS, math.max(MIN_SECTIONS, (int)math.ceil(this.health / MAX_HEALTH * MAX_SECTIONS)));
            internal tfloat health;
            /// <summary>
            /// Section width used by <c>DrawHealthBarsSystem.BarSettings</c>.
            /// </summary>
            public tfloat sectionWidth;
            /// <summary>
            /// Vertical extent used by the associated geometry or query.
            /// </summary>
            public tfloat height;
            /// <summary>
            /// Reference scale used by <c>DrawHealthBarsSystem.BarSettings</c>.
            /// </summary>
            public tfloat referenceScale;

            /// <summary>
            /// Returns width.
            /// </summary>
            public tfloat GetWidth(tfloat scale) => this.Sections * (this.sectionWidth * scale + 1f) - 1f;
            /// <summary>
            /// Returns height.
            /// </summary>
            public tfloat GetHeight(tfloat scale) => this.height * scale + 2f;

        }

        /// <summary>
        /// Health bar material used by <c>DrawHealthBarsSystem</c>.
        /// </summary>
        public BECS.ObjectReference<UnityEngine.Material> healthBarMaterial;
        /// <summary>
        /// Bar settings used by <c>DrawHealthBarsSystem</c>.
        /// </summary>
        public BarSettings barSettings;
        private ME.BECS.NativeCollections.NativeParallelList<BarItem> bars;
        private Ent cameraEnt;
        private ClassPtr<UnityEngine.Camera> cameraObject;

        /// <summary>
        /// Executes draw health bars system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForAspects<UnitAspect> {

            /// <summary>
            /// Fog-of-war state used by this operation.
            /// </summary>
            public SystemLink<CreateSystem> fow;
            /// <summary>
            /// Active player used by <c>DrawHealthBarsSystem.Job</c>.
            /// </summary>
            public PlayerAspect activePlayer;
            /// <summary>
            /// Bars used by <c>DrawHealthBarsSystem.Job</c>.
            /// </summary>
            public ME.BECS.NativeCollections.NativeParallelList<BarItem> bars;
            /// <summary>
            /// Bar settings used by <c>DrawHealthBarsSystem.Job</c>.
            /// </summary>
            public BarSettings barSettings;
            /// <summary>
            /// Camera used by <c>DrawHealthBarsSystem.Job</c>.
            /// </summary>
            public CameraAspect camera;
            
            /// <summary>
            /// Processes the job inputs for <c>DrawHealthBarsSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit) {

                if (unit.readHealth >= unit.readHealthMax) return;
                if (this.fow.IsCreated == true && this.fow.Value.IsVisible(in this.activePlayer, unit.ent) == false) return;
                
                var unitPos = unit.ent.GetAspect<TransformAspect>().GetWorldMatrixPosition();
                var screenPoint = this.camera.WorldToScreenPoint(unitPos);
                var healthBarHeightPos = this.camera.WorldToScreenPoint(unitPos + new float3(0f, unit.readHeight, 0f));

                var barInfo = this.barSettings;
                barInfo.health = unit.readHealthMax;
                var healthPerSection = unit.readHealthMax / barInfo.Sections;
                var percent = unit.readHealth / unit.readHealthMax;
                var healthPercent = math.clamp(unit.readHealth / (float)unit.readHealthMax - math.lerp(healthPerSection / (float)unit.readHealthMax * 2f, 0f, percent), 0f, 1f);
                var sectionIndex = (byte)(int)math.floor(healthPercent * barInfo.Sections);
                this.bars.Add(new BarItem() {
                    settings = barInfo,
                    position = screenPoint.xy,
                    heightPosition = healthBarHeightPos.xy,
                    barLerpIndex = sectionIndex,
                    barLerpValue = (unit.readHealth - healthPerSection * sectionIndex) / (float)healthPerSection,
                    healthPercent = healthPercent,
                });

            }

        }

        /// <summary>
        /// Updates draw health bars system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var logicWorld = context.world.parent;
            E.IS_CREATED(logicWorld);

            if (this.cameraEnt.IsAlive() == false) return;
            
            this.bars.Clear();
            var fow = logicWorld.GetSystemLink<CreateSystem>();
            var activePlayer = logicWorld.GetSystem<PlayersSystem>().GetActivePlayer();
            if (activePlayer.IsAlive() == false) return;
            
            var handle = API.Query(in logicWorld, context.dependsOn).AsParallel().Schedule<Job, UnitAspect>(new Job() {
                activePlayer = activePlayer,
                fow = fow,
                camera = this.cameraEnt.GetAspect<CameraAspect>(),
                bars = this.bars,
                barSettings = this.barSettings,
            });
            context.SetDependency(handle);
            
        }

        /// <summary>
        /// Initializes draw health bars system state from the supplied context.
        /// </summary>
        [WithoutBurst]
        public void OnAwake(ref SystemContext context) {

            this.bars = new ME.BECS.NativeCollections.NativeParallelList<BarItem>(100, Constants.ALLOCATOR_DOMAIN);

        }

        /// <summary>
        /// Releases draw health bars system state at the end of its owning lifecycle.
        /// </summary>
        public void OnDestroy(ref SystemContext context) {

            this.bars.Dispose();

        }

        /// <summary>
        /// Sets camera.
        /// </summary>
        public void SetCamera(in CameraAspect cameraAspect, UnityEngine.Camera camera) {
            CameraUtils.UpdateCamera(in cameraAspect, camera);
            this.cameraEnt = cameraAspect.ent;
            this.cameraObject = new ClassPtr<UnityEngine.Camera>(camera);
            
            var barsRender = this.cameraObject.Value.gameObject.AddComponent<HealthBarsRender>();
            barsRender.referenceScale = this.barSettings.referenceScale;
            barsRender.bars = this.bars;
            barsRender.material = this.healthBarMaterial.Value;
        }

    }

}