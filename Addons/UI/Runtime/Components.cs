namespace ME.BECS.UI {
    
    using ME.BECS.Addons;

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct UIComponent : IComponent {

        public RuntimeObjectReference<UIEntityView> target;
        public Ent entity;

    }

}