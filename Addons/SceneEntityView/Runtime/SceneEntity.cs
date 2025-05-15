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

    public class SceneEntity : MonoBehaviour {

        public bool usePrefab;
        public string worldName;
        public View prefab;
        public EntityView entityView;
        public Config config;
        [ViewsProvider]
        public uint providerId;

        public void Start() {
            if (string.IsNullOrEmpty(this.worldName) == true) {
                Debug.LogError("World Name is empty!");
                return;
            }

            var initializer = WorldInitializers.GetByWorldName(this.worldName);
            if (initializer == null) {
                Debug.LogError($"WorldInitializer was not found by the world name {this.worldName}");
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

                Debug.LogError($"WorldInitializer {this.worldName} is not created yet");


            }

        }

        protected virtual void OnCreate(in Ent ent) { }

    }

}