namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Jobs;
    using ME.BECS.Views;
    
    public struct VisualWorld {

        internal World world;
        internal Ent camera;
        internal ClassPtr<UnityEngine.Camera> cameraPtr;
        internal ClassPtr<ViewsModule> viewsModule;
        private JobHandle previousFrameUpdate;

        public World World => this.world;
        public Ent Camera => this.camera;

        [INLINE(256)]
        public JobHandle Update(uint deltaTimeMs, JobHandle dependsOn) {
            this.previousFrameUpdate.Complete();
            if (this.cameraPtr.IsValid == true) {
                CameraUtils.UpdateCamera(this.camera.GetAspect<CameraAspect>(), this.cameraPtr.Value);
            }
            dependsOn = this.world.Tick(deltaTimeMs, UpdateType.LATE_UPDATE, dependsOn);
            if (this.viewsModule.IsValid == true) {
                dependsOn = this.viewsModule.Value.OnUpdate(dependsOn);
            }
            this.previousFrameUpdate = dependsOn;
            return dependsOn;
        }

        [INLINE(256)]
        public JobHandle Dispose(JobHandle dependsOn = default) {

            if (this.cameraPtr.IsValid == true) {
                this.cameraPtr.Dispose();
            }
            if (this.viewsModule.IsValid == true) {
                this.viewsModule.Value.DoDestroy();
                UnityEngine.Object.DestroyImmediate(this.viewsModule.Value);
                this.viewsModule.Dispose();
            }
            
            return this.world.Dispose(dependsOn);
            
        }

        public UnityEngine.Camera GetCameraObject() {
            return this.cameraPtr.Value;
        }
        
        public ClassPtr<UnityEngine.Camera> GetCameraObjectPtr() {
            return this.cameraPtr;
        }

    }
    
    public static class WorldHelper {

        /// <summary>
        /// Set up global culling camera for world initializer
        /// Apply to first views module
        /// </summary>
        /// <param name="cameraEntity"></param>
        /// <param name="initializerInstance"></param>
        /// <exception cref="Exception"></exception>
        [INLINE(256)]
        public static bool SetFrustumCullingCamera(in Ent cameraEntity, BaseWorldInitializer initializerInstance = null) {
            
            if (initializerInstance == null) initializerInstance = BaseWorldInitializer.GetInstance();

            if (initializerInstance == null) {
                throw new System.Exception("BaseWorldInitializer is required");
            }

            foreach (var module in initializerInstance.modules.list) {
                if (module.IsEnabled() == true && module.obj is ViewsModule vm) {
                    if (cameraEntity.IsAlive() == true) vm.SetCamera(cameraEntity.GetAspect<CameraAspect>());
                    return true;
                }
            }

            return false;

        }

        /// <summary>
        /// Set up culling camera for visual world
        /// </summary>
        /// <param name="visualWorld"></param>
        /// <param name="camera"></param>
        [INLINE(256)]
        public static void SetFrustumCullingCamera(ref VisualWorld visualWorld, UnityEngine.Camera camera) {
            
            if (camera != null) {
                visualWorld.camera = CameraUtils.CreateCamera(camera, in visualWorld.world).ent;
                visualWorld.cameraPtr = new ClassPtr<UnityEngine.Camera>(camera);
            }
            
        }

        /// <summary>
        /// Create new visual world with default properties
        /// </summary>
        /// <param name="visualWorld"></param>
        /// <param name="dependsOn"></param>
        /// <param name="systemsGraph"></param>
        /// <param name="initializerInstance"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [INLINE(256)]
        public static JobHandle CreateVisualWorld(out VisualWorld visualWorld, JobHandle dependsOn, ME.BECS.FeaturesGraph.SystemsGraph systemsGraph = null, BaseWorldInitializer initializerInstance = null) {
            
            return CreateVisualWorld(WorldProperties.Default, out visualWorld, dependsOn, systemsGraph, initializerInstance);
            
        }

        /// <summary>
        /// Create new visual world
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="visualWorld"></param>
        /// <param name="dependsOn"></param>
        /// <param name="systemsGraph"></param>
        /// <param name="initializerInstance"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [INLINE(256)]
        public static JobHandle CreateVisualWorld(WorldProperties properties, out VisualWorld visualWorld, JobHandle dependsOn, ME.BECS.FeaturesGraph.SystemsGraph systemsGraph = null, BaseWorldInitializer initializerInstance = null) {

            if (initializerInstance == null) initializerInstance = BaseWorldInitializer.GetInstance();

            if (initializerInstance == null) {
                throw new System.Exception("BaseWorldInitializer is required");
            }

            visualWorld.camera = default;
            
            properties.stateProperties.mode = WorldMode.Visual;
            var viewsWorld = World.Create(properties, false);

            if (systemsGraph != null) {
                var group = systemsGraph.DoAwake(ref viewsWorld, UpdateType.LATE_UPDATE);
                viewsWorld.AssignRootSystemGroup(group);
            } else {
                var group = SystemGroup.Create(UpdateType.ANY);
                viewsWorld.AssignRootSystemGroup(group);
            }
            
            ViewsModule viewsModule = null;
            foreach (var module in initializerInstance.modules.list) {
                if (module.IsEnabled() == true && module.obj is ViewsModule vm) {
                    if (visualWorld.camera.IsAlive() == true) vm.SetCamera(visualWorld.camera.GetAspect<CameraAspect>());
                    viewsModule = UnityEngine.Object.Instantiate(vm);
                    if (visualWorld.camera.IsAlive() == true) vm.SetCamera(visualWorld.camera.GetAspect<CameraAspect>());
                    viewsModule.Setup(properties);
                    break;
                }
            }

            visualWorld = default;
            if (viewsModule != null) {
                {
                    viewsModule.OnAwake(ref viewsWorld);
                    dependsOn = viewsWorld.Awake(dependsOn);
                }
                if (visualWorld.camera.IsAlive() == true) viewsModule.SetCamera(visualWorld.camera.GetAspect<CameraAspect>());
                {
                    dependsOn = viewsModule.OnStart(ref viewsWorld, dependsOn);
                }
                visualWorld.viewsModule = new ClassPtr<ViewsModule>(viewsModule);
            }
            
            visualWorld.world = viewsWorld;

            return dependsOn;

        }

    }

}