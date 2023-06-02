using UnityEngine;

namespace ME.BECS.Views {
    
    public interface IViewModule { }

    public interface IViewInitialize : IViewModule {
        void OnInitialize(in Ent ent);
    }

    public interface IViewDeInitialize : IViewModule {
        void OnDeInitialize();
    }

    public interface IViewEnableFromPool : IViewModule {
        void OnEnableFromPool(in Ent ent);
    }

    public interface IViewDisableToPool : IViewModule {
        void OnDisableToPool();
    }

    public interface IViewApplyState : IViewModule {
        void ApplyState(in Ent ent);
    }

    public interface IViewUpdate : IViewModule {
        void OnUpdate(in Ent ent, float dt);
    }
    
    public abstract class EntityView : MonoBehaviour, IView {

        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes = false, runtimeAssembliesOnly = true)]
        [SerializeField]
        protected internal IViewModule[] viewModules = System.Array.Empty<IViewModule>();

        internal IViewInitialize[] initializeModules;
        internal IViewDeInitialize[] deInitializeModules;
        internal IViewEnableFromPool[] enableFromPoolModules;
        internal IViewDisableToPool[] disableToPoolModules;
        internal IViewApplyState[] applyStateModules;
        internal IViewUpdate[] updateModules;

        public ViewRoot rootInfo;
        public Ent ent;

        /// <summary>
        /// Called once when this view creates on scene
        /// </summary>
        /// <param name="ent"></param>
        public void DoInitialize(in Ent ent) {
            this.ent = ent;
            this.OnInitialize(in ent);
        }

        /// <summary>
        /// Called once when this view removed from scene
        /// </summary>
        /// <param name="ent"></param>
        public void DoDeInitialize() {
            this.OnDeInitialize();
        }

        /// <summary>
        /// Called every time this view comes from pool
        /// </summary>
        /// <param name="ent"></param>
        public void DoEnableFromPool(in Ent ent) {
            this.OnEnableFromPool(in ent);
        }
        
        /// <summary>
        /// Called every time this view moved to pool
        /// </summary>
        /// <param name="ent"></param>
        public void DoDisableToPool() {
            this.OnDisableToPool();
        }

        /// <summary>
        /// Called every time ent has been changed
        /// </summary>
        /// <param name="ent"></param>
        public void DoApplyState(in Ent ent) {
            this.ApplyState(in ent);
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="dt"></param>
        public void DoOnUpdate(in Ent ent, float dt) {
            this.OnUpdate(in ent, dt);
        }

        public void DoInitializeChildren(in Ent ent) {
            for (int i = 0; i < this.initializeModules.Length; ++i) {
                this.initializeModules[i].OnInitialize(in ent);
            }
        }

        public void DoDeInitializeChildren() {
            for (int i = 0; i < this.deInitializeModules.Length; ++i) {
                this.deInitializeModules[i].OnDeInitialize();
            }
        }

        public void DoEnableFromPoolChildren(in Ent ent) {
            for (int i = 0; i < this.enableFromPoolModules.Length; ++i) {
                this.enableFromPoolModules[i].OnEnableFromPool(in ent);
            }
        }

        public void DoDisableToPoolChildren() {
            for (int i = 0; i < this.disableToPoolModules.Length; ++i) {
                this.disableToPoolModules[i].OnDisableToPool();
            }
        }

        public void DoApplyStateChildren(in Ent ent) {
            for (int i = 0; i < this.applyStateModules.Length; ++i) {
                this.applyStateModules[i].ApplyState(in ent);
            }
        }

        public void DoOnUpdateChildren(in Ent ent, float dt) {
            for (int i = 0; i < this.updateModules.Length; ++i) {
                this.updateModules[i].OnUpdate(in ent, dt);
            }
        }

        protected internal virtual void OnEnableFromPool(in Ent ent) { }

        protected internal virtual void OnDisableToPool() { }

        protected internal virtual void OnInitialize(in Ent ent) { }

        protected internal virtual void OnDeInitialize() { }

        protected internal virtual void ApplyState(in Ent ent) { }

        protected internal virtual void OnUpdate(in Ent ent, float dt) { }

    }

}