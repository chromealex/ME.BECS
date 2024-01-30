using UnityEngine;

namespace ME.BECS.UI {

    public class UIEntityView : MonoBehaviour {

        public GroupChangedTracker groupChangedTracker;
        
        [SerializeField]
        protected internal Ent uiEntity;
        internal uint uiEntityVersion;
        internal uint worldEntityVersion;

        public void Start() {

            var instance = BaseWorldInitializer.GetInstance();
            var uiModule = instance.modules.Get<UIModule>();
            if (uiModule != null) {

                this.uiEntity = uiModule.Assign(this);

            } else {
                
                Logger.UI.Warning("UI Module required in initializer");
                
            }

        }

        public void Assign(in Ent worldEnt) {

            if (this.uiEntity.IsAlive() == false) {
                Logger.UI.Error("UI Module View has not been initialized");
                return;
            }
            
            this.uiEntity.Get<UIComponent>().entity = worldEnt;

        }

        internal void DoApplyState() {
            this.ApplyState();
        }

        protected virtual void ApplyState() {
            
        }

    }

}