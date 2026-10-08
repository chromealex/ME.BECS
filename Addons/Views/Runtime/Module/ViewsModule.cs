
namespace ME.BECS {

    using Views;
    using Unity.Jobs;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Tracks view state that requires presentation updates.
    /// </summary>
    public static class ViewsTracker {

        /// <summary>
        /// Stores view info for <c>ViewsTracker</c>.
        /// </summary>
        public struct ViewInfo {

            /// <summary>
            /// Change tracker used to decide whether processing is required.
            /// </summary>
            public Internal.Array<uint> tracker;

        }

        /// <summary>
        /// Metadata describing the associated entry.
        /// </summary>
        public static ViewInfo[] info;
        /// <summary>
        /// Type to index used to locate the associated entry.
        /// </summary>
        public static System.Collections.Generic.Dictionary<System.Type, uint> typeToIndex;

        /// <summary>
        /// Defines tracker state and operations for <c>ViewsTracker</c>.
        /// </summary>
        public static class Tracker {
            /// <summary>
            /// Identifier used to address this entry within its containing registry.
            /// </summary>
            public static uint id;
            /// <summary>
            /// Names used by <c>ViewsTracker.Tracker</c>.
            /// </summary>
            public static readonly System.Collections.Generic.Dictionary<System.Type, string> names = new System.Collections.Generic.Dictionary<System.Type, string>();
        }

        /// <summary>
        /// Defines tracker state and operations for <c>ViewsTracker</c>.
        /// </summary>
        public static class Tracker<T> {
            /// <summary>
            /// Identifier used to address this entry within its containing registry.
            /// </summary>
            public static uint id;
            /// <summary>
            /// Display or lookup name of this entry.
            /// </summary>
            public static string name;
        }

        /// <summary>
        /// Sets tracker.
        /// </summary>
        public static void SetTracker(uint count) {
            
            System.Array.Resize(ref info, (int)(count + 1u));
            typeToIndex = new System.Collections.Generic.Dictionary<System.Type, uint>();
            
        }
        
        /// <summary>
        /// Tracks view.
        /// </summary>
        public static void TrackView<T>(ViewsTracker.ViewInfo viewInfo) where T : IView {

            var idx = Tracker<T>.id;
            if (idx == 0u) idx = Tracker<T>.id = ++Tracker.id;
            info[idx] = viewInfo;
            typeToIndex.Add(typeof(T), idx);
            Tracker<T>.name = typeof(T).Name;
            Tracker.names.TryAdd(typeof(T), Tracker<T>.name);

        }

        /// <summary>
        /// Tracks view module.
        /// </summary>
        public static void TrackViewModule<T>(ViewsTracker.ViewInfo viewInfo) where T : IViewModule {
            
            var idx = Tracker<T>.id;
            if (idx == 0u) idx = Tracker<T>.id = ++Tracker.id;
            info[idx] = viewInfo;
            typeToIndex.Add(typeof(T), idx);
            Tracker<T>.name = typeof(T).Name;
            Tracker.names.TryAdd(typeof(T), Tracker<T>.name);
            
        }

        /// <summary>
        /// Returns tracker.
        /// </summary>
        [INLINE(256)]
        public static ViewInfo GetTracker<T>(T module) where T : IViewModule {
            if (typeToIndex.TryGetValue(module.GetType(), out uint idx) == true) {
                return info[idx];
            }
            return default;
        }

        /// <summary>
        /// Creates tracker.
        /// </summary>
        [INLINE(256)]
        public static GroupChangedTracker CreateTracker<T>(T applyStateModule) where T : IViewModule {
            var tracker = new GroupChangedTracker();
            tracker.Initialize(GetTracker(applyStateModule));
            return tracker;
        }

    }
    
    /// <summary>
    /// Provides lifecycle integration for the views feature.
    /// </summary>
    [UnityEngine.CreateAssetMenu(menuName = "ME.BECS/Views Module")]
    public class ViewsModule : Module {

        /// <summary>
        /// Stores provider info for <c>ViewsModule</c>.
        /// </summary>
        public struct ProviderInfo {

            /// <summary>
            /// Editor name used by <c>ViewsModule.ProviderInfo</c>.
            /// </summary>
            public Unity.Collections.FixedString64Bytes editorName;
            /// <summary>
            /// Identifier used to address this entry within its containing registry.
            /// </summary>
            public uint id;

        }

        /// <summary>
        /// Provider infos used by <c>ViewsModule</c>.
        /// </summary>
        public static readonly ProviderInfo[] providerInfos = new ProviderInfo[] {
            new ProviderInfo() { editorName = "None", id = 0u },
            new ProviderInfo() { editorName = "GameObject Provider", id = 1u },
            new ProviderInfo() { editorName = "DrawMesh Provider", id = 2u },
            new ProviderInfo() { editorName = "Particles Provider", id = 3u },
        };
        
        /// <summary>
        /// Gameobject provider id used to locate the associated entry.
        /// </summary>
        public static readonly uint GAMEOBJECT_PROVIDER_ID = providerInfos[1].id;
        /// <summary>
        /// Draw mesh provider id used to locate the associated entry.
        /// </summary>
        public static readonly uint DRAW_MESH_PROVIDER_ID = providerInfos[2].id;
        /// <summary>
        /// Particles provider id used to locate the associated entry.
        /// </summary>
        public static readonly uint PARTICLES_PROVIDER_ID = providerInfos[3].id;

        /// <summary>
        /// Configuration values used by this operation.
        /// </summary>
        public ViewsModuleProperties properties = ViewsModuleProperties.Default;
        private UnsafeViewsModule<EntityView> viewsGameObjects;
        private UnsafeViewsModule<EntityView> viewsDrawMeshes;
        private UnsafeViewsModule<EntityView> viewsParticles;
        private bool isActive;

        /// <summary>
        /// Initializes views module state from the supplied context.
        /// </summary>
        public override void OnAwake(ref World world) {

            if (this.isActive == true) return;
            
            if (this.properties.viewsGameObjects == true) this.viewsGameObjects = UnsafeViewsModule<EntityView>.Create(GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), this.worldProperties.stateProperties.EntitiesCapacity, this.properties);
            if (this.properties.viewsDrawMeshes == true) this.viewsDrawMeshes = UnsafeViewsModule<EntityView>.Create(DRAW_MESH_PROVIDER_ID, ref world, new DrawMeshProvider(), this.worldProperties.stateProperties.EntitiesCapacity, this.properties);
            if (this.properties.viewsParticles == true) {
                // Particle effects are batched into shared ParticleSystems. Deferring their view
                // requests can let short-lived effect entities expire before they are rendered.
                var particlesProperties = this.properties;
                particlesProperties.spawnLimitPerFrame = 0u;
                this.viewsParticles = UnsafeViewsModule<EntityView>.Create(PARTICLES_PROVIDER_ID, ref world, new ParticlesProvider(), this.worldProperties.stateProperties.EntitiesCapacity, particlesProperties);
            }
            this.isActive = true;

        }

        /// <summary>
        /// Returns unsafe views module.
        /// </summary>
        public UnsafeViewsModule<EntityView> GetUnsafeViewsModule(uint providerId) {

            if (providerId == GAMEOBJECT_PROVIDER_ID) {
                return this.viewsGameObjects;
            }

            if (providerId == DRAW_MESH_PROVIDER_ID) {
                return this.viewsDrawMeshes;
            }

            if (providerId == PARTICLES_PROVIDER_ID) {
                return this.viewsParticles;
            }

            return default;

        }

        /// <summary>
        /// Returns world.
        /// </summary>
        public unsafe World GetWorld(uint providerId) {

            if (providerId == GAMEOBJECT_PROVIDER_ID) {
                return this.viewsGameObjects.data.ptr->viewsWorld;
            }

            if (providerId == DRAW_MESH_PROVIDER_ID) {
                return this.viewsDrawMeshes.data.ptr->viewsWorld;
            }

            if (providerId == PARTICLES_PROVIDER_ID) {
                return this.viewsParticles.data.ptr->viewsWorld;
            }

            return default;

        }

        /// <summary>
        /// Sets camera.
        /// </summary>
        public void SetCamera(in CameraAspect camera) {
            
            if (this.properties.viewsGameObjects == true) this.viewsGameObjects.SetCamera(in camera);
            if (this.properties.viewsDrawMeshes == true) this.viewsDrawMeshes.SetCamera(in camera);
            if (this.properties.viewsParticles == true) this.viewsParticles.SetCamera(in camera);

        }

        /// <summary>
        /// Starts views module processing for the supplied context.
        /// </summary>
        public override JobHandle OnStart(ref World world, JobHandle dependsOn) {
            return dependsOn;
        }

        /// <summary>
        /// Updates views module using the current inputs and execution context.
        /// </summary>
        public override JobHandle OnUpdate(JobHandle dependsOn) {

            if (this.isActive == false) return dependsOn;
            
            var provider1Handle = (this.properties.viewsGameObjects == true ? this.viewsGameObjects.Update(UnityEngine.Time.deltaTime, dependsOn) : default);
            var provider2Handle = (this.properties.viewsDrawMeshes == true ? this.viewsDrawMeshes.Update(UnityEngine.Time.deltaTime, dependsOn) : default);
            var provider3Handle = (this.properties.viewsParticles == true ? this.viewsParticles.Update(UnityEngine.Time.deltaTime, dependsOn) : default);
            return JobHandle.CombineDependencies(provider1Handle, provider2Handle, provider3Handle);

        }

        /// <summary>
        /// Releases views module state at the end of its owning lifecycle.
        /// </summary>
        public override void DoDestroy() {

            if (this.isActive == false) return;
            
            if (this.properties.viewsGameObjects == true) this.viewsGameObjects.Dispose();
            if (this.properties.viewsDrawMeshes == true) this.viewsDrawMeshes.Dispose();
            if (this.properties.viewsParticles == true) this.viewsParticles.Dispose();
            this.isActive = false;

        }

        /// <summary>
        /// Registers view source.
        /// </summary>
        public ViewSource RegisterViewSource(EntityView entityView, uint providerId, bool sceneSource = false) {

            if (providerId == GAMEOBJECT_PROVIDER_ID && this.properties.viewsGameObjects == true) {
                return this.viewsGameObjects.RegisterViewSource(entityView, checkPrefab: false, sceneSource: sceneSource);
            } else if (providerId == DRAW_MESH_PROVIDER_ID && this.properties.viewsDrawMeshes == true) {
                return this.viewsDrawMeshes.RegisterViewSource(entityView, checkPrefab: false, sceneSource: sceneSource);
            } else if (providerId == PARTICLES_PROVIDER_ID && this.properties.viewsParticles == true) {
                return this.viewsParticles.RegisterViewSource(entityView, checkPrefab: false, sceneSource: sceneSource);
            }

            Logger.Views.Warning($"RegisterViewSource failed for {entityView} because provider #{providerId} was not found or disabled");
            
            return default;

        }

        /// <summary>
        /// Returns view data by entity.
        /// </summary>
        public Ent GetViewDataByEntity(in EntRO entity) {
            return this.GetViewDataByEntity(entity.GetEntity());
        }

        /// <summary>
        /// Returns view by entity.
        /// </summary>
        public IView GetViewByEntity(in EntRO entity) {
            return this.GetViewByEntity(entity.GetEntity());
        }

        /// <summary>
        /// Returns view data by entity.
        /// </summary>
        public Ent GetViewDataByEntity(in Ent entity) {
            var data = this.GetViewByEntity(entity);
            if (data == null) return default;
            return data.GetViewData().localViewEnt;
        }

        /// <summary>
        /// Returns view by entity.
        /// </summary>
        public IView GetViewByEntity(in Ent entity) {
            IView view = null;
            if (this.properties.viewsGameObjects == true) view = this.viewsGameObjects.GetViewByEntity(in entity);
            if (this.properties.viewsDrawMeshes == true && view == null) view = this.viewsDrawMeshes.GetViewByEntity(in entity);
            if (this.properties.viewsParticles == true && view == null) view = this.viewsParticles.GetViewByEntity(in entity);
            return view;
        }

    }

}