#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;

#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS {

    using ME.BECS.Transforms;
    using ME.BECS.Views;
    using UnityEngine;

    /// <summary>
    /// Defines scene entity state and operations.
    /// </summary>
    public class SceneEntity : MonoBehaviour {

        /// <summary>
        /// Whether use prefab behavior or state is selected.
        /// </summary>
        public bool usePrefab;
        /// <summary>
        /// World name used by <c>SceneEntity</c>.
        /// </summary>
        public string worldName;
        /// <summary>
        /// Prefab used to instantiate the associated presentation object.
        /// </summary>
        public View prefab;
        /// <summary>
        /// Entity view used by <c>SceneEntity</c>.
        /// </summary>
        public EntityView entityView;
        /// <summary>
        /// Configuration supplying values for this instance.
        /// </summary>
        public Config config;
        /// <summary>
        /// Provider id used to locate the associated entry.
        /// </summary>
        [ViewsProvider]
        public uint providerId;

        /// <summary>
        /// Starts scene entity processing for the supplied context.
        /// </summary>
        public void Start() {
            if (string.IsNullOrEmpty(this.worldName) == true) {
                Logger.Views.Error("World Name is empty!");
                return;
            }

            var initializer = WorldInitializers.GetByWorldName(this.worldName);
            if (initializer == null) {
                Logger.Views.Error($"WorldInitializer was not found by the world name {this.worldName}");
                return;
            }

            var world = initializer.world;
            if (world.isCreated == true) {

                var ent = Ent.New(in world);
                if (this.usePrefab == true) {
                    if (this.prefab.IsValid == true) {
                        ent.InstantiateView(this.prefab);
                    }
                } else {
                    if (this.entityView != null) {
                        var viewsModule = initializer.modules.Get<ViewsModule>();
                        var viewSource = viewsModule.RegisterViewSource(this.entityView, this.providerId, true);
                        ent.InstantiateView(viewSource);
                    }
                }

                var tr = ent.Set<TransformAspect>();
                tr.localPosition = (float3)this.transform.localPosition;
                tr.localRotation = (quaternion)this.transform.localRotation;
                tr.localScale = (float3)this.transform.localScale;
                if (this.config.IsValid) {
                    this.config.Apply(in ent);
                }

                this.OnCreate(in ent);
                if (this.usePrefab == true) {
                    DestroyImmediate(this.gameObject);
                } else {
                    DestroyImmediate(this);
                }

            } else {

                Logger.Views.Error($"WorldInitializer {this.worldName} is not created yet");

            }

        }

        /// <summary>
        /// Provides the <c>OnCreate</c> callback; this implementation performs no work.
        /// </summary>
        protected virtual void OnCreate(in Ent ent) { }

    }

}