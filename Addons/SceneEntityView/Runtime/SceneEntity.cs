#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

using ME.BECS.Views;
using UnityEngine;

namespace ME.BECS {

    public class SceneEntity : MonoBehaviour {

        public WorldInitializer worldInitializer;
        public EntityView entityView;
        public EntityConfig config;
        public uint providerId;

        public void Start() {

            if (this.worldInitializer != null && this.entityView != null) {

                var world = this.worldInitializer.world;
                if (world.isCreated == true) {

                    var ent = Ent.New(in world);
                    if (this.config != null) this.config.Apply(in ent);
                    ent.Set<ME.BECS.Transforms.TransformAspect>();
                    var tr = ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
                    tr.localPosition = (float3)this.transform.localPosition;
                    tr.localRotation = (quaternion)this.transform.localRotation;
                    tr.localScale = (float3)this.transform.localScale;
                    var viewsModule = this.worldInitializer.modules.Get<ViewsModule>();
                    var viewSource = viewsModule.RegisterViewSource(this.entityView, this.providerId, sceneSource: true);
                    ent.InstantiateView(viewSource);
                    this.OnCreate(in ent);
                    Object.DestroyImmediate(this);

                }

            }
            
        }
        
        protected virtual void OnCreate(in Ent ent) {}

    }

}