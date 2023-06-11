using UnityEngine;

namespace ME.BECS.UI {

    public class UIEntityView : MonoBehaviour {

        public GroupChangedTracker groupChangedTracker;
        
        [SerializeField]
        protected internal Ent uiEntity;
        internal uint uiEntityVersion;
        internal uint worldEntityVersion;

        public void Start() {

            var instance = WorldInitializer.GetInstance();
            var uiModule = instance.modules.Get<UIModule>();
            if (uiModule != null) {

                this.uiEntity = uiModule.Assign(this);

            } else {
                
                Debug.LogWarning("UI Module required in initializer");
                
            }

        }

        public void Assign(in Ent worldEnt) {

            if (this.uiEntity.IsAlive() == false) {
                Debug.LogError("UI Module View has not been initialized");
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