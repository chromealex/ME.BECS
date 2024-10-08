namespace ME.BECS.Views {

    public struct ViewsComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.magenta;

    }
    
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct ViewComponent : IComponent {

        public ViewSource source;

    }
    
    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct IsViewRequested : IComponent {}

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct InstantiateViewComponent : IConfigComponentStatic, IConfigInitialize {

        public View view;
        
        public void OnInitialize(in Ent ent) {

            ent.InstantiateView(this.view);

        }

    }

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct MeshFilterComponent : IComponent {

        public RuntimeObjectReference<UnityEngine.Mesh> mesh;

    }

    [ComponentGroup(typeof(ViewsComponentGroup))]
    public struct MeshRendererComponent : IComponent {

        public RuntimeObjectReference<UnityEngine.Material> material;
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;
        public int receiveShadows;
        public int layer;
        public uint renderingLayerMask;
        public int rendererPriority;
        public int instanceID;

    }

}