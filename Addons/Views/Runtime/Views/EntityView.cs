using System.Linq;
using ME.BECS.Addons.Views.Runtime.Providers;
using UnityEngine;

namespace ME.BECS.Views {
    
    /// <summary>
    /// Defines the operations required by view module.
    /// </summary>
    public interface IViewModule { }

    /// <summary>
    /// Defines the operations required by view on validate.
    /// </summary>
    public interface IViewOnValidate {

        /// <summary>
        /// Refreshes or validates state after values change in the Unity Inspector.
        /// </summary>
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

    /// <summary>
    /// Defines initialization performed once when a view module instance is initialized.
    /// </summary>
    public interface IViewInitialize : IViewModule {
        /// <summary>
        /// Initializes i view initialize state from the supplied context.
        /// </summary>
        void OnInitialize();
    }

    /// <summary>
    /// Defines final cleanup of resources owned by a view module instance.
    /// </summary>
    public interface IViewDeInitialize : IViewModule {
        /// <summary>
        /// Releases i view de initialize state at the end of its owning lifecycle.
        /// </summary>
        void OnDeInitialize();
    }

    /// <summary>Called after the spawn pose is committed, regardless of culling. Not called for pool prewarming.</summary>
    public interface IViewEnableFromPool : IViewModule {
        /// <summary>
        /// Activates presentation state when the view is taken from the pool.
        /// </summary>
        void OnEnableFromPool(in ViewData ent);
    }

    /// <summary>
    /// Defines cleanup of active presentation state when a view returns to the pool.
    /// </summary>
    public interface IViewDisableToPool : IViewModule {
        /// <summary>
        /// Resets active presentation state before the view returns to the pool.
        /// </summary>
        void OnDisableToPool();
    }

    /// <summary>
    /// Defines application of logic-entity state to the associated view.
    /// </summary>
    public interface IViewApplyState : IViewModule {
        /// <summary>
        /// Applies the current logic state to the presentation instance.
        /// </summary>
        void ApplyState(in ViewData ent);
    }

    /// <summary>
    /// Defines the operations required by view apply state parallel.
    /// </summary>
    public interface IViewApplyStateParallel : IViewModule {
        /// <summary>
        /// Applies logic state during the parallel phase of view processing.
        /// </summary>
        void ApplyStateParallel(in ViewData ent);
    }

    /// <summary>
    /// Defines per-frame presentation updates for a view module.
    /// </summary>
    public interface IViewUpdate : IViewModule {
        /// <summary>
        /// Updates i view update using the current inputs and execution context.
        /// </summary>
        void OnUpdate(in ViewData ent, float dt);
    }

    /// <summary>
    /// Defines the operations required by view update parallel.
    /// </summary>
    public interface IViewUpdateParallel : IViewModule {
        /// <summary>
        /// Updates presentation during the parallel phase of view processing.
        /// </summary>
        void OnUpdateParallel(in ViewData ent, float dt);
    }

    /// <summary>
    /// Associates a view with its logic entity and the state needed by view callbacks.
    /// </summary>
    public ref struct ViewData {

        internal ViewDataRaw data;

        /// <summary>
        /// Gets logic ent; this implementation returns <c>this.data.logicEnt</c>.
        /// </summary>
        public EntRO logicEnt => this.data.logicEnt;
        /// <summary>
        /// Gets local view ent; this implementation returns <c>this.data.localViewEnt</c>.
        /// </summary>
        public Ent localViewEnt => this.data.localViewEnt;

        internal ViewData(in EntRO ent, in Ent localEnt) {
            this.data.logicEnt = ent;
            this.data.localViewEnt = localEnt;
        }

        /// <summary>
        /// Converts the supplied value to <c>ViewData</c>.
        /// </summary>
        public static implicit operator ViewData(ViewDataRaw raw) {
            return new ViewData() {
                data = raw,
            };
        }

        /// <summary>
        /// Converts the supplied value to <c>EntRO</c>.
        /// </summary>
        public static implicit operator EntRO(ViewData data) {
            return data.logicEnt;
        }
        
        /// <summary>
        /// Converts the supplied value to <c>Ent</c>.
        /// </summary>
        public static implicit operator Ent(ViewData data) {
            return data.localViewEnt;
        }

    }

    /// <summary>
    /// Stores the raw entity and transform data used by view processing.
    /// </summary>
    [System.Serializable]
    public struct ViewDataRaw {

        /// <summary>
        /// Logic ent used by <c>ViewDataRaw</c>.
        /// </summary>
        public EntRO logicEnt;
        /// <summary>
        /// Local view ent used by <c>ViewDataRaw</c>.
        /// </summary>
        public Ent localViewEnt;

        internal ViewDataRaw(in EntRO ent, in Ent localEnt) {
            this.logicEnt = ent;
            this.localViewEnt = localEnt;
        }
        
        /// <summary>
        /// Converts the supplied value to <c>ViewDataRaw</c>.
        /// </summary>
        public static implicit operator ViewDataRaw(ViewData data) {
            return new ViewDataRaw() {
                logicEnt = data.logicEnt,
                localViewEnt = data.localViewEnt,
            };
        }

    }

    /// <summary>
    /// Defines the supported culling type values.
    /// </summary>
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

    /// <summary>
    /// Defines the supported culling job type values.
    /// </summary>
    public enum CullingJobType {
        /// <summary>
        /// Apply state option for <c>CullingJobType</c>.
        /// </summary>
        ApplyState,
        /// <summary>
        /// Update option for <c>CullingJobType</c>.
        /// </summary>
        Update,
        /// <summary>
        /// Apply state parallel option for <c>CullingJobType</c>.
        /// </summary>
        ApplyStateParallel,
        /// <summary>
        /// Update parallel option for <c>CullingJobType</c>.
        /// </summary>
        UpdateParallel,
    }

    /// <summary>
    /// Stores the modules attached to a view and exposes module lookup operations.
    /// </summary>
    [System.Serializable]
    public struct ViewModules {

        /// <summary>
        /// Provides lifecycle integration for the  feature.
        /// </summary>
        [System.Serializable]
        public struct Module {

            /// <summary>
            /// Whether enabled behavior or state is selected.
            /// </summary>
            public bool enabled;
            /// <summary>
            /// Module used by <c>ViewModules.Module</c>.
            /// </summary>
            [SerializeReference]
            [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes = false, runtimeAssembliesOnly = true)]
            [SerializeField]
            public IViewModule module;

        }

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public Module[] items;

        /// <summary>
        /// Creates <c>ViewModules</c> using the supplied creation arguments.
        /// </summary>
        public static ViewModules Create() {
            return new ViewModules() {
                items = System.Array.Empty<Module>(),
            };
        }

        /// <summary>
        /// Checks the supplied state against the constraints required by this API.
        /// </summary>
        public void Validate(ref IViewModule[] oldModules) {
            if (oldModules.Length == 0) return;
            this.items = oldModules.Select(x => new Module() {
                enabled = true,
                module = x,
            }).ToArray();
            oldModules = System.Array.Empty<IViewModule>();
        }

        /// <summary>
        /// Checks the supplied state against the constraints required by this API.
        /// </summary>
        public void Validate(GameObject gameObject) {
            foreach (var module in this.items) {
                if (module.module is IViewOnValidate onValidate) {
                    onValidate.OnValidate(gameObject);
                }
            }
        }

        /// <summary>
        /// Returns the first matching entry, or the default value when none exists.
        /// </summary>
        public IViewModule FirstOrDefault(System.Func<IViewModule, bool> predicate) {
            foreach (var item in this.items) {
                if (predicate.Invoke(item.module) == true) return item.module;
            }
            return null;
        }

        /// <summary>
        /// Tests whether any view module satisfies the supplied predicate.
        /// </summary>
        public bool Any(System.Func<IViewModule, bool> predicate) {
            foreach (var item in this.items) {
                if (predicate.Invoke(item.module) == true) return true;
            }
            return false;
        }

    }
    
    /// <summary>
    /// Presents a logic entity through a pooled Unity object and view lifecycle callbacks.
    /// </summary>
    public abstract class EntityView : MonoBehaviour, IView {
        
        /// <summary>
        /// View modules used by <c>EntityView</c>.
        /// </summary>
        [HideInInspector]
        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes = false, runtimeAssembliesOnly = true)]
        [SerializeField]
        protected internal IViewModule[] viewModules = System.Array.Empty<IViewModule>();
        /// <summary>
        /// Modules used by <c>EntityView</c>.
        /// </summary>
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

        /// <summary>
        /// Culling type used by <c>EntityView</c>.
        /// </summary>
        public CullingType cullingType;
        /// <summary>
        /// Pool count for the associated storage.
        /// </summary>
        public uint poolCount;
        /// <summary>
        /// Supported providers used by <c>EntityView</c>.
        /// </summary>
        [ViewsProviderMask]
        public uint supportedProviders = uint.MaxValue;
        /// <summary>
        /// Group changed tracker used by <c>EntityView</c>.
        /// </summary>
        public GroupChangedTracker groupChangedTracker;
        /// <summary>
        /// Group changed tracker parallel used by <c>EntityView</c>.
        /// </summary>
        public GroupChangedTracker groupChangedTrackerParallel;
        /// <summary>
        /// Root info used by <c>EntityView</c>.
        /// </summary>
        public ViewRoot rootInfo;
        [SerializeField]
        internal ViewDataRaw viewDataRaw;
        /// <summary>
        /// Gets view data; this implementation returns <c>this.viewDataRaw</c>.
        /// </summary>
        public ViewData viewData => this.viewDataRaw;
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        [System.Obsolete("Use viewData instead")]
        public ViewData ent => this.viewData;

        ViewData IView.GetViewData() => this.viewData;

        /// <summary>
        /// Returns module.
        /// </summary>
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
        public void DoInitialize() {
            this.OnInitialize();
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
        /// <param name="viewData"></param>
        public void DoEnableFromPool(in ViewData viewData) {
            this.OnEnableFromPool(in viewData);
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
        /// <param name="viewData"></param>
        public void DoApplyState(in ViewData viewData) {
            this.ApplyState(in viewData);
        }

        /// <summary>
        /// Called every time ent has been changed, but in parallel job
        /// </summary>
        /// <param name="viewData"></param>
        public void DoApplyStateParallel(in ViewData viewData) {
            this.ApplyStateParallel(in viewData);
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        /// <param name="viewData"></param>
        /// <param name="dt"></param>
        public void DoOnUpdate(in ViewData viewData, float dt) {
            this.OnUpdate(in viewData, dt);
        }
        
        /// <summary>
        /// Called every frame, but in parallel job
        /// </summary>
        /// <param name="viewData"></param>
        /// <param name="dt"></param>
        public void DoOnUpdateParallel(in ViewData viewData, float dt) {
            this.OnUpdateParallel(in viewData, dt);
        }

        /// <summary>
        /// Provides the <c>OnEnableFromPool</c> callback; this implementation performs no work.
        /// </summary>
        protected internal virtual void OnEnableFromPool(in ViewData viewData) { }

        /// <summary>
        /// Provides the <c>OnDisableToPool</c> callback; this implementation performs no work.
        /// </summary>
        protected internal virtual void OnDisableToPool() { }

        /// <summary>
        /// Provides the <c>OnInitialize</c> callback; this implementation performs no work.
        /// </summary>
        protected internal virtual void OnInitialize() { }

        /// <summary>
        /// Provides the <c>OnDeInitialize</c> callback; this implementation performs no work.
        /// </summary>
        protected internal virtual void OnDeInitialize() { }

        /// <summary>
        /// Provides the <c>ApplyState</c> callback; this implementation performs no work.
        /// </summary>
        protected internal virtual void ApplyState(in ViewData viewData) { }

        /// <summary>
        /// Provides the <c>ApplyStateParallel</c> callback; this implementation performs no work.
        /// </summary>
        protected internal virtual void ApplyStateParallel(in ViewData viewData) { }

        /// <summary>
        /// Provides the <c>OnUpdate</c> callback; this implementation performs no work.
        /// </summary>
        protected internal virtual void OnUpdate(in ViewData viewData, float dt) { }

        /// <summary>
        /// Provides the <c>OnUpdateParallel</c> callback; this implementation performs no work.
        /// </summary>
        protected internal virtual void OnUpdateParallel(in ViewData viewData, float dt) { }

        /// <summary>
        /// Refreshes or validates state after values change in the Unity Inspector.
        /// </summary>
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