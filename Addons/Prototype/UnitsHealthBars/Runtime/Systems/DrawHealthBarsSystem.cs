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
            public tfloat barLerpValue;
            public tfloat healthPercent;
            public byte barLerpIndex;

        }
        
        [System.Serializable]
        public struct BarSettings {

            public int Sections => math.min(MAX_SECTIONS, math.max(MIN_SECTIONS, (int)math.ceil(this.health / MAX_HEALTH * MAX_SECTIONS)));
            internal tfloat health;
            public tfloat sectionWidth;
            public tfloat height;
            public tfloat referenceScale;

            public tfloat GetWidth(tfloat scale) => this.Sections * (this.sectionWidth * scale + 1f) - 1f;
            public tfloat GetHeight(tfloat scale) => this.height * scale + 2f;

        }

        public BECS.ObjectReference<UnityEngine.Material> healthBarMaterial;
        public BarSettings barSettings;
        private ME.BECS.NativeCollections.NativeParallelList<BarItem> bars;
        private Ent cameraEnt;
        private ClassPtr<UnityEngine.Camera> cameraObject;

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForAspects<UnitAspect> {

            public SystemLink<CreateSystem> fow;
            public PlayerAspect activePlayer;
            public ME.BECS.NativeCollections.NativeParallelList<BarItem> bars;
            public BarSettings barSettings;
            public CameraAspect camera;
            
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
                var sectionIndex = (byte)math.floor(healthPercent * barInfo.Sections);
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

        [WithoutBurst]
        public void OnAwake(ref SystemContext context) {

            this.bars = new ME.BECS.NativeCollections.NativeParallelList<BarItem>(100, Constants.ALLOCATOR_DOMAIN);

        }

        public void OnDestroy(ref SystemContext context) {

            this.bars.Dispose();

        }

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