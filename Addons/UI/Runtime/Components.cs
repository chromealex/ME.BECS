namespace ME.BECS.UI {
    
    using BECS;

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct UIComponent : IComponent {

        public RuntimeObjectReference<UIEntityView> target;
        public Ent entity;

    }

}