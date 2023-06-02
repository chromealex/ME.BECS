namespace ME.BECS {

    [System.Serializable]
    public class OptionalModule {

        public bool enabled;
        public Module obj;
        
        public bool IsEnabled() => this.enabled == true && this.obj != null;

    }

}