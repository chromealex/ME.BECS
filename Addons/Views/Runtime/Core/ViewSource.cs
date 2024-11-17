namespace ME.BECS.Views {

    [System.Serializable]
    public struct View {

        public ViewSource viewSource;

        public bool IsValid => this.viewSource.IsValid;

        public static implicit operator ViewSource(View view) {
            return view.viewSource;
        }
        
    }

}