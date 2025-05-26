
namespace ME.BECS {

    using Views;
    using Unity.Jobs;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static class ViewsTracker {

        public struct ViewInfo {

            public Internal.Array<uint> tracker;

        }

        public static ViewInfo[] info;
        public static System.Collections.Generic.Dictionary<System.Type, uint> typeToIndex;

        public static class Tracker {
            public static uint id;
        }

        public static class Tracker<T> {
            public static uint id;
            public static string name;
        }

        public static void SetTracker(uint count) {
            
            System.Array.Resize(ref info, (int)(count + 1u));
            typeToIndex = new System.Collections.Generic.Dictionary<System.Type, uint>();
            
        }
        
        public static void TrackView<T>(ViewsTracker.ViewInfo viewInfo) where T : IView {

            var idx = Tracker<T>.id;
            if (idx == 0u) idx = Tracker<T>.id = ++Tracker.id;
            info[idx] = viewInfo;
            typeToIndex.Add(typeof(T), idx);
            Tracker<T>.name = typeof(T).Name;

        }

        public static void TrackViewModule<T>(ViewsTracker.ViewInfo viewInfo) where T : IViewModule {
            
            var idx = Tracker<T>.id;
            if (idx == 0u) idx = Tracker<T>.id = ++Tracker.id;
            info[idx] = viewInfo;
            typeToIndex.Add(typeof(T), idx);
            Tracker<T>.name = typeof(T).Name;
            
        }

        [INLINE(256)]
        public static ViewInfo GetTracker<T>(T module) where T : IViewModule {
            if (typeToIndex.TryGetValue(module.GetType(), out uint idx) == true) {
                return info[idx];
            }
            return default;
        }

        [INLINE(256)]
        public static GroupChangedTracker CreateTracker<T>(T applyStateModule) where T : IViewModule {
            var tracker = new GroupChangedTracker();
            tracker.Initialize(GetTracker(applyStateModule));
            return tracker;
        }

    }
    
    [UnityEngine.CreateAssetMenu(menuName = "ME.BECS/Views Module")]
    public class ViewsModule : Module {

        public struct ProviderInfo {

            public Unity.Collections.FixedString64Bytes editorName;
            public uint id;

        }

        public static readonly ProviderInfo[] providerInfos = new ProviderInfo[] {
            new ProviderInfo() { editorName = "None", id = 0u },
            new ProviderInfo() { editorName = "GameObject Provider", id = 1u },
            new ProviderInfo() { editorName = "DrawMesh Provider", id = 2u },
        };
        
        public static readonly uint GAMEOBJECT_PROVIDER_ID = providerInfos[1].id;
        public static readonly uint DRAW_MESH_PROVIDER_ID = providerInfos[2].id;
        
        public ViewsModuleProperties properties = ViewsModuleProperties.Default;
        private UnsafeViewsModule<EntityView> viewsGameObjects;
        private UnsafeViewsModule<EntityView> viewsDrawMeshes;
        private bool isActive;

        public override void OnAwake(ref World world) {

            if (this.isActive == true) return;
            
            if (this.properties.viewsGameObjects == true) this.viewsGameObjects = UnsafeViewsModule<EntityView>.Create(GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), this.worldProperties.stateProperties.entitiesCapacity, this.properties);
            if (this.properties.viewsDrawMeshes == true) this.viewsDrawMeshes = UnsafeViewsModule<EntityView>.Create(DRAW_MESH_PROVIDER_ID, ref world, new DrawMeshProvider(), this.worldProperties.stateProperties.entitiesCapacity, this.properties);
            this.isActive = true;

        }

        public void SetCamera(in CameraAspect camera) {
            
            if (this.properties.viewsGameObjects == true) this.viewsGameObjects.SetCamera(in camera);
            if (this.properties.viewsDrawMeshes == true) this.viewsDrawMeshes.SetCamera(in camera);
            
        }

        public override JobHandle OnStart(ref World world, JobHandle dependsOn) {
            return dependsOn;
        }

        public override JobHandle OnUpdate(JobHandle dependsOn) {

            if (this.isActive == false) return dependsOn;
            
            var provider1Handle = (this.properties.viewsGameObjects == true ? this.viewsGameObjects.Update(UnityEngine.Time.deltaTime, dependsOn) : default);
            var provider2Handle = (this.properties.viewsDrawMeshes == true ? this.viewsDrawMeshes.Update(UnityEngine.Time.deltaTime, dependsOn) : default);
            return JobHandle.CombineDependencies(provider1Handle, provider2Handle);

        }

        public override void DoDestroy() {

            if (this.isActive == false) return;
            
            if (this.properties.viewsGameObjects == true) this.viewsGameObjects.Dispose();
            if (this.properties.viewsDrawMeshes == true) this.viewsDrawMeshes.Dispose();
            this.isActive = false;

        }

        public ViewSource RegisterViewSource(EntityView entityView, uint providerId, bool sceneSource = false) {

            if (providerId == GAMEOBJECT_PROVIDER_ID) {
                return this.viewsGameObjects.RegisterViewSource(entityView, checkPrefab: false, sceneSource: sceneSource);
            } else if (providerId == DRAW_MESH_PROVIDER_ID) {
                return this.viewsDrawMeshes.RegisterViewSource(entityView, checkPrefab: false, sceneSource: sceneSource);
            }
            
            return default;

        }

        public IView GetViewByEntity(in Ent entity) {
            IView view = null;
            if (this.properties.viewsGameObjects == true) view = this.viewsGameObjects.GetViewByEntity(in entity);
            if (this.properties.viewsDrawMeshes == true && view == null) view = this.viewsDrawMeshes.GetViewByEntity(in entity);
            return view;
        }

    }

}