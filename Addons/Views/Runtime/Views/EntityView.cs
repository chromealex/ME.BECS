using UnityEngine;

namespace ME.BECS.Views {
    
    public interface IViewModule { }

    public interface IViewOnValidate {

        void OnValidate(GameObject gameObject);

    }

    public interface IViewInitialize : IViewModule {
        void OnInitialize(in EntRO ent);
    }

    public interface IViewDeInitialize : IViewModule {
        void OnDeInitialize();
    }

    public interface IViewEnableFromPool : IViewModule {
        void OnEnableFromPool(in EntRO ent);
    }

    public interface IViewDisableToPool : IViewModule {
        void OnDisableToPool();
    }

    public interface IViewApplyState : IViewModule {
        void ApplyState(in EntRO ent);
    }

    public interface IViewUpdate : IViewModule {
        void OnUpdate(in EntRO ent, float dt);
    }

    public enum CullingType {

        Frustum = 0,
        Never = 1,
        FrustumOnUpdateOnly = 2,
        FrustumApplyStateOnly = 3,

    }
    
    public abstract class EntityView : MonoBehaviour, IView {

        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes = false, runtimeAssembliesOnly = true)]
        [SerializeField]
        protected internal IViewModule[] viewModules = System.Array.Empty<IViewModule>();

        [SerializeField][HideInInspector]
        internal int[] initializeModules;
        [SerializeField][HideInInspector]
        internal int[] deInitializeModules;
        [SerializeField][HideInInspector]
        internal int[] enableFromPoolModules;
        [SerializeField][HideInInspector]
        internal int[] disableToPoolModules;
        [SerializeField][HideInInspector]
        internal int[] applyStateModules;
        [SerializeField][HideInInspector]
        internal int[] updateModules;

        public CullingType cullingType;
        public GroupChangedTracker groupChangedTracker;
        public ViewRoot rootInfo;
        public EntRO ent;

        public T GetModule<T>() where T : IViewModule {
            foreach (var module in this.viewModules) {
                if (module is T mod) return mod;
            }
            return default;
        }

        /// <summary>
        /// Called once when this view creates on scene
        /// </summary>
        /// <param name="ent"></param>
        public void DoInitialize(in EntRO ent) {
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
        public void DoEnableFromPool(in EntRO ent) {
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
        public void DoApplyState(in EntRO ent) {
            this.ApplyState(in ent);
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="dt"></param>
        public void DoOnUpdate(in EntRO ent, float dt) {
            this.OnUpdate(in ent, dt);
        }

        public void DoInitializeChildren(in EntRO ent) {
            for (int i = 0; i < this.initializeModules.Length; ++i) {
                ((IViewInitialize)this.viewModules[this.initializeModules[i]]).OnInitialize(in ent);
            }
        }

        public void DoDeInitializeChildren() {
            for (int i = 0; i < this.deInitializeModules.Length; ++i) {
                ((IViewDeInitialize)this.viewModules[this.deInitializeModules[i]]).OnDeInitialize();
            }
        }

        public void DoEnableFromPoolChildren(in EntRO ent) {
            for (int i = 0; i < this.enableFromPoolModules.Length; ++i) {
                ((IViewEnableFromPool)this.viewModules[this.enableFromPoolModules[i]]).OnEnableFromPool(in ent);
            }
        }

        public void DoDisableToPoolChildren() {
            for (int i = 0; i < this.disableToPoolModules.Length; ++i) {
                ((IViewDisableToPool)this.viewModules[this.disableToPoolModules[i]]).OnDisableToPool();
            }
        }

        public void DoApplyStateChildren(in EntRO ent) {
            for (int i = 0; i < this.applyStateModules.Length; ++i) {
                ((IViewApplyState)this.viewModules[this.applyStateModules[i]]).ApplyState(in ent);
            }
        }

        public void DoOnUpdateChildren(in EntRO ent, float dt) {
            for (int i = 0; i < this.updateModules.Length; ++i) {
                ((IViewUpdate)this.viewModules[this.updateModules[i]]).OnUpdate(in ent, dt);
            }
        }

        protected internal virtual void OnEnableFromPool(in EntRO ent) { }

        protected internal virtual void OnDisableToPool() { }

        protected internal virtual void OnInitialize(in EntRO ent) { }

        protected internal virtual void OnDeInitialize() { }

        protected internal virtual void ApplyState(in EntRO ent) { }

        protected internal virtual void OnUpdate(in EntRO ent, float dt) { }

        public virtual void OnValidate() {

            this.ValidateModules<IViewInitialize>(ref this.initializeModules);
            this.ValidateModules<IViewApplyState>(ref this.applyStateModules);
            this.ValidateModules<IViewUpdate>(ref this.updateModules);
            this.ValidateModules<IViewDeInitialize>(ref this.deInitializeModules);
            this.ValidateModules<IViewDisableToPool>(ref this.disableToPoolModules);
            this.ValidateModules<IViewEnableFromPool>(ref this.enableFromPoolModules);
            
            foreach (var module in this.viewModules) {
                if (module is IViewOnValidate onValidate) {
                    onValidate.OnValidate(this.gameObject);
                }
            }

            var systems = this.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var system in systems) {
                var main = system.main;
                if (main.stopAction == ParticleSystemStopAction.Destroy || main.stopAction == ParticleSystemStopAction.Disable) {
                    Debug.LogWarning($"ParticleSystem on object {this} can't have an action {main.stopAction}", this);
                    main.stopAction = ParticleSystemStopAction.None;
                }
            }

        }

        private void ValidateModules<T>(ref int[] indexes) {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < this.viewModules.Length; ++i) {
                var module = this.viewModules[i];
                if (module is T) {
                    list.Add(i);
                }
            }

            indexes = list.ToArray();
        }

    }

}