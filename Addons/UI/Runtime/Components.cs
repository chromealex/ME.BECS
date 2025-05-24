namespace ME.BECS.UI {
    
    using BECS;

    public struct UIComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.black;

    }

    [ComponentGroup(typeof(UIComponentGroup))]
    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct UIComponent : IComponent {

        public RuntimeObjectReference<UIEntityView> target;
        public Ent entity;

    }

}