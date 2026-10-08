namespace ME.BECS.Views {

    /// <summary>
    /// Presents view state through the associated view.
    /// </summary>
    [System.Serializable]
    public struct View {

        /// <summary>
        /// View source used by <c>View</c>.
        /// </summary>
        public ViewSource viewSource;

        /// <summary>
        /// Gets is valid; this implementation returns <c>this.viewSource.IsValid</c>.
        /// </summary>
        public bool IsValid => this.viewSource.IsValid;

        /// <summary>
        /// Converts the supplied value to <c>ViewSource</c>.
        /// </summary>
        public static implicit operator ViewSource(View view) {
            return view.viewSource;
        }
        
    }

}