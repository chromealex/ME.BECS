namespace ME.BECS.Views {

    public struct ViewComponent : IComponent {

        public ViewSource source;

    }
    
    public struct IsViewRequested : IComponent {}

    public struct InstantiateViewComponent : IComponentStatic, IConfigInitialize {

        public View view;
        
        public void OnInitialize(in Ent ent) {

            ent.InstantiateView(this.view);

        }

    }

}