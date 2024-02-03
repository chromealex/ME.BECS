namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Jobs;
    
    public struct VisualWorld {

        internal World world;
        internal ClassPtr<ViewsModule> viewsModule;
        private JobHandle previousFrameUpdate;

        public World World => this.world;

        [INLINE(256)]
        public JobHandle Update(float deltaTime, JobHandle dependsOn) {
            this.previousFrameUpdate.Complete();
            dependsOn = this.world.Tick(deltaTime, UpdateType.UPDATE, dependsOn);
            if (this.viewsModule.Value != null) {
                dependsOn = this.viewsModule.Value.OnUpdate(dependsOn);
            }
            this.previousFrameUpdate = dependsOn;
            return dependsOn;
        }

        [INLINE(256)]
        public JobHandle Dispose(JobHandle dependsOn = default) {
            
            if (this.viewsModule.Value != null) {
                this.viewsModule.Value.OnDestroy();
                UnityEngine.Object.DestroyImmediate(this.viewsModule.Value);
                this.viewsModule.Dispose();
            }
            
            return this.world.Dispose(dependsOn);
            
        }

    }
    
    public static class WorldHelper {

        [INLINE(256)]
        public static JobHandle CreateVisualWorld(JobHandle dependsOn, out VisualWorld visualWorld, BaseWorldInitializer initializerInstance = null) {
            
            return CreateVisualWorld(WorldProperties.Default, out visualWorld, dependsOn, initializerInstance);
            
        }

        [INLINE(256)]
        public static JobHandle CreateVisualWorld(WorldProperties properties, out VisualWorld visualWorld, JobHandle dependsOn, BaseWorldInitializer initializerInstance = null) {

            if (initializerInstance == null) initializerInstance = BaseWorldInitializer.GetInstance();

            if (initializerInstance == null) {
                throw new System.Exception("BaseWorldInitializer is required");
            }

            properties.stateProperties.mode = WorldMode.Visual;
            var viewsWorld = World.Create(properties, false);
            var systemGroup = SystemGroup.Create(UpdateType.UPDATE);
            systemGroup.Add<ME.BECS.Transforms.TransformWorldMatrixUpdateSystem>();
            viewsWorld.AssignRootSystemGroup(systemGroup);
            ViewsModule viewsModule = null;
            foreach (var module in initializerInstance.modules.list) {
                if (module.IsEnabled() == true && module.obj is ViewsModule vm) {
                    viewsModule = UnityEngine.Object.Instantiate(vm);
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