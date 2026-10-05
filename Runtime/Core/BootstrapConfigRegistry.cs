namespace ME.BECS {
    // Startup-only publication data, outside State. Phase boundaries come from
    // the complete selection, never the order in which owners publish fragments.
    internal sealed class BootstrapConfigRegistry {
        private readonly BootstrapTypeRegistry registrations = new BootstrapTypeRegistry();
        private int counts = -1;
        private int masks;
        private int collections;
        private bool conflict;

        internal void Install(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks) =>
            this.registrations.Install(identity, owner, count, ordinals, callbacks);

        internal void Expect(string identity, int counts, int masks, int collections) {
            if (counts < 0 || masks < 0 || collections < 0 || (long)counts + masks + collections > int.MaxValue)
                throw new System.ArgumentOutOfRangeException(nameof(counts));
            if (this.counts >= 0 && (this.counts != counts || this.masks != masks || this.collections != collections)) {
                this.conflict = true;
                throw new System.InvalidOperationException("Conflicting ME.BECS config publication phases.");
            }
            this.registrations.Install(identity, "$selection", counts + masks + collections,
                System.Array.Empty<int>(), System.Array.Empty<System.Action>());
            this.counts = counts;
            this.masks = masks;
            this.collections = collections;
        }

        internal void RequireComplete() {
            if (this.conflict || this.counts < 0)
                throw new System.InvalidOperationException("ME.BECS config publication phases are missing or conflicting.");
            this.registrations.RequireComplete();
        }

        internal void ExecuteCounts() { this.RequireComplete(); this.registrations.ExecuteRange(0, this.counts); }
        internal void ExecuteMasks() { this.RequireComplete(); this.registrations.ExecuteRange(this.counts, this.masks); }
        internal void ExecuteCollections() { this.RequireComplete(); this.registrations.ExecuteRange(this.counts + this.masks, this.collections); }
    }
}
