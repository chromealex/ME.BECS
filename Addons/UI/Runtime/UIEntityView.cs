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

        internal void DoApplyState() {
            this.ApplyState();
        }

        protected virtual void ApplyState() {
            
        }

    }

}