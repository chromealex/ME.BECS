using System.Linq;
using UnityEngine;

namespace ME.BECS.Views {
    
    public interface IViewModule { }

    public interface IViewOnValidate {

        void OnValidate(GameObject gameObject);

    }
    
    /// <summary>
    /// Use this interface for View or ViewModule to ignore auto components tracker
    /// If interface is on components tracker will be ignored (any changes will fire ApplyState)
    /// </summary>
    public interface IViewIgnoreTracker { }

    /// <summary>
    /// Use this interface for View or ViewModule
    /// Code Generator automatically find all components usage in your code, but if you want to
    /// track some additional component changes - you can use this interface to do that.
    /// You can use multiple interfaces to track as many components as you need.
    /// </summary>
    /// <typeparam name="T">Type to add</typeparam>
    public interface IViewTrack<T> { }
    
    /// <summary>
    /// Use this interface for View or ViewModule
    /// Code Generator automatically find all components usage in your code, but if you want to
    /// ignore some component changes - you can use this interface to do that.
    /// You can use multiple interfaces to ignore as many components as you need.
    /// </summary>
    /// <typeparam name="T">Type to ignore</typeparam>
    public interface IViewTrackIgnore<T> { }

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

    public interface IViewApplyStateParallel : IViewModule {
        void ApplyStateParallel(in EntRO ent);
    }

    public interface IViewUpdate : IViewModule {
        void OnUpdate(in EntRO ent, float dt);
    }

    public interface IViewUpdateParallel : IViewModule {
        void OnUpdateParallel(in EntRO ent, float dt);
    }

    public enum CullingType {
        /// <summary>
        /// Apply frustum culling for ApplyState/OnUpdate methods
        /// </summary>
        Frustum = 0,
        /// <summary>
        /// Ignore frustum culling
        /// </summary>
        Never = 1,
        /// <summary>
        /// Apply frustum culling for OnUpdate method only
        /// </summary>
        FrustumOnUpdateOnly = 2,
        /// <summary>
        /// Apply frustum culling for ApplyState method only
        /// </summary>
        FrustumApplyStateOnly = 3,
    }

    [System.Serializable]
    public struct ViewModules {

        [System.Serializable]
        public struct Module {

            public bool enabled;
            [SerializeReference]
            [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes = false, runtimeAssembliesOnly = true)]
            [SerializeField]
            public IViewModule module;

        }

        public Module[] items;

        public static ViewModules Create() {
            return new ViewModules() {
                items = System.Array.Empty<Module>(),
            };
        }

        public void Validate(ref IViewModule[] oldModules) {
            if (oldModules.Length == 0) return;
            this.items = oldModules.Select(x => new Module() {
                enabled = true,
                module = x,
            }).ToArray();
            oldModules = System.Array.Empty<IViewModule>();
        }

        public void Validate(GameObject gameObject) {
            foreach (var module in this.items) {
                if (module.module is IViewOnValidate onValidate) {
                    onValidate.OnValidate(gameObject);
                }
            }
        }

        public IViewModule FirstOrDefault(System.Func<IViewModule, bool> predicate) {
            foreach (var item in this.items) {
                if (predicate.Invoke(item.module) == true) return item.module;
            }
            return null;
        }

        public bool Any(System.Func<IViewModule, bool> predicate) {
            foreach (var item in this.items) {
                if (predicate.Invoke(item.module) == true) return true;
            }
            return false;
        }

    }
    
    public abstract class EntityView : MonoBehaviour, IView {
        
        [HideInInspector]
        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes = false, runtimeAssembliesOnly = true)]
        [SerializeField]
        protected internal IViewModule[] viewModules = System.Array.Empty<IViewModule>();
        [SerializeField]
        protected internal ViewModules modules = ViewModules.Create();

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
        internal int[] applyStateParallelModules;
        [SerializeField][HideInInspector]
        internal int[] updateModules;
        [SerializeField][HideInInspector]
        internal int[] updateParallelModules;

        public CullingType cullingType;
        public GroupChangedTracker groupChangedTracker;
        public ViewRoot rootInfo;
        public EntRO ent;

        public T GetModule<T>() where T : IViewModule {
            foreach (var module in this.modules.items) {
                if (module.enabled == true && module.module is T mod) return mod;
            }
            return default;
        }

        internal ViewModules GetAllModules() => this.modules;

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
        /// Called every time ent has been changed, but in parallel job
        /// </summary>
        /// <param name="ent"></param>
        public void DoApplyStateParallel(in EntRO ent) {
            this.ApplyStateParallel(in ent);
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="dt"></param>
        public void DoOnUpdate(in EntRO ent, float dt) {
            this.OnUpdate(in ent, dt);
        }
        
        /// <summary>
        /// Called every frame, but in parallel job
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="dt"></param>
        public void DoOnUpdateParallel(in EntRO ent, float dt) {
            this.OnUpdateParallel(in ent, dt);
        }

        protected internal virtual void OnEnableFromPool(in EntRO ent) { }

        protected internal virtual void OnDisableToPool() { }

        protected internal virtual void OnInitialize(in EntRO ent) { }

        protected internal virtual void OnDeInitialize() { }

        protected internal virtual void ApplyState(in EntRO ent) { }

        protected internal virtual void ApplyStateParallel(in EntRO ent) { }

        protected internal virtual void OnUpdate(in EntRO ent, float dt) { }

        protected internal virtual void OnUpdateParallel(in EntRO ent, float dt) { }

        public virtual void OnValidate() {
            
            this.modules.Validate(ref this.viewModules);

            this.ValidateModules<IViewInitialize>(ref this.initializeModules);
            this.ValidateModules<IViewApplyState>(ref this.applyStateModules);
            this.ValidateModules<IViewApplyStateParallel>(ref this.applyStateParallelModules);
            this.ValidateModules<IViewUpdate>(ref this.updateModules);
            this.ValidateModules<IViewUpdateParallel>(ref this.updateParallelModules);
            this.ValidateModules<IViewDeInitialize>(ref this.deInitializeModules);
            this.ValidateModules<IViewDisableToPool>(ref this.disableToPoolModules);
            this.ValidateModules<IViewEnableFromPool>(ref this.enableFromPoolModules);
            
            this.modules.Validate(this.gameObject);
            
            var systems = this.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var system in systems) {
                var main = system.main;
                if (main.stopAction == ParticleSystemStopAction.Destroy || main.stopAction == ParticleSystemStopAction.Disable) {
                    Debug.LogWarning($"ParticleSystem on object {this} can't have an action {main.stopAction}", this);
                    main.stopAction = ParticleSystemStopAction.None;
                }
            }

            var trails = this.GetComponentsInChildren<TrailRenderer>(true);
            foreach (var trail in trails) {
                if (trail.autodestruct == true) {
                    Debug.LogWarning($"TrailRenderer on object {this} can't have autodestruct as true", this);
                    trail.autodestruct = false;
                }
            }

        }

        private void ValidateModules<T>(ref int[] indexes) {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < this.modules.items.Length; ++i) {
                var module = this.modules.items[i];
                if (module.enabled == true && module.module is T) {
                    list.Add(i);
                }
            }

            indexes = list.ToArray();
        }

    }

}