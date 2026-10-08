using System.Linq;

namespace ME.BECS.Views {

    /// <summary>
    /// Stores view object item data for the associated views API.
    /// </summary>
    [System.Serializable]
    public struct ViewObjectItemData : IObjectItemData {

        /// <summary>
        /// Metadata describing the associated entry.
        /// </summary>
        public SourceRegistry.Info info;

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        public bool IsValid(UnityEngine.Object obj) {
            if (obj is UnityEngine.GameObject go && go.GetComponent<EntityView>() != null) return true;
            return obj is EntityView;
        }

        /// <summary>
        /// Checks the supplied state against the constraints required by this API.
        /// </summary>
        public void Validate(UnityEngine.Object obj) {

            if (obj is UnityEngine.GameObject go) {
                obj = go.GetComponent<EntityView>();
            }
            var prefab = (EntityView)obj;
            var typeInfo = new ViewTypeInfo {
                cullingType = prefab.cullingType,
            };
            var info = new SourceRegistry.Info() {
                typeInfo = typeInfo,
                poolCount = prefab.poolCount,
                supportedProviders = prefab.supportedProviders,
                flags = 0,
            };
            info.HasUpdateModules = prefab.modules.Any(x => x is IViewUpdate);
            info.HasUpdateParallelModules = prefab.modules.Any(x => x is IViewUpdateParallel);
            info.HasApplyStateModules = prefab.modules.Any(x => x is IViewApplyState);
            info.HasApplyStateParallelModules = prefab.modules.Any(x => x is IViewApplyStateParallel);
            info.HasInitializeModules = prefab.modules.Any(x => x is IViewInitialize);
            info.HasDeInitializeModules = prefab.modules.Any(x => x is IViewDeInitialize);
            info.HasEnableFromPoolModules = prefab.modules.Any(x => x is IViewEnableFromPool);
            info.HasDisableToPoolModules = prefab.modules.Any(x => x is IViewDisableToPool);
            this.info = info;

        }

    }

}