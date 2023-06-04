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
                    var tr = ent.GetAspect<ME.BECS.TransformAspect.TransformAspect>();
                    tr.localPosition = this.transform.localPosition;
                    tr.localRotation = this.transform.localRotation;
                    tr.localScale = this.transform.localScale;
                    var viewsModule = this.worldInitializer.modules.Get<ViewsModule>();
                    var viewSource = viewsModule.RegisterViewSource(this.entityView, this.providerId, sceneSource: true);
                    ent.InstantiateView(viewSource);
                    Object.DestroyImmediate(this);

                }

            }
            
        }

    }

}