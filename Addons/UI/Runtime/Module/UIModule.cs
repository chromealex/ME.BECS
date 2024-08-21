
namespace ME.BECS {
    
    using ME.BECS.UI;

    [UnityEngine.CreateAssetMenu(menuName = "ME.BECS/UI Module")]
    public unsafe class UIModule : Module {

        public WorldProperties properties = WorldProperties.Default;
        private World uiWorld;
        private readonly System.Collections.Generic.List<UIEntityView> views = new System.Collections.Generic.List<UIEntityView>();

        public Ent Assign(UIEntityView entityView) {

            E.IS_CREATED(this.uiWorld);

            entityView.groupChangedTracker.Initialize();
            
            this.views.Add(entityView);
            var ent = Ent.New(this.uiWorld);
            ent.Set(new UIComponent() {
                target = entityView,
                entity = Ent.Null,
            });
            return ent;

        }
        
        public override void OnAwake(ref World world) {

            this.uiWorld = World.Create(this.properties, false);

        }

        public override Unity.Jobs.JobHandle OnStart(ref World world, Unity.Jobs.JobHandle dependsOn) {
            return dependsOn;
        }

        public override Unity.Jobs.JobHandle OnUpdate(Unity.Jobs.JobHandle dependsOn) {
            dependsOn.Complete();
            Batches.Apply(this.uiWorld.state);
            for (int i = 0; i < this.views.Count; ++i) {
                var view = this.views[i];
                var worldEnt = view.uiEntity.Read<UIComponent>().entity;
                if (worldEnt == Ent.Null) continue;
                var v = view.uiEntity.Version;
                var wv = worldEnt.Version;
                if (view.uiEntityVersion != v ||
                    view.worldEntityVersion != wv) {
                    view.uiEntityVersion = v;
                    view.worldEntityVersion = wv;
                    
                    var changed = view.groupChangedTracker.HasChanged(in worldEnt);
                    
                    if (changed == true) {
                        view.DoApplyState();
                    }
                }
            }
            return dependsOn;
        }

        public override void DoDestroy() {

            if (this.uiWorld.isCreated == true) {
                this.uiWorld.Dispose();
            }
            
        }

    }

}
