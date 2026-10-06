namespace ME.BECS {

    // Immutable managed startup data, outside simulation State. The compiler
    // selects phase delegates and slot ordinals; the framework owns execution.
    internal sealed class BootstrapPhases {
        private readonly System.Action<bool>[] initialize;
        private readonly System.Action<bool>[] register;
        private readonly System.Action<bool>[] preflight;
        private readonly int[] jobSetupOrdinals;
        private readonly int jobSetupCount;
        private readonly int jobSetupMax;
        private readonly bool editor;
        internal readonly bool jobDebug;

        internal BootstrapPhases(System.Action<bool>[] initialize, System.Action<bool>[] register,
                                 System.Action<bool>[] preflight, int[] jobSetupOrdinals, bool editor, bool jobDebug) {
            ValidateCallbacks(initialize, nameof(initialize));
            ValidateCallbacks(register, nameof(register));
            ValidateCallbacks(preflight, nameof(preflight));
            if (initialize.Length != register.Length) throw new System.ArgumentException("Bootstrap feeder phases must have matching slots.");
            if (jobSetupOrdinals == null) throw new System.ArgumentNullException(nameof(jobSetupOrdinals));
            var selected = new System.Collections.Generic.HashSet<int>();
            this.jobSetupMax = -1;
            foreach (var ordinal in jobSetupOrdinals) {
                if (ordinal < 0) throw new System.ArgumentOutOfRangeException(nameof(jobSetupOrdinals));
                selected.Add(ordinal);
                if (ordinal > this.jobSetupMax) this.jobSetupMax = ordinal;
            }
            this.jobSetupCount = selected.Count;
            this.initialize = (System.Action<bool>[])initialize.Clone();
            this.register = (System.Action<bool>[])register.Clone();
            this.preflight = (System.Action<bool>[])preflight.Clone();
            this.jobSetupOrdinals = (int[])jobSetupOrdinals.Clone();
            this.editor = editor;
            this.jobDebug = jobDebug;
        }

        private static void ValidateCallbacks(System.Action<bool>[] callbacks, string parameter) {
            if (callbacks == null) throw new System.ArgumentNullException(parameter);
            foreach (var callback in callbacks) if (callback == null) throw new System.ArgumentException("Bootstrap phase cannot be null.", parameter);
        }

        private static bool Same<T>(T[] first, T[] second) {
            if (first == null || second == null || first.Length != second.Length) return false;
            var comparer = System.Collections.Generic.EqualityComparer<T>.Default;
            for (var i = 0; i < first.Length; ++i) if (!comparer.Equals(first[i], second[i])) return false;
            return true;
        }

        internal bool Matches(System.Action<bool>[] initialize, System.Action<bool>[] register,
                              System.Action<bool>[] preflight, int[] jobSetupOrdinals, bool jobDebug) =>
            this.jobDebug == jobDebug && Same(this.initialize, initialize) && Same(this.register, register) &&
            Same(this.preflight, preflight) && Same(this.jobSetupOrdinals, jobSetupOrdinals);

        internal void InitializeTypes() => BootstrapRuntime.InitializeTypes(this.RegisterTypes);
        private void RegisterTypes() {
            BootstrapRuntime.RegisterInstalledTypes(this.editor);
            foreach (var callback in this.initialize) callback(this.editor);
        }
        internal void RegisterMethods() {
            foreach (var callback in this.register) callback(this.editor);
        }
        internal void ValidateInputs() {
            BootstrapRuntime.ValidateJobSequence(this, this.editor);
            foreach (var callback in this.preflight) callback(this.editor);
        }

        internal void ValidateJobs(int setups, int slots) {
            // Distinct nonnegative indices, with count == table size and max in
            // range, prove full coverage without allocating during every check.
            if (slots != this.jobSetupOrdinals.Length || setups != this.jobSetupCount || this.jobSetupMax >= setups)
                throw new System.InvalidOperationException("ME.BECS job bootstrap sequence does not match the selected slots.");
        }

        internal void InitializeJobs(BootstrapTypeRegistry setups, BootstrapTypeRegistry slots) {
            this.ValidateJobs(setups.Count, slots.Count);
            // Do not deduplicate: a repeated job still repeats setup immediately
            // before its own EarlyInit/stat-only slot, regardless of owner order.
            for (var ordinal = 0; ordinal < this.jobSetupOrdinals.Length; ++ordinal) {
                setups.ExecuteRange(this.jobSetupOrdinals[ordinal], 1);
                slots.ExecuteRange(ordinal, 1);
            }
        }
    }
}
