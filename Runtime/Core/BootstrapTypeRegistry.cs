namespace ME.BECS {
    // Core phases use actions; addons use their own delegate contracts.
    internal sealed class BootstrapTypeRegistry {
        private readonly BootstrapCallbackRegistry<System.Action> callbacks = new BootstrapCallbackRegistry<System.Action>();
        internal int Count => this.callbacks.Count;
        internal void Install(string identity, string owner, int count, int[] ordinals, System.Action[] actions) =>
            this.callbacks.Install(identity, owner, count, ordinals, actions);
        internal void RequireComplete() => this.callbacks.RequireComplete();
        internal void Execute() => this.ExecuteRange(0, this.Count);
        internal void ExecuteRange(int start, int count) {
            this.RequireComplete();
            if (start < 0 || count < 0 || (long)start + count > this.Count) throw new System.ArgumentOutOfRangeException(nameof(count));
            for (var i = start; i < start + count; ++i) this.callbacks.Get(i)();
        }
    }
}
