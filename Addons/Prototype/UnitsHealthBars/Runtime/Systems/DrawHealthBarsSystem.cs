
namespace ME.BECS.UnitsHealthBars {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;
    using ME.BECS.Jobs;
    using ME.BECS.Views;
    using ME.BECS.Transforms;
    using ME.BECS.Units;
    using ME.BECS.FogOfWar;
    using ME.BECS.Players;
    
    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Drawing health bars via GL API")]
    public struct DrawHealthBarsSystem : IAwake, IUpdate, IDestroy {

        private const int MAX_SECTIONS = 20;
        private const int MIN_SECTIONS = 4;
        private const int MAX_HEALTH = 500;
        
        public static DrawHealthBarsSystem Default => new DrawHealthBarsSystem() {
            barSettings = new BarSettings() {
                sectionWidth = 8f,
                height = 8f,
            },
        };

        public struct BarItem {

            public BarSettings settings;
            public float2 position;
            public float2 heightPosition;
            public float barLerpValue;
            public float healthPercent;
            public byte barLerpIndex;

        }
        
        [System.Serializable]
        public struct BarSettings {

            public int Sections => math.min(MAX_SECTIONS, math.max(MIN_SECTIONS, (int)math.ceil(this.health / MAX_HEALTH * MAX_SECTIONS)));
            internal float health;
            public float sectionWidth;
            public float height;

            public float Width => this.Sections * (this.sectionWidth + 1f) - 1f;
            public float Height => this.height + 2f;

        }

        public BECS.ObjectReference<UnityEngine.Material> healthBarMaterial;
        public BarSettings barSettings;
        private ME.BECS.NativeCollections.NativeParallelList<BarItem> bars;
        private World logicWorld;
        private Ent cameraEnt;
        private ClassPtr<UnityEngine.Camera> cameraObject;

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<UnitAspect> {

            public SystemLink<CreateSystem> fow;
            public PlayerAspect activePlayer;
            public ME.BECS.NativeCollections.NativeParallelList<BarItem> bars;
            public BarSettings barSettings;
            public CameraAspect camera;
            
            public void Execute(in JobInfo jobInfo, ref UnitAspect unit) {

                if (unit.readHealth >= unit.readHealthMax) return;
                if (this.fow.IsCreated == true && this.fow.Value.IsVisible(in this.activePlayer, unit.ent) == false) return;
                
                var unitPos = unit.ent.GetAspect<TransformAspect>().GetWorldMatrixPosition();
                var screenPoint = this.camera.WorldToScreenPoint(unitPos);
                var healthBarHeightPos = this.camera.WorldToScreenPoint(unitPos + new float3(0f, unit.readHeight, 0f));

                var barInfo = this.barSettings;
                barInfo.health = unit.readHealthMax;
                var healthPerSection = unit.readHealthMax / barInfo.Sections;
                var percent = unit.readHealth / unit.readHealthMax;
                var healthPercent = math.clamp(unit.readHealth / unit.readHealthMax - math.lerp(healthPerSection / unit.readHealthMax * 2f, 0f, percent), 0f, 1f);
                var sectionIndex = (byte)math.floor(healthPercent * barInfo.Sections);
                this.bars.Add(new BarItem() {
                    settings = barInfo,
                    position = screenPoint.xy,
                    heightPosition = healthBarHeightPos.xy,
                    barLerpIndex = sectionIndex,
                    barLerpValue = (unit.readHealth - healthPerSection * sectionIndex) / healthPerSection,
                    healthPercent = healthPercent,
                });

            }

        }

        public void OnUpdate(ref SystemContext context) {

            if (this.logicWorld.isCreated == false) return;

            this.bars.Clear();
            var fow = this.logicWorld.GetSystemLink<CreateSystem>();
            var activePlayer = this.logicWorld.GetSystem<PlayersSystem>().GetActivePlayer();
            if (activePlayer.IsAlive() == false) return;
            
            var handle = API.Query(in this.logicWorld, context.dependsOn).Schedule<Job, UnitAspect>(new Job() {
                activePlayer = activePlayer,
                fow = fow,
                camera = this.cameraEnt.GetAspect<CameraAspect>(),
                bars = this.bars,
                barSettings = this.barSettings,
            });
            context.SetDependency(handle);
            
        }

        [WithoutBurst]
        public void OnAwake(ref SystemContext context) {

            this.bars = new ME.BECS.NativeCollections.NativeParallelList<BarItem>(100, Constants.ALLOCATOR_DOMAIN);

        }

        public void OnDestroy(ref SystemContext context) {

            this.bars.Dispose();

        }

        public void SetLogicWorld(in World logicWorld) {
            this.logicWorld = logicWorld;
        }

        public void SetCamera(in CameraAspect cameraAspect, UnityEngine.Camera camera) {
            CameraUtils.UpdateCamera(in cameraAspect, camera);
            this.cameraEnt = cameraAspect.ent;
            this.cameraObject = new ClassPtr<UnityEngine.Camera>(camera);
            
            var barsRender = this.cameraObject.Value.gameObject.AddComponent<HealthBarsRender>();
            barsRender.bars = this.bars;
            barsRender.material = this.healthBarMaterial.Value;
        }

    }

}