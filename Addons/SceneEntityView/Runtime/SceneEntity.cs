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

        public string worldName;
        public EntityView entityView;
        public EntityConfig config;
        [ViewsProvider]
        public uint providerId;

        public void Start() {

            if (string.IsNullOrEmpty(this.worldName) == false && this.entityView != null) {

                var initializer = WorldInitializers.GetByWorldName(this.worldName);
                if (initializer == null) {
                    Debug.LogError($"WorldInitializer was not found by the world name {this.worldName}");
                    return;
                }

                var world = initializer.world;
                if (world.isCreated == true) {

                    var ent = Ent.New(in world);
                    if (this.config != null) this.config.Apply(in ent);
                    var tr = ent.Set<TransformAspect>();
                    tr.localPosition = (float3)this.transform.localPosition;
                    tr.localRotation = (quaternion)this.transform.localRotation;
                    tr.localScale = (float3)this.transform.localScale;
                    var viewsModule = initializer.modules.Get<ViewsModule>();
                    var viewSource = viewsModule.RegisterViewSource(this.entityView, this.providerId, sceneSource: true);
                    ent.InstantiateView(viewSource);
                    this.OnCreate(in ent);
                    Object.DestroyImmediate(this);

                } else {
                    
                    Debug.LogError($"WorldInitializer {this.worldName} is not created yet");
                    
                }

            }
            
        }
        
        protected virtual void OnCreate(in Ent ent) {}

    }

}