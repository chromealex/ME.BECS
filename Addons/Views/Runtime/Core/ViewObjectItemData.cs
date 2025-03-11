using System.Linq;

namespace ME.BECS.Views {

    [System.Serializable]
    public struct ViewObjectItemData : IObjectItemData {

        public SourceRegistry.Info info;

        public bool IsValid(UnityEngine.Object obj) {
            if (obj is UnityEngine.GameObject go && go.GetComponent<EntityView>() != null) return true;
            return obj is EntityView;
        }

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
                flags = 0,
            };
            info.HasUpdateModules = prefab.viewModules.Any(x => x is IViewUpdate);
            info.HasApplyStateModules = prefab.viewModules.Any(x => x is IViewApplyState);
            info.HasInitializeModules = prefab.viewModules.Any(x => x is IViewInitialize);
            info.HasDeInitializeModules = prefab.viewModules.Any(x => x is IViewDeInitialize);
            info.HasEnableFromPoolModules = prefab.viewModules.Any(x => x is IViewEnableFromPool);
            info.HasDisableToPoolModules = prefab.viewModules.Any(x => x is IViewDisableToPool);
            this.info = info;

        }

    }

}